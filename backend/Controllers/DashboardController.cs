using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.Services.Interfaces;
using System.Security.Claims;

namespace ProjectManagementSystem.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("overview")]
        public async Task<ActionResult<ApiResponse<DashboardOverviewDto>>> GetOverview()
        {
            var userId = GetCurrentUserId();
            var data = await _dashboardService.GetOverviewAsync(userId);
            return Ok(new ApiResponse<DashboardOverviewDto>
            {
                Success = true,
                Data = data
            });
        }

        [HttpGet("my-tasks")]
        public async Task<ActionResult<ApiResponse<List<Models.DTOs.TaskDto>>>> GetMyTasks()
        {
            var userId = GetCurrentUserId();
            var data = await _dashboardService.GetMyTasksAsync(userId);
            return Ok(new ApiResponse<List<Models.DTOs.TaskDto>>
            {
                Success = true,
                Data = data
            });
        }

        [HttpGet("my-projects")]
        public async Task<ActionResult<ApiResponse<List<MyProjectDto>>>> GetMyProjects()
        {
            var userId = GetCurrentUserId();
            var data = await _dashboardService.GetMyProjectsAsync(userId);
            return Ok(new ApiResponse<List<MyProjectDto>>
            {
                Success = true,
                Data = data
            });
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }
    }
}
