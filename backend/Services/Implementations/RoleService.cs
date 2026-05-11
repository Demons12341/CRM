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
                new()
                {
                    Code = "projects",
                    Name = "项目管理",
                    Children = new List<MenuOptionDto>
                    {
                        new() { Code = "projects.create", Name = "新建项目" },
                        new() { Code = "projects.edit", Name = "编辑项目" },
                        new() { Code = "projects.delete", Name = "删除项目" },
                        new() { Code = "projects.members", Name = "管理成员" }
                    }
                },
                new()
                {
                    Code = "tasks",
                    Name = "任务管理",
                    Children = new List<MenuOptionDto>
                    {
                        new() { Code = "tasks.create", Name = "新建任务" },
                        new() { Code = "tasks.edit", Name = "编辑任务" },
                        new() { Code = "tasks.delete", Name = "删除任务" },
                        new() { Code = "tasks.status", Name = "变更状态" }
                    }
                },
                new()
                {
                    Code = "files",
                    Name = "文件管理",
                    Children = new List<MenuOptionDto>
                    {
                        new() { Code = "files.upload", Name = "上传文件" },
                        new() { Code = "files.delete", Name = "删除文件" },
                        new() { Code = "files.download", Name = "下载文件" }
                    }
                },
                new() { Code = "alerts", Name = "超期告警" },
                new() { Code = "processes", Name = "项目任务模板" },
                new()
                {
                    Code = "business-lines",
                    Name = "业务线管理",
                    Children = new List<MenuOptionDto>
                    {
                        new() { Code = "business-lines.create", Name = "新建业务线" },
                        new() { Code = "business-lines.edit", Name = "编辑业务线" },
                        new() { Code = "business-lines.delete", Name = "删除业务线" }
                    }
                },
                new()
                {
                    Code = "data_scope",
                    Name = "数据权限",
                    Children = new List<MenuOptionDto>
                    {
                        new() { Code = "project.view_all", Name = "查看所有项目" },
                        new() { Code = "project.edit_all", Name = "编辑所有项目" },
                        new() { Code = "project.manage_members_all", Name = "管理所有项目成员" },
                        new() { Code = "task.view_all", Name = "查看所有任务" },
                        new() { Code = "task.edit_all", Name = "编辑所有任务" },
                        new() { Code = "task.modify_due_date", Name = "修改任务截止日期" },
                        new() { Code = "file.access_all", Name = "访问所有项目文件" },
                        new() { Code = "dashboard.view_all", Name = "查看全部仪表盘" },
                        new() { Code = "alert.edit_all", Name = "编辑所有超期原因" }
                    }
                },
                new()
                {
                    Code = "settings",
                    Name = "系统设置",
                    Children = new List<MenuOptionDto>
                    {
                        new()
                        {
                            Code = "settings.users",
                            Name = "用户管理",
                            Children = new List<MenuOptionDto>
                            {
                                new() { Code = "settings.users.create", Name = "新建用户" },
                                new() { Code = "settings.users.edit", Name = "编辑用户" },
                                new() { Code = "settings.users.delete", Name = "删除用户" },
                                new() { Code = "settings.users.import", Name = "导入用户" }
                            }
                        },
                        new()
                        {
                            Code = "settings.roles",
                            Name = "角色管理",
                            Children = new List<MenuOptionDto>
                            {
                                new() { Code = "settings.roles.create", Name = "新建角色" },
                                new() { Code = "settings.roles.edit", Name = "编辑角色" },
                                new() { Code = "settings.roles.delete", Name = "删除角色" }
                            }
                        },
                        new() { Code = "settings.menu", Name = "权限管理" },
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
