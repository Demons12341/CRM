using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Services.Interfaces;
using System.Security.Claims;

namespace ProjectManagementSystem.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/alerts")]
    [Route("api/[controller]")]
    public class AlertController : ControllerBase
    {
        private readonly IAlertService _alertService;

        public AlertController(IAlertService alertService)
        {
            _alertService = alertService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResult<AlertDto>>>> GetAlerts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int? alertType = null,
            [FromQuery] bool? isRead = null)
        {
            var userId = GetCurrentUserId();
            var data = await _alertService.GetAlertsAsync(userId, page, pageSize, alertType, isRead);
            return Ok(new ApiResponse<PaginatedResult<AlertDto>>
            {
                Success = true,
                Data = data
            });
        }

        [HttpGet("unread-count")]
        public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            var data = await _alertService.GetUnreadCountAsync(userId);
            return Ok(new ApiResponse<int>
            {
                Success = true,
                Data = data
            });
        }

        [HttpPut("{id}/read")]
        public async Task<ActionResult<ApiResponse<object>>> MarkAsRead(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _alertService.MarkAsReadAsync(id, userId);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "标记成功"
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

        [HttpPut("read-all")]
        public async Task<ActionResult<ApiResponse<object>>> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            await _alertService.MarkAllAsReadAsync(userId);
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "已全部标记为已读"
            });
        }

        [HttpPut("{id}/overdue-reason")]
        public async Task<ActionResult<ApiResponse<object>>> UpdateOverdueReason(int id, [FromBody] ProjectManagementSystem.Models.DTOs.UpdateAlertOverdueReasonRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _alertService.UpdateOverdueReasonAsync(id, userId, request.OverdueReason);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "超期原因已更新"
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

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }
    }
}
