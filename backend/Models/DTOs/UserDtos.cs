using System.ComponentModel.DataAnnotations;

namespace ProjectManagementSystem.Models.DTOs
{
    public class CreateUserRequest
    {
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(50)]
        public string? RealName { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public int RoleId { get; set; }
    }

    public class UpdateUserRequest
    {
        [MaxLength(50)]
        public string? Username { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(50)]
        public string? RealName { get; set; }

        public int? RoleId { get; set; }

        public bool? IsActive { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? RealName { get; set; }
        public string? Phone { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class UserListRequest
    {
        public string? Keyword { get; set; }
        public int? RoleId { get; set; }
        public bool? IsActive { get; set; }
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;
        [Range(1, 200)]
        public int PageSize { get; set; } = 10;
    }

    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
