using System.ComponentModel.DataAnnotations;

namespace ProjectManagementSystem.Models.DTOs
{
    public class UpdateAlertOverdueReasonRequest
    {
        [MaxLength(1000)]
        public string? OverdueReason { get; set; }
    }

    /// <summary>
    /// 告警查询结果DTO（用于内部查询投影）
    /// </summary>
    public class AlertQueryDto
    {
        public int Id { get; set; }
        public int? ProjectId { get; set; }
        public int? ProjectManagerId { get; set; }
        public string? ProjectName { get; set; }
        public string? ProjectManagerRealName { get; set; }
        public string? ProjectManagerUsername { get; set; }
        public int? TaskId { get; set; }
        public string? TaskTitle { get; set; }
        public int? TaskStatus { get; set; }
        public int? ProjectStatus { get; set; }
        public int? TaskAssigneeId { get; set; }
        public string? TaskAssigneeRealName { get; set; }
        public string? TaskAssigneeUsername { get; set; }
        public string? TaskDescription { get; set; }
        public string? TaskOverdueReason { get; set; }
        public int UserId { get; set; }
        public int AlertType { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
