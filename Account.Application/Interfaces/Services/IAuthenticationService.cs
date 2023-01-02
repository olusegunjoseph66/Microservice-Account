﻿using Account.Application.DTOs.APIDataFormatters;
using Account.Application.ViewModels.Requests;

namespace Account.Application.Interfaces.Services
{
    public interface IAuthenticationService
    {
        Task<ApiResponse> ValidateUser(AuthenticationRequest request, CancellationToken cancellationToken);
        Task<ApiResponse> ValidateAdminUser(AuthenticationRequest authenticationRequest, CancellationToken cancellationToken);
    }
}
