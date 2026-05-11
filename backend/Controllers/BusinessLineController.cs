using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Services.Interfaces;

namespace ProjectManagementSystem.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/business-lines")]
    [Route("api/[controller]")]
    public class BusinessLineController : ControllerBase
    {
        private readonly IBusinessLineService _businessLineService;
        private readonly IPermissionService _permissionService;

        public BusinessLineController(IBusinessLineService businessLineService, IPermissionService permissionService)
        {
            _businessLineService = businessLineService;
            _permissionService = permissionService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<BusinessLineDto>>>> GetBusinessLines()
        {
            var data = await _businessLineService.GetBusinessLinesAsync();
            return Ok(new ApiResponse<List<BusinessLineDto>>
            {
                Success = true,
                Data = data
            });
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<BusinessLineDto>>> CreateBusinessLine([FromBody] CreateBusinessLineRequest request)
        {
            var userId = GetCurrentUserId();
            if (!await _permissionService.HasPermissionAsync(userId, "business-lines.create"))
            {
                return Forbid();
            }

            try
            {
                var data = await _businessLineService.CreateBusinessLineAsync(request);
                return Ok(new ApiResponse<BusinessLineDto>
                {
                    Success = true,
                    Data = data,
                    Message = "创建成功"
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
        public async Task<ActionResult<ApiResponse<BusinessLineDto>>> UpdateBusinessLine(int id, [FromBody] UpdateBusinessLineRequest request)
        {
            var userId = GetCurrentUserId();
            if (!await _permissionService.HasPermissionAsync(userId, "business-lines.edit"))
            {
                return Forbid();
            }

            try
            {
                var data = await _businessLineService.UpdateBusinessLineAsync(id, request);
                return Ok(new ApiResponse<BusinessLineDto>
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
        public async Task<ActionResult<ApiResponse<object>>> DeleteBusinessLine(int id)
        {
            var userId = GetCurrentUserId();
            if (!await _permissionService.HasPermissionAsync(userId, "business-lines.delete"))
            {
                return Forbid();
            }

            try
            {
                await _businessLineService.DeleteBusinessLineAsync(id);
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
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }
    }
}
