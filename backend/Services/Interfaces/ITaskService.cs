using ProjectManagementSystem.Models.DTOs;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface ITaskService
    {
        Task<PaginatedResult<TaskDto>> GetTasksAsync(TaskListRequest request, int currentUserId);
        Task<TaskDto> GetTaskByIdAsync(int id);
        Task<TaskDto> CreateTaskAsync(CreateTaskRequest request, int userId);
        Task<TaskDto> UpdateTaskAsync(int id, UpdateTaskRequest request, int userId);
        Task<bool> DeleteTaskAsync(int id);
        Task<TaskDto> UpdateTaskProgressAsync(int id, UpdateTaskProgressRequest request, int userId);
        Task<TaskDto> UpdateTaskStatusAsync(int id, UpdateTaskStatusRequest request, int userId);
        Task<TaskDto> ClaimTaskAsync(int id, int userId);
        Task<List<TaskLogDto>> GetTaskLogsAsync(int taskId);
        Task<int> ImportMicrogridTemplateAsync(int projectId, int userId, int? defaultAssigneeId);
        Task<TaskDto> SubmitTaskWorkAsync(int id, SubmitTaskWorkRequest request, int userId);
        Task<TaskDto> ReviewTaskWorkAsync(int id, ReviewTaskWorkRequest request, int userId);
    }
}
