using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManagementSystem.Models.Entities
{
    [Table("Alerts")]
    public class Alert
    {
        [Key]
        public int Id { get; set; }

        public int? ProjectId { get; set; }

        public int? TaskId { get; set; }

        public int UserId { get; set; }

        public int AlertType { get; set; } // 1:任务超期 2:项目超期 3:进度滞后

        [Required]
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        // 导航属性
        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }

        [ForeignKey("TaskId")]
        public virtual Task? Task { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}
