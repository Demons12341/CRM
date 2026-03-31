using System.ComponentModel.DataAnnotations;

namespace ProjectManagementSystem.Models.DTOs
{
    public class LoginRequest
    {
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public UserInfo User { get; set; } = null!;
    }

    public class UserInfo
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? RealName { get; set; }
        public string? Phone { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public List<string> Permissions { get; set; } = new();
    }

    public class ChangePasswordRequest
    {
        [Required]
        public string OldPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare("NewPassword")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
