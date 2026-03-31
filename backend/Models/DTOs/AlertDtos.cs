using System.ComponentModel.DataAnnotations;

namespace ProjectManagementSystem.Models.DTOs
{
    public class UpdateAlertOverdueReasonRequest
    {
        [MaxLength(1000)]
        public string? OverdueReason { get; set; }
    }
}
