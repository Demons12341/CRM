using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.Entities;
using ProjectManagementSystem.Services.Interfaces;

namespace ProjectManagementSystem.Services.Implementations
{
    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;

        private static readonly HashSet<string> DataScopeCodes = new()
        {
            "project.view_all",
            "project.edit_all",
            "project.manage_members_all",
            "task.view_all",
            "task.edit_all",
            "task.modify_due_date",
            "file.access_all",
            "dashboard.view_all",
            "alert.edit_all"
        };

        public PermissionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasPermissionAsync(int userId, string permissionCode)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (user == null) return false;

            return HasPermission(user, permissionCode);
        }

        public bool HasPermission(User user, string permissionCode)
        {
            if (user?.Role == null) return false;

            if (user.Role.Name == "管理员") return true;

            var permissions = ParsePermissions(user.Role.Permissions);

            return permissions.Contains("*") || permissions.Contains(permissionCode);
        }

        public bool IsAdmin(User user)
        {
            return user?.Role?.Name == "管理员" || HasPermission(user, "*");
        }

        public async Task<bool> IsAdminAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            return user != null && IsAdmin(user);
        }

        public static List<string> ParsePermissions(string? permissionsText)
        {
            if (string.IsNullOrWhiteSpace(permissionsText))
            {
                return new List<string>();
            }

            try
            {
                var jsonResult = JsonSerializer.Deserialize<List<string>>(permissionsText);
                if (jsonResult != null)
                {
                    return jsonResult.Where(code => !string.IsNullOrWhiteSpace(code)).ToList();
                }
            }
            catch
            {
            }

            return permissionsText
                .Split(new[] { ',', '，', ';', '；', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();
        }
    }
}
