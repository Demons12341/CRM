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
                    var normalizedRelativePath = file.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var fullPath = Path.Combine(_environment.ContentRootPath, "wwwroot", normalizedRelativePath);

                    if (System.IO.File.Exists(fullPath))
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
    }
}
