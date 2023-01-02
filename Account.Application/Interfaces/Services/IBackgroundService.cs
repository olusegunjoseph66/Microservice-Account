using Account.Application.DTOs.APIDataFormatters;

namespace Account.Application.Interfaces.Services
{
    public interface IBackgroundService
    {
        Task<ApiResponse> AutoExpireAccount(CancellationToken cancellationToken);
    }
}