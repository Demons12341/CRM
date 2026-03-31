using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Services.Interfaces;

namespace ProjectManagementSystem.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/roles")]
    [Route("api/[controller]")]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetRoles()
        {
            var data = await _roleService.GetRolesAsync();
            return Ok(new ApiResponse<List<RoleDto>>
            {
                Success = true,
                Data = data
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<RoleDto>>> GetRoleById(int id)
        {
            try
            {
                var data = await _roleService.GetRoleByIdAsync(id);
                return Ok(new ApiResponse<RoleDto>
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
        public async Task<ActionResult<ApiResponse<RoleDto>>> CreateRole([FromBody] CreateRoleRequest request)
        {
            try
            {
                var data = await _roleService.CreateRoleAsync(request);
                return Ok(new ApiResponse<RoleDto>
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
        public async Task<ActionResult<ApiResponse<RoleDto>>> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
        {
            try
            {
                var data = await _roleService.UpdateRoleAsync(id, request);
                return Ok(new ApiResponse<RoleDto>
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
        public async Task<ActionResult<ApiResponse<object>>> DeleteRole(int id)
        {
            try
            {
                await _roleService.DeleteRoleAsync(id);
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
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpGet("menu-options")]
        public async Task<ActionResult<ApiResponse<List<MenuOptionDto>>>> GetMenuOptions()
        {
            var data = await _roleService.GetMenuOptionsAsync();
            return Ok(new ApiResponse<List<MenuOptionDto>>
            {
                Success = true,
                Data = data
            });
        }

        [HttpGet("{id}/menu-permissions")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetRoleMenuPermissions(int id)
        {
            try
            {
                var data = await _roleService.GetRoleMenuPermissionsAsync(id);
                return Ok(new ApiResponse<List<string>>
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

        [HttpPut("{id}/menu-permissions")]
        public async Task<ActionResult<ApiResponse<object>>> UpdateRoleMenuPermissions(int id, [FromBody] UpdateRoleMenuPermissionsRequest request)
        {
            try
            {
                await _roleService.UpdateRoleMenuPermissionsAsync(id, request.MenuCodes);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "菜单权限更新成功"
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
    }
}
