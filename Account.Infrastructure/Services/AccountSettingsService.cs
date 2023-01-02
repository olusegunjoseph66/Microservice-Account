using Account.Application.Configurations;
using Account.Application.Constants;
using Account.Application.DTOs.APIDataFormatters;
using Account.Application.DTOs.Events;
using Account.Application.Exceptions;
using Account.Application.Interfaces.Services;
using Account.Application.ViewModels.Requests;
using Account.Application.ViewModels.Responses.ResponseDto;
using Account.Application.ViewModels.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Data.Models;
using Shared.Data.Repository;
using Shared.ExternalServices.Interfaces;
using Shared.Utilities.Helpers;
using System.Text.RegularExpressions;
using System.Security.Policy;

namespace Account.Infrastructure.Services
{
    public class AccountSettingsService : IAccountSettingsService
    {
        private readonly IAsyncRepository<User> _userRepository;

        private readonly IOtpService _otpService;

        public readonly IMessagingService _messageBus;

        private readonly TokenSettings _tokenSetting;
        private readonly PasswordSettings _passwordSetting;

        public AccountSettingsService(IOptions<TokenSettings> tokenSetting, IOptions<PasswordSettings> passwordSetting, IAsyncRepository<User> userRepository, IMessagingService messageBus, IOtpService otpService)
        {
            _userRepository = userRepository;

            _messageBus = messageBus;
            _otpService = otpService;

            _tokenSetting = tokenSetting.Value;
            _passwordSetting = passwordSetting.Value;
        }

        public async Task<ApiResponse> InitiatePasswordReset(InitiateResetPasswordRequest request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.Table.FirstOrDefaultAsync(x => !x.IsDeleted && x.UserName.ToLower() == request.UserName.ToLower(), cancellationToken);
            if (user == null)
                throw new NotFoundException(ErrorMessages.USER_WITH_USERNAME_NOTFOUND, ErrorCodes.USER_WITH_USERNAME_NOTFOUND_CODE);

            var token = RandomValueGenerator.GenerateRandomString(stringSize: _tokenSetting.TokenLength);
            user.ResetToken = token;
            user.ResetTokenExpiryDate = DateTime.UtcNow.AddMinutes(_tokenSetting.TokenExpiryInMinutes);
            _userRepository.Update(user);
            await _userRepository.CommitAsync(cancellationToken);

            var otpResponse = await _otpService.GenerateOtp(user.EmailAddress, cancellationToken, phoneNumber: user.PhoneNumber, userId: user.Id);

            var messageObject = new OtpGenerationPublishMessage
            {
                DateCreated = otpResponse.DateCreated,
                DateExpiry = otpResponse.ExpiryTime,
                EmailAddress = otpResponse.EmailAddress,
                OtpCode = otpResponse.Code,
                OtpId = otpResponse.Id,
                PhoneNumber = otpResponse.PhoneNumber
            };
            await _messageBus.PublishTopicMessage(messageObject, EventMessages.ACCOUNT_OTP_GENERATED);

            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_PASSWORD_RESET_INITIATION, new OtpResponse(new OtpResponseDto(otpResponse.Reference)));
        }

        public async Task<ApiResponse> CompletePasswordReset(CompleteResetPasswordRequest request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.Table.Where(x => !x.IsDeleted && x.ResetToken == request.ResetToken).Include(x => x.UserStatus).FirstOrDefaultAsync(cancellationToken);
            if (user == null)
                throw new NotFoundException(ErrorMessages.USER_WITH_RESET_TOKEN_NOTFOUND, ErrorCodes.USER_WITH_RESET_TOKEN_NOTFOUND_CODE);

            if (user.ResetTokenExpiryDate < DateTime.UtcNow)
                throw new ConflictException(ErrorMessages.RESET_TOKEN_EXPIRED, ErrorCodes.RESET_TOKEN_EXPIRED_CODE);

            var isPasswordValid = Regex.IsMatch(request.Password, _passwordSetting.RegexPattern);
            if (!isPasswordValid)
                throw new ConflictException(ErrorMessages.PASSWORD_INVALID, ErrorCodes.PASSWORD_INVALID_CODE);

            user.PasswordExpiryDate = DateTime.UtcNow.AddDays(_passwordSetting.PasswordExpiryDays);
            user.Password = EncryptionHelper.Hash(request.Password);
            user.DateModified = DateTime.UtcNow;
            user.ModifiedByUserId = user.Id;
            _userRepository.Update(user);
            await _userRepository.CommitAsync(cancellationToken);

            AccountsUserUpdatedMessage messageObject = new()
            {
                DateCreated = DateTime.UtcNow,
                DateModified = user.DateModified.Value,
                ModifiedByUserId = user.Id,
                OldDeviceId = user.DeviceId,
                OldEmailAddress = user.EmailAddress,
                OldFirstName = user.FirstName,
                OldLastName = user.LastName,
                OldPhoneNumber = user.PhoneNumber,
                OldUserName = user.UserName,
                OldAccountStatus = new Application.DTOs.NameAndCode(user.UserStatus.Name, user.UserStatus.Code),
                UserId = user.Id,
                NewAccountStatus = new Application.DTOs.NameAndCode(user.UserStatus.Name, user.UserStatus.Code),
                NewDeviceId = user.DeviceId,
                NewEmailAddress = user.EmailAddress,
                NewFirstName = user.FirstName,
                NewLastName = user.LastName,
                NewPhoneNumber = user.PhoneNumber,
                NewUserName = user.UserName
            };
            await _messageBus.PublishTopicMessage(messageObject, EventMessages.ACCOUNT_USER_UPDATED);

            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_PASSWORD_RESET_COMPLETION);
        }
    }
}
