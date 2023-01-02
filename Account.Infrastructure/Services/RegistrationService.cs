using Account.Application.Configurations;
using Account.Application.Constants;
using Account.Application.DTOs.APIDataFormatters;
using Account.Application.DTOs.Events;
using Account.Application.DTOs.Features.Login;
using Account.Application.DTOs.Features.Registration;
using Account.Application.Enums;
using Account.Application.Exceptions;
using Account.Application.Interfaces.Services;
using Account.Application.ViewModels.Requests;
using Account.Application.ViewModels.Responses;
using Account.Application.ViewModels.Responses.ResponseDto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Shared.Data.Models;
using Shared.Data.Repository;
using Shared.ExternalServices.DTOs;
using Shared.ExternalServices.Interfaces;
using Shared.ExternalServices.ViewModels.Response;
using Shared.Utilities.Handlers;
using Shared.Utilities.Helpers;
using System.Text.RegularExpressions;

namespace Account.Infrastructure.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly IAsyncRepository<DistributorSapAccount> _distributorAccountRepository;
        private readonly IAsyncRepository<Registration> _registrationRepository;
        private readonly IAsyncRepository<User> _userRepository;
        private readonly IAsyncRepository<Role> _roleRepository;

        private readonly PasswordSettings _passwordSetting;
        private readonly RoleSettings _roleSetting;

        private readonly ICachingService _cachingService;
        private readonly ISapService _sapService;
        private readonly IOtpService _otpService;
        public readonly IMessagingService _messageBus;
        public readonly IFileService _fileService;

        public RegistrationService(IAuthenticatedUserService authenticatedUserService, IAsyncRepository<DistributorSapAccount> distributorAccountRepository, ISapService sapService, IOtpService otpService, IMessagingService messageBus, ICachingService cachingService, IFileService fileService, IAsyncRepository<Registration> registrationRepository, IAsyncRepository<User> userRepository, IOptions<PasswordSettings> passwordSetting, IOptions<RoleSettings> roleSetting, IAsyncRepository<Role> roleRepository)
        {
            _distributorAccountRepository = distributorAccountRepository;
            _registrationRepository = registrationRepository;
            _userRepository = userRepository;
            _roleRepository = roleRepository;

            _cachingService = cachingService;
            _sapService = sapService;
            _otpService = otpService;
            _messageBus = messageBus;
            _fileService = fileService;

            _passwordSetting = passwordSetting.Value;
            _roleSetting = roleSetting.Value;
        }

        public async Task<ApiResponse> InitiateRegistration(InitiateRegistrationRequest request, CancellationToken cancellationToken)
        {
            if (await _distributorAccountRepository.Table.AnyAsync(x => x.DistributorSapNumber == request.DistributorNumber && x.CompanyCode == request.CompanyCode && x.CountryCode == request.CountryCode, cancellationToken))
                throw new ConflictException(ErrorMessages.DISTRIBUTOR_ACCOUNT_ALREADY_EXIST, ErrorCodes.DISTRIBUTOR_ACCOUNT_ALREADY_EXIST_CODE);

            var sapCustomer = await _sapService.FindCustomer(request.CompanyCode, request.CountryCode, request.DistributorNumber);

            if (sapCustomer == null)
                throw new NotFoundException(ErrorMessages.SAP_ACCOUNT_NOTFOUND, ErrorCodes.SAP_ACCOUNT_NOTFOUND_CODE);

            if (sapCustomer.Status == Shared.ExternalServices.Enums.SapAccountStatusEnum.Blocked.ToDescription())
                throw new ConflictException(ErrorMessages.SAP_ACCOUNT_BLOCKED, ErrorCodes.SAP_ACCOUNT_BLOCKED_CODE);

            if (string.IsNullOrWhiteSpace(sapCustomer.PhoneNumber) && string.IsNullOrWhiteSpace(sapCustomer.EmailAddress))
                throw new ConflictException(ErrorMessages.SAP_ACCOUNT_INFORMATION_INCOMPLETE, ErrorCodes.SAP_ACCOUNT_INFORMATION_INCOMPLETE_CODE);

            var registration = await _registrationRepository.AddAsync(new Registration
            {
                ChannelCode = request.ChannelCode,
                CompanyCode = request.CompanyCode,
                CountryCode = request.CountryCode,
                DateCreated = DateTime.UtcNow,
                DeviceId = request.DeviceId,
                DistributorNumber = request.DistributorNumber,
                RegistrationStatusId = (byte)RegistrationStatusEnum.New
            }, cancellationToken);
            await _distributorAccountRepository.CommitAsync(cancellationToken);

            var key = $"{CacheKeys.REGISTRATION_INITIATION}{registration.Id}";
            var cacheData = new RegistrationCacheDto(sapCustomer, registration.Id);
            await _cachingService.SetAsync(key, cacheData, TimeSpan.FromMinutes(30), cancellationToken: cancellationToken);

            var otpResponse = await _otpService.GenerateOtp(sapCustomer.EmailAddress, cancellationToken, phoneNumber: sapCustomer.PhoneNumber, registrationId: registration.Id);

            var messageObject = new RegistrationPublishMessage
            {
                DateCreated = DateTime.UtcNow,
                DateExpiry = otpResponse.ExpiryTime,
                EmailAddress = sapCustomer.EmailAddress,
                OtpCode = otpResponse.Code,
                OtpId = otpResponse.Id,
                PhoneNumber = sapCustomer.PhoneNumber
            };
            await _messageBus.PublishTopicMessage(messageObject, EventMessages.ACCOUNT_OTP_GENERATED);

            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_ACCOUNT_REGISTRATION.Replace("{EmailAddress}", sapCustomer.EmailAddress.MaskString(40)).Replace("{PhoneNumber}", sapCustomer.PhoneNumber.MaskString(40)), new OtpResponse(new OtpResponseDto(otpResponse.Reference)));
        }

        public async Task<ApiResponse> CompleteRegistration(CompleteRegistrationRequest request, CancellationToken cancellationToken)
        {
            var registration = await _registrationRepository.Table.FirstOrDefaultAsync(x => x.Id == request.RegistrationId, cancellationToken);

            if (registration == null)
                throw new NotFoundException(ErrorMessages.REGISTRATION_NOTFOUND, ErrorCodes.REGISTRATION_NOTFOUND_CODE);

            if (registration.RegistrationStatusId == (byte)RegistrationStatusEnum.Completed)
                throw new ConflictException(ErrorMessages.REGISTRATION_PREVIOUSLY_COMPLETED, ErrorCodes.REGISTRATION_PREVIOUSLY_COMPLETED_CODE);

            var isUserNameExisting = await _userRepository.Table.AnyAsync(x => x.UserName == request.UserName, cancellationToken);

            if (isUserNameExisting)
                throw new ConflictException(ErrorMessages.USERNAME_ALREADY_EXIST, ErrorCodes.USERNAME_ALREADY_EXIST_CODE);

            var isPasswordValid = Regex.IsMatch(request.Password, _passwordSetting.RegexPattern);
            if (!isPasswordValid)
                throw new ConflictException(ErrorMessages.PASSWORD_INVALID, ErrorCodes.PASSWORD_INVALID_CODE);

            var key = $"{CacheKeys.REGISTRATION_INITIATION}{registration.Id}";
            var cacheResponse = await _cachingService.GetAsync(key, cancellationToken: cancellationToken);
            if (cacheResponse == null)
                throw new ConflictException(ErrorMessages.INVALIDATED_RECORD, ErrorCodes.CONFLICT_ERROR_CODE);

            var jObjectResponse = (JObject)cacheResponse;
            RegistrationCacheDto cacheData = (RegistrationCacheDto)jObjectResponse.ToObject(typeof(RegistrationCacheDto));

            var role = await _roleRepository.Table.Where(x => x.Name.ToLower() == _roleSetting.DefaultRoleName.ToLower()).Select(x => new Role
            {
                Id = x.Id,
                Name = x.Name
            }).FirstOrDefaultAsync(cancellationToken);

            if (role == null)
                throw new NotFoundException(ErrorMessages.DEFAULT_ROLE_NOTFOUND, ErrorCodes.SERVER_CONFIGURATION_ERROR_CODE);

            UploadResponse? upload = new();
            if (!string.IsNullOrWhiteSpace(request.ProfilePhoto))
            {
                (upload, bool isValid) = await _fileService.FileUpload(request.ProfilePhoto, cancellationToken);

                if(!isValid)
                    throw new ValidationException(ErrorMessages.INVALID_FILE_UPLOAD_FORMAT, ErrorCodes.DEFAULT_VALIDATION_CODE);
            }
           
            var user = new User
            {
                DateCreated = DateTime.UtcNow,
                EmailAddress = cacheData.SapAccount.EmailAddress,
                FirstName = cacheData.SapAccount.FirstName,
                IsDeleted = false,
                LastName = cacheData.SapAccount.LastName,
                Password = EncryptionHelper.Hash(request.Password),
                PasswordResetRequired = false,
                PhoneNumber = cacheData.SapAccount.PhoneNumber,
                UserName = request.UserName,
                UserStatusId = (byte)UserStatusEnum.Active,
                PasswordExpiryDate = DateTime.UtcNow.AddDays(_passwordSetting.PasswordExpiryDays)
            };

            if(upload != null)
            {
                user.ProfilePhotoCloudPath = upload.CloudUrl;
                user.ProfilePhotoPublicUrl = upload.PublicUrl;
            }

            var userRoles = new List<UserRole>()
            {
                new UserRole
                {
                    RoleId = role.Id,
                    UserId = user.Id
                }
            };

            var distributorAccounts = new List<DistributorSapAccount>() 
            {
                new DistributorSapAccount
                {
                    CompanyCode = registration.CompanyCode,
                    CountryCode = registration.CountryCode,
                    DateCreated = DateTime.UtcNow,
                    DistributorSapNumber = registration.DistributorNumber,
                    UserId = user.Id,
                    DistributorName = cacheData.SapAccount.DistributorName,
                    AccountTypeId = (byte)AccountTypeEnum.BankGuarantee
                }
            };
            
            user.UserRoles = userRoles; 
            user.DistributorSapAccounts = distributorAccounts;
            await _userRepository.AddAsync(user, cancellationToken);

            registration.RegistrationStatusId = (byte)RegistrationStatusEnum.Completed;
            _registrationRepository.Update(registration);
            await _userRepository.CommitAsync(cancellationToken);

            var messageObject = new UserPublishMessage
            {
                DateCreated = DateTime.UtcNow,
                EmailAddress = user.EmailAddress,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                UserName = user.UserName
            };
            await _messageBus.PublishTopicMessage(messageObject, EventMessages.ACCOUNT_USER_CREATED);
            await _cachingService.RemoveAsync(key, cancellationToken);

            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_REGISTRATION_COMPLETION);
        }
    }
}
