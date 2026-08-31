// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LauncherModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.LogicLayer.Logic.LauncherLogic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
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
    public class StatementController : ControllerBase
    {
        private readonly ILogger<LauncherController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILauncherLogic _context;

        public StatementController(
            ILogger<LauncherController> logger,
             IHttpContextAccessor httpContextAccessor,
             ILauncherLogic context
            )
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }


        /// <summary>
        /// Initialize statement
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> StatementInitializer([FromBody] SimulationInitializerViewModel input)
        {
            Statement output = new Statement();
            try
            {
                output = await _context.InitalizeStatement(input);
                return Ok(output);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                return BadRequest();
                throw;
            }

           
        }


        /// <summary>
        /// Submit statement
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("[action]")]
        public async Task<IActionResult> StatementInitialization([FromBody] StatementInitalizationRequestViewModel input)
        {
            StatementInitalizationResponseViewModel output = new StatementInitalizationResponseViewModel();
            try
            {
                output = await _context.InitalizeStatement(input);
                return Ok(output);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                return BadRequest();
                throw;
            }
        }


        // RETIRED 2026-08-10 (audit T2): POST api/Statement -- StatementSubmission([FromBody] Statement).
        //
        // It was the ONLY ingest route that bypassed StatementFactor: the factoring call is
        // commented out at StatementLogic:762-767, and StatementLogic:823 wrote
        // `Authority = input.Authority ?? null` straight from the model-bound body. A client could
        // therefore assert ANY xAPI authority -- Actor Name, Mbox, Mbox_sha1sum, OpenId, Account --
        // and the Portal renders it to educators as fact (Views/Statement/DetailsModal.cshtml:217-242).
        // Its two siblings do not behave that way; this route was the odd one out.
        //
        // Removed rather than fixed, because it is the SUPERSEDED route. Both shipping clients moved
        // to /api/Statement/Submit in Phase 3 and say so themselves -- see
        // mobile/FebrisMobileServer/.../StatementRequest.cs:207 and
        // pc/FebrisPCStatementManager/.../StatementRequest.cs:122, both reading "Previously targeted
        // the parameterless /api/Statement/". /Backup remains their fallback, and a repo-wide search
        // found no live caller of the bare route.

        [HttpPost("Backup")]
        public async Task<IActionResult> BackupStatementSubmission([FromBody] JObject input)
        {
            // Legacy ingest endpoint. Kept for one release cycle while
            // producers migrate to the typed /Submit endpoint below.
            //
            // T4: this used to say raw-byte preservation was impossible here, because the
            // [FromBody] JObject bind consumes the body before the action runs. That was true of
            // the stream, not of the request: Startup enables buffering for this ONE route, so the
            // consumed stream can be rewound and the original bytes read back below.
            //
            // The signature deliberately stays [FromBody] JObject rather than going parameterless
            // like /Submit. Measured against a running node, the two routes are NOT equivalent:
            // /Backup answers malformed, empty, null and non-object bodies with MVC's
            // application/problem+json, and an unsupported Content-Type with 415, while /Submit
            // returns its own {"error","detail"} shape and does not check Content-Type at all
            // (a text/plain body is accepted and processed, returning 200). Rewriting this action
            // to own its body would have moved all five of those behaviours into hand-written code
            // and regressed the 415 into a 200 for every legacy client.
            StatementUploadResponseViewModel output = new StatementUploadResponseViewModel();
            try
            {
                Statement statement = await _context.SubmitStatement(input);
                // Audit C-02: was `statement?.Id != default`, which lifts to long? so `default`
                // binds to null -- (long?)0 != null is ALWAYS true, so this reported success
                // unconditionally, including on failure, and clients deleted records that were
                // never stored. Same explicit condition as the other two routes now.
                output.Success = statement != null && statement.Id != default;

                // T4: keep the verbatim body, exactly as /Submit does. This route is BOTH shipping
                // clients' fallback, so the statements landing here are the ones that already
                // failed somewhere else -- precisely the ones most likely to need forensic
                // recovery, and the ones that previously kept only the lossy re-serialized copy.
                // As on /Submit, a failed audit write is a monitoring concern and never fails the
                // request: the statement is already durable in the database.
                if (statement != null && statement.UUID != Guid.Empty)
                {
                    byte[] rawBody = await ReadBufferedBodyAsync();
                    if (rawBody != null && rawBody.Length > 0)
                    {
                        bool persisted = await Febris.SharedServices.XApiStatementBinding
                            .PersistRawBytesAsync(rawBody, statement.UUID);
                        if (!persisted)
                        {
                            _logger.LogWarning(
                                "Statement {Uuid} persisted to DB but raw-bytes audit file write failed on the Backup route.",
                                statement.UUID);
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Statement {Uuid} persisted to DB but the buffered request body was unreadable, so no verbatim copy was kept.",
                            statement.UUID);
                    }
                }

                return Ok(output);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                return BadRequest();
                throw;
            }
        }

        /// <summary>
        /// T4: re-reads the request body AFTER MVC has consumed it during model binding, so the
        /// legacy <c>/Backup</c> route can keep a verbatim copy without giving up its
        /// <c>[FromBody]</c> binding and the framework behaviours that come with it.
        /// <para>
        /// Depends on <c>Request.EnableBuffering()</c> having run for this route, which Startup
        /// does for <c>/api/Statement/Backup</c> only. The <c>CanSeek</c> check is the safety net:
        /// if that middleware is ever removed, or the route is renamed so the path test stops
        /// matching, this returns null and the caller logs a warning instead of throwing on a
        /// non-seekable stream. A missing audit copy must never fail an ingest request.
        /// </para>
        /// </summary>
        private async Task<byte[]> ReadBufferedBodyAsync()
        {
            try
            {
                if (Request == null || Request.Body == null || !Request.Body.CanSeek)
                {
                    return null;
                }

                Request.Body.Position = 0;
                using (MemoryStream buffer = new MemoryStream())
                {
                    await Request.Body.CopyToAsync(buffer);
                    return buffer.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not re-read the buffered request body for the raw-bytes audit copy.");
                return null;
            }
        }

        /// <summary>
        /// xAPI statement submission (Phase 3 LMS optim, Option A --
        /// audit-grade raw-bytes preservation). Reads the POST body
        /// ONCE via <c>XApiStatementBinding</c>, captures verbatim
        /// bytes alongside a typed <c>XApiStatementDto</c>, then routes
        /// through the typed-DTO BLL entry point.
        /// <para>
        /// <b>Phase 3.3c (landed):</b> the BLL exposes
        /// <c>SubmitStatement(XApiStatementSubmission)</c> directly, so
        /// the controller no longer reparses captured bytes into a
        /// JObject -- the BLL owns that bridge in one place. The
        /// JObject reparse step is gone; downstream the BLL still calls
        /// the legacy <c>StatementFactor.FactorStatement(JObject)</c>
        /// via <c>JObject.FromObject(submission.Dto)</c>. The deep
        /// typed-factor rewrite (eliminating the JObject step entirely
        /// in <c>StatementFactor</c>) is the next phase.
        /// </para>
        /// </summary>
        [HttpPost("Submit")]
        public async Task<IActionResult> SubmitStatement()
        {
            // Step 1: read body once, capture bytes, parse as typed DTO.
            Febris.ModelLibrary.ViewModels.XApi.XApiStatementSubmission submission;
            try
            {
                submission = await Febris.SharedServices.XApiStatementBinding.ReadAsync(Request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "xAPI statement binding threw before parse.");
                return BadRequest(new { Error = "Could not read request body.", Detail = ex.Message });
            }

            if (!submission.DtoBound)
            {
                // Typed parse failed. We have the bytes but the producer
                // sent something that doesn't match xAPI 1.0.3 shape.
                _logger.LogWarning(
                    "xAPI statement submission rejected at binder. Bytes={Bytes} Encoding={Enc} CT={CT} Err={Err}",
                    submission.RawBody?.Length ?? 0,
                    submission.RawBodyEncoding,
                    submission.ContentType,
                    submission.ParseError);
                return BadRequest(new { Error = "Statement JSON did not parse.", Detail = submission.ParseError });
            }

            // Step 2: hand the typed submission straight to the BLL.
            // No per-controller JObject reparse -- the BLL's typed
            // overload owns the JObject bridge in one consolidated place.
            StatementUploadResponseViewModel output = new StatementUploadResponseViewModel();
            try
            {
                Statement statement = await _context.SubmitStatement(submission);

                // Step 3: persist raw bytes for audit trail (Option A).
                // Writes submission.RawBody verbatim to
                // StaticDetails.JSONStatementFileSystemPath keyed on the
                // persisted statement UUID. Failure here is logged but
                // does NOT fail the request -- the typed Statement is
                // already durable in the DB; a missing audit file is a
                // monitoring concern, not a data-loss event.
                // The SAME null-lifting trap the C-02 comment below describes, and it was still
                // live here. When the BLL REJECTS a statement it returns null, and
                // `statement?.UUID` then lifts to (Guid?)null -- which is NOT equal to
                // Guid.Empty, so this guard was TRUE precisely when there was no statement,
                // and the next line dereferenced statement.UUID. A NullReferenceException.
                //
                // That is the whole of the tracked "/Submit 400 where /Backup succeeds"
                // defect. It was never a binding or validation refusal: the BLL was correctly
                // refusing a statement whose actor is not provisioned on this node, and the
                // typed route crashed on the refusal instead of reporting it. /Backup answers
                // the same case honestly with 200 {"success":false}.
                if (statement != null && statement.UUID != Guid.Empty && submission.RawBody != null)
                {
                    bool persisted = await Febris.SharedServices.XApiStatementBinding
                        .PersistRawBytesAsync(submission.RawBody, statement.UUID);
                    if (!persisted)
                    {
                        _logger.LogWarning(
                            "Statement {Uuid} persisted to DB but raw-bytes audit file write failed.",
                            statement.UUID);
                    }
                }

                // Audit C-02: was `statement?.Id != default` (always true -- see /Backup). Same
                // explicit condition as the other two routes now.
                output.Success = statement != null && statement.Id != default;
                return Ok(output);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Statement submission BLL threw.");
                return BadRequest();
            }
        }
    }
}
