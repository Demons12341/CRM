using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Models.Entities;
using ProjectManagementSystem.Services.Interfaces;
using Spire.Doc;
using Spire.Doc.Documents;
using System.Text;
using FileEntity = ProjectManagementSystem.Models.Entities.File;

namespace ProjectManagementSystem.Services.Implementations
{
    public class FileService : IFileService
    {
        private const string SharedFolderProjectName = "共享文件夹";
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public FileService(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<PaginatedResult<FileDto>> GetFilesByProjectIdAsync(int projectId, int? parentId, int? page, int? pageSize, string? keyword, bool recursive, int currentUserId)
        {
            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);

            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            var project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                throw new KeyNotFoundException("项目不存在");
            }

            await EnsureProjectFileAccessAsync(currentUser, project, currentUserId);
            await EnsureValidParentFolderAsync(projectId, parentId);

            var isProjectManager = IsProjectManager(project, currentUserId);
            var isSharedFolderProject = IsSharedFolderProject(project);

            var query = _context.Files
                .Include(f => f.Uploader)
                .Where(f => f.ProjectId == projectId)
                .AsQueryable();

            var shouldRecursiveSearch = recursive && !string.IsNullOrWhiteSpace(keyword);
            if (shouldRecursiveSearch)
            {
                if (parentId.HasValue)
                {
                    var descendantFolderIds = await GetDescendantFolderIdsAsync(projectId, parentId.Value);
                    query = query.Where(f =>
                        f.ParentId == parentId.Value
                        || (f.ParentId.HasValue && descendantFolderIds.Contains(f.ParentId.Value)));
                }

                query = query.Where(f => !f.IsFolder);
            }
            else
            {
                if (parentId.HasValue)
                {
                    query = query.Where(f => f.ParentId == parentId.Value);
                }
                else
                {
                    query = query.Where(f => f.ParentId == null);
                }
            }

            if (isSharedFolderProject)
            {
                query = query.Where(f => f.IsFolder || f.IsShared || f.UploadedBy == currentUserId);
            }
            else if (!IsAdmin(currentUser) && !isProjectManager)
            {
                query = query.Where(f => f.IsFolder || f.UploadedBy == currentUserId || f.IsShared);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(f => f.FileName.Contains(keyword) || (f.Description != null && f.Description.Contains(keyword)));
            }

            var totalCount = await query.CountAsync();
            IOrderedQueryable<FileEntity> orderedQuery;
            if (isSharedFolderProject)
            {
                orderedQuery = query
                    .OrderByDescending(f => f.IsFolder)
                    .ThenBy(f => f.IsFolder ? 1 : (f.UploadedBy == currentUserId && !f.IsShared ? 0 : 1))
                    .ThenBy(f => f.FileName)
                    .ThenByDescending(f => f.UploadedAt);
            }
            else
            {
                orderedQuery = query
                    .OrderByDescending(f => f.IsFolder)
                    .ThenBy(f => f.FileName)
                    .ThenByDescending(f => f.UploadedAt);
            }

            var shouldPaginate = page.HasValue && pageSize.HasValue;
            var currentPage = shouldPaginate ? Math.Max(page!.Value, 1) : 1;
            var currentPageSize = shouldPaginate ? Math.Max(pageSize!.Value, 1) : Math.Max(totalCount, 1);
            var totalPages = shouldPaginate
                ? (int)Math.Ceiling(totalCount / (double)currentPageSize)
                : (totalCount > 0 ? 1 : 0);

            var pagedQuery = shouldPaginate
                ? orderedQuery.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize)
                : orderedQuery;

            var files = await pagedQuery
                .Select(f => new FileDto
                {
                    Id = f.Id,
                    ProjectId = f.ProjectId,
                    ParentId = f.ParentId,
                    IsFolder = f.IsFolder,
                    FileName = f.FileName,
                    FilePath = f.FilePath,
                    FileType = f.FileType,
                    FileSize = f.FileSize,
                    UploadedBy = f.UploadedBy,
                    IsShared = f.IsShared,
                    UploaderName = !string.IsNullOrWhiteSpace(f.Uploader.RealName) ? f.Uploader.RealName! : f.Uploader.Username,
                    UploadedAt = f.UploadedAt,
                    Description = f.Description,
                    DeletedAt = f.DeletedAt
                })
                .ToListAsync();

