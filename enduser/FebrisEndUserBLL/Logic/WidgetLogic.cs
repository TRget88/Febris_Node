// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.LegalModels;
using Febris.ModelLibrary.Models.UserModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic
{
    public interface IWidgetLogic
    {
        Task<byte[]> RemoteImageLoader(string path);
    }

    public class WidgetLogic : IWidgetLogic
    {
        private readonly IWidgetQueries _context;
        //private readonly IHardwareQueries _hardwareContext;
        //private readonly ICohortQueries _cohortContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<LocalApplicationUser> _userManager;
        private readonly ClaimsPrincipal User;
        //private readonly IUserClaimsPrincipalFactory<LocalApplicationUser> _claimsFactory;
        private readonly SignInManager<LocalApplicationUser> _signInManager;

        // DI refactor
        public WidgetLogic(
            SignInManager<LocalApplicationUser> signInManager,
            IHttpContextAccessor httpContextAccessor,
            UserManager<LocalApplicationUser> userManager,
            IWidgetQueries context
            )
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            //_cohortContext = new CohortQueries();
            //_hardwareContext = new HardwareQueries();
            User = _httpContextAccessor?.HttpContext?.User;
            _userManager = userManager;
            _signInManager = signInManager;
            //_claimsFactory = claimsFactory;
        }

        public WidgetLogic(
            SignInManager<LocalApplicationUser> signInManager,
            IHttpContextAccessor httpContextAccessor,
            UserManager<LocalApplicationUser> userManager//,
            //IUserClaimsPrincipalFactory<LocalApplicationUser> claimsFactory
            )
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new WidgetQueries();
            //_cohortContext = new CohortQueries();
            //_hardwareContext = new HardwareQueries();
            User = _httpContextAccessor.HttpContext.User;
            _userManager = userManager;
            _signInManager = signInManager;
            //_claimsFactory = claimsFactory;
        }

        public async Task<byte[]> RemoteImageLoader(string path)
        {
            byte[] output = { };
            try
            {
                output = await _context.RemoteImageLoader(path);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
    }

}
