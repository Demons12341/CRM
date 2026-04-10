using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Services.Interfaces;

namespace ProjectManagementSystem.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/users")]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResult<UserDto>>>> GetUsers([FromQuery] UserListRequest request)
        {
            var data = await _userService.GetUsersAsync(request);
            return Ok(new ApiResponse<PaginatedResult<UserDto>>
            {
                Success = true,
                Data = data
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetUserById(int id)
        {
            try
            {
                var data = await _userService.GetUserByIdAsync(id);
                return Ok(new ApiResponse<UserDto>
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
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var data = await _userService.CreateUserAsync(request);
                return Ok(new ApiResponse<UserDto>
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
        public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var data = await _userService.UpdateUserAsync(id, request);
                return Ok(new ApiResponse<UserDto>
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
        public async Task<ActionResult<ApiResponse<object>>> DeleteUser(int id)
        {
            try
            {
                await _userService.DeleteUserAsync(id);
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

        [HttpGet("import-template/download")]
        public async Task<IActionResult> DownloadImportTemplate()
        {
            var fileBytes = await _userService.BuildUserImportTemplateAsync();
            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "用户导入模板.xlsx"
            );
        }

        [HttpPost("import-template/upload")]
        public async Task<ActionResult<ApiResponse<ImportUsersResultDto>>> ImportUsersByExcel([FromForm] IFormFile file)
        {
            try
            {
                var data = await _userService.ImportUsersFromExcelAsync(file);
                return Ok(new ApiResponse<ImportUsersResultDto>
                {
                    Success = true,
                    Data = data,
                    Message = $"导入完成：成功 {data.ImportedCount} 条，跳过 {data.SkippedCount} 条"
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
    }
}
