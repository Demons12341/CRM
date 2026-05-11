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

        public int Status { get; set; } = 0; // 0:售前阶段 2:已中标，待签合同 3:需求确定阶段 4:设计阶段 5:采购生产阶段 6:装配阶段 7:测试阶段 8:已发货 9:现场调试 10:已完成

        public int Priority { get; set; } = 2; // 1:低 2:中 3:高 4:紧急

        [MaxLength(50)]
        public string? BusinessLine { get; set; }

        [Column(TypeName = "decimal(15,2)")]
        public decimal? Budget { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? StatusChangedAt { get; set; }

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
