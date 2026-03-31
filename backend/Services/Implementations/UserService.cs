using Microsoft.EntityFrameworkCore;
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
                    IsActive = u.IsActive
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
                IsActive = user.IsActive
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
    }
}
