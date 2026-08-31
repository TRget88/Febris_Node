// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.UserModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Attributes
{
    class AttributeClasses
    { }
    /// <summary>
    /// I am unsure if this is needed. It may not be.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var factories = context.ValueProviderFactories;
            factories.RemoveType<FormValueProviderFactory>();
            //factories.RemoveType<FormFileValueProviderFactory>();
            factories.RemoveType<JQueryFormValueProviderFactory>();
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
        }
    }

    /// <summary>
    /// Authorization filter for the EndUserApi (UserNode). Parallels
    /// <c>Febris.SharedLogicLayer.Attributes.AuthorizeAttribute</c>, which composes its accepted
    /// item schemes (License/Hardware) in one filter with opt-in properties; this host composes
    /// its own two schemes the same way (auth severance sub-slice 3):
    /// <list type="bullet">
    /// <item><b>Hardware</b> (scheme C, device JWT) -- the default and, on plain
    /// <c>[Authorize]</c>, the ONLY accepted scheme, exactly as before.</item>
    /// <item><b>NodeAdmin</b> (Portal-minted human-admin JWT) -- accepted only where a
    /// controller/action opts in via <see cref="AllowNodeAdmin"/>, and REQUIRED where an action
    /// demands it via <see cref="RequireNodeAdmin"/> (the ingest/upload endpoints are
    /// admin-only).</item>
    /// </list>
    /// The item types stay distinct end to end (a NodeAdmin never satisfies a hardware-shaped
    /// handler implicitly -- handlers on opted-in controllers must handle the null-Hardware,
    /// admin-authorized case explicitly).
    /// <para>
    /// Audit A-02 Stage 1 (2026-05-20): added explicit <c>Hardware.IsLockedOut</c>
    /// re-check (defense in depth). The Hardware model has no tenant identifier
    /// in this code base (no InstitutionUUID on Hardware), so the RequiresTenant
    /// pattern from the SharedLogicLayer sibling attribute is not applicable
    /// here -- tenant scoping in EndUserApi is the local institution's, and is
    /// already implicit because each EndUserApi deployment IS scoped to a
    /// single UserNode. The deeper BLL-level tenant scoping audit is
    /// Stage 2 of A-02.
    /// </para>
    /// <para>
    /// Caveat on the lockout check: Hardware is hydrated from a JWT claim, so
    /// <c>Hardware.IsLockedOut</c> reflects the value AT JWT ISSUANCE TIME, not
    /// the live DB value. Stage 2 of A-02 will add a per-request DB re-fetch.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        // The AllowNodeAdmin / RequireNodeAdmin flags that composed a second scheme here were
        // deleted with the NodeAdmin token (ROADMAP 16): the admin-only writes they gated moved
        // to the Portal behind cookie auth, so this filter authorizes exactly one scheme again --
        // a signature-valid hardware token attached by the middleware.
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // skip authorization if action is decorated with [AllowAnonymous] attribute
            var allowAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any();
            if (allowAnonymous)
                return;

            // authorization presence -- the middleware attaches the item only from a
            // signature-valid token.
            var hardware = (Hardware)context.HttpContext.Items["Hardware"];
            if (hardware == null)
            {
                context.Result = new JsonResult(new { message = "Unauthorized" }) { StatusCode = StatusCodes.Status401Unauthorized };
                return;
            }

            // A-02 Stage 1: explicit lockout re-check. Defense in depth.
            if (hardware.IsLockedOut)
            {
                context.Result = new JsonResult(new { message = "Hardware is locked out" }) { StatusCode = StatusCodes.Status401Unauthorized };
                return;
            }
        }
    }



}
