using BoardWalk.Api.Services.Models;
using BoardWalk.Api.Services.Models.Common;

namespace BoardWalk.Api.Services.Interfaces
{
    /// <summary>
    /// Reads the currently authenticated user's identity from the validated JWT
    /// attached to the current HTTP request. Available to any service, not just controllers.
    /// </summary>
    public interface ICurrentUserService
    {
        /// <summary>
        /// The current user's identity, built from their JWT claims.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if called outside an authenticated request context (e.g. no HttpContext,
        /// or the required claims are missing) — this should never happen on an
        /// [Authorize]-protected endpoint, so if it does, something is misconfigured.
        /// </exception>
        LoggedInUser GetCurrentUser();
    }
}