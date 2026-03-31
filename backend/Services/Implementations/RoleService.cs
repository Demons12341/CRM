using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Services.Interfaces;
using System.Text.Json;

namespace ProjectManagementSystem.Services.Implementations
{
    public class RoleService : IRoleService
    {
        private readonly ApplicationDbContext _context;

        public RoleService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<RoleDto>> GetRolesAsync()
        {
            return await _context.Roles
                .Select(r => new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    Permissions = r.Permissions,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<RoleDto> GetRoleByIdAsync(int id)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
            if (role == null)
            {
                throw new KeyNotFoundException("角色不存在");
            }

            return new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                Permissions = role.Permissions,
                CreatedAt = role.CreatedAt
            };
        }

        public async Task<RoleDto> CreateRoleAsync(CreateRoleRequest request)
        {
            if (await _context.Roles.AnyAsync(r => r.Name == request.Name))
            {
                throw new InvalidOperationException("角色名称已存在");
            }

            var role = new ProjectManagementSystem.Models.Entities.Role
            {
                Name = request.Name,
                Description = request.Description,
                Permissions = request.Permissions,
                CreatedAt = DateTime.UtcNow
            };

            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            return await GetRoleByIdAsync(role.Id);
        }

        public async Task<RoleDto> UpdateRoleAsync(int id, UpdateRoleRequest request)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
            if (role == null)
            {
                throw new KeyNotFoundException("角色不存在");
            }

            if (request.Name != null)
            {
                if (await _context.Roles.AnyAsync(r => r.Name == request.Name && r.Id != id))
                {
                    throw new InvalidOperationException("角色名称已存在");
                }
                role.Name = request.Name;
            }

            if (request.Description != null)
            {
                role.Description = request.Description;
            }

            if (request.Permissions != null)
            {
                role.Permissions = request.Permissions;
            }

            await _context.SaveChangesAsync();

            return await GetRoleByIdAsync(id);
        }

        public async Task<bool> DeleteRoleAsync(int id)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
            if (role == null)
            {
                throw new KeyNotFoundException("角色不存在");
            }

            // 检查是否有用户使用该角色
            if (await _context.Users.AnyAsync(u => u.RoleId == id))
            {
                throw new InvalidOperationException("该角色下还有用户，无法删除");
            }

            role.IsDeleted = true;
            await _context.SaveChangesAsync();

            return true;
        }

        public Task<List<MenuOptionDto>> GetMenuOptionsAsync()
        {
            return System.Threading.Tasks.Task.FromResult(BuildMenuOptions());
        }

        public async Task<List<string>> GetRoleMenuPermissionsAsync(int roleId)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
            if (role == null)
            {
                throw new KeyNotFoundException("角色不存在");
            }

            var allCodes = BuildMenuOptions()
                .SelectMany(FlattenMenuCodes)
                .Distinct()
                .ToList();

            if (role.Name == "管理员")
            {
                return allCodes;
            }

            return ParsePermissions(role.Permissions)
                .Where(code => allCodes.Contains(code))
                .Distinct()
                .ToList();
        }

        public async Task<bool> UpdateRoleMenuPermissionsAsync(int roleId, List<string> menuCodes)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
            if (role == null)
            {
                throw new KeyNotFoundException("角色不存在");
            }

            if (role.Name == "管理员")
            {
                throw new InvalidOperationException("管理员默认拥有全部权限，无需分配");
            }

            var allCodes = BuildMenuOptions()
                .SelectMany(FlattenMenuCodes)
                .Distinct()
                .ToHashSet();

            var normalizedCodes = (menuCodes ?? new List<string>())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Distinct()
                .ToList();

            if (normalizedCodes.Any(code => !allCodes.Contains(code)))
            {
                throw new InvalidOperationException("存在无效菜单权限标识");
            }

            role.Permissions = JsonSerializer.Serialize(normalizedCodes);
            await _context.SaveChangesAsync();
            return true;
        }

        private static List<MenuOptionDto> BuildMenuOptions()
        {
            return new List<MenuOptionDto>
            {
                new() { Code = "dashboard", Name = "仪表盘" },
                new() { Code = "projects", Name = "项目管理" },
                new() { Code = "tasks", Name = "任务管理" },
                new() { Code = "files", Name = "文件管理" },
                new() { Code = "alerts", Name = "超期告警" },
                new() { Code = "processes", Name = "项目任务模板" },
                new()
                {
                    Code = "settings",
                    Name = "系统设置",
                    Children = new List<MenuOptionDto>
                    {
                        new() { Code = "settings.users", Name = "用户管理" },
                        new() { Code = "settings.roles", Name = "角色管理" },
                        new() { Code = "settings.menu", Name = "菜单权限" },
                        new() { Code = "settings.profile", Name = "个人设置" }
                    }
                }
            };
        }

        private static IEnumerable<string> FlattenMenuCodes(MenuOptionDto menu)
        {
            yield return menu.Code;

            foreach (var child in menu.Children)
            {
                foreach (var code in FlattenMenuCodes(child))
                {
                    yield return code;
                }
            }
        }

        private static List<string> ParsePermissions(string? permissionsText)
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
