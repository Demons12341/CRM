using ProjectManagementSystem.Models.DTOs;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface IRoleService
    {
        Task<List<RoleDto>> GetRolesAsync();
        Task<RoleDto> GetRoleByIdAsync(int id);
        Task<RoleDto> CreateRoleAsync(CreateRoleRequest request);
        Task<RoleDto> UpdateRoleAsync(int id, UpdateRoleRequest request);
        Task<bool> DeleteRoleAsync(int id);
        Task<List<MenuOptionDto>> GetMenuOptionsAsync();
        Task<List<string>> GetRoleMenuPermissionsAsync(int roleId);
        Task<bool> UpdateRoleMenuPermissionsAsync(int roleId, List<string> menuCodes);
    }

    public class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Permissions { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateRoleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Permissions { get; set; }
    }

    public class UpdateRoleRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Permissions { get; set; }
    }
}
