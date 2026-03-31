using Microsoft.AspNetCore.Http;
using ProjectManagementSystem.Models.DTOs;

namespace ProjectManagementSystem.Services.Interfaces
{
    public interface IFileService
    {
        Task<PaginatedResult<FileDto>> GetFilesByProjectIdAsync(int projectId, int? parentId, int? page, int? pageSize, string? keyword, int currentUserId);
        Task<PaginatedResult<FileDto>> GetRecycleBinFilesByProjectIdAsync(int projectId, int? page, int? pageSize, string? keyword, int currentUserId);
        Task<FileDto> GetFileByIdAsync(int id);
        Task<FileDto> UploadFileAsync(int projectId, int? parentId, IFormFile file, int userId, string? description);
        Task<FileDto> CreateFolderAsync(int projectId, int? parentId, string folderName, int userId);
        Task<FileDto> MoveFileAsync(int id, int? targetFolderId, int userId);
        Task<FileDto> RenameEntryAsync(int id, string name, int userId);
        Task<bool> DeleteFileAsync(int id, int currentUserId);
        Task<FileDto> RestoreFileAsync(int id, int currentUserId);
        Task<bool> PermanentlyDeleteFileAsync(int id, int currentUserId);
        Task<(byte[] fileBytes, string contentType, string fileName)> DownloadFileAsync(int id, int currentUserId);
        Task<string> GetDocPreviewHtmlAsync(int id, int currentUserId);
    }

    public class FileDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int? ParentId { get; set; }
        public bool IsFolder { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? FileType { get; set; }
        public long FileSize { get; set; }
        public int UploadedBy { get; set; }
        public string UploaderName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public string? Description { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public class CreateFolderRequest
    {
        public int? ParentId { get; set; }
        public string FolderName { get; set; } = string.Empty;
    }

    public class MoveFileRequest
    {
        public int? TargetFolderId { get; set; }
    }

    public class RenameEntryRequest
    {
        public string Name { get; set; } = string.Empty;
    }
}
