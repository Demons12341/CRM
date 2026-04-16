using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManagementSystem.Models.Entities
{
    [Table("ProcessTemplateSteps")]
    public class ProcessTemplateStep
    {
        [Key]
        public int Id { get; set; }

        public int ProcessTemplateId { get; set; }

        public int SortOrder { get; set; }

        [Required]
        [MaxLength(100)]
        public string Stage { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int Priority { get; set; } = 2;

        public int? DefaultAssigneeId { get; set; }

        [MaxLength(500)]
        public string DefaultAssigneeIds { get; set; } = string.Empty;

        public int EstimatedDays { get; set; } = 3;

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ProcessTemplateId")]
        public virtual ProcessTemplate ProcessTemplate { get; set; } = null!;
    }
}
