// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.LogicLayer.Logic;
using Febris.UserNode.Portal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.Portal.Controllers
{
    //[Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    [Authorize(Roles = Febris.Constants.RoleConstants.EndUserAll)]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWidgetLogic _context;
        

        public HomeController(
            ILogger<HomeController> logger,
            IWidgetLogic context
           )
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Index", "Switchboard");
        }

        public IActionResult Privacy()
        {
            return View();
        }




        // AllowAnonymous because this is the UseExceptionHandler("/Home/Error") target: without
        // it the class [Authorize] bounced an anonymous user who hit an unhandled exception to
        // the login page instead of the error page (ROADMAP 17). The view renders only the
        // request id, so there is nothing here to protect.
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ModelLibrary.ViewModels.ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
