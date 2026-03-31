using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManagementSystem.Models.Entities
{
    [Table("Tasks")]
    public class Task
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int? AssigneeId { get; set; }

        public int? MilestoneId { get; set; }

        public int Priority { get; set; } = 1; // 1:低 2:中 3:高 4:紧急

        public int Status { get; set; } = 0; // 0:待办 1:进行中 2:已完成 3:已取消

        public DateTime? StartDate { get; set; }

        public DateTime? DueDate { get; set; }

        public DateTime? CompletedAt { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal Progress { get; set; } = 0;

        [MaxLength(1000)]
        public string? OverdueReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        // 导航属性
        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        [ForeignKey("AssigneeId")]
        public virtual User? Assignee { get; set; }

        [ForeignKey("MilestoneId")]
        public virtual Milestone? Milestone { get; set; }

        public virtual ICollection<TaskLog> TaskLogs { get; set; } = new List<TaskLog>();
        public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    }
}
