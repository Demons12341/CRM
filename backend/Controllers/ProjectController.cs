using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Services.Interfaces;
using System.Security.Claims;

namespace ProjectManagementSystem.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/projects")]
    [Route("api/[controller]")]
    public class ProjectController : ControllerBase
    {
        private readonly IProjectService _projectService;

        public ProjectController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResult<ProjectDto>>>> GetProjects([FromQuery] ProjectListRequest request)
        {
            var data = await _projectService.GetProjectsAsync(request);
            return Ok(new ApiResponse<PaginatedResult<ProjectDto>>
            {
                Success = true,
                Data = data
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<ProjectDto>>> GetProjectById(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var canAccess = await _projectService.CanUserAccessProjectAsync(id, userId);
                if (!canAccess)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
                    {
                        Success = false,
                        Message = "您无权访问此项目"
                    });
                }

                var data = await _projectService.GetProjectByIdAsync(id);
                return Ok(new ApiResponse<ProjectDto>
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
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<ProjectDto>>> CreateProject([FromBody] CreateProjectRequest request)
        {
            var data = await _projectService.CreateProjectAsync(request);
            return Ok(new ApiResponse<ProjectDto>
            {
                Success = true,
                Data = data,
                Message = "创建成功"
            });
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<ProjectDto>>> UpdateProject(int id, [FromBody] UpdateProjectRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var canEdit = await _projectService.CanUserEditProjectAsync(id, userId);
                if (!canEdit)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
                    {
                        Success = false,
                        Message = "仅项目负责人或管理员可编辑项目"
                    });
                }

                var data = await _projectService.UpdateProjectAsync(id, request);
                return Ok(new ApiResponse<ProjectDto>
                {
                    Success = true,
                    Data = data,
                    Message = "更新成功"
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
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteProject(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _projectService.DeleteProjectAsync(id, userId);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "删除成功"
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

        [HttpGet("{projectId}/members")]
        public async Task<ActionResult<ApiResponse<List<ProjectMemberDto>>>> GetProjectMembers(int projectId)
        {
            var data = await _projectService.GetProjectMembersAsync(projectId);
            return Ok(new ApiResponse<List<ProjectMemberDto>>
            {
                Success = true,
                Data = data
            });
        }

        [HttpPost("{projectId}/members")]
        public async Task<ActionResult<ApiResponse<ProjectMemberDto>>> AddProjectMember(int projectId, [FromBody] AddProjectMemberRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var canManage = await _projectService.CanUserManageProjectMembersAsync(projectId, userId);
                if (!canManage)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
                    {
                        Success = false,
                        Message = "暂无项目成员管理权限"
                    });
                }

                var data = await _projectService.AddProjectMemberAsync(projectId, request);
                return Ok(new ApiResponse<ProjectMemberDto>
                {
                    Success = true,
                    Data = data,
                    Message = "添加成员成功"
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

        [HttpDelete("{projectId}/members/{userId}")]
        public async Task<ActionResult<ApiResponse<object>>> RemoveProjectMember(int projectId, int userId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var canManage = await _projectService.CanUserManageProjectMembersAsync(projectId, currentUserId);
                if (!canManage)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>
                    {
                        Success = false,
                        Message = "暂无项目成员管理权限"
                    });
                }

                await _projectService.RemoveProjectMemberAsync(projectId, userId);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "移除成员成功"
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
