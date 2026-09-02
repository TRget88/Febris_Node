// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.TicketModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.SharedServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


/// <summary>
/// License key logic for generating a jwt 
/// 
/// Generic for getting the claims from a jwt
/// 
/// Middleware for attaching License Model Claims to the httpcontext "Items" section (Think similar to "User")
/// </summary>
namespace Febris.UserNode.LogicLayer.Logic.AuthorizationLogic
{
    public interface IHardwareKeyAuthorization
    {
        Task<HardwareAuthenticationResponse> HardwareAuthentication(HardwareAuthenticationRequest model);
        Task<HardwareAuthenticationResponse> RefreshHardwareToken(string refreshToken);

        //object RefreshLicenseToken(string refreshToken);
    }


    public class HardwareKeyAuthorization : IHardwareKeyAuthorization
    {
        private readonly IHardwareQueries _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        //private readonly ClaimsPrincipal User;
        private readonly IConfiguration _config;
        //private readonly IDistributedCache _cache;
        private readonly IDistributedHardwareCache _cache;
        // HIGH-2 (2026-05-24): centralized JWT signing-key source. Greedy ctor
        // injects it; legacy ctors leave it null and fall back to the raw
        // `_config["JwtSettings:Secret"]` read.
        private readonly IJwtSigningKeyProvider _jwtKeyProvider;
        //private readonly IRedisCacheLicenseTicketStore _cache;
        private const string KeyPrefix = "FebrisHardwareToken-";


        public HardwareKeyAuthorization(
            IHttpContextAccessor httpContextAccessor,
            IDistributedHardwareCache cache
            )
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new HardwareQueries();
            //User = _httpContextAccessor.HttpContext.User;
            //_config = config;
            _cache = cache;
            _config = StaticDetails.PassedBackConfig;
        }
        public HardwareKeyAuthorization(
            IHttpContextAccessor httpContextAccessor,
            IDistributedHardwareCache cache,
            IConfiguration config
            )
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new HardwareQueries();
            _config = config;
            // User = _httpContextAccessor.HttpContext.User;
            _cache = cache;

