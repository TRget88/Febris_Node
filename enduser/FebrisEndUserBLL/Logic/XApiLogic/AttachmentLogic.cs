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
    public class AttachmentLogic: IAttachmentLogic
    {
        private readonly IAttachmentQueries _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        // DI refactor
        public AttachmentLogic(IHttpContextAccessor httpContextAccessor, IAttachmentQueries context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            User = _httpContextAccessor?.HttpContext?.User;
        }

        public AttachmentLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = new AttachmentQueries();
            User = _httpContextAccessor.HttpContext.User;
        }

    }

    public interface IAttachmentLogic
    {
    }
}
