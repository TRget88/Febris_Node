// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.TicketModels;
using Febris.ModelLibrary.ViewModels;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries
{
    public interface ITokenQueries
    {
        Task<LicenseAuthenticateResponse> Get(LicenseAuthenticationRequest input);
        Task<LicenseAuthenticateResponse> Refresh(string input);
        Task<bool> RenewToken();
    }

    /// <summary>
    /// The scheme-B license bootstrap against the hub's authentication API. Operating auth is
    /// decoupled from the central license: this is now an OPT-IN federation
    /// credential exchange, not an operate-requirement. Every method consults the ONE
    /// hub-federation gate first; with the gate closed (no hub configured) the class is inert --
    /// it constructs cleanly with no <c>ApiUrlPath</c>/<c>LicenseKey</c> anywhere in config, makes
    /// no HTTP attempt, logs nothing, and quietly returns null/false so the Remote queries'
    /// renewal retry hooks fall through into local-only behavior.
    /// </summary>
    public class TokenQueries : ITokenQueries
    {
        public string _endpoint;
        private readonly IHubFederationSettings _federation;
        public TokenQueries()
        {
            // Legacy self-newing path: derive the gate from the same passed-back configuration
            // this class always read. Null-safe -- a config-less process gets a closed gate, not
            // an NRE (the pre-gate ctor dereferenced PassedBackConfig unconditionally).
            _federation = HubFederationSettings.Resolve(StaticDetails.PassedBackConfig, JwtSigningKeyProvider.IsUnsubstitutedTemplate);
            _endpoint = _federation.AuthenticationApi;
        }
        public TokenQueries(IHttpContextAccessor httpContextAccessor)
            : this()
        {
        }

        /// <summary>
        /// DI ctor: the host-registered gate flows in. Strict superset of the
        /// legacy ctors so MS.DI's greedy selection prefers it wherever
        /// <c>AddFebrisUserNodeDataAccess</c> registered <see cref="IHubFederationSettings"/>.
        /// </summary>
        public TokenQueries(IHttpContextAccessor httpContextAccessor, IHubFederationSettings federation)
        {
            _federation = federation ?? HubFederationSettings.Disabled();
            _endpoint = _federation.AuthenticationApi;
        }

        #region Requests
        private async Task<string> MakeAuthenticationPostRequest(string method, string dataPackage)
        {
            try
            {
                //string endpoint = _endpoint;
                //APIRequest request = new APIRequest()
                //{
                //    endPoint = endpoint + "Token/" + method,
                //    httpMethod = httpVerb.POST,
                //    authTech = AuthenticaitonTechnique.None,
                //    authType = Authenticationtype.Basic,
                //    postJSON = dataPackage ?? string.Empty,
                //    //contentType = "",

                //};
                //string response = string.Empty;
                //HttpStatusCode status;
                //response = await request.MakeRequest();
                ////if (status != HttpStatusCode.OK)
                ////{

                ////}
                //return response;
                string endpoint = _endpoint;
                IAPIRequestFactory request = new APIRequestFactory()
                {
                    endPoint = endpoint + "Token/" + method,
                    httpMethod = httpVerb.POST,
                    authTech = AuthenticaitonTechnique.None,
                    authType = Authenticationtype.Basic,
                    postJSON = dataPackage ?? string.Empty,
                    //contentType = "",

                };
                string response = string.Empty;
                HttpStatusCode status;
                (response, status) = await request.MakeStringRequest();
                if (status != HttpStatusCode.OK)
                {

                }
                return response;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "TokenQueries.MakeAuthenticationPostRequest: suppressed exception");
                return ex.Message;
            }
        }
        private async Task<string> MakeGetRequest(string method, string dataPackage)
        {
            try
            {
                string endpoint = _endpoint;
                IAPIRequestFactory request = new APIRequestFactory()
                {
                    endPoint = endpoint + "Token/" + method,
                    httpMethod = httpVerb.GET,
                    authTech = AuthenticaitonTechnique.Token,
                    authType = Authenticationtype.BearerToken,
                    postJSON = dataPackage ?? string.Empty,
                    //contentType = "",

                };
                string response = string.Empty;
                HttpStatusCode status;
                (response, status) = await request.MakeStringRequest();
                if (status != HttpStatusCode.OK)
                {
                    await Authenticate();
                }
                return response;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "TokenQueries.MakeGetRequest: suppressed exception");
                return ex.Message;
            }
        }
        private async Task<string> MakePostRequest(string method, string dataPackage)
        {
            try
            {
                string endpoint = _endpoint;
                IAPIRequestFactory request = new APIRequestFactory()
                {
                    endPoint = endpoint + "Token/" + method,
                    httpMethod = httpVerb.POST,
                    authTech = AuthenticaitonTechnique.Token,
                    authType = Authenticationtype.BearerToken,
                    postJSON = dataPackage ?? string.Empty,
                    token = StaticDetails.LicenseAuthenticateResponse.RefreshToken
                };
                string response = string.Empty;
                HttpStatusCode status;
                (response, status) = await request.MakeStringRequest();
                if (status != HttpStatusCode.OK)
                {

                }
                return response;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "TokenQueries.MakePostRequest: suppressed exception");
                return ex.Message;
            }
        }
        private async Task<string> MakePutRequest(string method, string dataPackage)
        {
            try
            {
                string endpoint = _endpoint;
                IAPIRequestFactory request = new APIRequestFactory()
                {
                    endPoint = endpoint + "Token/" + method,
                    httpMethod = httpVerb.PUT,
                    authTech = AuthenticaitonTechnique.Token,
                    authType = Authenticationtype.BearerToken,
                    postJSON = dataPackage ?? string.Empty
                };
                string response = string.Empty;
                HttpStatusCode status;
                (response, status) = await request.MakeStringRequest();
                if (status != HttpStatusCode.OK)
                {

                }
                return response;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "TokenQueries.MakePutRequest: suppressed exception");
                return ex.Message;
            }
        }
        #endregion


        public async Task<LicenseAuthenticateResponse> Authenticate()
        {
            ///Hub-federation gate: the license bootstrap needs BOTH an authentication endpoint and
            ///a hub credential. Closed gate -> no HTTP, no logging, no Guid.Parse crash on a
            ///missing LicenseKey -- just the same null an unreachable hub already produced.
            if (!_federation.CanReachAuthenticationApi || !_federation.HasLicenseKey)
            {
                return null;
            }
            string dataPackage = string.Empty;
            string method = string.Empty;
            string result = string.Empty;
            try
            {
                LicenseAuthenticationRequest request = new LicenseAuthenticationRequest()
                {
                    LicenseKey = Guid.Parse(_federation.LicenseKey)
                };
                dataPackage = JsonConvert.SerializeObject(request);
                method = "authenticate";
                result = await MakeAuthenticationPostRequest(method, dataPackage);
                LicenseAuthenticateResponse output = JsonConvert.DeserializeObject<LicenseAuthenticateResponse>(result);
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        public async Task<LicenseAuthenticateResponse> Get(LicenseAuthenticationRequest input)
        {
            ///Hub-federation gate: closed -> quiet null, no HTTP attempt.
            if (!_federation.CanReachAuthenticationApi)
            {
                return null;
            }
            string dataPackage = string.Empty;
            string method = string.Empty;
            string result = string.Empty;
            try
            {
                dataPackage = JsonConvert.SerializeObject(input);
                method = "authentication";
                result = await MakeAuthenticationPostRequest(method, dataPackage);
                LicenseAuthenticateResponse output = JsonConvert.DeserializeObject<LicenseAuthenticateResponse>(result);
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        public async Task<LicenseAuthenticateResponse> Refresh(string input)
        {
            ///Hub-federation gate: closed -> quiet null, no HTTP attempt.
            if (!_federation.CanReachAuthenticationApi)
            {
                return null;
            }
            string dataPackage = string.Empty;
            string method = string.Empty;
            string result = string.Empty;
            try
            {
                result = await MakePostRequest(method, dataPackage);
                LicenseAuthenticateResponse output = JsonConvert.DeserializeObject<LicenseAuthenticateResponse>(result);
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<bool> RenewToken()
        {
            ///Hub-federation gate: RenewToken is the retry hook every DataApi Remote query calls
            ///on a non-OK response. Closed gate -> fail FAST and QUIETLY (false, no HTTP, no
            ///logging) so a hub-less node never spins through authenticate/refresh attempts.
            if (!_federation.CanReachAuthenticationApi)
            {
                return false;
            }
            bool output = default;
            try
            {
                LicenseAuthenticateResponse newTokens = default;
                //try refresh
                if (StaticDetails.LicenseAuthenticateResponse != default)
                {
                    newTokens = await Refresh(StaticDetails.LicenseAuthenticateResponse.RefreshToken);
                }
                
                if(newTokens!= null && newTokens != default && !string.IsNullOrEmpty(newTokens.JwtToken)&& string.IsNullOrEmpty(newTokens.RefreshToken))
                {
                    StaticDetails.LicenseAuthenticateResponse.JwtToken = newTokens.JwtToken;
                }
                else if (newTokens != null && !string.IsNullOrEmpty(newTokens.JwtToken) && !string.IsNullOrEmpty(newTokens.RefreshToken))
                {
                    StaticDetails.LicenseAuthenticateResponse = newTokens;
                }
                else
                {
                    newTokens = await Authenticate();
                    StaticDetails.LicenseAuthenticateResponse = newTokens;
                }
                if (newTokens != default)
                {
                    output = true;
                }
                //try get
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        #region Get
        //public async Task<LicenseAuthenticateResponse> Authenticate()
        //{
        //    string dataPackage = string.Empty;
        //    string method = string.Empty;
        //    string result = string.Empty;
        //    try
        //    {
        //        LicenseAuthenticationRequest request = new LicenseAuthenticationRequest()
        //        {
        //            LicenseKey = Guid.Parse(StaticDetails.PassedBackConfig.GetValue<string>("LicenseKey"))
        //        };
        //        dataPackage = JsonConvert.SerializeObject(request);
        //        method = "authentication";
        //        result = await MakeAuthenticationGetRequest(method, dataPackage);
        //        LicenseAuthenticateResponse output = JsonConvert.DeserializeObject<LicenseAuthenticateResponse>(result);
        //        return output;
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //}
        //public async Task<LicenseAuthenticateResponse> Get(LicenseAuthenticationRequest input)
        //{
        //    string dataPackage = string.Empty;
        //    string method = string.Empty;
        //    string result = string.Empty;
        //    try
        //    {
        //        dataPackage = JsonConvert.SerializeObject(input);
        //        method = "authentication";
        //        result = await MakeAuthenticationGetRequest(method, dataPackage);
        //        LicenseAuthenticateResponse output = JsonConvert.DeserializeObject<LicenseAuthenticateResponse>(result);
        //        return output;
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //}
        //public async Task<LicenseAuthenticateResponse> Refresh(string input)
        //{
        //    string dataPackage = string.Empty;
        //    string method = string.Empty;
        //    string result = string.Empty;
        //    try
        //    {
        //        result = await MakeGetRequest(method, dataPackage);
        //        LicenseAuthenticateResponse output = JsonConvert.DeserializeObject<LicenseAuthenticateResponse>(result);
        //        return output;
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //}




        #endregion

    }
}
