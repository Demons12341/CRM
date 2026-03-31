using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManagementSystem.Models.Entities
{
    [Table("ProjectMembers")]
    public class ProjectMember
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public int UserId { get; set; }

        [MaxLength(50)]
        public string Role { get; set; } = "成员"; // 负责人、成员、观察者

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        // 导航属性
        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}
