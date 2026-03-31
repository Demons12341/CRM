using ProjectManagementSystem.Models.DTOs;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request);
        Task<UserInfo> GetCurrentUserAsync(int userId);
    }
}
