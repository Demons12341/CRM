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
        private readonly IPermissionService _permissionService;

        public AlertService(ApplicationDbContext context, IPermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }

        public async Task<PaginatedResult<AlertDto>> GetAlertsAsync(int userId, int page, int pageSize, int? alertType, bool? isRead, int? alertStatus)
        {
            await EnsureOverdueAlertsAsync();

            // 获取隐藏的共享文件夹项目ID（一次性查询）
            var hiddenProjectId = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Name == SharedFolderProjectName)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();

            // 构建基础查询 - 使用Select投影，避免Include
            var query = _context.Alerts
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => new AlertQueryDto
                {
                    Id = a.Id,
                    ProjectId = a.ProjectId,
                    ProjectManagerId = a.Project != null ? a.Project.ManagerId : null,
                    ProjectName = a.Project != null ? a.Project.Name : null,
                    ProjectManagerRealName = a.Project != null && a.Project.Manager != null ? a.Project.Manager.RealName : null,
                    ProjectManagerUsername = a.Project != null && a.Project.Manager != null ? a.Project.Manager.Username : null,
                    ProjectStatus = a.Project != null ? (int?)a.Project.Status : null,
                    TaskId = a.TaskId,
                    TaskTitle = a.Task != null ? a.Task.Title : null,
                    TaskStatus = a.Task != null ? (int?)a.Task.Status : null,
                    TaskAssigneeId = a.Task != null ? a.Task.AssigneeId : null,
                    TaskAssigneeRealName = a.Task != null && a.Task.Assignee != null ? a.Task.Assignee.RealName : null,
                    TaskAssigneeUsername = a.Task != null && a.Task.Assignee != null ? a.Task.Assignee.Username : null,
                    TaskDescription = a.Task != null ? a.Task.Description : null,
                    TaskOverdueReason = a.Task != null ? a.Task.OverdueReason : null,
                    UserId = a.UserId,
                    AlertType = a.AlertType,
                    Message = a.Message,
                    IsRead = a.IsRead,
                    CreatedAt = a.CreatedAt
                });

            // 应用隐藏项目过滤
            if (hiddenProjectId.HasValue)
            {
                query = query.Where(a => !a.ProjectId.HasValue || a.ProjectId.Value != hiddenProjectId.Value);
            }

            // 应用告警类型过滤
            if (alertType.HasValue)
            {
                query = query.Where(a => a.AlertType == alertType.Value);
            }

            // 应用已读状态过滤
            if (isRead.HasValue)
            {
                query = query.Where(a => a.IsRead == isRead.Value);
            }

            // 应用告警状态过滤（简化后的条件）
            if (alertStatus.HasValue)
            {
                // alertStatus: 1=已处理(完成), 0=未处理
                // 任务超期(AlertType=1)且任务状态=2(已完成) 或 项目超期(AlertType=2)且项目状态=2(已完成)
                if (alertStatus.Value == 1)
                {
                    query = query.Where(a =>
                        (a.AlertType == 1 && a.TaskStatus == 2) ||
                        (a.AlertType == 2 && a.ProjectStatus == 10));
                }
                else
                {
                    query = query.Where(a =>
                        (a.AlertType == 1 && a.TaskStatus != 2) ||
                        (a.AlertType == 2 && a.ProjectStatus != 10) ||
                        a.AlertType == 3);
                }
            }

            // 计算总数
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            if (totalCount == 0)
            {
                return new PaginatedResult<AlertDto>
                {
                    Items = new List<AlertDto>(),
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = 0
                };
            }

            // 分页查询 - 只取需要的数据
            var alertDtos = await query
                .OrderBy(a => a.AlertType == 2 ? 0 : (a.AlertType == 1 ? 1 : 2))
                .ThenByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 提取任务描述中的默认负责人用户名（用于后续查询）
            var assigneeUsernames = alertDtos
                .Where(a => !string.IsNullOrEmpty(a.TaskDescription))
                .SelectMany(a => ExtractDefaultAssigneeNames(a.TaskDescription))
                .Distinct()
                .ToList();

            // 一次性查询所有需要的用户信息（避免N+1问题）
            var userDisplayNameMap = assigneeUsernames.Count > 0
                ? await _context.Users
                    .AsNoTracking()
                    .Where(u => assigneeUsernames.Contains(u.Username))
                    .Select(u => new { u.Username, DisplayName = !string.IsNullOrWhiteSpace(u.RealName) ? u.RealName : u.Username })
                    .ToDictionaryAsync(u => u.Username, u => u.DisplayName)
                : new Dictionary<string, string>();

            // 组装最终DTO
            var alerts = alertDtos.Select(a => new AlertDto
            {
                Id = a.Id,
                ProjectId = a.ProjectId,
                ProjectManagerId = a.ProjectManagerId,
                ProjectName = a.ProjectName,
                ProjectManagerName = GetDisplayNameFromFields(a.ProjectManagerRealName, a.ProjectManagerUsername),
                TaskId = a.TaskId,
                TaskName = a.TaskTitle,
                TaskAssigneeName = BuildTaskAssigneeDisplay(a.TaskDescription, a.TaskAssigneeRealName, a.TaskAssigneeUsername, userDisplayNameMap),
                UserId = a.UserId,
                AlertType = a.AlertType,
                AlertTypeName = GetAlertTypeName(a.AlertType),
                AlertStatus = GetAlertStatusFromDto(a),
                AlertStatusName = GetAlertStatusNameFromDto(a),
                OverdueReason = a.AlertType == 1
                    ? a.TaskOverdueReason
                    : (a.AlertType == 2 ? ExtractProjectOverdueReasonFromMessage(a.Message) : null),
                Message = a.Message,
                IsRead = a.IsRead,
                CreatedAt = a.CreatedAt
            }).ToList();

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
            // 使用EXISTS（Any）替代LEFT JOIN，提高索引命中率
            // 生成SQL: SELECT COUNT(*) FROM Alerts WHERE UserId=@uid AND IsRead=0 AND ...
            // EXISTS条件会生成子查询，避免JOIN带来的性能损耗
            return await _context.Alerts
                .AsNoTracking()
                .Where(a => a.UserId == userId && !a.IsRead)
                .Where(a =>
                    // 非共享文件夹项目（通过Project.Name判断）
                    a.ProjectId == null ||
                    _context.Projects.Any(p => p.Id == a.ProjectId && p.Name != SharedFolderProjectName))
                .Where(a =>
                    // 任务超期告警(AlertType=1)时，任务状态未完成(Status!=2)
                    a.AlertType != 1 ||
                    _context.Tasks.Any(t => t.Id == a.TaskId && t.Status != 2))
                .Where(a =>
                    // 项目超期告警(AlertType=2)时，项目状态未完成(Status!=10)
                    a.AlertType != 2 ||
                    _context.Projects.Any(p => p.Id == a.ProjectId && p.Status != 10))
                .CountAsync();
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
            // 使用ExecuteUpdate进行批量更新，更高效
            await _context.Alerts
                .Where(a => a.UserId == userId && !a.IsRead)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.IsRead, true));

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
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            var isAdmin = _permissionService.HasPermission(currentUser, "alert.edit_all");

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
                var isOverdueActive = project.Status != 10
                && project.EndDate!.Value.Date < today;

                var isCompletedOverdue = project.Status == 10
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

        private static int GetAlertStatusFromDto(AlertQueryDto alert)
        {
            if (alert.AlertType == 1 && alert.TaskStatus == 2)
            {
                return 1;
            }

            if (alert.AlertType == 2 && alert.ProjectStatus == 10)
            {
                return 1;
            }

            return 0;
        }

        private static string GetAlertStatusNameFromDto(AlertQueryDto alert)
        {
            return GetAlertStatusFromDto(alert) == 1 ? "已完成" : "超期中";
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

        private static string? BuildTaskAssigneeDisplay(string? taskDescription, string? assigneeRealName, string? assigneeUsername, Dictionary<string, string> userDisplayNameMap)
        {
            if (string.IsNullOrEmpty(taskDescription))
            {
                // 没有描述，直接返回主负责人
                return GetDisplayNameFromFields(assigneeRealName, assigneeUsername);
            }

            var names = ExtractDefaultAssigneeNames(taskDescription)
            .Select(username => userDisplayNameMap.TryGetValue(username, out var displayName) ? displayName : username)
            .Distinct()
            .ToList();

            var primaryAssigneeName = GetDisplayNameFromFields(assigneeRealName, assigneeUsername);
            if (!string.IsNullOrEmpty(primaryAssigneeName) && !names.Contains(primaryAssigneeName))
            {
                names.Insert(0, primaryAssigneeName);
            }

            return names.Any() ? string.Join("、", names) : null;
        }

        private static string? GetDisplayNameFromFields(string? realName, string? username)
        {
            if (!string.IsNullOrWhiteSpace(realName))
                return realName;
            if (!string.IsNullOrWhiteSpace(username))
                return username;
            return null;
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
