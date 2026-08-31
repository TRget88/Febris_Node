// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
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
    /// <summary>
    /// The node's module catalog + delivery surface, hardware-scheme only: the catalog
    /// reads and the entitlement-gated module download are authorized by the hardware's own
    /// HardwareLinkedModule links, and every handler fails closed without a hardware identity.
    /// Module ingest is NOT here: it moved to the Portal's authoring form behind cookie auth
    /// (ROADMAP 16), which also deleted the NodeAdmin token that existed only to reach the
    /// API-side write.
    /// </summary>
    [Febris.UserNode.LogicLayer.Attributes.Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ModuleController : ControllerBase
    {
        private readonly ILogger<ModuleController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IModuleLogic _context;
        private readonly IHardwareLinkedModuleLogic _linkedContext;

        public ModuleController(
            ILogger<ModuleController> logger,
             IHttpContextAccessor httpContextAccessor,
             IModuleLogic context,
             IHardwareLinkedModuleLogic linkedContext
            )
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _linkedContext = linkedContext;
        }



        [HttpGet]
        public async Task<IActionResult> Get()
        {
            List<Module> output = new List<Module>();
            try
            {
                List<LocalHardwareLinkedModuleViewModel> linkList = new List<LocalHardwareLinkedModuleViewModel>();
                // FIX (MDM-B2): middleware stores a Hardware, not LocalHardware, so the old cast threw InvalidCastException. Match the type used by every other consumer.
                //LocalHardware hardware = (LocalHardware)_httpContextAccessor.HttpContext.Items["Hardware"];
                Hardware hardware = (Hardware)_httpContextAccessor.HttpContext.Items["Hardware"];
                ///Hardware-scheme endpoint: an admin-authorized request carries no hardware
                ///identity and therefore no entitlement links -- fail closed (auth severance).
                if (hardware == null)
                {
                    return Unauthorized();
                }
                linkList = await _linkedContext.GetByHardware(hardware.Id);
                output = linkList.Select(i => i.Module).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw;
            }

            return Ok(output);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetModuleIdList()
        {
            List<Guid> output = new List<Guid>();
            try
            {
                List<LocalHardwareLinkedModuleViewModel> linkList = new List<LocalHardwareLinkedModuleViewModel>();
                Hardware hardware = (Hardware)_httpContextAccessor.HttpContext.Items["Hardware"];
                ///Hardware-scheme endpoint: fail closed without a hardware identity (auth severance).
                if (hardware == null)
                {
                    return Unauthorized();
                }
                linkList = await _linkedContext.GetByHardware(hardware.Id);
                foreach (var i in linkList)
                {
                    output.Add(i.Module.UUID);
                }
                //output = output;
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
        public async Task<IActionResult> Get(Guid? input)//[FromBody] Module module)
        {
            //List<Module> output = new List<Module>();
            try
            {
                Module module = await _context.Get(input);
                //List<HardwareLinkedModule> linkList = new List<HardwareLinkedModule>();
                Hardware hardware = (Hardware)_httpContextAccessor.HttpContext.Items["Hardware"];
                ///Hardware-scheme endpoint: module delivery is authorized by the hardware's own
                ///entitlement link -- an admin token carries none, so fail closed (auth severance).
                if (hardware == null)
                {
                    return Unauthorized();
                }
                //linkList = await _linkedContext.GetByHardware(hardware.Id);
                //output = linkList.Select(i => i.Module).ToList();

                ///Check that link exists and return a file stream (Stream, not
                ///FileStream -- store-ingested packages come from IStorageProvider)
                Stream fileStream = await _linkedContext.Download(hardware, module);

                ///Need to put in extension************************************
                return File(fileStream, GetMimeTypes()[".zip"], module.UUID.ToString() + ".zip");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw;
            }
        }

        // Module-package ingest lived here as POST Upload until ROADMAP 16: it moved to the
        // Portal's authoring form (Module/Create, cookie auth + role gates), which fronts the
        // same IPackageIngestLogic. The NodeAdmin token that existed solely to reach this write
        // was deleted with it.

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
