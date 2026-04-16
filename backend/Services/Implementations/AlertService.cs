using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.Common;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Models.Entities;
using ProjectManagementSystem.Services.Interfaces;
using TaskEntity = ProjectManagementSystem.Models.Entities.Task;

namespace ProjectManagementSystem.Services.Implementations
{
    public class AlertService : IAlertService
    {
        private const string SharedFolderProjectName = "共享文件夹";
        private readonly ApplicationDbContext _context;

        public AlertService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedResult<AlertDto>> GetAlertsAsync(int userId, int page, int pageSize, int? alertType, bool? isRead, int? alertStatus)
        {
            await EnsureOverdueAlertsAsync();

            var hiddenProjectIds = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Name == SharedFolderProjectName)
                .Select(p => p.Id)
                .ToListAsync();

            var query = _context.Alerts
                .Include(a => a.Project)
                .ThenInclude(p => p!.Manager)
                .Include(a => a.Task)
                .ThenInclude(t => t!.Assignee)
                .Where(a => a.UserId == userId)
                .AsQueryable();

            if (hiddenProjectIds.Any())
            {
                query = query.Where(a => !a.ProjectId.HasValue || !hiddenProjectIds.Contains(a.ProjectId.Value));
            }

            if (alertType.HasValue)
            {
                query = query.Where(a => a.AlertType == alertType.Value);
            }

            if (isRead.HasValue)
            {
                query = query.Where(a => a.IsRead == isRead.Value);
            }

            if (alertStatus.HasValue)
            {
                if (alertStatus.Value == 1)
                {
                    query = query.Where(a =>
                        (a.AlertType == 1 && a.Task != null && a.Task.Status == 2) ||
                        (a.AlertType == 2 && a.Project != null && a.Project.Status == 2));
                }
                else
                {
                    query = query.Where(a =>
                        !((a.AlertType == 1 && a.Task != null && a.Task.Status == 2) ||
                          (a.AlertType == 2 && a.Project != null && a.Project.Status == 2)));
                }
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var alertEntities = await query
                .OrderBy(a => a.AlertType == 2 ? 0 : (a.AlertType == 1 ? 1 : 2))
                .ThenByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var taskAssigneeUsernames = alertEntities
                .Where(a => a.Task != null)
                .SelectMany(a => ExtractDefaultAssigneeNames(a.Task!.Description))
                .Distinct()
                .ToList();

            var userDisplayNameMap = taskAssigneeUsernames.Any()
                ? await _context.Users
                    .AsNoTracking()
                    .Where(u => taskAssigneeUsernames.Contains(u.Username))
                    .Select(u => new { u.Username, DisplayName = !string.IsNullOrWhiteSpace(u.RealName) ? u.RealName! : u.Username })
                    .ToDictionaryAsync(u => u.Username, u => u.DisplayName)
                : new Dictionary<string, string>();

            var alerts = alertEntities
                .Select(a => new AlertDto
                {
                    Id = a.Id,
                    ProjectId = a.ProjectId,
                    ProjectManagerId = a.Project != null ? a.Project.ManagerId : null,
                    ProjectName = a.Project != null ? a.Project.Name : null,
                    ProjectManagerName = a.Project != null && a.Project.Manager != null
                        ? GetDisplayName(a.Project.Manager)
                        : null,
                    TaskId = a.TaskId,
                    TaskName = a.Task != null ? a.Task.Title : null,
                    TaskAssigneeName = BuildTaskAssigneeDisplay(a.Task, userDisplayNameMap),
                    UserId = a.UserId,
                    AlertType = a.AlertType,
                    AlertTypeName = GetAlertTypeName(a.AlertType),
                    AlertStatus = GetAlertStatus(a),
                    AlertStatusName = GetAlertStatusName(a),
                    OverdueReason = a.AlertType == 1
                        ? a.Task?.OverdueReason
                        : (a.AlertType == 2 ? ExtractProjectOverdueReasonFromMessage(a.Message) : null),
                    Message = a.Message,
                    IsRead = a.IsRead,
                    CreatedAt = a.CreatedAt
                })
                .ToList();

            return new PaginatedResult<AlertDto>
            {
                Items = alerts,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            await EnsureOverdueAlertsAsync();

            var hiddenProjectIds = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Name == SharedFolderProjectName)
                .Select(p => p.Id)
                .ToListAsync();

            var query = _context.Alerts
                .Include(a => a.Project)
                .Include(a => a.Task)
                .Where(a => a.UserId == userId && !a.IsRead)
                .AsQueryable();

            if (hiddenProjectIds.Any())
            {
                query = query.Where(a => !a.ProjectId.HasValue || !hiddenProjectIds.Contains(a.ProjectId.Value));
            }

            query = query.Where(a =>
                !((a.AlertType == 1 && a.Task != null && a.Task.Status == 2) ||
                  (a.AlertType == 2 && a.Project != null && a.Project.Status == 2)));

            return await query.CountAsync();
        }

