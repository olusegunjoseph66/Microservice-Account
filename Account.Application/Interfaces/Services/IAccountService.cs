using Account.Application.DTOs.APIDataFormatters;
using Account.Application.ViewModels.QueryFilters;
using Account.Application.ViewModels.Requests;
using Shared.ExternalServices.ViewModels.Response;

namespace Account.Application.Interfaces.Services
{
    public interface IAccountService
    {
        Task<ApiResponse> CreateSapAccounts(List<SAPCustomerResponse> accounts);
        Task<ApiResponse> GetCacheAccounts();

        Task<ApiResponse> GetSapAccounts(DistributorUserQueryFilter filter, CancellationToken cancellationToken);
        Task<ApiResponse> RenameFriendlyName(RenameSapAccountRequest request, int SapAccountId, CancellationToken cancellationToken);
        Task<ApiResponse> RequestDeleteSapAccount(SapAccountDeletionRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> LinkDistributorAccount(LinkDistributorAccountRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> ValidateLinkAccountOtp(ValidateOtpRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> UnlinkAccount(UnLinkSapAccountRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> AutoExpireAccount(CancellationToken cancellationToken);
    }
}
