using BoardWalk.Api.Services.Models.Requests;
using BoardWalk.Api.Services.Models.Responses;

namespace BoardWalk.Api.Services.Interfaces
{
    public interface IUserService
    {
        /// <summary>
        /// Registers a new user with the provided details, then immediately issues a JWT
        /// so the frontend can treat them as logged in without a separate login call.
        /// </summary>
        /// <param name="request">First name, last name, email, and password.</param>
        /// <returns>The new user's token and profile info (same shape as login), or null if the email is already registered.</returns>
        Task<LoginResponse?> RegisterAsync(RegisterUserRequest request);

        /// <summary>
        /// Logs in a user with the provided email and password, returning a JWT and profile info if successful.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Task<LoginResponse> LoginAsync(LoginRequest request);

        /// <summary>
        /// Updates the current user's first name, last name, and email. If NewPassword is
        /// supplied in the request, also verifies CurrentPassword and updates the password
        /// (generating a new salt) in the same operation.
        /// </summary>
        /// <param name="userId">The authenticated user's ID, taken from their JWT — never trust a client-supplied ID here.</param>
        /// <param name="request">The new profile values, and optionally a password change.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the user doesn't exist, the new email is already taken by another account,
        /// NewPassword was given without a matching correct CurrentPassword, or CurrentPassword
        /// was given without a NewPassword.
        /// </exception>
        Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request);

        /// <summary>
        /// If an account exists with the given email, generates a reset token and emails a
        /// reset link. Always completes successfully regardless of whether the email exists,
        /// to avoid revealing which emails are registered.
        /// </summary>
        Task ForgotPasswordAsync(ForgotPasswordRequest request);

        /// <summary>
        /// Resets a user's password using a valid, unexpired, unused token from ForgotPasswordAsync.
        /// </summary>
        /// <exception cref="InvalidOperationException">Token is invalid, expired, or already used.</exception>
        Task ResetPasswordAsync(ResetPasswordRequest request);
    }
}
