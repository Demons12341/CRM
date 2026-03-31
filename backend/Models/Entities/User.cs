using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManagementSystem.Models.Entities
{
    [Table("Users")]
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(50)]
        public string? RealName { get; set; }

        public int RoleId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        // 导航属性
        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; } = null!;

        public virtual ICollection<Project> ManagedProjects { get; set; } = new List<Project>();
        public virtual ICollection<ProjectMember> ProjectMemberships { get; set; } = new List<ProjectMember>();
        public virtual ICollection<Task> AssignedTasks { get; set; } = new List<Task>();
        public virtual ICollection<File> UploadedFiles { get; set; } = new List<File>();
        public virtual ICollection<TaskLog> TaskLogs { get; set; } = new List<TaskLog>();
        public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    }
}
