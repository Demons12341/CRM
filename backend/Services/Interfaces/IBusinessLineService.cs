using ProjectManagementSystem.Models.DTOs;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface IBusinessLineService
    {
        Task<List<BusinessLineDto>> GetBusinessLinesAsync();
        Task<BusinessLineDto> CreateBusinessLineAsync(CreateBusinessLineRequest request);
        Task<BusinessLineDto> UpdateBusinessLineAsync(int id, UpdateBusinessLineRequest request);
        Task<bool> DeleteBusinessLineAsync(int id);
    }

    public class BusinessLineDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateBusinessLineRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.MaxLength(200)]
        public string? Description { get; set; }

        public int SortOrder { get; set; } = 0;
    }

    public class UpdateBusinessLineRequest
    {
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string? Name { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(200)]
        public string? Description { get; set; }

        public int? SortOrder { get; set; }
    }
}
