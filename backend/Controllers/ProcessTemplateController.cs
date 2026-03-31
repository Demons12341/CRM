using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Services.Interfaces;
using System.Security.Claims;

namespace ProjectManagementSystem.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/process-templates")]
    public class ProcessTemplateController : ControllerBase
    {
        private readonly IProcessTemplateService _processTemplateService;

        public ProcessTemplateController(IProcessTemplateService processTemplateService)
        {
            _processTemplateService = processTemplateService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<ProcessTemplateDto>>>> GetTemplates()
        {
            var data = await _processTemplateService.GetTemplatesAsync();
            return Ok(new ApiResponse<List<ProcessTemplateDto>>
            {
                Success = true,
                Data = data
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<ProcessTemplateDto>>> GetTemplateById(int id)
        {
            try
            {
                var data = await _processTemplateService.GetTemplateByIdAsync(id);
                return Ok(new ApiResponse<ProcessTemplateDto>
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
        public async Task<ActionResult<ApiResponse<ProcessTemplateDto>>> CreateTemplate([FromBody] CreateProcessTemplateRequest request)
        {
            var data = await _processTemplateService.CreateTemplateAsync(request);
            return Ok(new ApiResponse<ProcessTemplateDto>
            {
                Success = true,
                Data = data,
                Message = "创建成功"
            });
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<ProcessTemplateDto>>> UpdateTemplate(int id, [FromBody] UpdateProcessTemplateRequest request)
        {
            try
            {
                var data = await _processTemplateService.UpdateTemplateAsync(id, request);
                return Ok(new ApiResponse<ProcessTemplateDto>
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
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteTemplate(int id)
        {
            try
            {
                await _processTemplateService.DeleteTemplateAsync(id);
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

        [HttpPost("{id}/set-default")]
        public async Task<ActionResult<ApiResponse<ProcessTemplateDto>>> SetDefaultTemplate(int id)
        {
            try
            {
                var data = await _processTemplateService.SetDefaultTemplateAsync(id);
                return Ok(new ApiResponse<ProcessTemplateDto>
                {
                    Success = true,
                    Data = data,
                    Message = "已设置为默认模板"
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
    }
}