            if (files.Count > 0)
            {
                var pathNodes = await _context.Files
                    .AsNoTracking()
                    .Where(f => f.ProjectId == projectId)
                    .Select(f => new { f.Id, f.ParentId, f.FileName })
                    .ToListAsync();

                var nodeLookup = pathNodes.ToDictionary(node => node.Id, node => node);

                string ResolveLocationPath(int? parent)
                {
                    if (!parent.HasValue)
                    {
                        return "项目根目录";
                    }

                    var names = new List<string>();
                    var currentId = parent.Value;
                    var guard = 0;

                    while (guard < 200 && nodeLookup.TryGetValue(currentId, out var current))
                    {
                        names.Add(current.FileName);
                        if (!current.ParentId.HasValue)
                        {
                            break;
                        }

                        currentId = current.ParentId.Value;
                        guard++;
                    }

                    names.Reverse();
                    return names.Count == 0 ? "项目根目录" : string.Join("/", names);
                }

                foreach (var file in files)
                {
                    file.LocationPath = ResolveLocationPath(file.ParentId);
                }
            }

            return new PaginatedResult<FileDto>
            {
                Items = files,
                TotalCount = totalCount,
                Page = currentPage,
                PageSize = currentPageSize,
                TotalPages = totalPages
            };
        }

        private async Task<List<int>> GetDescendantFolderIdsAsync(int projectId, int rootFolderId)
        {
            var folders = await _context.Files
                .AsNoTracking()
                .Where(f => f.ProjectId == projectId && f.IsFolder)
                .Select(f => new { f.Id, f.ParentId })
                .ToListAsync();

            var childrenByParent = folders
                .Where(folder => folder.ParentId.HasValue)
                .GroupBy(folder => folder.ParentId!.Value)
                .ToDictionary(group => group.Key, group => group.Select(folder => folder.Id).ToList());

            var result = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(rootFolderId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!childrenByParent.TryGetValue(current, out var children))
                {
                    continue;
                }

                foreach (var child in children)
                {
                    result.Add(child);
                    queue.Enqueue(child);
                }
            }

            return result;
        }

        public async Task<PaginatedResult<FileDto>> GetRecycleBinFilesByProjectIdAsync(int projectId, int? page, int? pageSize, string? keyword, int currentUserId)
        {
            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);

            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            var project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                throw new KeyNotFoundException("项目不存在");
            }

            await EnsureProjectFileAccessAsync(currentUser, project, currentUserId);

            var isProjectManager = IsProjectManager(project, currentUserId);
            var isSharedFolderProject = IsSharedFolderProject(project);

            var query = _context.Files
                .IgnoreQueryFilters()
                .Include(f => f.Uploader)
                .Where(f => f.ProjectId == projectId && f.IsDeleted)
                .AsQueryable();

            if (!IsAdmin(currentUser) && !isProjectManager && !isSharedFolderProject)
            {
                query = query.Where(f => f.UploadedBy == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(f => f.FileName.Contains(keyword) || (f.Description != null && f.Description.Contains(keyword)));
            }

            var totalCount = await query.CountAsync();
            var currentPage = page.HasValue ? Math.Max(page.Value, 1) : 1;
            var currentPageSize = pageSize.HasValue ? Math.Max(pageSize.Value, 1) : Math.Max(totalCount, 1);
            var totalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)currentPageSize) : 0;

            var items = await query
                .OrderByDescending(f => f.DeletedAt)
                .ThenByDescending(f => f.UploadedAt)
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .Select(f => new FileDto
                {
                    Id = f.Id,
                    ProjectId = f.ProjectId,
                    ParentId = f.ParentId,
                    IsFolder = f.IsFolder,
                    FileName = f.FileName,
                    FilePath = f.FilePath,
                    FileType = f.FileType,
                    FileSize = f.FileSize,
                    UploadedBy = f.UploadedBy,
                    IsShared = f.IsShared,
                    UploaderName = !string.IsNullOrWhiteSpace(f.Uploader.RealName) ? f.Uploader.RealName! : f.Uploader.Username,
                    UploadedAt = f.UploadedAt,
                    Description = f.Description,
                    DeletedAt = f.DeletedAt
                })
                .ToListAsync();

            await FillLocationPathsAsync(items, includeDeletedNodes: true);

            return new PaginatedResult<FileDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = currentPage,
                PageSize = currentPageSize,
                TotalPages = totalPages
            };
        }

        public async Task<PaginatedResult<FileDto>> GetRecycleBinFilesAsync(int? page, int? pageSize, string? keyword, int currentUserId)
        {
            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);

            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            var query = _context.Files
                .IgnoreQueryFilters()
                .Include(f => f.Uploader)
                .Where(f => f.IsDeleted)
                .AsQueryable();

            var managedProjectIds = new List<int>();
            var sharedFolderProjectIds = new List<int>();

            if (!IsAdmin(currentUser))
            {
                managedProjectIds = await _context.Projects
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(p => p.ManagerId == currentUserId)
                    .Select(p => p.Id)
                    .ToListAsync();

                sharedFolderProjectIds = await _context.Projects
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(p => p.Name == SharedFolderProjectName)
                    .Select(p => p.Id)
                    .ToListAsync();

                query = query.Where(f =>
                    f.UploadedBy == currentUserId
                    || managedProjectIds.Contains(f.ProjectId)
                    || sharedFolderProjectIds.Contains(f.ProjectId));
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var keywordText = keyword.Trim();
                var keywordProjectIds = await _context.Projects
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(p => p.Name.Contains(keywordText))
                    .Select(p => p.Id)
                    .ToListAsync();

                query = query.Where(f =>
                    f.FileName.Contains(keywordText)
                    || keywordProjectIds.Contains(f.ProjectId)
                    || (f.Description != null && f.Description.Contains(keywordText)));
            }

            var totalCount = await query.CountAsync();
            var currentPage = page.HasValue ? Math.Max(page.Value, 1) : 1;
            var currentPageSize = pageSize.HasValue ? Math.Max(pageSize.Value, 1) : Math.Max(totalCount, 1);
            var totalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)currentPageSize) : 0;

            var items = await query
                .OrderByDescending(f => f.DeletedAt)
                .ThenByDescending(f => f.UploadedAt)
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .Select(f => new FileDto
                {
                    Id = f.Id,
                    ProjectId = f.ProjectId,
                    ParentId = f.ParentId,
                    IsFolder = f.IsFolder,
                    FileName = f.FileName,
                    FilePath = f.FilePath,
                    FileType = f.FileType,
                    FileSize = f.FileSize,
                    UploadedBy = f.UploadedBy,
                    IsShared = f.IsShared,
                    UploaderName = !string.IsNullOrWhiteSpace(f.Uploader.RealName) ? f.Uploader.RealName! : f.Uploader.Username,
                    UploadedAt = f.UploadedAt,
                    Description = f.Description,
                    DeletedAt = f.DeletedAt
                })
                .ToListAsync();

            if (items.Count > 0)
            {
                var projectIds = items.Select(item => item.ProjectId).Distinct().ToList();
                var projectNameMap = await _context.Projects
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(project => projectIds.Contains(project.Id))
                    .Select(project => new { project.Id, project.Name })
                    .ToDictionaryAsync(project => project.Id, project => project.Name ?? "-");

                foreach (var item in items)
                {
                    item.ProjectName = projectNameMap.TryGetValue(item.ProjectId, out var projectName)
                        ? projectName
                        : "-";
                }
            }

            return new PaginatedResult<FileDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = currentPage,
                PageSize = currentPageSize,
                TotalPages = totalPages
            };
        }

        public async Task<FileDto> GetFileByIdAsync(int id)
        {
            var file = await _context.Files
                .Include(f => f.Uploader)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (file == null)
            {
                throw new KeyNotFoundException("文件不存在");
            }

            return new FileDto
            {
                Id = file.Id,
                ProjectId = file.ProjectId,
                ParentId = file.ParentId,
                IsFolder = file.IsFolder,
                FileName = file.FileName,
                FilePath = file.FilePath,
                FileType = file.FileType,
                FileSize = file.FileSize,
                UploadedBy = file.UploadedBy,
                IsShared = file.IsShared,
                UploaderName = !string.IsNullOrWhiteSpace(file.Uploader.RealName) ? file.Uploader.RealName! : file.Uploader.Username,
                UploadedAt = file.UploadedAt,
                Description = file.Description,
                DeletedAt = file.DeletedAt
            };
        }

        public async Task<FileDto> UploadFileAsync(int projectId, int? parentId, IFormFile file, int userId, string? description)
        {
            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            var project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                throw new KeyNotFoundException("项目不存在");
            }

            await EnsureProjectFileAccessAsync(currentUser, project, userId);
            await EnsureValidParentFolderAsync(projectId, parentId);

            var normalizedFileName = Path.GetFileName(file.FileName).Trim();
            if (string.IsNullOrWhiteSpace(normalizedFileName))
            {
                throw new ArgumentException("文件名不能为空");
            }

            var duplicateExists = await _context.Files
                .AnyAsync(f => f.ProjectId == projectId
                    && f.ParentId == parentId
                    && f.FileName == normalizedFileName);

            if (duplicateExists)
            {
                throw new InvalidOperationException("同级目录下已存在同名文件/文件夹");
            }

            var uploadsPath = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            var fileName = $"{Guid.NewGuid()}_{normalizedFileName}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileEntity = new FileEntity
            {
                ProjectId = projectId,
                ParentId = parentId,
                IsFolder = false,
                FileName = normalizedFileName,
                FilePath = $"/uploads/{fileName}",
                FileType = Path.GetExtension(normalizedFileName),
                FileSize = file.Length,
                UploadedBy = userId,
                IsShared = IsSharedFolderProject(project),
                UploadedAt = DateTime.UtcNow,
                Description = description
            };

            _context.Files.Add(fileEntity);
            await _context.SaveChangesAsync();

            return await GetFileByIdAsync(fileEntity.Id);
        }

        public async Task<FileDto> CreateFolderAsync(int projectId, int? parentId, string folderName, int userId)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentException("文件夹名称不能为空");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            var project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                throw new KeyNotFoundException("项目不存在");
            }

            await EnsureProjectFileAccessAsync(currentUser, project, userId);
            await EnsureValidParentFolderAsync(projectId, parentId);

            var normalizedName = folderName.Trim();
            var duplicateExists = await _context.Files
                .AnyAsync(f => f.ProjectId == projectId
                    && f.ParentId == parentId
                    && f.IsFolder
                    && f.FileName == normalizedName);

            if (duplicateExists)
            {
                throw new InvalidOperationException("同级目录下已存在同名文件夹");
            }

            var folder = new FileEntity
            {
                ProjectId = projectId,
                ParentId = parentId,
                IsFolder = true,
                FileName = normalizedName,
                FilePath = string.Empty,
                FileType = null,
                FileSize = 0,
                UploadedBy = userId,
                UploadedAt = DateTime.UtcNow,
                Description = null
            };

            _context.Files.Add(folder);
            await _context.SaveChangesAsync();

            return await GetFileByIdAsync(folder.Id);
        }

        public async Task<FileDto> MoveFileAsync(int id, int? targetFolderId, int userId)
        {
            var file = await _context.Files
                .Include(f => f.Project)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (file == null)
            {
                throw new KeyNotFoundException("文件不存在");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            await EnsureProjectFileAccessAsync(currentUser, file.Project, userId);

            var isProjectManager = IsProjectManager(file.Project, userId);
            var isSharedFolderProject = IsSharedFolderProject(file.Project);
            if (!IsAdmin(currentUser) && !isProjectManager && !isSharedFolderProject && file.UploadedBy != userId)
            {
                throw new UnauthorizedAccessException("普通成员只能移动自己上传的文件");
            }

            if (file.ParentId == targetFolderId)
            {
                return await GetFileByIdAsync(file.Id);
            }

            if (targetFolderId.HasValue)
            {
                var targetFolder = await _context.Files
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == targetFolderId.Value && f.ProjectId == file.ProjectId);
                if (targetFolder == null)
                {
                    throw new KeyNotFoundException("目标文件夹不存在");
                }

                if (!targetFolder.IsFolder)
                {
                    throw new InvalidOperationException("目标节点不是文件夹");
                }

                if (file.IsFolder)
                {
                    if (targetFolder.Id == file.Id)
                    {
                        throw new InvalidOperationException("不能将文件夹移动到自身");
                    }

                    var isDescendant = await IsDescendantFolderAsync(file.Id, targetFolder.Id);
                    if (isDescendant)
                    {
                        throw new InvalidOperationException("不能将文件夹移动到其子文件夹中");
                    }
                }
            }

            var duplicateExists = await _context.Files
                .AnyAsync(f => f.ProjectId == file.ProjectId
                    && f.ParentId == targetFolderId
                    && f.FileName == file.FileName
                    && f.Id != file.Id);

            if (duplicateExists)
            {
                throw new InvalidOperationException("目标文件夹中已存在同名文件");
            }

            file.ParentId = targetFolderId;
            await _context.SaveChangesAsync();

            return await GetFileByIdAsync(file.Id);
        }

        public async Task<FileDto> RenameEntryAsync(int id, string name, int userId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("名称不能为空");
            }

            var entry = await _context.Files
                .Include(f => f.Project)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (entry == null)
            {
                throw new KeyNotFoundException("文件不存在");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            await EnsureProjectFileAccessAsync(currentUser, entry.Project, userId);

            var isProjectManager = IsProjectManager(entry.Project, userId);
            var isSharedFolderProject = IsSharedFolderProject(entry.Project);
            if (!IsAdmin(currentUser) && !isProjectManager && !isSharedFolderProject && entry.UploadedBy != userId)
            {
                throw new UnauthorizedAccessException("普通成员只能重命名自己创建的文件/文件夹");
            }

            var normalizedName = name.Trim();
            var duplicateExists = await _context.Files
                .AnyAsync(f => f.ProjectId == entry.ProjectId
                    && f.ParentId == entry.ParentId
                    && f.FileName == normalizedName
                    && f.Id != entry.Id);

            if (duplicateExists)
            {
                throw new InvalidOperationException("同级目录下已存在同名项");
            }

            entry.FileName = normalizedName;
            await _context.SaveChangesAsync();

            return await GetFileByIdAsync(entry.Id);
        }

        public async Task<FileDto> ShareFileAsync(int id, int userId)
        {
            var file = await _context.Files
                .Include(f => f.Project)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (file == null)
            {
                throw new KeyNotFoundException("文件不存在");
            }

            if (file.IsFolder)
            {
                throw new InvalidOperationException("文件夹不支持共享");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            await EnsureProjectFileAccessAsync(currentUser, file.Project, userId);

            if (file.UploadedBy != userId)
            {
                throw new UnauthorizedAccessException("仅上传人可共享该文件");
            }

            file.IsShared = true;
            await _context.SaveChangesAsync();

            return await GetFileByIdAsync(file.Id);
        }

        public async Task<FileDto> MakeFilePrivateAsync(int id, int userId)
        {
            var file = await _context.Files
                .Include(f => f.Project)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (file == null)
            {
                throw new KeyNotFoundException("文件不存在");
            }

            if (file.IsFolder)
            {
                throw new InvalidOperationException("文件夹不支持私密操作");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            await EnsureProjectFileAccessAsync(currentUser, file.Project, userId);

            if (file.UploadedBy != userId)
            {
                throw new UnauthorizedAccessException("仅上传人可修改该文件共享状态");
            }

            file.IsShared = false;
            await _context.SaveChangesAsync();

            return await GetFileByIdAsync(file.Id);
        }

        public async Task<bool> DeleteFileAsync(int id, int currentUserId)
        {
            var file = await _context.Files
                .Include(f => f.Project)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (file == null)
            {
                throw new KeyNotFoundException("文件不存在");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            await EnsureProjectFileAccessAsync(currentUser, file.Project, currentUserId);
            if (!IsAdmin(currentUser) && file.UploadedBy != currentUserId)
            {
                throw new UnauthorizedAccessException("仅上传人可删除该文件");
            }

            if (file.IsFolder)
            {
                var hasChildren = await _context.Files
                    .AnyAsync(f => f.ParentId == file.Id);
                if (hasChildren)
                {
                    throw new InvalidOperationException("请先删除文件夹中的内容");
                }

                _context.Files.Remove(file);
                await _context.SaveChangesAsync();
                return true;
            }

            file.IsDeleted = true;
            file.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<FileDto> RestoreFileAsync(int id, int currentUserId)
        {
            var file = await _context.Files
                .IgnoreQueryFilters()
                .Include(f => f.Project)
                .FirstOrDefaultAsync(f => f.Id == id && f.IsDeleted);
            if (file == null)
            {
                throw new KeyNotFoundException("回收站中不存在该文件");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            await EnsureProjectFileAccessAsync(currentUser, file.Project, currentUserId);
            var isProjectManager = IsProjectManager(file.Project, currentUserId);
            var isSharedFolderProject = IsSharedFolderProject(file.Project);
            if (!IsAdmin(currentUser) && !isProjectManager && !isSharedFolderProject && file.UploadedBy != currentUserId)
            {
                throw new UnauthorizedAccessException("普通成员只能恢复自己上传的文件");
            }

            if (!file.DeletedAt.HasValue || file.DeletedAt.Value.AddDays(7) < DateTime.UtcNow)
            {
                throw new InvalidOperationException("该文件已超过恢复期限（7天）");
            }

            if (file.ParentId.HasValue)
            {
                var parent = await _context.Files
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == file.ParentId.Value && f.ProjectId == file.ProjectId);

                if (parent == null || parent.IsDeleted)
                {
                    file.ParentId = null;
                }
            }

            var duplicateExists = await _context.Files
                .AnyAsync(f => f.ProjectId == file.ProjectId
                    && f.ParentId == file.ParentId
                    && f.FileName == file.FileName
                    && f.Id != file.Id);

            if (duplicateExists)
            {
                throw new InvalidOperationException("目标位置已存在同名文件/文件夹，无法恢复");
            }

            if (!file.IsFolder)
            {
                if (string.IsNullOrWhiteSpace(file.FilePath))
                {
                    throw new InvalidOperationException("源文件已被清理，无法恢复");
                }

                var fullPath = ResolvePhysicalPath(file.FilePath);
                if (string.IsNullOrWhiteSpace(fullPath))
                {
                    throw new InvalidOperationException("源文件路径无效，无法恢复");
                }

                if (!System.IO.File.Exists(fullPath))
                {
                    throw new InvalidOperationException("源文件不存在，无法恢复");
                }
            }

            file.IsDeleted = false;
            file.DeletedAt = null;
            await _context.SaveChangesAsync();

            return await GetFileByIdAsync(file.Id);
        }

        public async Task<bool> PermanentlyDeleteFileAsync(int id, int currentUserId)
        {
            var file = await _context.Files
                .IgnoreQueryFilters()
                .Include(f => f.Project)
                .FirstOrDefaultAsync(f => f.Id == id && f.IsDeleted);
            if (file == null)
            {
                throw new KeyNotFoundException("回收站中不存在该文件");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            await EnsureProjectFileAccessAsync(currentUser, file.Project, currentUserId);
            if (!IsAdmin(currentUser) && file.UploadedBy != currentUserId)
            {
                throw new UnauthorizedAccessException("仅上传人可彻底删除该文件");
            }

            if (!file.IsFolder)
            {
                if (!string.IsNullOrWhiteSpace(file.FilePath))
                {
                    var fullPath = ResolvePhysicalPath(file.FilePath);
                    if (!string.IsNullOrWhiteSpace(fullPath) && System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }

                _context.Files.Remove(file);
                await _context.SaveChangesAsync();

                return true;
            }

            await PermanentlyDeleteFolderTreeAsync(file.ProjectId, file.Id, includeDeletedNodes: true);

            return true;
        }

        private async System.Threading.Tasks.Task PermanentlyDeleteFolderTreeAsync(int projectId, int rootFolderId, bool includeDeletedNodes)
        {
            var query = includeDeletedNodes
                ? _context.Files.IgnoreQueryFilters().AsQueryable()
                : _context.Files.AsQueryable();

            var projectFiles = await query
                .Where(f => f.ProjectId == projectId)
                .ToListAsync();

            var childrenMap = projectFiles
                .Where(f => f.ParentId.HasValue)
                .GroupBy(f => f.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

            var idsToDelete = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(rootFolderId);

            while (stack.Count > 0)
            {
                var currentId = stack.Pop();
                if (!idsToDelete.Add(currentId))
                {
                    continue;
                }

                if (childrenMap.TryGetValue(currentId, out var childIds))
                {
                    foreach (var childId in childIds)
                    {
                        stack.Push(childId);
                    }
                }
            }

            var deleteTargets = projectFiles
                .Where(f => idsToDelete.Contains(f.Id))
                .ToList();

            foreach (var deleteTarget in deleteTargets.Where(f => !f.IsFolder && !string.IsNullOrWhiteSpace(f.FilePath)))
            {
                var fullPath = ResolvePhysicalPath(deleteTarget.FilePath);
                if (!string.IsNullOrWhiteSpace(fullPath) && System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }

            var remain = deleteTargets.ToList();
            while (remain.Count > 0)
            {
                var leafNodes = remain
                    .Where(candidate => !remain.Any(other => other.ParentId == candidate.Id))
                    .ToList();

                if (leafNodes.Count == 0)
                {
                    throw new InvalidOperationException("文件夹结构异常，无法彻底删除");
                }

                _context.Files.RemoveRange(leafNodes);
                remain.RemoveAll(node => leafNodes.Any(leaf => leaf.Id == node.Id));
            }

            await _context.SaveChangesAsync();
        }

        public async Task<(byte[] fileBytes, string contentType, string fileName)> DownloadFileAsync(int id, int currentUserId)
        {
            var file = await _context.Files
                .Include(f => f.Project)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (file == null)
            {
                throw new KeyNotFoundException("文件不存在");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            await EnsureProjectFileAccessAsync(currentUser, file.Project, currentUserId);
            if (!CanReadFile(currentUser, file.Project, file, currentUserId))
            {
                throw new UnauthorizedAccessException("您无权下载该文件");
            }

            if (file.IsFolder)
            {
                throw new InvalidOperationException("文件夹不支持下载");
            }

            var fullPath = ResolvePhysicalPath(file.FilePath);
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                throw new KeyNotFoundException("文件不存在");
            }

            if (!System.IO.File.Exists(fullPath))
            {
                throw new KeyNotFoundException("文件不存在");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var contentType = GetContentType(file.FileType ?? "");

            return (fileBytes, contentType, file.FileName);
        }

        public async Task<string> GetDocPreviewHtmlAsync(int id, int currentUserId)
        {
            var file = await _context.Files
                .Include(f => f.Project)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (file == null)
            {
                throw new KeyNotFoundException("文件不存在");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);
            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            await EnsureProjectFileAccessAsync(currentUser, file.Project, currentUserId);
            if (!CanReadFile(currentUser, file.Project, file, currentUserId))
            {
                throw new UnauthorizedAccessException("您无权预览该文件");
            }

            if (file.IsFolder)
            {
                throw new InvalidOperationException("文件夹不支持预览");
            }

            var ext = (file.FileType ?? string.Empty).ToLowerInvariant();
            if (ext != ".doc")
            {
                throw new InvalidOperationException("当前文件不是 .doc 格式");
            }

            var fullPath = ResolvePhysicalPath(file.FilePath);
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                throw new KeyNotFoundException("文件不存在");
            }

            if (!System.IO.File.Exists(fullPath))
            {
                throw new KeyNotFoundException("文件不存在");
            }

            try
            {
                var document = new Document();
                document.LoadFromFile(fullPath);

                using var stream = new MemoryStream();
                document.SaveToStream(stream, FileFormat.Html);
                stream.Position = 0;

                using var reader = new StreamReader(stream, Encoding.UTF8);
                var html = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(html))
                {
                    return "<p>文档内容为空</p>";
                }

                return html;
            }
            catch
            {
                throw new InvalidOperationException("该 .doc 文件暂不支持在线预览，请下载后查看");
            }
        }

        private async System.Threading.Tasks.Task EnsureProjectFileAccessAsync(User currentUser, Project project, int currentUserId)
        {
            if (IsSharedFolderProject(project))
            {
                return;
            }

            if (IsAdmin(currentUser))
            {
                return;
            }

            if (IsProjectManager(project, currentUserId))
            {
                return;
            }

            var isMember = await _context.ProjectMembers
                .AsNoTracking()
                .AnyAsync(pm => pm.ProjectId == project.Id && pm.UserId == currentUserId);

            if (!isMember)
            {
                throw new UnauthorizedAccessException("您无权访问该项目文件");
            }
        }

        private async System.Threading.Tasks.Task EnsureValidParentFolderAsync(int projectId, int? parentId)
        {
            if (!parentId.HasValue)
            {
                return;
            }

            var parent = await _context.Files
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == parentId.Value && f.ProjectId == projectId);

            if (parent == null)
            {
                throw new KeyNotFoundException("父级文件夹不存在");
            }

            if (!parent.IsFolder)
            {
                throw new InvalidOperationException("父级节点不是文件夹");
            }
        }

        private async System.Threading.Tasks.Task<bool> IsDescendantFolderAsync(int folderId, int targetFolderId)
        {
            var currentParentId = targetFolderId;
            while (true)
            {
                if (currentParentId == folderId)
                {
                    return true;
                }

                var current = await _context.Files
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == currentParentId);

                if (current == null || !current.ParentId.HasValue)
                {
                    return false;
                }

                currentParentId = current.ParentId.Value;
            }
        }

        private static bool IsAdmin(User user)
        {
            return user.Role.Name == "管理员";
        }

        private static bool IsProjectManager(Project project, int userId)
        {
            return project.ManagerId == userId;
        }

        private static bool IsSharedFolderProject(Project project)
        {
            return string.Equals(project.Name?.Trim(), SharedFolderProjectName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanReadFile(User currentUser, Project project, FileEntity file, int currentUserId)
        {
            if (file.IsFolder)
            {
                return true;
            }

            if (IsSharedFolderProject(project))
            {
                return file.IsShared || file.UploadedBy == currentUserId;
            }

            if (IsAdmin(currentUser) || IsProjectManager(project, currentUserId))
            {
                return true;
            }

            return file.UploadedBy == currentUserId || file.IsShared;
        }

        private async System.Threading.Tasks.Task FillLocationPathsAsync(List<FileDto> files, bool includeDeletedNodes)
        {
            if (files.Count == 0)
            {
                return;
            }

            var projectIds = files.Select(f => f.ProjectId).Distinct().ToList();
            var nodeQuery = includeDeletedNodes
                ? _context.Files.IgnoreQueryFilters().AsNoTracking()
                : _context.Files.AsNoTracking();

            var nodes = await nodeQuery
                .Where(f => projectIds.Contains(f.ProjectId))
                .Select(f => new { f.Id, f.ProjectId, f.ParentId, f.FileName })
                .ToListAsync();

            var lookup = nodes.ToDictionary(node => (node.ProjectId, node.Id), node => node);

            foreach (var file in files)
            {
                if (!file.ParentId.HasValue)
                {
                    file.LocationPath = "项目根目录";
                    continue;
                }

                var names = new List<string>();
                var currentId = file.ParentId.Value;
                var guard = 0;

                while (guard < 200 && lookup.TryGetValue((file.ProjectId, currentId), out var current))
                {
                    names.Add(current.FileName);
                    if (!current.ParentId.HasValue)
                    {
                        break;
                    }

                    currentId = current.ParentId.Value;
                    guard++;
                }

                names.Reverse();
                file.LocationPath = names.Count == 0 ? "项目根目录" : string.Join("/", names);
            }
        }

        private string? ResolvePhysicalPath(string? storedFilePath)
        {
            if (string.IsNullOrWhiteSpace(storedFilePath))
            {
                return null;
            }

            var candidate = storedFilePath.Trim();
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var absoluteUri))
            {
                candidate = absoluteUri.LocalPath;
            }

            var queryIndex = candidate.IndexOfAny(new[] { '?', '#' });
            if (queryIndex >= 0)
            {
                candidate = candidate[..queryIndex];
            }

            candidate = candidate.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return null;
            }

            var webRoot = !string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? _environment.WebRootPath
                : Path.Combine(_environment.ContentRootPath, "wwwroot");

            var rootFullPath = Path.GetFullPath(webRoot);
            var normalizedRelativePath = candidate.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(rootFullPath, normalizedRelativePath));

            return fullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : null;
        }

        private string GetContentType(string fileExtension)
        {
            return fileExtension.ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                ".csv" => "text/csv",
                ".json" => "application/json",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                _ => "application/octet-stream"
            };
        }
    }
}
