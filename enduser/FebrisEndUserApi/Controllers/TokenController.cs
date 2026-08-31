// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.TicketModels;
using Febris.UserNode.LogicLayer.Logic.AuthorizationLogic;
using Febris.SharedServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.Api.Controllers
{
    [Febris.UserNode.LogicLayer.Attributes.Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly IHardwareKeyAuthorization _jwtSettings;
        //private readonly JwtSettings _jwtSettings;
        //private readonly IInstitutionSubscriptionLogic _institutionSubscriptionLogic= new Febris.SharedLogicLayer.Logic.DataLogic.InstitutionSubscriptionLogic();
        //private readonly ILicenseLogic _licenseLogic = new Febris.SharedLogicLayer.Logic.DataLogic.LicenseLogic();
        //private readonly LicenseLogic _licenseLogic = new Febris.SharedLogicLayer.Logic.DataLogic.LicenseLogic();
        private readonly ILogger<TokenController> _logger;
        private readonly IDistributedHardwareCache _distributedCache;



        public TokenController(
            ILogger<TokenController> logger,
            IHardwareKeyAuthorization jwtSettings,
            IDistributedHardwareCache distributedCache
            //IOptions<IJwtSettings> jwtTokenOptions
            )
        {
            // _jwtSettings = (JwtSettings)jwtTokenOptions.Value;
            _jwtSettings = jwtSettings;
            _logger = logger;
            _distributedCache = distributedCache;
        }
              
        [AllowAnonymous]
        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate([FromBody] HardwareAuthenticationRequest model)
        {
            HardwareAuthenticationResponse response = await _jwtSettings.HardwareAuthentication(model);

            if (response == null)
            {
                return BadRequest(new { message = "Your license key is not valid" });
            }

            return Ok(response);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> RefreshHardwareToken()//[FromBody] RefreshHardwareToken model)
        {
            var refreshToken = Request.Headers["Authorization"];
            //var accessToken = Request.Headers[HeaderNames.Authorization];

            HardwareAuthenticationResponse response = await _jwtSettings.RefreshHardwareToken(refreshToken);

            //if (response == null)
            //    return Unauthorized(new { message = "Invalid token" });

            //setTokenCookie(response.RefreshToken);

            return Ok(response);
        }
    }
}
