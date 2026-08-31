// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.AspNetCore.Authentication;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Claims
{
    public class FebrisClaimsTransformer : IClaimsTransformation
    {
        private readonly ApplicationDbContext _context;
        public FebrisClaimsTransformer(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            var existingClaimsIdentity = (ClaimsIdentity)principal.Identity;
            //existingClaimsIdentity.AddClaim(new Claim("idk", "not sure what this one is for"));
            return new ClaimsPrincipal(principal);
        }
    }
}
