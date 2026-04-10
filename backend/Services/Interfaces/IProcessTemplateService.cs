using ProjectManagementSystem.Models.DTOs;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface IProcessTemplateService
    {
        Task<List<ProcessTemplateDto>> GetTemplatesAsync();
        Task<ProcessTemplateDto> GetTemplateByIdAsync(int id);
        Task<ProcessTemplateDto> CreateTemplateAsync(CreateProcessTemplateRequest request);
        Task<ProcessTemplateDto> UpdateTemplateAsync(int id, UpdateProcessTemplateRequest request);
        Task<bool> DeleteTemplateAsync(int id);
        Task<ProcessTemplateDto> SetDefaultTemplateAsync(int id);
        Task<int> ApplyDefaultTemplateToProjectAsync(int projectId, int? fallbackAssigneeId, int operatorUserId, int? templateId = null);
    }
}
