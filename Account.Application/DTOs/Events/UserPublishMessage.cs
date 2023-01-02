using Shared.ExternalServices.DTOs;

namespace Account.Application.DTOs.Events
{
    public class UserPublishMessage : IntegrationBaseMessage
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? EmailAddress { get; set; }
        public string UserName { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
