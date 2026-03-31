using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManagementSystem.Models.Entities
{
    [Table("Projects")]
    public class Project
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int ManagerId { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public int Status { get; set; } = 0; // 0:规划中 1:进行中 2:已完成 3:已暂停

        public int Priority { get; set; } = 2; // 1:低 2:中 3:高 4:紧急

        [Column(TypeName = "decimal(15,2)")]
        public decimal? Budget { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        // 导航属性
        [ForeignKey("ManagerId")]
        public virtual User Manager { get; set; } = null!;

        public virtual ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
        public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
        public virtual ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
        public virtual ICollection<File> Files { get; set; } = new List<File>();
        public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    }
}
