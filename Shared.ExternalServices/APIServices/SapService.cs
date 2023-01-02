using Shared.ExternalServices.Interfaces;
using Shared.ExternalServices.ViewModels.Response;

namespace Shared.ExternalServices.APIServices
{
    public class SapService : ISapService
    {
        public async Task<SAPCustomerResponse> FindCustomer(string companyCode, string countryCode, string distributorNumber)
        {
            return await Task.Run(() => new SAPCustomerResponse());
        }
    }
}
