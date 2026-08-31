// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.LogicLayer.Attributes;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.Api.Controllers
{
    [Febris.UserNode.LogicLayer.Attributes.Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CompanionAppController : ControllerBase
    {
        private readonly ILogger<CompanionAppController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalSoftwarePackageLogic _context;

        public CompanionAppController(
           ILogger<CompanionAppController> logger,
            IHttpContextAccessor httpContextAccessor,
            ILocalSoftwarePackageLogic context,
            IHardwareLinkedModuleLogic linkedContext
           )
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }


        [HttpGet("[action]")]
        public async Task<IActionResult> GetLatestVersion()
        {
            LocalSoftwarePackage output = new LocalSoftwarePackage();
            try
            {
                Hardware hardware = (Hardware)_httpContextAccessor.HttpContext.Items["Hardware"];
                output = await _context.Get(LocalSoftwarePackageType.AndroidMobileCompanion, hardware);

                // 200-with-a-null-body asserts that a package was found when none was. See the
                // fuller note on SoftwarePackageController.GetLatestVersion, including why this does
                // not by itself fix the mobile Server's empty-catalog crash (that was fixed in
                // CompanionSoftwareLogic.CheckVersion).
                if (output == null)
                {
                    return NotFound();
                }

                return Ok(output);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw;
            }

            //return Ok(output);
        }

        [HttpGet("Download/{input}")]
        [DisableFormValueModelBinding]
        public async Task<IActionResult> Get(Guid input)//[FromBody] Module module)
        {
            //List<Module> output = new List<Module>();
            try
            {
                Hardware hardware = (Hardware)_httpContextAccessor.HttpContext.Items["Hardware"];
                LocalSoftwarePackage item = await _context.Get(input, hardware);
                                
                Stream fileStream = await _context.Download(input);

                ///Need to put in extension************************************
                return File(fileStream, GetMimeTypes()[".zip"], item.UUID.ToString() + ".zip");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                return NotFound();
                //throw;
            }
        }

        private Dictionary<string, string> GetMimeTypes()
        {
            return new Dictionary<string, string>
            {            
                //{".zip", "application/octet-stream"},
                {".zip", "applicaiton/zip"}
            };
        }
    }
}
