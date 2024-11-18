// <copyright file="AspNetCoreUserContext.cs" company="KPMG">
//    Copyright (c) KPMG. All rights reserved.
// </copyright>

namespace ApiGateway.Extensions
{
    using System.Security.Claims;
    using ApiGateway.Identity.context;
    using Microsoft.AspNetCore.Http;

    public class AspNetCoreUserContext : IUserContext
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        public AspNetCoreUserContext(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        public ClaimsPrincipal User => this.httpContextAccessor.HttpContext?.User;
    }
}