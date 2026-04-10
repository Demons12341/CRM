using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Services.Interfaces;
using System.Security.Claims;

namespace ProjectManagementSystem.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/files")]
    [Route("api/[controller]")]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;

        public FileController(IFileService fileService)
        {
            _fileService = fileService;
        }

        [HttpGet("/api/projects/{projectId}/files")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<FileDto>>>> GetProjectFiles(
            int projectId,
            [FromQuery] int? parentId = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] string? keyword = null,
            [FromQuery] bool recursive = false)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _fileService.GetFilesByProjectIdAsync(projectId, parentId, page, pageSize, keyword, recursive, userId);
                return Ok(new ApiResponse<PaginatedResult<FileDto>>
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

        [HttpGet("/api/projects/{projectId}/files/recycle-bin")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<FileDto>>>> GetProjectRecycleBinFiles(
            int projectId,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] string? keyword = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _fileService.GetRecycleBinFilesByProjectIdAsync(projectId, page, pageSize, keyword, userId);
                return Ok(new ApiResponse<PaginatedResult<FileDto>>
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

        [HttpGet("/api/files/recycle-bin")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<FileDto>>>> GetRecycleBinFiles(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] string? keyword = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _fileService.GetRecycleBinFilesAsync(page, pageSize, keyword, userId);
                return Ok(new ApiResponse<PaginatedResult<FileDto>>
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
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("upload")]
        public async Task<ActionResult<ApiResponse<FileDto>>> UploadFile(
            [FromForm] int projectId,
            [FromForm] int? parentId,
            [FromForm] IFormFile file,
            [FromForm] string? description)
        {
            return await UploadFileCore(projectId, parentId, file, description);
        }

        [HttpPost("/api/projects/{projectId}/files")]
        public async Task<ActionResult<ApiResponse<FileDto>>> UploadProjectFile(
            int projectId,
            [FromForm] int? parentId,
            [FromForm] IFormFile file,
            [FromForm] string? description)
        {
            return await UploadFileCore(projectId, parentId, file, description);
        }

        private async Task<ActionResult<ApiResponse<FileDto>>> UploadFileCore(int projectId, int? parentId, IFormFile file, string? description)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _fileService.UploadFileAsync(projectId, parentId, file, userId, description);
                return Ok(new ApiResponse<FileDto>
                {
                    Success = true,
                    Data = data,
                    Message = "上传成功"
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

        [HttpPost("/api/projects/{projectId}/folders")]
        public async Task<ActionResult<ApiResponse<FileDto>>> CreateFolder(int projectId, [FromBody] CreateFolderRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _fileService.CreateFolderAsync(projectId, request.ParentId, request.FolderName, userId);
                return Ok(new ApiResponse<FileDto>
                {
                    Success = true,
                    Data = data,
                    Message = "文件夹创建成功"
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
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPut("{id}/move")]
        public async Task<ActionResult<ApiResponse<FileDto>>> MoveFile(int id, [FromBody] MoveFileRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _fileService.MoveFileAsync(id, request.TargetFolderId, userId);
                return Ok(new ApiResponse<FileDto>
                {
                    Success = true,
                    Data = data,
                    Message = "移动成功"
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

        [HttpPut("{id}/rename")]
        public async Task<ActionResult<ApiResponse<FileDto>>> RenameEntry(int id, [FromBody] RenameEntryRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _fileService.RenameEntryAsync(id, request.Name, userId);
                return Ok(new ApiResponse<FileDto>
                {
                    Success = true,
                    Data = data,
                    Message = "重命名成功"
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
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPut("{id}/share")]
        public async Task<ActionResult<ApiResponse<FileDto>>> ShareFile(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _fileService.ShareFileAsync(id, userId);
                return Ok(new ApiResponse<FileDto>
                {
                    Success = true,
                    Data = data,
                    Message = "共享成功"
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

        [HttpPut("{id}/private")]
        public async Task<ActionResult<ApiResponse<FileDto>>> MakeFilePrivate(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _fileService.MakeFilePrivateAsync(id, userId);
                return Ok(new ApiResponse<FileDto>
                {
                    Success = true,
                    Data = data,
                    Message = "已设为私密"
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

        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var (fileBytes, contentType, fileName) = await _fileService.DownloadFileAsync(id, userId);
                return File(fileBytes, contentType, fileName);
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

        [HttpGet("{id}/doc-preview")]
        public async Task<ActionResult<ApiResponse<string>>> GetDocPreview(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var html = await _fileService.GetDocPreviewHtmlAsync(id, userId);
                return Ok(new ApiResponse<string>
                {
                    Success = true,
                    Data = html
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

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteFile(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _fileService.DeleteFileAsync(id, userId);
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

        [HttpPut("{id}/restore")]
        public async Task<ActionResult<ApiResponse<FileDto>>> RestoreFile(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _fileService.RestoreFileAsync(id, userId);
                return Ok(new ApiResponse<FileDto>
                {
                    Success = true,
                    Data = data,
                    Message = "恢复成功"
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

        [HttpDelete("{id}/permanent")]
        public async Task<ActionResult<ApiResponse<object>>> PermanentlyDeleteFile(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _fileService.PermanentlyDeleteFileAsync(id, userId);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "彻底删除成功"
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

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }
    }
}
