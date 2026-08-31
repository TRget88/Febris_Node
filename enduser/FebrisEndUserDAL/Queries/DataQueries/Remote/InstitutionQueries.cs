// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    public interface IInstitutionQueries
    {
    }

    public class InstitutionQueries: IInstitutionQueries
    {
        private readonly string _jwtToken;
        public string _endpoint;
        ///The ONE hub-federation gate. Resolved from the same
        ///passed-back configuration this legacy class always read (null-safe; a
        ///config-less process gets a CLOSED gate, never an NRE). Every request helper
        ///below consults it before any HTTP is attempted.
        private readonly Febris.ModelLibrary.ViewModels.IHubFederationSettings _federation;
        private readonly ITokenQueries _tokenContext;
        public InstitutionQueries()
        {
            _federation = Febris.ModelLibrary.ViewModels.HubFederationSettings.Resolve(StaticDetails.PassedBackConfig, Febris.SharedServices.JwtSigningKeyProvider.IsUnsubstitutedTemplate);
            _endpoint = _federation.DataApi;
            _tokenContext = new TokenQueries();
        }

        public InstitutionQueries(IHttpContextAccessor httpContextAccessor)
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
        public InstitutionQueries(IHttpContextAccessor httpContextAccessor, Febris.ModelLibrary.ViewModels.IHubFederationSettings federation)
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
                    endPoint = _endpoint + "Institution/" + method,
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
                Febris.SharedServices.FebrisLog.Error(ex, "InstitutionQueries.MakeGetRequest: suppressed exception");
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
                    endPoint = _endpoint + "Institution/" + method,
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
                Febris.SharedServices.FebrisLog.Error(ex, "InstitutionQueries.MakePostRequest: suppressed exception");
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
                    endPoint = _endpoint + "Institution/" + method,
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
                Febris.SharedServices.FebrisLog.Error(ex, "InstitutionQueries.MakePutRequest: suppressed exception");
                return ex.Message;
            }
        }
        #endregion

        #region Get
        public async Task<List<Institution>> Get(List<Guid> input)
        {
            List<Institution> output = new List<Institution>();
            string dataPackage = string.Empty;
            string method = string.Empty;
            string result = string.Empty;
            try
            {
                dataPackage = JsonConvert.SerializeObject(input);
                method = "GetListByUUID";
                result = await MakeGetRequest(method, dataPackage);
                output = string.IsNullOrEmpty(result) ? output : JsonConvert.DeserializeObject<List<Institution>>(result);
                //output = await _dataDbContext.Verb.Where(i => i.Id == input).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<Institution> Get(Guid? input)
        {
            string dataPackage = string.Empty;
            string method = string.Empty;
            string result = string.Empty;
            try
            {
                method = "getbyuuid/" + input.ToString();
                //method = input.ToString();
                result = await MakeGetRequest(method, dataPackage);
                Institution output = string.IsNullOrEmpty(result) ? new Institution() : JsonConvert.DeserializeObject<Institution>(result);
                //output = await _dataDbContext.Verb.Where(i => i.Id == input).FirstOrDefaultAsync();
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<List<Institution>> Get()
        {
            List<Institution> output = new List<Institution>();
            string dataPackage = string.Empty;
            string method = string.Empty;
            string result = string.Empty;
            try
            {
                // method = input.ToString();
                result = await MakeGetRequest(method, dataPackage);
                output = string.IsNullOrEmpty(result) ? output : JsonConvert.DeserializeObject<List<Institution>>(result);
                //output = await _dataDbContext.Verb.Where(i => i.Id == input).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
            }
            return output;
        }

        public async Task<Institution> Get(long input)
        {
            string dataPackage = string.Empty;
            string method = string.Empty;
            string result = string.Empty;
            try
            {
                method = JsonConvert.SerializeObject(input);
                //method = input.ToString();
                result = await MakeGetRequest(method, dataPackage);
                Institution output = string.IsNullOrEmpty(result) ? new Institution() : JsonConvert.DeserializeObject<Institution>(result);
                //output = await _dataDbContext.Verb.Where(i => i.Id == input).FirstOrDefaultAsync();
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }



        //public async Task<Tag> Get(Uri input)
        //{
        //    string dataPackage = string.Empty;
        //    string method = string.Empty;
        //    string result = string.Empty;
        //    try
        //    {
        //        method = JsonConvert.SerializeObject(input);
        //        //method = input.ToString();
        //        result = await MakeGetRequest(method, dataPackage);
        //        Tag output = JsonConvert.DeserializeObject<Tag>(result);
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
