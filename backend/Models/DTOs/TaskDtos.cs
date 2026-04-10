using System.ComponentModel.DataAnnotations;

namespace ProjectManagementSystem.Models.DTOs
{
    public class CreateTaskRequest
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int? AssigneeId { get; set; }

        public List<int>? AssigneeIds { get; set; }

        public int? MilestoneId { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? DueDate { get; set; }
    }

    public class UpdateTaskRequest
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        public int? AssigneeId { get; set; }

        public List<int>? AssigneeIds { get; set; }

        public int? MilestoneId { get; set; }

        public int? Status { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? DueDate { get; set; }

        public decimal? Progress { get; set; }
    }

    public class TaskDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int ProjectManagerId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? AssigneeId { get; set; }
        public string? AssigneeName { get; set; }
        public string? AssigneeDisplay { get; set; }
        public List<int> AssigneeIds { get; set; } = new();
        public int? MilestoneId { get; set; }
        public string? MilestoneName { get; set; }
        public int Priority { get; set; }
        public string PriorityName { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }
        public decimal Progress { get; set; }
        public string? OverdueReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class TaskListRequest
    {
        public int? ProjectId { get; set; }
        public int? AssigneeId { get; set; }
        public int? Status { get; set; }
        public string? Keyword { get; set; }
        public bool? OverdueOnly { get; set; }
        public bool? MyOpenScope { get; set; }
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;
        [Range(1, 200)]
        public int PageSize { get; set; } = 10;
    }

    public class UpdateTaskProgressRequest
    {
        [Required]
        [Range(0, 100)]
        public decimal Progress { get; set; }
    }

    public class UpdateTaskStatusRequest
    {
        [Required]
        [Range(0, 3)]
        public int Status { get; set; }
    }

    public class TaskLogDto
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ImportMicrogridTemplateRequest
    {
        public int? DefaultAssigneeId { get; set; }
        public int? TemplateId { get; set; }
    }

    public class ExportMicrogridTemplateRequest
    {
        [MaxLength(100)]
        public string? TemplateName { get; set; }
    }

    public class SubmitTaskWorkRequest
    {
        [Required]
        [MaxLength(2000)]
        public string WorkContent { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Deliverables { get; set; }

        [MaxLength(1000)]
        public string? Blockers { get; set; }

        [MaxLength(1000)]
        public string? NextPlan { get; set; }

        public List<int>? NextAssigneeIds { get; set; }
    }

    public class ReviewTaskWorkRequest
    {
        [Required]
        public bool Approved { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        [Range(0, 100)]
        public decimal? Progress { get; set; }

        public bool MarkAsCompleted { get; set; } = false;
    }

    public class ClaimTaskResponse
    {
        public int TaskId { get; set; }
        public int AssigneeId { get; set; }
    }

    public class ClaimTaskRequest
    {
        public DateTime? DueDate { get; set; }
    }
}
