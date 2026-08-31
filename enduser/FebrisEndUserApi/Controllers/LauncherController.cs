// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LauncherModels;
using Febris.UserNode.LogicLayer.Logic.LauncherLogic;
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
    public class LauncherController : ControllerBase
    {
        private readonly ILogger<LauncherController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILauncherLogic _context;

        public LauncherController(
            ILogger<LauncherController> logger,
             IHttpContextAccessor httpContextAccessor,
             ILauncherLogic context
            )
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }



        // GET: api/<InitalizeController>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            //var sfgh = _httpContextAccessor.HttpContext.Items["Hardware"];
            HardwareInitializationResponse output = new HardwareInitializationResponse();
            try
            {
                output = await _context.Initalize();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw;
            }

            return Ok(output);
        }


        //[HttpGet("StatementInitializer")]
        //public async Task<IActionResult> StatementInitializer([FromBody] SimulationInitializerViewModel input)
        //{            
        //    Statement output = new Statement();
        //    try
        //    {
        //        output = await _context.InitalizeStatement(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex.StackTrace);
        //        throw;
        //    }

        //    return Ok(output);
        //}

        //[HttpPost]
        //public async Task<IActionResult> StatementSubmission([FromBody] Statement input)
        //{
        //    Statement output = new Statement();
        //    try
        //    {
        //        output = await _context.SubmitStatement(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex.StackTrace);
        //        return BadRequest();
        //        throw;
        //    }

        //    return Ok("SubmissionAccepted");
        //}

        //[HttpPost("Backup")]
        //public async Task<IActionResult> BackupStatementSubmission([FromBody] JObject input)
        //{
        //    Statement output = new Statement();
        //    try
        //    {
        //        output = await _context.SubmitStatement(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex.StackTrace);
        //        return BadRequest();
        //        throw;
        //    }

        //    return Ok("SubmissionAccepted");
        //}
    }
}
