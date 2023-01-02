﻿using Shared.ExternalServices.ViewModels.Response;

namespace Account.Application.DTOs.Features.Account
{
    public class AccountCacheDto
    {
        public AccountCacheDto(SAPCustomerResponse sapAccount, int userId)
        {
            SapAccount = sapAccount;
            UserId = userId;
        }

        public SAPCustomerResponse SapAccount { get; set; }
        public int UserId { get; set; }
    }
}
