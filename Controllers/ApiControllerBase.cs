using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using BoardWalk.Api.Services.Models.Responses;

namespace BoardWalk.Api.Controllers
{
    [ApiController]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

        /// <summary>
        /// Wraps a successful result in the standard ApiResponse envelope and returns it
        /// with the matching HTTP status code (default 200).
        /// </summary>
        protected IActionResult SuccessResponse<T>(T? data, string message = "Request successful.", int statusCode = 200)
        {
            var response = ApiResponse<T>.Success(data, message, statusCode);
            return StatusCode(statusCode, response);
        }

        /// <summary>
        /// Wraps a failed result in the standard ApiResponse envelope and returns it
        /// with the matching HTTP status code (default 400).
        /// </summary>
        protected IActionResult FailResponse(string message, int statusCode = 400)
        {
            var response = ApiResponse<object>.Fail(message, statusCode);
            return StatusCode(statusCode, response);
        }
    }
}