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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Shared.Data.Extensions;
using Shared.Data.Models;
using Shared.Data.Repository;

namespace Account.Infrastructure.Services
{
    public class BackgroundService : IBackgroundService
    {
        private readonly ILogger<BackgroundService> _logger;
        private readonly IAsyncRepository<User> _userRepository;

        public BackgroundService(ILogger<BackgroundService> logger, IAsyncRepository<User> userRepository)
        {
            _logger = logger;
            _userRepository = userRepository;
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

            return ResponseHandler.SuccessResponse(SuccessMessages.SUCCESSFUL_AUTO_EXPIRE_ACCOUNT, new SingleCountDto { TotalCount = totalCount });
        }
    }
}
