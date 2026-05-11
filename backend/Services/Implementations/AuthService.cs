using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Models.Entities;
using ProjectManagementSystem.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace ProjectManagementSystem.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user != null && !user.IsActive)
            {
                throw new UnauthorizedAccessException("账号已被禁用，请联系管理员");
            }

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("用户名或密码错误");
            }

            var token = GenerateJwtToken(user);
            var expiration = DateTime.UtcNow.AddMinutes(
                Convert.ToDouble(_configuration["JwtSettings:ExpirationInMinutes"]));

            user.LastLoginAt = DateTime.UtcNow;
            user.LoginCount += 1;
            await _context.SaveChangesAsync();

            return new LoginResponse
            {
                Token = token,
                Expiration = expiration,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    RealName = user.RealName,
                    Phone = user.Phone,
                    RoleName = user.Role.Name,
                    RoleId = user.RoleId,
                    Permissions = ResolvePermissions(user.Role.Name, user.Role.Permissions)
                }
            };
        }

        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (user == null)
            {
                throw new KeyNotFoundException("用户不存在");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("原密码错误");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<UserInfo> GetCurrentUserAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (user == null)
            {
                throw new KeyNotFoundException("用户不存在");
            }

            return new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                RealName = user.RealName,
                Phone = user.Phone,
                RoleName = user.Role.Name,
                RoleId = user.RoleId,
                Permissions = ResolvePermissions(user.Role.Name, user.Role.Permissions)
            };
        }

        private static List<string> ResolvePermissions(string roleName, string? permissionText)
        {
            if (roleName == "管理员")
            {
                return new List<string> { "*" };
            }

            if (string.IsNullOrWhiteSpace(permissionText))
            {
                return new List<string>();
            }

            try
            {
                var jsonPermissions = JsonSerializer.Deserialize<List<string>>(permissionText);
                if (jsonPermissions != null)
                {
                    return jsonPermissions.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().ToList();
                }
            }
            catch
            {
            }

            return permissionText
                .Split(new[] { ',', '，', ';', '；', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secret = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey 未配置");
            var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JwtSettings:Issuer 未配置");
            var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JwtSettings:Audience 未配置");
            var expirationInMinutes = Convert.ToDouble(jwtSettings["ExpirationInMinutes"] ?? throw new InvalidOperationException("JwtSettings:ExpirationInMinutes 未配置"));
            var secretKey = Encoding.UTF8.GetBytes(secret);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.Name),
                new Claim("RoleId", user.RoleId.ToString())
            }.ToList();

            var key = new SymmetricSecurityKey(secretKey);
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expirationInMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
