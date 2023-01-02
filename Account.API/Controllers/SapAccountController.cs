using Account.Application.DTOs.APIDataFormatters;
using Account.Application.Interfaces.Services;
using Account.Application.ViewModels.QueryFilters;
using Account.Application.ViewModels.Requests;
using Account.Application.ViewModels.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.ExternalServices.ViewModels.Response;
using Shared.Utilities.DTO.Pagination;

namespace Account.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class SapAccountController : BaseController
    {
        private readonly IAccountService _accountService;
        private readonly IOtpService _otpService;
        public SapAccountController(IAccountService accountService, IOtpService otpService)
        {
            _accountService = accountService;
            _otpService = otpService;
        }

        [AllowAnonymous]
        [HttpPost("create")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SwaggerResponse<List<SAPCustomerResponse>>))]
        public async Task<IActionResult> CreateSapAccounts([FromBody] List<SAPCustomerResponse> request) => Response(await _accountService.CreateSapAccounts(request));

        [AllowAnonymous]
        [HttpGet("cache")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SwaggerResponse<List<SAPCustomerResponse>>))]
        public async Task<IActionResult> GetSapAccounts() => Response(await _accountService.GetCacheAccounts());


        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SwaggerResponse<PaginatedList<DistributorAccountResponse>>))]
        public async Task<IActionResult> GetSapAccounts([FromQuery] DistributorUserQueryFilter filter, CancellationToken cancellationToken = default) => Response(await _accountService.GetSapAccounts(filter, cancellationToken));

        [HttpPatch("{SapAccountId}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SwaggerResponse<EmptyResponse>))]
        public async Task<IActionResult> RenameSapAccount(int SapAccountId, [FromBody] RenameSapAccountRequest request, CancellationToken cancellationToken) =>  Response(await _accountService.RenameFriendlyName(request, SapAccountId, cancellationToken));

        [HttpDelete("deletionRequest")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SwaggerResponse<EmptyResponse>))]
        public async Task<IActionResult> RequestDeletion([FromQuery] SapAccountDeletionRequest request, CancellationToken cancellationToken) => Response(await _accountService.RequestDeleteSapAccount(request, cancellationToken));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost("validate")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SwaggerResponse<ValidateOtpResponse>))]
        public async Task<IActionResult> ValidateOtp([FromBody] ValidateOtpRequest request, CancellationToken cancellationToken) => Response(await _otpService.ValidateOtp(request, cancellationToken));

        [HttpPost("link")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SwaggerResponse<LinkDistributorAccountResponse>))]
        public async Task<IActionResult> LinkDistributorAccount([FromBody] LinkDistributorAccountRequest request, CancellationToken cancellationToken = default) => Response(await _accountService.LinkDistributorAccount(request, cancellationToken));

        [HttpPost("otp/validate")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SwaggerResponse<PaginatedList<EmptyResponse>>))]
        public async Task<IActionResult> ValidateLinkAccountOtp([FromBody] ValidateOtpRequest request, CancellationToken cancellationToken = default) => Response(await _accountService.ValidateLinkAccountOtp(request, cancellationToken));

        [HttpPost("unlink")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SwaggerResponse<EmptyResponse>))]
        public async Task<IActionResult> UnlinkAccount([FromBody] UnLinkSapAccountRequest request, CancellationToken cancellationToken = default) => Response(await _accountService.UnlinkAccount(request, cancellationToken));

    }
}
