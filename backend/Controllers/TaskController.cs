using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Services.Interfaces;
using System.Security.Claims;

namespace ProjectManagementSystem.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/tasks")]
    [Route("api/[controller]")]
    public class TaskController : ControllerBase
    {
        private readonly ITaskService _taskService;

        public TaskController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResult<TaskDto>>>> GetTasks([FromQuery] TaskListRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _taskService.GetTasksAsync(request, userId);
                return Ok(new ApiResponse<PaginatedResult<TaskDto>>
                {
                    Success = true,
                    Data = data
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<TaskDto>>> GetTaskById(int id)
        {
            try
            {
                var data = await _taskService.GetTaskByIdAsync(id);
                return Ok(new ApiResponse<TaskDto>
                {
                    Success = true,
                    Data = data
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<TaskDto>>> CreateTask([FromBody] CreateTaskRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _taskService.CreateTaskAsync(request, userId);
                return Ok(new ApiResponse<TaskDto>
                {
                    Success = true,
                    Data = data,
                    Message = "创建成功"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<TaskDto>>> UpdateTask(int id, [FromBody] UpdateTaskRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _taskService.UpdateTaskAsync(id, request, userId);
                return Ok(new ApiResponse<TaskDto>
                {
                    Success = true,
                    Data = data,
                    Message = "更新成功"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteTask(int id)
        {
            try
            {
                await _taskService.DeleteTaskAsync(id);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPut("{id}/progress")]
        public async Task<ActionResult<ApiResponse<TaskDto>>> UpdateTaskProgress(int id, [FromBody] UpdateTaskProgressRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _taskService.UpdateTaskProgressAsync(id, request, userId);
                return Ok(new ApiResponse<TaskDto>
                {
                    Success = true,
                    Data = data,
                    Message = "进度更新成功"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPut("{id}/status")]
        public async Task<ActionResult<ApiResponse<TaskDto>>> UpdateTaskStatus(int id, [FromBody] UpdateTaskStatusRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _taskService.UpdateTaskStatusAsync(id, request, userId);
                return Ok(new ApiResponse<TaskDto>
                {
                    Success = true,
                    Data = data,
                    Message = "状态更新成功"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPut("{id}/claim")]
        public async Task<ActionResult<ApiResponse<TaskDto>>> ClaimTask(int id, [FromBody] ClaimTaskRequest? request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _taskService.ClaimTaskAsync(id, userId, request?.DueDate);
                return Ok(new ApiResponse<TaskDto>
                {
                    Success = true,
                    Data = data,
                    Message = "认领成功"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpGet("{id}/logs")]
        public async Task<ActionResult<ApiResponse<List<TaskLogDto>>>> GetTaskLogs(int id)
        {
            var data = await _taskService.GetTaskLogsAsync(id);
            return Ok(new ApiResponse<List<TaskLogDto>>
            {
                Success = true,
                Data = data
            });
        }

        [HttpPost("/api/projects/{projectId}/tasks/microgrid-template")]
        public async Task<ActionResult<ApiResponse<object>>> ImportMicrogridTemplate(int projectId, [FromBody] ImportMicrogridTemplateRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var count = await _taskService.ImportMicrogridTemplateAsync(projectId, userId, request.DefaultAssigneeId, request.TemplateId);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"已导入 {count} 个标准任务"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("/api/projects/{projectId}/tasks/microgrid-template/export")]
        public async Task<ActionResult<ApiResponse<object>>> ExportMicrogridTemplate(int projectId, [FromBody] ExportMicrogridTemplateRequest? request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var count = await _taskService.ExportMicrogridTemplateAsync(projectId, userId, request?.TemplateName);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"已导出 {count} 个任务到微电网标准工序，并新增项目任务模板"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("{id}/work-submit")]
        public async Task<ActionResult<ApiResponse<TaskDto>>> SubmitTaskWork(int id, [FromBody] SubmitTaskWorkRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _taskService.SubmitTaskWorkAsync(id, request, userId);
                return Ok(new ApiResponse<TaskDto>
                {
                    Success = true,
                    Data = data,
                    Message = "工作记录提交成功"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("{id}/work-review")]
        public async Task<ActionResult<ApiResponse<TaskDto>>> ReviewTaskWork(int id, [FromBody] ReviewTaskWorkRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _taskService.ReviewTaskWorkAsync(id, request, userId);
                return Ok(new ApiResponse<TaskDto>
                {
                    Success = true,
                    Data = data,
                    Message = request.Approved ? "验收通过" : "已驳回并退回修改"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }
    }
}
