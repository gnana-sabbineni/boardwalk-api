using BoardWalk.Api.Services.Models.Requests;
using BoardWalk.Api.Services.Models.Responses;

namespace BoardWalk.Api.Services.Interfaces
{
    public interface IUserService
    {
        Task<Guid?> RegisterAsync(RegisterUserRequest request);
        Task<LoginResponse> LoginAsync(LoginRequest request);
    }
}
