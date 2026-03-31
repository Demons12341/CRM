using ProjectManagementSystem.Models.DTOs;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface IMilestoneService
    {
        Task<List<MilestoneDto>> GetMilestonesByProjectIdAsync(int projectId);
        Task<MilestoneDto> GetMilestoneByIdAsync(int id);
        Task<MilestoneDto> CreateMilestoneAsync(CreateMilestoneRequest request);
        Task<MilestoneDto> UpdateMilestoneAsync(int id, UpdateMilestoneRequest request);
        Task<bool> DeleteMilestoneAsync(int id);
    }

    public class MilestoneDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateMilestoneRequest
    {
        public int ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class UpdateMilestoneRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public int? Status { get; set; }
    }
}
