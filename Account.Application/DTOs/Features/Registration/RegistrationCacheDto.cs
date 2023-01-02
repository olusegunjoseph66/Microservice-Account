using Shared.ExternalServices.ViewModels.Response;

namespace Account.Application.DTOs.Features.Registration
{
    public class RegistrationCacheDto
    {
        public RegistrationCacheDto(SAPCustomerResponse sapAccount, int registrationId)
        {
            SapAccount = sapAccount;
            RegistrationId = registrationId;
        }

        public SAPCustomerResponse SapAccount { get; set; }
        public int RegistrationId { get; set; }
    }
}
