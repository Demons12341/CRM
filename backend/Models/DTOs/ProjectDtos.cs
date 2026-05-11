using System.ComponentModel.DataAnnotations;

namespace ProjectManagementSystem.Models.DTOs
{
    public class CreateProjectRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public int ManagerId { get; set; }

        [Required]
        public DateTime? StartDate { get; set; }

        [Required]
        public DateTime? EndDate { get; set; }

        [Range(1, 4)]
        public int Priority { get; set; } = 2;

        [Required]
        [MaxLength(50)]
        public string BusinessLine { get; set; } = string.Empty;

        public decimal? Budget { get; set; }

        public List<int>? MemberIds { get; set; }

        public int? ProcessTemplateId { get; set; }
    }

    public class UpdateProjectRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        public string? Description { get; set; }

        public int? ManagerId { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public int? Status { get; set; }

        [Range(1, 4)]
        public int? Priority { get; set; }

        [MaxLength(50)]
        public string? BusinessLine { get; set; }

        public decimal? Budget { get; set; }

        public List<int>? MemberIds { get; set; }
    }

    public class ProjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ManagerId { get; set; }
        public string ManagerName { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string PriorityName { get; set; } = string.Empty;
        public string? BusinessLine { get; set; }
        public decimal? Budget { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int MemberCount { get; set; }
        public int TaskCount { get; set; }
        public decimal Progress { get; set; }
        public bool HasOverdueTask { get; set; }
    }

    public class ProjectListRequest
    {
        public string? Keyword { get; set; }
        public int? Status { get; set; }
        public int? ManagerId { get; set; }
        public string? BusinessLine { get; set; }
        public bool ExcludeSharedFolder { get; set; } = false;
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;
        [Range(1, 200)]
        public int PageSize { get; set; } = 10;
    }

    public class AddProjectMemberRequest
    {
        [Required]
        public int UserId { get; set; }

        [MaxLength(50)]
        public string Role { get; set; } = "成员";
    }

    public class UpdateMemberRoleRequest
    {
        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "成员";
    }

    public class ProjectMemberDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }
}
