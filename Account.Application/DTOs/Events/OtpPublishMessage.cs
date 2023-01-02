using Shared.ExternalServices.DTOs;

namespace Account.Application.DTOs.Events
{
    public class OtpPublishMessage : IntegrationBaseMessage
    {
        public string EmailAddress { get; set; }
        public DateTime ExpiryDate { get; set; }
        public long OtpId { get; set; }
    }
}
