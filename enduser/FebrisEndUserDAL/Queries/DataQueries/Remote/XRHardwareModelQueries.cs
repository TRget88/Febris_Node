// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    public interface IXRHardwareModelQueries
    {
        Task<List<XRHardwareModel>> Get();
        Task<XRHardwareModel> Get(Guid? input);
        Task<XRHardwareModel> Get(long? input);
    }

    public class XRHardwareModelQueries: IXRHardwareModelQueries
    {
        public string _endpoint;
        ///The ONE hub-federation gate. Resolved from the same
        ///passed-back configuration this legacy class always read (null-safe; a
        ///config-less process gets a CLOSED gate, never an NRE). Every request helper
        ///below consults it before any HTTP is attempted.
        private readonly Febris.ModelLibrary.ViewModels.IHubFederationSettings _federation;
        private readonly ITokenQueries _tokenContext;

        public  XRHardwareModelQueries()
        {
            _federation = Febris.ModelLibrary.ViewModels.HubFederationSettings.Resolve(StaticDetails.PassedBackConfig, Febris.SharedServices.JwtSigningKeyProvider.IsUnsubstitutedTemplate);
            _endpoint = _federation.DataApi;
            _tokenContext = new TokenQueries();

        }

        public  XRHardwareModelQueries(IHttpContextAccessor httpContextAccessor)
        {
            _federation = Febris.ModelLibrary.ViewModels.HubFederationSettings.Resolve(StaticDetails.PassedBackConfig, Febris.SharedServices.JwtSigningKeyProvider.IsUnsubstitutedTemplate);
            _endpoint = _federation.DataApi;
            _tokenContext = new TokenQueries();

        }

        /// <summary>DI ctor (): the host-registered gate flows in --
        /// DB-first when the operator saved a HubFederationConfig row via the portal's Hub
        /// Federation page, config resolution otherwise. Strict superset of the legacy ctors so
        /// MS.DI's greedy selection prefers it wherever AddFebrisUserNodeDataAccess
        /// registered <see cref="Febris.ModelLibrary.ViewModels.IHubFederationSettings"/>.</summary>
        public XRHardwareModelQueries(IHttpContextAccessor httpContextAccessor, Febris.ModelLibrary.ViewModels.IHubFederationSettings federation)
        {
            _federation = federation ?? Febris.ModelLibrary.ViewModels.HubFederationSettings.Disabled();
            _endpoint = _federation.DataApi;
            _tokenContext = new TokenQueries(httpContextAccessor, federation);

        }


        #region Requests
        private async Task<string> MakeGetRequest(string method, string dataPackage)
        {
            try
            {
                ///Hub-federation gate: no hub configured -> local-only.
                ///No HTTP attempt, nothing logged; the empty result flows into the same quiet
                ///defaults the callers already tolerate for an unreachable hub.
                if (!_federation.CanReachDataApi)
                {
                    return string.Empty;
                }
                //string cookie = TempData["ThinMint"].Value;
                IAPIRequestFactory request = new APIRequestFactory()
                {
                    endPoint = _endpoint + "XRHardwareModel/" + method,
                    httpMethod = httpVerb.GET,
                    authTech = AuthenticaitonTechnique.Token,
                    authType = Authenticationtype.BearerToken,
                    token = StaticDetails.LicenseAuthenticateResponse?.JwtToken ?? string.Empty,
                    postJSON = dataPackage ?? string.Empty,
                };
                string response = string.Empty;
                HttpStatusCode status;
                (response, status) = await request.MakeStringRequest();
                if (status != HttpStatusCode.OK)
                {
                    bool complete = await _tokenContext.RenewToken();
                    if (complete)
                    {
                        request.token = StaticDetails.LicenseAuthenticateResponse.JwtToken;
                        (response, status) = await request.MakeStringRequest();
                    }
                }
                return response;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "XRHardwareModelQueries.MakeGetRequest: suppressed exception");
                return ex.Message;
            }
        }
        private async Task<string> MakePostRequest(string method, string dataPackage)
        {
            try
            {
                ///Hub-federation gate: no hub configured -> local-only.
                ///No HTTP attempt, nothing logged; the empty result flows into the same quiet
                ///defaults the callers already tolerate for an unreachable hub.
                if (!_federation.CanReachDataApi)
                {
                    return string.Empty;
                }
                IAPIRequestFactory request = new APIRequestFactory()
                {
                    endPoint = _endpoint + "XRHardwareModel/" + method,
                    httpMethod = httpVerb.POST,
                    authTech = AuthenticaitonTechnique.Token,
                    authType = Authenticationtype.BearerToken,
                    token = StaticDetails.LicenseAuthenticateResponse?.JwtToken ?? string.Empty,
                    postJSON = dataPackage ?? string.Empty,
                };
                string response = string.Empty;
                HttpStatusCode status;
                (response, status) = await request.MakeStringRequest();
                if (status != HttpStatusCode.OK)
                {
                    bool complete = await _tokenContext.RenewToken();
                    if (complete)
                    {
                        request.token = StaticDetails.LicenseAuthenticateResponse.JwtToken;
                        (response, status) = await request.MakeStringRequest();
                    }
                }
                return response;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "XRHardwareModelQueries.MakePostRequest: suppressed exception");
                return ex.Message;
            }
        }
        private async Task<string> MakePutRequest(string method, string dataPackage)
        {
            try
            {
                ///Hub-federation gate: no hub configured -> local-only.
                ///No HTTP attempt, nothing logged; the empty result flows into the same quiet
                ///defaults the callers already tolerate for an unreachable hub.
                if (!_federation.CanReachDataApi)
                {
                    return string.Empty;
                }
                IAPIRequestFactory request = new APIRequestFactory()
                {
                    endPoint = _endpoint + "XRHardwareModel/" + method,
                    httpMethod = httpVerb.PUT,
                    authTech = AuthenticaitonTechnique.Token,
                    authType = Authenticationtype.BearerToken,
                    token = StaticDetails.LicenseAuthenticateResponse?.JwtToken ?? string.Empty,
                    postJSON = dataPackage ?? string.Empty,
                };
                string response = string.Empty;
                HttpStatusCode status;
                (response, status) = await request.MakeStringRequest();
                if (status != HttpStatusCode.OK)
                {
                    bool complete = await _tokenContext.RenewToken();
                    if (complete)
                    {
                        request.token = StaticDetails.LicenseAuthenticateResponse.JwtToken;
                        (response, status) = await request.MakeStringRequest();
                    }
                }
                return response;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "XRHardwareModelQueries.MakePutRequest: suppressed exception");
                return ex.Message;
            }
        }
        #endregion

        #region Get
        public async Task<XRHardwareModel> Get(Guid? input)
        {
            string dataPackage = string.Empty;
            string method = string.Empty;
            string result = string.Empty;
            try
            {
                //method = JsonConvert.SerializeObject(input);
                method = "byuuid/"+input.ToString();
                result = await MakeGetRequest(method, dataPackage);
                 XRHardwareModel output = string.IsNullOrEmpty(result) ? new XRHardwareModel() : JsonConvert.DeserializeObject< XRHardwareModel>(result);
                //output = await _dataDbContext.Verb.Where(i => i.Id == input).FirstOrDefaultAsync();
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<List<XRHardwareModel>> Get()
        {
            List<XRHardwareModel> output = new List<XRHardwareModel>();
            string dataPackage = string.Empty;
            string method = string.Empty;
            string result = string.Empty;
            try
            {
                // method = input.ToString();
                result = await MakeGetRequest(method, dataPackage);
                output = string.IsNullOrEmpty(result) ? output : JsonConvert.DeserializeObject<List<XRHardwareModel>>(result);
                //output = await _dataDbContext.Verb.Where(i => i.Id == input).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<XRHardwareModel> Get(long? input)
        {
            string dataPackage = string.Empty;
            string method = string.Empty;
            string result = string.Empty;
            try
            {
                //method = JsonConvert.SerializeObject(input);
                method = input.ToString();
                result = await MakeGetRequest(method, dataPackage);
                 XRHardwareModel output = string.IsNullOrEmpty(result) ? new XRHardwareModel() : JsonConvert.DeserializeObject< XRHardwareModel>(result);
                //output = await _dataDbContext.Verb.Where(i => i.Id == input).FirstOrDefaultAsync();
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }



        //public async Task< XRHardwareModel> Get(Uri input)
        //{
        //    string dataPackage = string.Empty;
        //    string method = string.Empty;
        //    string result = string.Empty;
        //    try
        //    {
        //        method = JsonConvert.SerializeObject(input);
        //        //method = input.ToString();
        //        result = await MakeGetRequest(method, dataPackage);
        //         XRHardwareModel output = JsonConvert.DeserializeObject< XRHardwareModel>(result);
        //        //output = await _dataDbContext.Verb.Where(i => i.Id == input).FirstOrDefaultAsync();
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
