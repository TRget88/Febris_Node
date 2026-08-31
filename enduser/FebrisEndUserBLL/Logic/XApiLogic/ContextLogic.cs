// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace Febris.PrimaryLogicLayer.Logic.XApiLogic
{
    public class ContextLogic: IContextLogic
    {
        private readonly IContextQueries _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        public ContextLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new ContextQueries();
            User = _httpContextAccessor.HttpContext.User;
        }

        // DI refactor
        public ContextLogic(IHttpContextAccessor httpContextAccessor, IContextQueries context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            User = _httpContextAccessor?.HttpContext?.User;
        }



    }

    public interface IContextLogic
    {
    }
}
