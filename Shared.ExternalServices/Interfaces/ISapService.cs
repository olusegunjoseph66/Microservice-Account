using Shared.ExternalServices.ViewModels.Response;

namespace Shared.ExternalServices.Interfaces
{
    public interface ISapService
    {
        Task<SAPCustomerResponse> FindCustomer(string companyCode, string countryCode, string distributorNumber);
    }
}