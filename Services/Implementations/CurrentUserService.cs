using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Models;
using BoardWalk.Api.Services.Models.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BoardWalk.Api.Services.Implementations
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public LoggedInUser GetCurrentUser()
        {
            var user = _httpContextAccessor.HttpContext?.User
                ?? throw new InvalidOperationException("No active HTTP context — GetCurrentUser() can only be called during a request.");

            var idClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? throw new InvalidOperationException("Token is missing the 'sub' claim.");

            var email = user.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? string.Empty;
            var firstName = user.FindFirst(ClaimTypes.GivenName)?.Value ?? string.Empty;
            var lastName = user.FindFirst(ClaimTypes.Surname)?.Value ?? string.Empty;

            return new LoggedInUser
            {
                Id = Guid.Parse(idClaim),
                Email = email,
                FirstName = firstName,
                LastName = lastName
            };
        }
    }
}