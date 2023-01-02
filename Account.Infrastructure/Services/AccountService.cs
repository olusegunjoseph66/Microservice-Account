using Account.Application.Constants;
using Account.Application.DTOs;
using Account.Application.DTOs.APIDataFormatters;
using Account.Application.DTOs.Events;
using Account.Application.DTOs.Features.Account;
using Account.Application.DTOs.Filters;
using Account.Application.DTOs.Sortings;
using Account.Application.Enums;
using Account.Application.Exceptions;
using Account.Application.Interfaces.Services;
using Account.Application.ViewModels.QueryFilters;
using Account.Application.ViewModels.Requests;
using Account.Application.ViewModels.Responses;
using Account.Application.ViewModels.Responses.ResponseDto;
using Account.Infrastructure.QueryObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Shared.Data.Extensions;
using Shared.Data.Models;
using Shared.Data.Repository;
using Shared.ExternalServices.Interfaces;
using Shared.ExternalServices.ViewModels.Response;
using Shared.Utilities.DTO.Pagination;
using Shared.Utilities.Helpers;

namespace Account.Infrastructure.Services
{
    public class AccountService : BaseService, IAccountService
    {
        private readonly ILogger<AccountService> _logger;

        private readonly IAsyncRepository<DistributorSapAccount> _distributorSapRepository;
        private readonly IAsyncRepository<DeletionRequest> _deletionRequestRepository;
        
        private readonly IAsyncRepository<User> _userRepository;
        private readonly IAsyncRepository<AccountType> _accountTypeRepository;

        public readonly IMessagingService _messageBus;
        private readonly ISapService _sapService;
        private readonly ICachingService _cachingService;
        private IMemoryCache _cache;

        private readonly IOtpService _otpService;

        public AccountService(ILogger<AccountService> logger, IAsyncRepository<DistributorSapAccount> distributorSapRepository,
            IAsyncRepository<DeletionRequest> deletionRequestRepository, IMemoryCache cache,
            IAsyncRepository<User> userRepository, IAsyncRepository<AccountType> accountTypeRepository,
            IMessagingService messageBus, ISapService sapService, ICachingService cachingService, IOtpService otpService,
            IAuthenticatedUserService authenticatedUserService) : base(authenticatedUserService)
        {
            _logger = logger;

            _distributorSapRepository = distributorSapRepository;
            _deletionRequestRepository = deletionRequestRepository;
            _userRepository = userRepository;
            _accountTypeRepository = accountTypeRepository;

            _messageBus = messageBus;
            _sapService = sapService;
            _cachingService = cachingService;
            _cache = cache;

            _otpService = otpService;
        }

        public async Task<ApiResponse> CreateSapAccounts(List<SAPCustomerResponse> accounts)
        {
            var accountKey = "SapAccounts";
            List<SAPCustomerResponse> accountsToAdd = new();

            if (_cache.TryGetValue(accountKey, out List<SAPCustomerResponse> cacheProducts))
            {
                cacheProducts.AddRange(accounts);
                accountsToAdd = cacheProducts.DistinctBy(x => x.DistributorNumber).ToList();

                _cache.Remove(accountKey);
            }
            else
                accountsToAdd = accounts;

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetSlidingExpiration(TimeSpan.FromDays(6))
                            .SetAbsoluteExpiration(TimeSpan.FromDays(30))
                            .SetPriority(CacheItemPriority.NeverRemove)
                            .SetSize(1024);
            _cache.Set(accountKey, accountsToAdd, cacheEntryOptions);

            return ResponseHandler.SuccessResponse("Account Successfully added to Memory", accountsToAdd);
        }

        public async Task<ApiResponse> GetCacheAccounts()
        {
            var sapAccounts = GetSapAccounts();
            return ResponseHandler.SuccessResponse("Cache Accounts Successfully fetched", sapAccounts);
        }

