// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LauncherModels;
using Febris.UserNode.LogicLayer.Attributes;
using Febris.UserNode.LogicLayer.Logic.LauncherLogic;
using Febris.SharedServices;
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
    public class VideoController : ControllerBase
    {
        private readonly ILogger<VideoController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IVideoUploadLogic _context;

        // Node hygiene: the self-newed IFileServerHandler field was dead --
        // every request flows through IVideoUploadLogic -- so the `new FileServerHandler()` call
        // site is removed outright rather than migrated to IStorageProvider.
        public VideoController(
            ILogger<VideoController> logger,
             IHttpContextAccessor httpContextAccessor,
             IVideoUploadLogic context
            )
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }


        /// <summary>
        /// Submit statement
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost]
        [DisableFormValueModelBinding]
        public async Task<IActionResult> Post()
        {
            VideoFileUploadResponseViewModel output = new VideoFileUploadResponseViewModel();
            // Statement output = new Statement();
            try
            {
                if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
                {
                    ModelState.AddModelError("File",
                        $"The request couldn't be processed (Error 1).");
                    // Log error
                    return BadRequest("Error");
                }

                bool complete = await _context.ProcessVideoFiles(Request.Form.Files);
                if (complete)
                {
                    output.Success = true;
                    return Ok(output);
                }

                return BadRequest("Error");

                //   output = await _context.SubmitStatement(input);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                return BadRequest();
                throw;
            }

            //return Ok("SubmissionAccepted");
        }


    }
}