        public async Task<bool> MarkAsReadAsync(int id, int userId)
        {
            var alert = await _context.Alerts
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (alert == null)
            {
                throw new KeyNotFoundException("告警不存在");
            }

            alert.IsRead = true;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> MarkAllAsReadAsync(int userId)
        {
            var alerts = await _context.Alerts
                .Where(a => a.UserId == userId && !a.IsRead)
                .ToListAsync();

            foreach (var alert in alerts)
            {
                alert.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdateOverdueReasonAsync(int alertId, int userId, string? overdueReason)
        {
            var alert = await _context.Alerts
                .Include(a => a.Task)
                .Include(a => a.Project)
                .FirstOrDefaultAsync(a => a.Id == alertId && a.UserId == userId);

            if (alert == null)
            {
                throw new KeyNotFoundException("告警不存在");
            }

            var currentUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            var isAdmin = currentUser.RoleId == 1;

            if (alert.AlertType == 1)
            {
                if (!alert.TaskId.HasValue || alert.Task == null)
                {
                    throw new InvalidOperationException("任务超期告警数据异常");
                }

                var allowedAssigneeIds = await ResolveTaskAssigneeIdsAsync(alert.Task);
                if (!isAdmin && !allowedAssigneeIds.Contains(userId))
                {
                    throw new UnauthorizedAccessException("仅任务负责人可填写超期原因");
                }

                var normalizedReason = string.IsNullOrWhiteSpace(overdueReason) ? null : overdueReason.Trim();
                if (string.Equals(alert.Task.OverdueReason, normalizedReason, StringComparison.Ordinal))
                {
                    return false;
                }

                var oldReason = alert.Task.OverdueReason;
                alert.Task.OverdueReason = normalizedReason;
                await _context.SaveChangesAsync();

                _context.TaskLogs.Add(new TaskLog
                {
                    TaskId = alert.Task.Id,
                    UserId = userId,
                    Action = "填写超期原因",
                    OldValue = oldReason,
                    NewValue = normalizedReason,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                await CheckOverdueTasksAsync();
                return true;
            }

            if (alert.AlertType == 2)
            {
                if (!alert.ProjectId.HasValue)
                {
                    throw new InvalidOperationException("项目超期告警数据异常");
                }

                var project = alert.Project ?? await _context.Projects.FirstOrDefaultAsync(p => p.Id == alert.ProjectId.Value);
                if (project == null)
                {
                    throw new KeyNotFoundException("项目不存在");
                }

                if (!isAdmin && project.ManagerId != userId)
                {
                    throw new UnauthorizedAccessException("仅项目负责人可填写超期原因");
                }

                var normalizedReason = string.IsNullOrWhiteSpace(overdueReason) ? null : overdueReason.Trim();
                var oldReason = ExtractProjectOverdueReasonFromMessage(alert.Message);
                if (string.Equals(oldReason, normalizedReason, StringComparison.Ordinal))
                {
                    return false;
                }

                var projectAlerts = await _context.Alerts
                    .Where(a => a.AlertType == 2 && a.ProjectId == alert.ProjectId.Value)
                    .ToListAsync();

                foreach (var projectAlert in projectAlerts)
                {
                    projectAlert.Message = UpsertProjectOverdueReasonInMessage(projectAlert.Message, normalizedReason);
                }

                await _context.SaveChangesAsync();
                await CheckOverdueProjectsAsync();
                return true;
            }

            throw new InvalidOperationException("仅任务超期或项目超期告警支持填写超期原因");
        }

        public async System.Threading.Tasks.Task CheckOverdueTasksAsync()
        {
            var activeUserIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .Select(u => u.Id)
                .ToListAsync();

            var tasks = await _context.Tasks
                .Include(t => t.Project)
                .ThenInclude(p => p!.Manager)
                .Include(t => t.Assignee)
                .Where(t => t.DueDate.HasValue)
                .ToListAsync();

            var existingAlerts = await _context.Alerts
                .Where(a => a.AlertType == 1)
                .ToListAsync();

            var duplicatedAlerts = existingAlerts
                .Where(a => a.TaskId.HasValue)
                .GroupBy(a => new { a.TaskId, a.UserId })
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.OrderByDescending(x => x.CreatedAt).Skip(1))
                .ToList();

            if (duplicatedAlerts.Any())
            {
                _context.Alerts.RemoveRange(duplicatedAlerts);
                existingAlerts = existingAlerts.Except(duplicatedAlerts).ToList();
            }

            var existingMap = existingAlerts
                .Where(a => a.TaskId.HasValue)
                .ToDictionary(a => $"{a.TaskId!.Value}_{a.UserId}", a => a);

            var validKeys = new HashSet<string>();
            var today = AppTime.Today;

            foreach (var task in tasks)
            {
                var isOverdueActive = task.Status != 2
                    && task.Status != 3
                    && task.DueDate!.Value.Date < today;

                var isCompletedOverdue = task.Status == 2
                    && task.CompletedAt.HasValue
                    && task.CompletedAt.Value.Date > task.DueDate!.Value.Date;

                if (!isOverdueActive && !isCompletedOverdue)
                {
                    continue;
                }

                var overdueDays = isCompletedOverdue
                    ? Math.Max(1, (task.CompletedAt!.Value.Date - task.DueDate!.Value.Date).Days)
                    : Math.Max(1, (today - task.DueDate!.Value.Date).Days);
                var projectName = task.Project?.Name ?? "-";
                var taskAssigneeName = task.Assignee != null ? GetDisplayName(task.Assignee) : "未指定";
                var projectManagerName = task.Project?.Manager != null ? GetDisplayName(task.Project.Manager) : "未指定";

                var reasonSuffix = string.IsNullOrWhiteSpace(task.OverdueReason)
                    ? string.Empty
                    : $"；超期原因：{task.OverdueReason}";

                var message = isCompletedOverdue
                    ? $"项目「{projectName}」的任务「{task.Title}」曾超期 {overdueDays} 天，当前已完成（预计截止：{task.DueDate:yyyy-MM-dd}，完成时间：{task.CompletedAt:yyyy-MM-dd}）{reasonSuffix}，任务负责人：{taskAssigneeName}，项目负责人：{projectManagerName}"
                    : $"项目「{projectName}」的任务「{task.Title}」已超期 {overdueDays} 天（预计截止：{task.DueDate:yyyy-MM-dd}）{reasonSuffix}，任务负责人：{taskAssigneeName}，项目负责人：{projectManagerName}";

                foreach (var recipientId in activeUserIds)
                {
                    var key = $"{task.Id}_{recipientId}";
                    validKeys.Add(key);
                    existingMap.TryGetValue(key, out var existingAlert);

                    if (existingAlert == null)
                    {
                        var alert = new Alert
                        {
                            ProjectId = task.ProjectId,
                            TaskId = task.Id,
                            UserId = recipientId,
                            AlertType = 1,
                            Message = message,
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Alerts.Add(alert);
                    }
                    else
                    {
                        existingAlert.Message = message;
                    }
                }
            }

            var invalidAlerts = existingAlerts
                .Where(a => !a.TaskId.HasValue || !validKeys.Contains($"{a.TaskId.Value}_{a.UserId}"))
                .ToList();

            if (invalidAlerts.Any())
            {
                _context.Alerts.RemoveRange(invalidAlerts);
            }

            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task CheckOverdueProjectsAsync()
        {
            var activeUserIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .Select(u => u.Id)
                .ToListAsync();

            var projects = await _context.Projects
                .Include(p => p.Manager)
                .Where(p => p.EndDate.HasValue)
                .ToListAsync();

            var existingAlerts = await _context.Alerts
                .Where(a => a.AlertType == 2)
                .ToListAsync();

            var duplicatedAlerts = existingAlerts
                .Where(a => a.ProjectId.HasValue)
                .GroupBy(a => new { a.ProjectId, a.UserId })
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.OrderByDescending(x => x.CreatedAt).Skip(1))
                .ToList();

            if (duplicatedAlerts.Any())
            {
                _context.Alerts.RemoveRange(duplicatedAlerts);
                existingAlerts = existingAlerts.Except(duplicatedAlerts).ToList();
            }

            var existingMap = existingAlerts
                .Where(a => a.ProjectId.HasValue)
                .ToDictionary(a => $"{a.ProjectId!.Value}_{a.UserId}", a => a);

            var validKeys = new HashSet<string>();
            var today = AppTime.Today;

            foreach (var project in projects)
            {
                var isOverdueActive = project.Status != 2
                    && project.Status != 3
                    && project.EndDate!.Value.Date < today;

                var isCompletedOverdue = project.Status == 2
                    && project.EndDate!.Value.Date < today;

                if (!isOverdueActive && !isCompletedOverdue)
                {
                    continue;
                }

                var overdueDays = Math.Max(1, (today - project.EndDate!.Value.Date).Days);
                var projectManagerName = project.Manager != null ? GetDisplayName(project.Manager) : "未指定";

                var projectOverdueReason = existingAlerts
                    .Where(a => a.ProjectId == project.Id)
                    .Select(a => ExtractProjectOverdueReasonFromMessage(a.Message))
                    .FirstOrDefault(reason => !string.IsNullOrWhiteSpace(reason));

                var reasonSuffix = string.IsNullOrWhiteSpace(projectOverdueReason)
                    ? string.Empty
                    : $"；超期原因：{projectOverdueReason}";

                var message = isCompletedOverdue
                    ? $"项目「{project.Name}」曾超期 {overdueDays} 天，当前已完成（项目截止：{project.EndDate:yyyy-MM-dd}）{reasonSuffix}，项目负责人：{projectManagerName}"
                    : $"项目「{project.Name}」已超期 {overdueDays} 天（项目截止：{project.EndDate:yyyy-MM-dd}）{reasonSuffix}，项目负责人：{projectManagerName}";

                foreach (var recipientId in activeUserIds)
                {
                    var key = $"{project.Id}_{recipientId}";
                    validKeys.Add(key);
                    existingMap.TryGetValue(key, out var existingAlert);

                    if (existingAlert == null)
                    {
                        var alert = new Alert
                        {
                            ProjectId = project.Id,
                            UserId = recipientId,
                            AlertType = 2,
                            Message = message,
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Alerts.Add(alert);
                    }
                    else
                    {
                        existingAlert.Message = message;
                    }
                }
            }

            var invalidAlerts = existingAlerts
                .Where(a => !a.ProjectId.HasValue || !validKeys.Contains($"{a.ProjectId.Value}_{a.UserId}"))
                .ToList();

            if (invalidAlerts.Any())
            {
                _context.Alerts.RemoveRange(invalidAlerts);
            }

            await _context.SaveChangesAsync();
        }

        private async System.Threading.Tasks.Task EnsureOverdueAlertsAsync()
        {
            await CheckOverdueTasksAsync();
            await CheckOverdueProjectsAsync();
        }

        private static string GetAlertTypeName(int alertType)
        {
            return alertType switch
            {
                1 => "任务超期",
                2 => "项目超期",
                3 => "进度滞后",
                _ => "未知"
            };
        }

        private static int GetAlertStatus(Alert alert)
        {
            if (alert.AlertType == 1 && alert.Task != null && alert.Task.Status == 2)
            {
                return 1;
            }

            if (alert.AlertType == 2 && alert.Project != null && alert.Project.Status == 2)
            {
                return 1;
            }

            return 0;
        }

        private static string GetAlertStatusName(Alert alert)
        {
            return GetAlertStatus(alert) == 1 ? "已完成" : "超期中";
        }

        private static List<string> ExtractDefaultAssigneeNames(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return new List<string>();
            }

            var line = description
                .Split('\n')
                .Select(item => item.Trim())
                .FirstOrDefault(item => item.StartsWith("默认负责人：", StringComparison.Ordinal));

            if (string.IsNullOrWhiteSpace(line))
            {
                return new List<string>();
            }

            return line.Replace("默认负责人：", string.Empty)
                .Split(new[] { '、', '，', ',', ';', '；' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();
        }

        private static string? BuildTaskAssigneeDisplay(TaskEntity? task, Dictionary<string, string> userDisplayNameMap)
        {
            if (task == null)
            {
                return null;
            }

            var names = ExtractDefaultAssigneeNames(task.Description)
                .Select(username => userDisplayNameMap.TryGetValue(username, out var displayName) ? displayName : username)
                .Distinct()
                .ToList();

            if (task.Assignee != null)
            {
                var primaryAssigneeName = GetDisplayName(task.Assignee);
                if (!names.Contains(primaryAssigneeName))
                {
                    names.Insert(0, primaryAssigneeName);
                }
            }

            return names.Any() ? string.Join("、", names) : null;
        }

        private static string? ExtractProjectOverdueReasonFromMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            const string startMarker = "；超期原因：";
            const string endMarker = "，项目负责人：";

            var startIndex = message.IndexOf(startMarker, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return null;
            }

            startIndex += startMarker.Length;
            var endIndex = message.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                endIndex = message.Length;
            }

            var reason = message.Substring(startIndex, endIndex - startIndex).Trim();
            return string.IsNullOrWhiteSpace(reason) ? null : reason;
        }

        private static string UpsertProjectOverdueReasonInMessage(string message, string? overdueReason)
        {
            const string startMarker = "；超期原因：";
            const string endMarker = "，项目负责人：";

            var result = message;
            var startIndex = result.IndexOf(startMarker, StringComparison.Ordinal);
            if (startIndex >= 0)
            {
                var endIndex = result.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
                if (endIndex < 0)
                {
                    endIndex = result.Length;
                }

                result = result.Remove(startIndex, endIndex - startIndex);
            }

            var normalizedReason = string.IsNullOrWhiteSpace(overdueReason) ? null : overdueReason.Trim();
            if (string.IsNullOrWhiteSpace(normalizedReason))
            {
                return result;
            }

            var managerIndex = result.IndexOf(endMarker, StringComparison.Ordinal);
            if (managerIndex < 0)
            {
                return $"{result}；超期原因：{normalizedReason}";
            }

            return result.Insert(managerIndex, $"；超期原因：{normalizedReason}");
        }

        private static string GetDisplayName(User user)
        {
            return !string.IsNullOrWhiteSpace(user.RealName) ? user.RealName! : user.Username;
        }

        private async Task<List<int>> ResolveTaskAssigneeIdsAsync(TaskEntity task)
        {
            var names = ExtractDefaultAssigneeNames(task.Description);
            var ids = names.Any()
                ? await _context.Users
                    .AsNoTracking()
                    .Where(u => names.Contains(u.Username))
                    .Select(u => u.Id)
                    .ToListAsync()
                : new List<int>();

            if (task.AssigneeId.HasValue && !ids.Contains(task.AssigneeId.Value))
            {
                ids.Add(task.AssigneeId.Value);
            }

            return ids.Distinct().ToList();
        }
    }
}
