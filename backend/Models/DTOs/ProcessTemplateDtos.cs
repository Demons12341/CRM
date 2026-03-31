using System.ComponentModel.DataAnnotations;

namespace ProjectManagementSystem.Models.DTOs
{
    public class ProcessTemplateDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<ProcessStepDto> Steps { get; set; } = new();
    }

    public class ProcessStepDto
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public string Stage { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Priority { get; set; } = 2;
        public int? DefaultAssigneeId { get; set; }
        public string? DefaultAssigneeName { get; set; }
        public List<int> DefaultAssigneeIds { get; set; } = new();
        public List<string> DefaultAssigneeNames { get; set; } = new();
        public int EstimatedDays { get; set; } = 3;
    }

    public class CreateProcessTemplateRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsDefault { get; set; }
        public List<ProcessStepRequest> Steps { get; set; } = new();
    }

    public class UpdateProcessTemplateRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsDefault { get; set; }
        public List<ProcessStepRequest> Steps { get; set; } = new();
    }

    public class ProcessStepRequest
    {
        public int SortOrder { get; set; }

        [Required]
        [MaxLength(100)]
        public string Stage { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Range(1, 4)]
        public int Priority { get; set; } = 2;

        public int? DefaultAssigneeId { get; set; }

        public List<int> DefaultAssigneeIds { get; set; } = new();

        [Range(1, 365)]
        public int EstimatedDays { get; set; } = 3;
    }
}
