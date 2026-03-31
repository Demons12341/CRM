using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManagementSystem.Models.Entities
{
    [Table("Milestones")]
    public class Milestone
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime? DueDate { get; set; }

        public int Status { get; set; } = 0; // 0:未开始 1:进行中 2:已完成

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        // 导航属性
        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
    }
}
