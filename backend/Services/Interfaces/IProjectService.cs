using ProjectManagementSystem.Models.DTOs;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface IProjectService
    {
        Task<PaginatedResult<ProjectDto>> GetProjectsAsync(ProjectListRequest request);
        Task<ProjectDto> GetProjectByIdAsync(int id);
        Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request);
        Task<ProjectDto> UpdateProjectAsync(int id, UpdateProjectRequest request);
        Task<bool> DeleteProjectAsync(int id, int currentUserId);
        Task<bool> CanUserAccessProjectAsync(int projectId, int userId);
        Task<bool> CanUserEditProjectAsync(int projectId, int userId);
        Task<bool> CanUserManageProjectMembersAsync(int projectId, int userId);
        Task<List<ProjectMemberDto>> GetProjectMembersAsync(int projectId);
        Task<ProjectMemberDto> AddProjectMemberAsync(int projectId, AddProjectMemberRequest request);
        Task<bool> RemoveProjectMemberAsync(int projectId, int userId);
        Task<ProjectMemberDto> UpdateMemberRoleAsync(int projectId, int userId, UpdateMemberRoleRequest request);
    }
}