        public async Task<ApiResponse> GetSapAccounts(DistributorUserQueryFilter filter, CancellationToken cancellationToken)
        {
            GetUserId();
            BasePageFilter pageFilter = new(filter.PageSize, filter.PageIndex);
            DistributorUserSortingDto sorting = new();
            if (filter.Sort == Application.Enums.UserDateSortingEnum.DateDescending)
                sorting.IsDateDescending = true;
            else if (filter.Sort == Application.Enums.UserDateSortingEnum.DateAscending)
                sorting.IsDateAscending = true;
            DistributorUserFilterDto userFilter = new()
            {
                CompanyCode = filter.CompanyCode,
                CountryCode = filter.CountryCode,
                SearchKeyword = filter.SearchKeyword
            };
            var expression = new DistributorUserQueryObject(userFilter).Expression;
            var orderExpression = ProcessOrderFunc(sorting);
            var query = _distributorSapRepository.Table.Where(sr => sr.UserId == LoggedInUserId).AsNoTrackingWithIdentityResolution()
                    .OrderByWhere(expression, orderExpression);
            var totalCount = await query.CountAsync(cancellationToken);

            query = query.Select(x => new DistributorSapAccount
            {
                CompanyCode = x.CompanyCode,
                CountryCode = x.CountryCode,
                FriendlyName = x.FriendlyName,
                Id = x.Id,
                DistributorSapNumber = x.DistributorSapNumber,
                DateCreated = x.DateCreated,
                DateModified = x.DateModified, 
                AccountType = new AccountType
                {
                    Code = x.AccountType.Code, 
                    Id = x.AccountType.Id, 
                    Name = x.AccountType.Name
                }
            }).Paginate(pageFilter.PageNumber, pageFilter.PageSize);

            var distributorUsers = await query.ToListAsync(cancellationToken);
            var totalPages = NumberManipulator.PageCountConverter(totalCount, pageFilter.PageSize);
            var response = new PaginatedList<DistributorAccountResponse>(ProcessQuery(distributorUsers), new PaginationMetaData(filter.PageIndex, filter.PageSize, totalPages, totalCount));

            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_RETRIEVAL_OF_DISTRIBUTOR_ACCOUNT, response);
        }

