using BoardWalk.Api.Services.Implementations;
using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Models.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

namespace BoardWalk.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Registers a new user with the provided details.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("register")]
        [ProducesResponseType(201)]
        [ProducesResponseType(409)]
        
        public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
        {
            
            var result = await _userService.RegisterAsync(request);

            if (result == null)
            {
                return Conflict(new { message = "An account with this email already exists." });
            }

            return StatusCode(201, result);
        }

        /// <summary>
        /// Authenticates a user and returns a JWT token if successful.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("login")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> Login([FromBody] Services.Models.Requests.LoginRequest request)
        {
            try
            {
                var result = await _userService.LoginAsync(request);
                return Ok(result); 
            }
            catch (InvalidOperationException ex)
            {
                return Unauthorized(new { message = ex.Message }); 
            }
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            // These claims come directly from the validated JWT —
            // no database call needed to know who's asking.
            var userId = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            var email = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value;

            return Ok(new { userId, email });
        }
    }
}
