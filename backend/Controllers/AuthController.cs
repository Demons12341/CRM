using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Services.Interfaces;
using System.Security.Claims;

namespace ProjectManagementSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                return Ok(new ApiResponse<LoginResponse>
                {
                    Success = true,
                    Data = result,
                    Message = "登录成功"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("logout")]
        public ActionResult<ApiResponse<object>> Logout()
        {
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "登出成功"
            });
        }

        [Authorize]
        [HttpGet("current")]
        public async Task<ActionResult<ApiResponse<UserInfo>>> GetCurrentUser()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _authService.GetCurrentUserAsync(userId);
                return Ok(new ApiResponse<UserInfo>
                {
                    Success = true,
                    Data = user
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

        [Authorize]
        [HttpPost("change-password")]
        public async Task<ActionResult<ApiResponse<object>>> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _authService.ChangePasswordAsync(userId, request);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "密码修改成功"
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
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }
}
