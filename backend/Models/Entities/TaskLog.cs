using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManagementSystem.Models.Entities
{
    [Table("TaskLogs")]
    public class TaskLog
    {
        [Key]
        public int Id { get; set; }

        public int TaskId { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        // 导航属性
        [ForeignKey("TaskId")]
        public virtual Task Task { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}
