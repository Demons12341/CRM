using ProjectManagementSystem.Models.Entities;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface IPermissionService
    {
        Task<bool> HasPermissionAsync(int userId, string permissionCode);
        bool HasPermission(User user, string permissionCode);
        bool IsAdmin(User user);
        Task<bool> IsAdminAsync(int userId);
    }
}
