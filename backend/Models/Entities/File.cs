using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManagementSystem.Models.Entities
{
    [Table("Files")]
    public class File
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public int? ParentId { get; set; }

        public bool IsFolder { get; set; } = false;

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? FileType { get; set; }

        public long FileSize { get; set; }

        public int UploadedBy { get; set; }

        public bool IsShared { get; set; } = false;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        // 导航属性
        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        [ForeignKey("UploadedBy")]
        public virtual User Uploader { get; set; } = null!;

        [ForeignKey("ParentId")]
        public virtual File? Parent { get; set; }

        public virtual ICollection<File> Children { get; set; } = new List<File>();
    }
}
