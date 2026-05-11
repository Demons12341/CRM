using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Models.Entities;
using ProjectManagementSystem.Services.Interfaces;

namespace ProjectManagementSystem.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;

        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedResult<UserDto>> GetUsersAsync(UserListRequest request)
        {
            var query = _context.Users
                .Include(u => u.Role)
                .AsQueryable();

            // 筛选条件
            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                query = query.Where(u => u.Username.Contains(request.Keyword)
                    || (u.RealName != null && u.RealName.Contains(request.Keyword)));
            }

            if (request.RoleId.HasValue)
            {
                query = query.Where(u => u.RoleId == request.RoleId.Value);
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(u => u.IsActive == request.IsActive.Value);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    RealName = u.RealName,
                    Phone = u.Phone,
                    RoleId = u.RoleId,
                    RoleName = u.Role.Name,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt,
                    IsActive = u.IsActive,
                    LastLoginAt = u.LastLoginAt,
                    LoginCount = u.LoginCount
                })
                .ToListAsync();

            return new PaginatedResult<UserDto>
            {
                Items = users,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = totalPages
            };
        }

        public async Task<UserDto> GetUserByIdAsync(int id)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                throw new KeyNotFoundException("用户不存在");
            }

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                RealName = user.RealName,
                Phone = user.Phone,
                RoleId = user.RoleId,
                RoleName = user.Role.Name,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt,
                LoginCount = user.LoginCount
            };
        }

        public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
        {
            var realName = string.IsNullOrWhiteSpace(request.RealName) ? null : request.RealName.Trim();

            // 检查用户名是否已存在
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                throw new InvalidOperationException("用户名已存在");
            }

            var user = new User
            {
                Username = request.Username,
                RealName = realName,
                Phone = request.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId = request.RoleId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return await GetUserByIdAsync(user.Id);
        }

        public async Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                throw new KeyNotFoundException("用户不存在");
            }

            if (request.Username != null)
            {
                if (await _context.Users.AnyAsync(u => u.Username == request.Username && u.Id != id))
                {
                    throw new InvalidOperationException("用户名已存在");
                }
                user.Username = request.Username;
            }

            if (request.RealName != null)
            {
                user.RealName = string.IsNullOrWhiteSpace(request.RealName) ? null : request.RealName.Trim();
            }

            if (request.Phone != null)
            {
                user.Phone = request.Phone;
            }

            if (request.RoleId.HasValue)
            {
                user.RoleId = request.RoleId.Value;
            }

            if (request.IsActive.HasValue)
            {
                user.IsActive = request.IsActive.Value;
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await GetUserByIdAsync(id);
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                throw new KeyNotFoundException("用户不存在");
            }

            // 软删除：设置为不活跃
            user.IsActive = false;
            user.IsDeleted = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public Task<byte[]> BuildUserImportTemplateAsync()
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("用户导入模板");

            sheet.Cell(1, 1).Value = "用户名*";
            sheet.Cell(1, 2).Value = "姓名";
            sheet.Cell(1, 3).Value = "手机号";
            sheet.Cell(1, 4).Value = "密码*";
            sheet.Cell(1, 5).Value = "角色";
            sheet.Cell(1, 6).Value = "启用状态(是/否)";

            sheet.Cell(2, 1).Value = "zhangsan";
            sheet.Cell(2, 2).Value = "张三";
            sheet.Cell(2, 3).Value = "13800000001";
            sheet.Cell(2, 4).Value = "123456";
            sheet.Cell(2, 5).Value = "项目成员";
            sheet.Cell(2, 6).Value = "是";

            var headerRange = sheet.Range(1, 1, 1, 6);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F6FC");
            sheet.Columns(1, 6).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return System.Threading.Tasks.Task.FromResult(stream.ToArray());
        }

        public async Task<ImportUsersResultDto> ImportUsersFromExcelAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new InvalidOperationException("请上传Excel文件");
            }

            var extension = Path.GetExtension(file.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("仅支持 .xlsx 格式文件");
            }

            var roles = await _context.Roles
                .AsNoTracking()
                .ToListAsync();

            if (roles.Count == 0)
            {
                throw new InvalidOperationException("系统中不存在角色，无法导入用户");
            }

            var defaultRoleId = roles
                .OrderBy(r => r.Id)
                .Select(r => r.Id)
                .First();

            var existedUsers = await _context.Users
                .IgnoreQueryFilters()
                .Select(u => u.Username)
                .ToListAsync();

            var existedUserSet = new HashSet<string>(existedUsers, StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;
            var result = new ImportUsersResultDto();

            var entities = new List<User>();
            using var workbook = new XLWorkbook(file.OpenReadStream());
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet == null)
            {
                throw new InvalidOperationException("Excel文件内容为空");
            }

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow < 2)
            {
                throw new InvalidOperationException("Excel文件没有可导入的数据");
            }

            for (var rowIndex = 2; rowIndex <= lastRow; rowIndex++)
            {
                var username = (sheet.Cell(rowIndex, 1).GetString() ?? string.Empty).Trim();
                var realName = (sheet.Cell(rowIndex, 2).GetString() ?? string.Empty).Trim();
                var phone = (sheet.Cell(rowIndex, 3).GetString() ?? string.Empty).Trim();
                var password = (sheet.Cell(rowIndex, 4).GetString() ?? string.Empty).Trim();
                var roleName = (sheet.Cell(rowIndex, 5).GetString() ?? string.Empty).Trim();
                var isActiveText = (sheet.Cell(rowIndex, 6).GetString() ?? string.Empty).Trim();

                var isBlankRow = string.IsNullOrWhiteSpace(username)
                    && string.IsNullOrWhiteSpace(realName)
                    && string.IsNullOrWhiteSpace(phone)
                    && string.IsNullOrWhiteSpace(password)
                    && string.IsNullOrWhiteSpace(roleName)
                    && string.IsNullOrWhiteSpace(isActiveText);

                if (isBlankRow)
                {
                    continue;
                }

                result.TotalCount++;

                if (string.IsNullOrWhiteSpace(username))
                {
                    result.SkippedUsers.Add($"第{rowIndex}行（用户名为空）");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    result.SkippedUsers.Add($"{username}（密码为空）");
                    continue;
                }

                if (!existedUserSet.Add(username))
                {
                    result.SkippedUsers.Add($"{username}（用户名已存在）");
                    continue;
                }

                var roleItem = string.IsNullOrWhiteSpace(roleName)
                    ? null
                    : roles.FirstOrDefault(r => string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(roleName) && roleItem == null)
                {
                    result.SkippedUsers.Add($"{username}（角色“{roleName}”不存在）");
                    continue;
                }

                var isActive = ParseActiveValue(isActiveText);

                entities.Add(new User
                {
                    Username = username,
                    RealName = string.IsNullOrWhiteSpace(realName) ? null : realName,
                    Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    RoleId = roleItem?.Id ?? defaultRoleId,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsActive = isActive
                });
            }

            if (entities.Count > 0)
            {
                _context.Users.AddRange(entities);
                await _context.SaveChangesAsync();
            }

            result.ImportedCount = entities.Count;
            result.SkippedCount = result.SkippedUsers.Count;
            return result;
        }

        private static bool ParseActiveValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (normalized is "是" or "启用" or "true" or "1" or "y" or "yes")
            {
                return true;
            }

            if (normalized is "否" or "禁用" or "false" or "0" or "n" or "no")
            {
                return false;
            }

            return true;
        }
    }
}
