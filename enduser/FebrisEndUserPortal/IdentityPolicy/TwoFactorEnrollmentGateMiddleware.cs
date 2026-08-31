// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.UserModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Febris.UserNode.Portal.IdentityPolicy
{
    /// <summary>
    /// Enforces <c>TwoFactor.Enforcement</c>. When the operator requires two-factor auth
    /// (<see cref="TwoFactorEnforcement.AllRequired"/>, or <see cref="TwoFactorEnforcement.AdminsRequired"/>
    /// for admin-role users), an AUTHENTICATED user who has not enrolled is redirected to the
    /// authenticator-setup page and blocked from everything else until they enroll. <c>Off</c> (the default)
    /// is a pass-through with no per-request cost. Must run after UseAuthentication/UseAuthorization so
    /// <c>HttpContext.User</c> is populated; the enrollment/logout paths are always allowed through so the
    /// gate can never trap a user with no way to comply or leave.
    /// </summary>
    public class TwoFactorEnrollmentGateMiddleware
    {
        /// <summary>The authenticator-setup page (Identity UI) users are redirected to.</summary>
        private const string EnrollmentPath = "/Identity/Account/Manage/EnableAuthenticator";

        // Reaching these must NEVER be blocked, or the gate would trap the user: the enrollment flow
        // itself, recovery codes / reset (part of enrolling), logout, and the anonymous health probes.
        private static readonly string[] AllowedPrefixes =
        {
            EnrollmentPath,
            "/Identity/Account/Manage/TwoFactorAuthentication",
            "/Identity/Account/Manage/GenerateRecoveryCodes",
            "/Identity/Account/Manage/ResetAuthenticator",
            "/Identity/Account/Logout",
            "/Identity/Account/LoggedOut",
            "/health",
        };

        // Roles AdminsRequired applies to -- the centralized "org administrators" set (single source of truth).
        private static readonly string[] AdminRoles = Febris.Constants.RoleConstants.OrgAdmins.Split(',');

        // Short TTL so an admin ResetAuthenticator / self-disable re-gates the user within this window --
        // well under SecurityStampValidator's 30-min interval. Only the enrolled==true verdict is cached.
        private static readonly TimeSpan EnrolledCacheTtl = TimeSpan.FromMinutes(5);

        private readonly RequestDelegate _next;
        private readonly TwoFactorEnforcement _enforcement;
        private readonly IMemoryCache _cache;

        public TwoFactorEnrollmentGateMiddleware(RequestDelegate next, IOptions<IdentityPolicyOptions> options, IMemoryCache cache)
        {
            _next = next;
            _enforcement = options?.Value?.TwoFactor?.Enforcement ?? TwoFactorEnforcement.Off;
            _cache = cache;
        }

        [EnforcesGate("TwoFactor.Enforcement")]
        public async Task Invoke(HttpContext context, UserManager<LocalApplicationUser> userManager)
        {
            // Fast pass-through: no enforcement (default), anonymous request, or an always-allowed path.
            if (_enforcement == TwoFactorEnforcement.Off
                || context.User?.Identity?.IsAuthenticated != true
                || IsAllowedPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            if (!EnforcementAppliesTo(context.User))
            {
                await _next(context);
                return;
            }

            // Steady-state fast path: a positively-cached "enrolled" verdict skips the per-request user
            // lookup. Only enrolled==true is cached (short TTL) -- a just-enrolled user is picked up
            // immediately by the live check below, and an admin ResetAuthenticator / self-disable is re-gated
            // within the TTL. Unenrolled users are NEVER cached; they are being blocked anyway.
            string userId = userManager.GetUserId(context.User);   // reads the principal's claim; no DB hit
            if (userId != null && _cache.TryGetValue(EnrolledCacheKey(userId), out bool cachedEnrolled) && cachedEnrolled)
            {
                await _next(context);
                return;
            }

            LocalApplicationUser user = await userManager.GetUserAsync(context.User);
            if (user != null && !await userManager.GetTwoFactorEnabledAsync(user))
            {
                // Block until enrolled. A navigational request redirects to enrollment; an AJAX/XHR request
                // gets a 403 + hint header instead, so client JS reacts rather than silently rendering the
                // enrollment HTML (a 302 is followed transparently by the browser and handed back as 200).
                // Mirrors ASP.NET Core's own RedirectToLogin, which returns a status for AJAX and 302 for nav.
                if (IsAjaxRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.Headers["X-Mfa-Enrollment-Required"] = EnrollmentPath;
                }
                else
                {
                    context.Response.Redirect(EnrollmentPath);
                }
                return;
            }

            // Enrolled (or the user row is gone): cache the positive verdict so subsequent requests in the
            // TTL window skip the lookup.
            if (user != null && userId != null)
            {
                _cache.Set(EnrolledCacheKey(userId), true, EnrolledCacheTtl);
            }

            await _next(context);
        }

        private static string EnrolledCacheKey(string userId) => "mfa_enrolled:" + userId;

        private static bool IsAjaxRequest(HttpRequest request)
        {
            if (string.Equals(request.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.Ordinal))
            {
                return true;
            }
            // JSON-preferring clients (fetch with Accept: application/json) that are not asking for HTML.
            string accept = request.Headers["Accept"].ToString();
            return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
        }

        private bool EnforcementAppliesTo(ClaimsPrincipal principal)
        {
            if (_enforcement == TwoFactorEnforcement.AllRequired)
            {
                return true;
            }
            // AdminsRequired: only the org-administrator roles.
            return AdminRoles.Any(role => principal.IsInRole(role));
        }

        private static bool IsAllowedPath(PathString path)
        {
            return AllowedPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Pipeline registration for <see cref="TwoFactorEnrollmentGateMiddleware"/>.</summary>
    public static class TwoFactorEnrollmentGateMiddlewareExtensions
    {
        /// <summary>Add MFA-enrollment enforcement. Call AFTER UseAuthentication/UseAuthorization.</summary>
        public static IApplicationBuilder UseTwoFactorEnrollmentGate(this IApplicationBuilder app)
        {
            return app.UseMiddleware<TwoFactorEnrollmentGateMiddleware>();
        }
    }
}
