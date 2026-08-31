// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.AuthorizationLogic
{
    public static class TokenClaims
    {
        #region Get Token Claims
        public static async Task<T> GetTokenClaim<T>(this string token, string claimKey)
        //, IConfiguration _config)//this may not be needed
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            //var key = Encoding.ASCII.GetBytes(_config.GetValue<string>("JwtSettings:Secret"));//may not be needed
            var readToken = tokenHandler.ReadJwtToken(token);
            var claim = readToken.Claims.FirstOrDefault(i => i.Type == claimKey).Value;
            //var jsonData = await cache.GetStringAsync(claim);
            if (claim is null)
            {
                return default(T);
            }

            return JsonSerializer.Deserialize<T>(claim);
        }
        #endregion
    }
}