        public async Task<ApiResponse> RenameFriendlyName(RenameSapAccountRequest request, int SapAccountId, CancellationToken cancellationToken)
        {
            var distributorSapUserDetail = await _distributorSapRepository.Table.FirstOrDefaultAsync(p => p.Id == SapAccountId, cancellationToken);

            if (distributorSapUserDetail == null)
                throw new NotFoundException(ErrorMessages.DANGOTE_RECORD_NOT_FOUND, ErrorCodes.DANGOTE_RECORD_NOT_FOUND);

            distributorSapUserDetail.FriendlyName = request.FriendlyName;
            distributorSapUserDetail.DateModified = DateTime.UtcNow;
            _distributorSapRepository.Update(distributorSapUserDetail);

            //Azure ServiceBus Accounts.SapAccount.Updated
            AccountsSapAccountUpdatedMessage adminSapUserUpdatedMessage = new()
            {
                DistributorSapAccountId = distributorSapUserDetail.Id,
                NewFriendlyName = request.FriendlyName,
                OldFriendlyName = distributorSapUserDetail.FriendlyName,
                DateModified = (DateTime)distributorSapUserDetail.DateModified,
                DistributorSapNumber = distributorSapUserDetail.DistributorSapNumber, 
                DateCreated = distributorSapUserDetail.DateCreated, 
                UserId = distributorSapUserDetail.UserId 
            };

            await _messageBus.PublishTopicMessage(adminSapUserUpdatedMessage, EventMessages.ACCOUNT_SAP_USER_UPDATED);

            _distributorSapRepository.Commit();
            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_UPDATED_USER);
        }

        public async Task<ApiResponse> RequestDeleteSapAccount(SapAccountDeletionRequest request, CancellationToken cancellationToken)
        {
            GetUserId();

            DeletionRequest deletionRequest = new()
            {
                DateCreated = DateTime.UtcNow,
                Reason = request.Reason,
                UserId = LoggedInUserId
            };
            await _deletionRequestRepository.AddAsync(deletionRequest, cancellationToken);
            await _deletionRequestRepository.CommitAsync(cancellationToken);

            //Azure ServiceBus  
            SapAccountDeletedRequestMessage adminSapAccountDeletedMessage = new()
            {
                UserId = LoggedInUserId,
                Reason = request.Reason, 
                DateCreated = DateTime.UtcNow, 
                DeletionRequestId = deletionRequest.Id
            };

            await _messageBus.PublishTopicMessage(adminSapAccountDeletedMessage, EventMessages.ACCOUNT_SAP_DELETION_REQUEST_CREATED);

            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_USER_DELETION_REQUESTED);
        }

        public async Task<ApiResponse> LinkDistributorAccount(LinkDistributorAccountRequest request, CancellationToken cancellationToken)
        {
            GetUserId();

            if (await _distributorSapRepository.Table.AnyAsync(x => x.DistributorSapNumber == request.DistributorNumber && x.CompanyCode == request.CompanyCode && x.CountryCode == request.CountryCode, cancellationToken))
                throw new ConflictException(ErrorMessages.DISTRIBUTOR_ACCOUNT_ALREADY_EXIST, ErrorCodes.DISTRIBUTOR_ACCOUNT_ALREADY_EXIST_CODE);

            var sapCustomer = await _sapService.FindCustomer(request.CompanyCode, request.CountryCode, request.DistributorNumber);

            if (sapCustomer == null)
                throw new NotFoundException(ErrorMessages.SAP_ACCOUNT_NOTFOUND, ErrorCodes.SAP_ACCOUNT_NOTFOUND_CODE);

            if (sapCustomer.Status == Shared.ExternalServices.Enums.SapAccountStatusEnum.Blocked.ToDescription())
                throw new ConflictException(ErrorMessages.SAP_ACCOUNT_BLOCKED, ErrorCodes.SAP_ACCOUNT_BLOCKED_CODE);

            var key = $"{CacheKeys.ACCOUNT}{LoggedInUserId}";
            var cacheData = new AccountCacheDto(sapCustomer, LoggedInUserId);
            await _cachingService.SetAsync(key, cacheData, TimeSpan.FromMinutes(30), cancellationToken: cancellationToken);

            var userDetails = await _userRepository.Table.Where(u => u.Id == LoggedInUserId)
                .Select(x => new User
                {
                     Id = x.Id, EmailAddress = x.EmailAddress, PhoneNumber = x.PhoneNumber
                })
                .FirstOrDefaultAsync(cancellationToken);

            var otpResponse = await _otpService.GenerateOtp(userDetails.EmailAddress, cancellationToken, phoneNumber: userDetails.PhoneNumber, userId: userDetails.Id);

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

            return ResponseHandler.SuccessResponse(SuccessMessages.SAP_ACCOUNT_SUCCESSFULLY_LINK, new LinkDistributorAccountResponse(new OtpResponseDto(otpResponse.Reference)));
        }

        public async Task<ApiResponse> ValidateLinkAccountOtp(ValidateOtpRequest request, CancellationToken cancellationToken)
        {
            GetUserId();
            var otpResponse = await _otpService.ValidateLinkAccountOtp(request, cancellationToken);

            var cacheResponse = await _cachingService.GetAsync($"{CacheKeys.ACCOUNT}{LoggedInUserId}", cancellationToken);
            if (cacheResponse == null)
                throw new ConflictException();

            var jObjectResponse = (JObject)cacheResponse;
            AccountCacheDto cacheData = (AccountCacheDto)jObjectResponse.ToObject(typeof(AccountCacheDto));

            var accountType = await _accountTypeRepository.Table.Where(x => x.Name.ToLower() == cacheData.SapAccount.AccountType.ToLower()).FirstOrDefaultAsync(cancellationToken);        
            if (accountType == null)
                throw new ConflictException("");

            var newAccount = new DistributorSapAccount
            {
                AccountTypeId = accountType.Id,
                DateCreated = DateTime.UtcNow,
                CompanyCode = cacheData.SapAccount.CompanyCode,
                CountryCode = cacheData.SapAccount.CountryCode,
                DistributorName = cacheData.SapAccount.DistributorName,
                DistributorSapNumber = cacheData.SapAccount.DistributorNumber,
                UserId = cacheData.UserId
            };
            await _distributorSapRepository.AddAsync(newAccount, cancellationToken);
            await _distributorSapRepository.CommitAsync(cancellationToken);

            var messageObject = new AccountsSapAccountCreatedMessage
            {
                DateCreated = otpResponse.DateCreated,
                CompanyCode = newAccount.CompanyCode, 
                CountryCode = newAccount.CountryCode, 
                DistributorSapAccountId = newAccount.Id, 
                DistributorSapNumber = newAccount.DistributorSapNumber,
                FriendlyName = newAccount.FriendlyName, 
                UserId = newAccount.UserId,  
                DistributorName = newAccount.DistributorName,
                AccountType = new NameAndCode(accountType.Name, accountType.Code)
            };
            await _messageBus.PublishTopicMessage(messageObject, EventMessages.ACCOUNT_SAP_CREATED);

            await _cachingService.RemoveAsync($"{CacheKeys.ACCOUNT}{LoggedInUserId}", cancellationToken);

            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_OTP_VALIDATION);
        }

        public async Task<ApiResponse> UnlinkAccount(UnLinkSapAccountRequest request, CancellationToken cancellationToken)
        {
            GetUserId();
            var sapAccountOwned = _distributorSapRepository.Table.Count(ds => ds.UserId == LoggedInUserId);

            if (sapAccountOwned == 1)
                throw new NotFoundException(ErrorMessages.CANNOT_UNLINK_DANGOTE_ACCOUNT, ErrorCodes.CANNOT_UNLINK_DANGOTE_ACCOUNT);

            var distributorSapUserDetail = await _distributorSapRepository.Table.FirstOrDefaultAsync(p => p.Id == request.SapAccountId, cancellationToken);

            if (distributorSapUserDetail == null)
                throw new NotFoundException(ErrorMessages.DANGOTE_RECORD_NOT_FOUND, ErrorCodes.DANGOTE_RECORD_NOT_FOUND);

            _distributorSapRepository.Delete(distributorSapUserDetail);
            _distributorSapRepository.Commit();

            //Azure ServiceBus Accounts.SapAccount.Deleted
            SapAccountDeletedMessage adminSapUserDeletedMessage = new()
            {
                SapAccountId = distributorSapUserDetail.Id,
                UserId = LoggedInUserId,
                DateDeleted = DateTime.UtcNow,
                DistributorSapNumber = distributorSapUserDetail.DistributorSapNumber
            };

            await _messageBus.PublishTopicMessage(adminSapUserDeletedMessage, EventMessages.ACCOUNT_SAP_DELETED);

            _distributorSapRepository.Commit();
            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_DELETED_SAPACCOUNT);
        }

        public async Task<ApiResponse> AutoExpireAccount(CancellationToken cancellationToken)
        {
            var usersQuery = _userRepository.Table.Where(x => !x.IsDeleted && x.PasswordExpiryDate < DateTime.UtcNow && x.UserStatusId != (byte)UserStatusEnum.Expired);

            var totalCount = await usersQuery.CountAsync(cancellationToken);
            var users = await usersQuery.ToListAsync(cancellationToken);

            users.ForEach(x =>
            {
                x.UserStatusId = (byte)UserStatusEnum.Expired;
            });
            _userRepository.UpdateRange(users);
            await _userRepository.CommitAsync(cancellationToken);
            _logger.LogInformation($"The AutoExpireAccount Service completed with {totalCount} users updated at {DateTime.UtcNow}");
            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_AUTO_EXPIRE_ACCOUNT, new SingleCountDto { TotalCount = totalCount});
        }

        #region Private Methods
        private static Func<IQueryable<DistributorSapAccount>, IOrderedQueryable<DistributorSapAccount>> ProcessOrderFunc(DistributorUserSortingDto? orderExpression = null)
        {
            IOrderedQueryable<DistributorSapAccount> orderFunction(IQueryable<DistributorSapAccount> queryable)
            {
                if (orderExpression == null)
                    return queryable.OrderByDescending(p => p.DateCreated);

                var orderQueryable = orderExpression.IsDateAscending
                   ? queryable.OrderBy(p => p.DateCreated).ThenByDescending(p => p.DateCreated)
                   : orderExpression.IsDateDescending
                       ? queryable.OrderByDescending(p => p.DateCreated).ThenByDescending(p => p.DateCreated)
                       : queryable.OrderByDescending(p => p.DateCreated);
                return orderQueryable;
            }
            return orderFunction;
        }

        private static IReadOnlyList<DistributorAccountResponse> ProcessQuery(IReadOnlyList<DistributorSapAccount> disUsers)
        {
            return disUsers.Select(p =>
            {
                var item = new DistributorAccountResponse(p.Id, p.CompanyCode, p.CountryCode, p.FriendlyName, p.DistributorSapNumber, p.DateCreated, new Application.DTOs.NameAndCode(p.AccountType.Name, p.AccountType.Code));
                return item;
            }).ToList();
        }

        private List<SAPCustomerResponse> GetSapAccounts()
        {
            var accountKey = "SapAccounts";

            if (_cache.TryGetValue(accountKey, out List<SAPCustomerResponse> cacheProducts))
                return cacheProducts;

            return new List<SAPCustomerResponse>();
        }
        #endregion
    }
}
