using ProjectManagementSystem.Models.DTOs;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface IUserService
    {
        Task<PaginatedResult<UserDto>> GetUsersAsync(UserListRequest request);
        Task<UserDto> GetUserByIdAsync(int id);
        Task<UserDto> CreateUserAsync(CreateUserRequest request);
        Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request);
        Task<bool> DeleteUserAsync(int id);
    }
}
