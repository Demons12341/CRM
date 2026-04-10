using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.Data;
using FileEntity = ProjectManagementSystem.Models.Entities.File;

namespace ProjectManagementSystem.Services.Implementations
{
    public class FileCleanupHostedService : BackgroundService
    {
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
        private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FileCleanupHostedService> _logger;
        private readonly IWebHostEnvironment _environment;

        public FileCleanupHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<FileCleanupHostedService> logger,
            IWebHostEnvironment environment)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _environment = environment;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredDeletedFilesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "延迟清理文件源文件失败");
                }

                await Task.Delay(CleanupInterval, stoppingToken);
            }
        }

        private async Task CleanupExpiredDeletedFilesAsync(CancellationToken cancellationToken)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(RetentionPeriod);

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var expiredDeletedFiles = await dbContext.Files
                .IgnoreQueryFilters()
                .Where(f => f.IsDeleted
                    && !f.IsFolder
                    && f.DeletedAt.HasValue
                    && f.DeletedAt.Value <= cutoffTime
                    && !string.IsNullOrWhiteSpace(f.FilePath))
                .ToListAsync(cancellationToken);

            if (expiredDeletedFiles.Count == 0)
            {
                return;
            }

            var cleanedCount = 0;

            foreach (var file in expiredDeletedFiles)
            {
                if (string.IsNullOrWhiteSpace(file.FilePath))
                {
                    continue;
                }

                try
                {
                    var fullPath = ResolvePhysicalPath(file.FilePath);

                    if (!string.IsNullOrWhiteSpace(fullPath) && System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }

                    file.FilePath = string.Empty;
                    cleanedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理文件源文件失败，FileId={FileId}, FileName={FileName}", file.Id, file.FileName);
                }
            }

            if (cleanedCount > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("延迟清理完成，本轮清理源文件数量：{Count}", cleanedCount);
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
    }
}
