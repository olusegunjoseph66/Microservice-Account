﻿using Shared.ExternalServices.DTOs;

namespace Account.Application.DTOs.Events
{
    public class AdminUserCreatedMessage : IntegrationBaseMessage
    {
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmailAddress { get; set; }
        public string PhoneNumber { get; set; }
        public string Username { get; set; }
        public string DeviceId { get; set; }
        public NameAndCode AccountStatus { get; set; }
        public List<string> Roles { get; set; }
        public string ChannelCode { get; set; }


    }
}