            // _cache = new RedisCacheLicenseTicketStore(_distributedCache);
        }

        // HIGH-2 (2026-05-24): greedy ctor preferred by the DI container when
        // IJwtSigningKeyProvider is registered.
        public HardwareKeyAuthorization(
            IHttpContextAccessor httpContextAccessor,
            IDistributedHardwareCache cache,
            IConfiguration config,
            IJwtSigningKeyProvider jwtKeyProvider
            )
            : this(httpContextAccessor, cache, config)
        {
            _jwtKeyProvider = jwtKeyProvider;
        }

        // DI refactor: the full constructor-injection path. It is a strict
        // superset of the four-argument HIGH-2 constructor above, adding the one
        // remaining newed dependency, IHardwareQueries. It assigns every field
        // directly rather than chaining to a legacy constructor (those new the
        // HardwareQueries and would set the readonly _context first). Where a host
        // registers IHardwareQueries (through the per-tenant data-access
        // registration) and IJwtSigningKeyProvider, the container selects this
        // constructor and the query is injected rather than newed; other hosts
        // fall back to a legacy constructor. No HttpContext is read here.
        public HardwareKeyAuthorization(
            IHttpContextAccessor httpContextAccessor,
            IDistributedHardwareCache cache,
            IConfiguration config,
            IJwtSigningKeyProvider jwtKeyProvider,
            IHardwareQueries context
            )
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _config = config;
            _cache = cache;
            _jwtKeyProvider = jwtKeyProvider;
        }


        public async Task<HardwareAuthenticationResponse> HardwareAuthentication(HardwareAuthenticationRequest input)
        {

            try
            {
                LocalHardware item = await _context.GetByKey(input.LicenseKey);
                if (item == null || item.IsLockedOut == true)
                {
                    return null;
                }
                var jwtToken = await generateJwtToken(item);
                RefreshHardwareToken refreshTokenData = await generateRefreshToken(_httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString(), jwtToken);
                HardwareAuthenticationResponse output = new HardwareAuthenticationResponse(jwtToken, refreshTokenData.Token);

                //add to redis cache
                //await SharedServices.DistributedCacheExtensions.SetRecord<RefreshHardwareToken>(_cache, jwtToken, refreshTokenData, TimeSpan.FromDays(8), null);
                //await SharedServices.DistributedCacheExtensions.SetRecord<RefreshHardwareToken>(_cache, refreshTokenData.Token, refreshTokenData, TimeSpan.FromDays(8), null);
                await _cache.SetRecord(KeyPrefix + refreshTokenData.Token, refreshTokenData,
                    Febris.SharedServices.JwtLifetimeSettings.RefreshTokenCacheTtl(_config), null);

                return output;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }


        /// <summary>
        /// grab the refresh token stored in the cache matching the token -- this needs to be correct
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        public async Task<HardwareAuthenticationResponse> RefreshHardwareToken(string input)
        {
            string refreshOutput = string.Empty;
            string authOutput = string.Empty;
            //check redis cache to see if there is a refresh token
            var token = input?.Split(" ").Last();
            //RefreshHardwareToken refreshToken = await SharedServices.DistributedCacheExtensions.GetRecord<RefreshHardwareToken>(_cache, token);
            RefreshHardwareToken refreshToken = await _cache.GetRecord<RefreshHardwareToken>(KeyPrefix + token);

            string ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString();

            //License license = await _context.GetByLicenseKey(input.LicenseKey);
            if (refreshToken == null) return null;

            // REUSE DETECTION. Rotation writes the old token back marked Revoked with
            // ReplacedByToken set, so a token that is present-but-revoked is a REPLAY of one
            // already rotated out -- a materially different event from an unknown token, and
            // until now the two were indistinguishable because both simply returned null.
            //
            // LOGGED, NOT ESCALATED, deliberately. Since rotation happens on every refresh, a
            // device whose response was lost retries with the token it still holds -- which is
            // now revoked. That is a benign false positive and is indistinguishable here from
            // real theft, so automatically revoking the device would lock out headsets on a
            // flaky network. Escalation (for example driving IHardwareRevocationList) is an
            // owner decision and is NOT taken here. The refusal itself is unchanged.
            if (refreshToken.Revoked != null)
            {
                Febris.SharedServices.FebrisLog.Warn(
                    "Device refresh token REPLAY refused: a token rotated out at " +
                    refreshToken.Revoked.Value.ToString("o") + " was presented again from " +
                    (ipAddress ?? "an unknown address") + ". It was originally issued to " +
                    (refreshToken.CreatedByIp ?? "an unknown address") +
                    ". Either a device retried after a lost response, or a stolen token was replayed.");
                return null;
            }

            // return null if token is no longer active (expired)
            if (!refreshToken.IsActive) return null;

            //get hardware data from refreshtoken
            LocalHardware item = await ExtractHardwareData(refreshToken.LastAuthToken);
            item = await _context.Get(item.Id);
            if (item.IsLockedOut)
            {
                return null;
            }

            // CREDENTIAL REGENERATION ENDS THE SESSION IT REPLACED.
            //
            // This block re-read the live row and then tested ONLY IsLockedOut, so regenerating a
            // device's credential -- the documented recovery for a LOST OR STOLEN one -- did not
            // touch a thief who had already authenticated. Refresh tokens rotate on every call, so
            // the stolen chain renewed itself indefinitely while the honest device, which does have
            // to re-authenticate, was the only party actually locked out.
            //
            // The credential itself cannot be compared here: it is stored as a hash and is
            // deliberately kept out of the token claim. CredentialRegeneratedAt records when the old
            // one died instead, and any token minted before that moment belonged to it.
            //
            // Both sides are UTC. RefreshHardwareToken.Created is DateTime.UtcNow and
            // RegenerateCredential writes DateTime.UtcNow, so this compares like with like.
            if (item.CredentialRegeneratedAt.HasValue
                && refreshToken.Created < item.CredentialRegeneratedAt.Value)
            {
                Febris.SharedServices.FebrisLog.Warn(
                    "Device refresh REFUSED: the token was minted at " +
                    refreshToken.Created.ToString("o") + " but this device's credential was " +
                    "regenerated at " + item.CredentialRegeneratedAt.Value.ToString("o") +
                    ". The session belonging to the replaced credential is over. If this is the " +
                    "honest device, it re-authenticates with the new credential.");
                return null;
            }
            var jwtToken = await generateJwtToken(item);


            // ROTATE ON EVERY REFRESH (owner ruling 2026-08-10). This used to be guarded by
            // `refreshToken.Expires <= DateTime.UtcNow.AddDays(1)`, so with an eight-DAY token the
            // rotation branch only ran on the last day and every refresh before it handed back the
            // SAME token. A stolen refresh token was therefore good for about a week.
            //
            // Rotating every time is safe here precisely because the token is now short and a device
            // can always re-authenticate from scratch with its PhysicalLicense: the worst case for a
            // lost response is one extra auth round-trip, not a stranded headset.
            RefreshHardwareToken newRefreshToken = await generateRefreshToken(ipAddress, jwtToken);

            // Revoke the rotated-out token (so its cached IsActive flips to false) and persist both
            // it and the new token via the ONE shared rotation seam (FebrisSharedServices). Single
            // call site closes the drift that previously needed this fix in three copies.
            await _cache.RevokeAndReplaceAsync(
                refreshToken, newRefreshToken, KeyPrefix, ipAddress,
                Febris.SharedServices.JwtLifetimeSettings.RefreshTokenCacheTtl(_config));

            refreshOutput = newRefreshToken.Token;
            authOutput = jwtToken;

            return new HardwareAuthenticationResponse(authOutput, refreshOutput);
            #region retired

            //// replace old refresh token with a new one and save
            //RefreshHardwareToken newRefreshToken = await generateRefreshToken(ipAddress,jwtToken);
            //refreshToken.Revoked = DateTime.UtcNow;
            //refreshToken.RevokedByIp = ipAddress;
            //refreshToken.ReplacedByToken = newRefreshToken.Token;
            ////store refreshtoken in cache with token as the key
            ////await SharedServices.DistributedCacheExtensions.SetRecord<RefreshHardwareToken>(_cache,token,newRefreshToken, TimeSpan.FromDays(8), null);

            ////retire old listing or update if it is not
            ////await _cache.SetRecord(KeyPrefix+refreshToken, refreshToken, TimeSpan.Zero, null);


            //await _cache.SetRecord(newRefreshToken.Token, newRefreshToken, TimeSpan.FromDays(8), null);

            // generate new jwt
            //Hardware license = await TokenClaims.GetTokenClaim<Hardware>(newRefreshToken.Token, "Hardware");//,_config);
            //Hardware license = await newRefreshToken.GetTokenClaim<Hardware>("Hardware");
            //if (license.Id == 0)
            //{
            //    return null;
            //}
            //license = await _context.Get(license.Id);
            //if (license.IsLockedOut)
            //{
            //    return null;
            //}
            //var jwtToken = await generateJwtToken(license);

            //return new HardwareAuthenticateResponse(/*license.Id, license.Institution.Id,*/ jwtToken, newRefreshToken.Token);
            #endregion
        }

        #region Generate tokens
        /// <summary>
        /// Projects the tenant's <see cref="LocalHardware"/> into the shared
        /// <see cref="Hardware"/> model that the inbound middleware and every consumer
        /// of <c>context.Items["Hardware"]</c> expect (the read-side contract set by
        /// the MDM-B2 fix). Copies the BaseModel + shared fields. <c>Hardware.HardwareType</c>
        /// (an enum) has no <c>LocalHardware</c> counterpart -- LocalHardware carries a
        /// <c>HardwareTypeId</c> (long) instead -- so it is left at its default, which is
        /// exactly the value the previous lenient LocalHardware-as-Hardware deserialize
        /// produced. This keeps the JWT "Hardware" claim type-consistent with the read
        /// side rather than relying on cross-type deserialization.
        /// </summary>
        public static Hardware ToHardwareClaim(LocalHardware input)
        {
            if (input == null) return null;
            return new Hardware
            {
                Id = input.Id,
                UUID = input.UUID,
                TimeStamp = input.TimeStamp,
                LastUpdateTimeStamp = input.LastUpdateTimeStamp,
                HardwareTypeUUID = input.HardwareTypeUUID,
                DescriptiveName = input.DescriptiveName,
                Description = input.Description,
                // PhysicalLicense DELIBERATELY OMITTED. It is the device AUTHENTICATION
                // CREDENTIAL, and a JWT is base64, not encrypted -- carrying it here put the
                // credential in every access token the device holds, in anything that logs a
                // token, and on the wire. Verified before removing: NOTHING reads it back.
                // The claim's consumers are ExtractHardwareData, which uses only Id before
                // re-reading the live row, and JwtHardwareMiddleware, which hands the claim to
                // the API controllers via Items["Hardware"] -- and PhysicalLicense appears
                // NOWHERE in enduser/FebrisEndUserApi.
                HardwareCondition = input.HardwareCondition,
                IsLockedOut = input.IsLockedOut,
            };
        }

        private async Task<string> generateJwtToken(LocalHardware input)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            // HIGH-2 (2026-05-24): prefer the centralized provider when DI
            // injected it. Legacy fallback preserved for non-DI callers.
            SymmetricSecurityKey signingKey = _jwtKeyProvider?.GetSigningKey()
                ?? new SymmetricSecurityKey(
                    Encoding.ASCII.GetBytes(_config.GetValue<string>("JwtSettings:Secret")));

            IDictionary<string, object> claimsToAdd = new Dictionary<string, object>();
            // Serialize a Hardware (the read-side contract), not the LocalHardware -- and to a
            // JSON STRING explicitly (auth severance sub-slice 3, latent net8 fix): the net8
            // Microsoft.IdentityModel serializer refuses raw POCO claim values with IDX11025,
            // so passing the object itself made every device-token mint throw at CreateToken.
            // The string form is the same wire shape the 3.1-era Newtonsoft serialization
            // produced, and TokenClaims.GetTokenClaim<Hardware> (the read side) parses the
            // claim's string value unchanged.
            claimsToAdd.Add("Hardware", JsonSerializer.Serialize(ToHardwareClaim(input)));
            claimsToAdd.Add("IsLockedOut", input.IsLockedOut);


            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Claims = claimsToAdd,
                Expires = DateTime.UtcNow.Add(Febris.SharedServices.JwtLifetimeSettings.AccessTokenLifetime(_config)),
                // SSO Tier 1: sign RS256 when an asymmetric key is configured, else
                // HMAC during the transition (verifiers accept both, see T1.3).
                SigningCredentials = _jwtKeyProvider != null && _jwtKeyProvider.HasAsymmetricKey
                    ? _jwtKeyProvider.GetAsymmetricSigningCredentials()
                    : new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        // REMOVED 2026-08-10: a second generateRefreshToken(string) overload with ZERO callers.
        // Both live call sites use the two-argument version below. It also carried a stray
        // AddDays(7) where the live path used AddDays(8), so the two had silently drifted apart.
        private async Task<RefreshHardwareToken> generateRefreshToken(string ipAddress, string jwtToken)
        {
            using (var rngCryptoServiceProvider = new RNGCryptoServiceProvider())
            {
                var randomBytes = new byte[64];
                rngCryptoServiceProvider.GetBytes(randomBytes);
                return new RefreshHardwareToken
                {
                    Token = Convert.ToBase64String(randomBytes),
                    LastAuthToken = jwtToken,
                    Expires = DateTime.UtcNow.Add(Febris.SharedServices.JwtLifetimeSettings.RefreshTokenLifetime(_config)),
                    Created = DateTime.UtcNow,
                    CreatedByIp = ipAddress
                };
            }
        }
        #endregion

        private async Task<LocalHardware> ExtractHardwareData(string token)
        {
            try
            {
                var license = await TokenClaims.GetTokenClaim<LocalHardware>(token, "Hardware");
                return license;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "HardwareKeyAuthorization.ExtractHardwareData: suppressed exception");
                return null;
                // do nothing if jwt validation fails
                // user is not attached to context so request won't have access to secure routes
            }
        }

    }


    #region middleware
    public static class JwtHardwareMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtHardwareMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JwtHardwareMiddleware>();
        }
    }
    public class JwtHardwareMiddleware
    {
        private readonly RequestDelegate _next;
        //private readonly AppSettings _appSettings;
        private readonly IConfiguration _config;

        // HIGH-2 (2026-05-24): centralized signing-key provider, captured at
        // middleware-construction (i.e., app start).
        private readonly IJwtSigningKeyProvider _jwtKeyProvider;

        public JwtHardwareMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            IJwtSigningKeyProvider jwtKeyProvider = null)
        {
            _next = next;
            //_appSettings = appSettings.Value;
            _config = configuration;
            _jwtKeyProvider = jwtKeyProvider;
        }

        public async Task Invoke(HttpContext context, IHardwareKeyAuthorization licenseService, IHardwareRevocationList revocations)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (token != null)
                await AttachHardwareToContextAsync(context, licenseService, token, revocations);

            await _next(context);
        }

        /// <summary>
        /// Validates the bearer token ONCE against the node's own signing keys, then attaches
        /// each scheme's item the token actually carries -- <c>Items["Hardware"]</c> for the
        /// device scheme, and (auth severance sub-slice 3)
        /// <c>Items["NodeAdmin"]</c> for the Portal-minted human-admin scheme. The two types stay
        /// distinct end to end: a claim is only deserialized into its own contract, and a token
        /// carrying neither claim attaches nothing (the [Authorize] filter then rejects).
        /// </summary>
        private async Task AttachHardwareToContextAsync(HttpContext context, IHardwareKeyAuthorization licenseService, string token, IHardwareRevocationList revocations)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                // NOISE (2026-09-02). Shape-gate the bearer value BEFORE validation. The mobile
                // client sends an Authorization header with an EMPTY bearer token on its very
                // first call by design. InitalizationRequest.MakeGetRequest defaults the token to
                // string.Empty, takes the 401, re-authenticates and retries, so ValidateToken threw
                // SecurityTokenMalformedException IDX12741 straight into the catch below, which
                // logged a full stack trace at ERROR on a flow that then succeeds.
                //
                // CanReadToken applies the same segment-count and format gate that ValidateToken
                // applies first, so it cannot classify a token differently. Returning here leaves
                // the Hardware context item unset exactly as the catch did, and the authorize
                // attribute reads only that item, so the 401 is unchanged. This changes the log
                // level and nothing else. A well-formed JWS or JWE that fails signature, key or
                // expiry still reaches ValidateToken, still throws, and is still logged at ERROR.
                if (!tokenHandler.CanReadToken(token))
                {
                    Serilog.Log.Debug(
                        "JwtHardwareMiddleware: bearer value is not a well-formed JWS or JWE, "
                        + "skipping validation and attaching nothing");
                    return;
                }

                // HIGH-2 (2026-05-24): use the centralized provider's cached
                // signing key. Falls back to the legacy config read when the
                // provider isn't available.
                // SSO Tier 1: accept legacy HMAC and new RS256 tokens during the
                // transition (symmetric key plus the RSA public key, matched by kid).
                // Falls back to the legacy raw symmetric read only when the provider
                // isn't available.
                IList<SecurityKey> validationKeys = _jwtKeyProvider != null
                    ? _jwtKeyProvider.GetAllValidationKeys()
                    : new List<SecurityKey>
                    {
                        new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_config.GetValue<string>("JwtSettings:Secret")))
                    };
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = validationKeys,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    // set clockskew to zero so tokens expire exactly at token expiration time (instead of 5 minutes later)
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                //var userId = int.Parse(jwtToken.Claims.First(x => x.Type == "id").Value);

                // attach each carried scheme on successful jwt validation. Claim presence is
                // checked on the VALIDATED token first -- the legacy unconditional
                // GetTokenClaim call NRE'd (suppressed) on any token without a "Hardware"
                // claim, which would have swallowed the admin attach below.
                if (jwtToken.Claims.Any(c => c.Type == "Hardware"))
                {
                    var license = TokenClaims.GetTokenClaim<Hardware>(token, "Hardware");
                    Hardware attached = license.Result;

                    // A-02 Stage 2. Everything above this line trusts the SIGNED CLAIM, IsLockedOut
                    // included, so a device locked AFTER its token was minted kept working until the
                    // token expired (15 minutes, HardwareKeyAuthorization token descriptor). Issuance
                    // and refresh both re-read the live row and refuse, but neither runs inside that
                    // window. The revocation list is the only thing on this path that can know.
                    //
                    // MARKED, not detached. Leaving Items["Hardware"] null would NRE the six
                    // controllers that read it directly. Setting IsLockedOut routes the request into
                    // the existing Stage 1 refusal in AttributeClasses, which answers 401 "Hardware
                    // is locked out" -- the same response a locked device gets everywhere else.
                    if (attached != null && revocations != null && await revocations.IsRevokedAsync(attached.UUID))
                    {
                        attached.IsLockedOut = true;
                    }

                    context.Items["Hardware"] = attached;
                }
                // The "NodeAdmin" claim attach that sat here was deleted with the NodeAdmin token
                // (ROADMAP 16): the admin-only API writes moved to the Portal behind cookie auth,
                // so the API validates exactly one scheme again -- hardware. The claim-presence
                // guard above stays: it exists because the legacy unconditional GetTokenClaim
                // NRE'd on any token without a "Hardware" claim.
                //context.User.Identity.IsAuthenticated = true;

            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "JwtHardwareMiddleware.attachHardwareToContext: suppressed exception");
                // do nothing if jwt validation fails
                // user is not attached to context so request won't have access to secure routes
            }
        }
    }

    ///This should be useful but it does not seem to work.

    //[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    //public class AuthorizeAttribute : Attribute, IAuthorizationFilter
    //{
    //    public void OnAuthorization(AuthorizationFilterContext context)
    //    {
    //        // skip authorization if action is decorated with [AllowAnonymous] attribute
    //        var allowAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any();
    //        if (allowAnonymous)
    //            return;

    //        // authorization
    //        var hardware = (Hardware)context.HttpContext.Items["Hardware"];
    //        if (hardware == null)
    //            context.Result = new JsonResult(new { message = "Unauthorized" }) { StatusCode = StatusCodes.Status401Unauthorized };
    //    }
    //}

    #endregion
    

}
