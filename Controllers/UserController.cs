using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Models.Requests;
using BoardWalk.Api.Services.Models.Responses;

namespace BoardWalk.Api.Controllers
{
    [Route("api/auth")]
    public class UserController : ApiControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Registers a new user account and immediately issues a JWT bearer token,
        /// so the caller is logged in without needing a separate login request.
        /// </summary>
        /// <param name="request">First name, last name, email, and password for the new account.</param>
        /// <returns>
        /// 201 Created with the new user's ID and a JWT on success.
        /// 409 Conflict if the email is already registered.
        /// </returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
        {
            var result = await _userService.RegisterAsync(request);

            if (result == null)
                return FailResponse("An account with this email already exists.", statusCode: 409);

            return SuccessResponse(result, "Account created successfully.", statusCode: 201);
        }

        /// <summary>
        /// Authenticates a user and issues a JWT bearer token.
        /// </summary>
        /// <param name="request">Email and password.</param>
        /// <returns>
        /// 200 OK with the JWT and user details on success.
        /// 401 Unauthorized if the credentials are invalid.
        /// </returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _userService.LoginAsync(request);
                return SuccessResponse(result, "Login successful.");
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message, statusCode: 401);
            }
        }

        /// <summary>
        /// Returns the currently authenticated user's ID and email, taken from their JWT claims.
        /// </summary>
        /// <returns>200 OK with the current user's identity.</returns>
        [Authorize]
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            var email = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value;
            return SuccessResponse(new { userId = CurrentUserId, email }, "Current user retrieved.");
        }

        /// <summary>
        /// Updates the currently authenticated user's profile: first name, last name, and email.
        /// Optionally changes the password in the same call — include both <c>CurrentPassword</c>
        /// and <c>NewPassword</c> in the request body to do so; omit both to leave the password unchanged.
        /// </summary>
        /// <param name="request">The updated profile fields, and optional password change fields.</param>
        /// <returns>
        /// 200 OK on success.
        /// 409 Conflict if the new email is already used by another account.
        /// 400 Bad Request if the password fields are provided inconsistently, or the current password is wrong.
        /// </returns>
        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                await _userService.UpdateProfileAsync(CurrentUserId, request);
                return SuccessResponse<object>(null, "Profile updated successfully.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already in use"))
            {
                return FailResponse(ex.Message, statusCode: 409);
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message, statusCode: 400);
            }
        }

        /// <summary>
        /// Requests a password reset email. Always returns the same success response
        /// whether or not the email belongs to an existing account, to prevent
        /// attackers from using this endpoint to discover which emails are registered.
        /// </summary>
        /// <param name="request">The account email.</param>
        /// <returns>200 OK, always — check your email/console log for the reset link in development.</returns>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            await _userService.ForgotPasswordAsync(request);
            return SuccessResponse<object>(null, "If an account exists with that email, a reset link has been sent.");
        }

        /// <summary>
        /// Resets a user's password using the token emailed by /forgot-password.
        /// </summary>
        /// <param name="request">The raw token from the reset link, and the new password.</param>
        /// <returns>200 OK on success. 400 if the token is invalid, expired, or already used.</returns>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                await _userService.ResetPasswordAsync(request);
                return SuccessResponse<object>(null, "Password reset successfully.");
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message, statusCode: 400);
            }
        }
    }
}