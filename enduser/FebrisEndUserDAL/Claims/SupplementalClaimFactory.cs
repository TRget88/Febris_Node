// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.UserModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Claims
{
    public class SupplementalClaimFactory : UserClaimsPrincipalFactory<LocalApplicationUser, ApplicationRole>
    {
        //private readonly ApplicationDbContext _context;
        public SupplementalClaimFactory(
            //ApplicationDbContext context,
            UserManager<LocalApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IOptions<IdentityOptions> options)
            : base(userManager, roleManager, options)
        {
            //_context = context;
        }

        protected async override Task<ClaimsIdentity> GenerateClaimsAsync(LocalApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            //identity.AddClaim(new Claim("HasProfilePicture", user.HasProfilePicture.ToString()));
            if (!string.IsNullOrEmpty(user.FirstName))
            {
                identity.AddClaim(new Claim("FirstName", user.FirstName.ToString()));
            }
            if (!string.IsNullOrEmpty(user.LastName))
            {
                identity.AddClaim(new Claim("LastName", user.LastName.ToString()));
            }
            if (!string.IsNullOrEmpty(user.ProfilePicturePath))
            {
                identity.AddClaim(new Claim("ProfilePicturePath", user.ProfilePicturePath));
            }           
            if (user.Institution != Guid.Empty)
            {
                identity.AddClaim(new Claim("Institution", user.Institution.ToString()));
            }            
            if (user.Actor != Guid.Empty)
            {
                identity.AddClaim(new Claim("Actor", user.Actor.ToString()));
            }
            if (user.LiabilityWaiver != Guid.Empty)
            {
                identity.AddClaim(new Claim("LiabilityWaiver", user.LiabilityWaiver.ToString()));
            }
            if (user.ServiceAgreement != Guid.Empty)
            {
                identity.AddClaim(new Claim("ServiceAgreement", user.ServiceAgreement.ToString()));
            }
            if (user.EULA != Guid.Empty && user.EULA != null)
            {
                identity.AddClaim(new Claim("EULA", user.EULA.ToString()));
            }


            return identity;
        }
    }
}
