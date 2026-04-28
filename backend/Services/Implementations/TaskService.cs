using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using ProjectManagementSystem.Common;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Models.Entities;
using ProjectManagementSystem.Services.Interfaces;
using System.Text.Json;
using TaskEntity = ProjectManagementSystem.Models.Entities.Task;

namespace ProjectManagementSystem.Services.Implementations
{
    public class TaskService : ITaskService
    {
        private const string SharedFolderProjectName = "共享文件夹";
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IProcessTemplateService _processTemplateService;

        public TaskService(ApplicationDbContext context, IWebHostEnvironment environment, IProcessTemplateService processTemplateService)
        {
            _context = context;
            _environment = environment;
            _processTemplateService = processTemplateService;
        }

        public async Task<PaginatedResult<TaskDto>> GetTasksAsync(TaskListRequest request, int currentUserId)
        {
            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);

            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            var query = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.Assignee)
                .Include(t => t.Milestone)
                .AsQueryable();

            query = query.Where(t => t.Project.Name != SharedFolderProjectName);

            var overdueOnly = request.OverdueOnly == true;

            if (currentUser.Role.Name != "管理员")
            {
                query = query.Where(t =>
                    t.Project.ManagerId == currentUserId ||
                    _context.ProjectMembers.Any(pm => pm.ProjectId == t.ProjectId && pm.UserId == currentUserId));
            }

            if (overdueOnly)
            {
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value < AppTime.Today && t.Status != 2 && t.Status != 3);
            }

            if (request.ProjectId.HasValue)
            {
                if (currentUser.Role.Name != "管理员")
                {
                    var hasProjectAccess = await _context.Projects
                        .AsNoTracking()
                        .AnyAsync(p => p.Id == request.ProjectId.Value &&
                            (p.ManagerId == currentUserId || _context.ProjectMembers.Any(pm => pm.ProjectId == p.Id && pm.UserId == currentUserId)));

                    if (!hasProjectAccess)
                    {
                        throw new UnauthorizedAccessException("暂无该项目访问权限");
                    }
                }

                query = query.Where(t => t.ProjectId == request.ProjectId.Value);
            }

            if (request.AssigneeId.HasValue)
            {
                query = query.Where(t => t.AssigneeId == request.AssigneeId.Value);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(t => t.Status == request.Status.Value);
            }

            if (request.MyOpenScope == true)
            {
                query = query.Where(t =>
                    (t.AssigneeId.HasValue && t.AssigneeId.Value == currentUserId)
                    || (t.Project.ManagerId == currentUserId && t.Status != 2 && t.Status != 3));
            }

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                query = query.Where(t => t.Title.Contains(request.Keyword) || (t.Description != null && t.Description.Contains(request.Keyword)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

            var normalizedSortBy = (request.SortBy ?? string.Empty).Trim().ToLowerInvariant();
            var orderedQuery = normalizedSortBy switch
            {
                "urgency" or "priority" => query
                    .OrderBy(t => t.Status == 2 || t.Status == 3 ? 1 : 0)
                    .ThenBy(t => (t.Status == 0 || t.Status == 1) && t.DueDate.HasValue ? 0 : 1)
                    .ThenBy(t => (t.Status == 0 || t.Status == 1) && t.DueDate.HasValue ? t.DueDate : DateTime.MaxValue)
                    .ThenByDescending(t => t.Priority)
                    .ThenBy(t => t.Id),
                _ => query
                    .OrderBy(t => t.Id)
            };

            var tasks = await orderedQuery
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    ProjectId = t.ProjectId,
                    ProjectManagerId = t.Project.ManagerId,
                    ProjectName = t.Project.Name,
                    Title = t.Title,
                    Description = t.Description,
                    AssigneeId = t.AssigneeId,
                    AssigneeName = t.Assignee != null ? (!string.IsNullOrWhiteSpace(t.Assignee.RealName) ? t.Assignee.RealName : t.Assignee.Username) : null,
                    AssigneeDisplay = null,
                    MilestoneId = t.MilestoneId,
                    MilestoneName = t.Milestone != null ? t.Milestone.Name : null,
                    Priority = t.Priority,
                    PriorityName = GetPriorityName(t.Priority),
                    Status = t.Status,
                    StatusName = GetStatusName(t.Status),
                    StartDate = t.StartDate,
                    DueDate = t.DueDate,
                    CompletedAt = t.CompletedAt,
                    Progress = t.Progress,
                    OverdueReason = t.OverdueReason,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    IsOverdue = t.DueDate.HasValue && t.DueDate.Value < AppTime.Now && t.Status != 2
                })
                .ToListAsync();

            var assigneeNames = tasks
                .SelectMany(task => ExtractDefaultAssigneeNames(task.Description))
                .Distinct()
                .ToList();

            var assigneeUserMap = new Dictionary<string, (int Id, string DisplayName)>();
            if (assigneeNames.Any())
            {
                var assigneeUsers = await _context.Users
                    .AsNoTracking()
                    .Where(u => assigneeNames.Contains(u.Username))
                    .Select(u => new { u.Id, u.Username, u.RealName })
                    .ToListAsync();

                assigneeUserMap = assigneeUsers.ToDictionary(
                    u => u.Username,
                    u => (u.Id, !string.IsNullOrWhiteSpace(u.RealName) ? u.RealName! : u.Username));
            }

            foreach (var task in tasks)
            {
                var collaboratorNames = ExtractDefaultAssigneeNames(task.Description);
                var ids = collaboratorNames
                    .Where(name => assigneeUserMap.ContainsKey(name))
                    .Select(name => assigneeUserMap[name].Id)
                    .ToList();

                var collaboratorDisplayNames = collaboratorNames
                    .Select(name => assigneeUserMap.TryGetValue(name, out var assignee) ? assignee.DisplayName : name)
                    .Distinct()
                    .ToList();

                if (task.AssigneeId.HasValue && !ids.Contains(task.AssigneeId.Value))
                {
                    ids.Insert(0, task.AssigneeId.Value);
                }

                task.AssigneeIds = ids.Distinct().ToList();
                task.AssigneeDisplay = BuildAssigneeDisplay(task.AssigneeName, collaboratorDisplayNames);
            }

            return new PaginatedResult<TaskDto>
            {
                Items = tasks,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = totalPages
            };
        }

        public async Task<TaskDto> GetTaskByIdAsync(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.Assignee)
                .Include(t => t.Milestone)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                throw new KeyNotFoundException("任务不存在");
            }

            var dto = new TaskDto
            {
                Id = task.Id,
                ProjectId = task.ProjectId,
                ProjectManagerId = task.Project.ManagerId,
                ProjectName = task.Project.Name,
                Title = task.Title,
                Description = task.Description,
                AssigneeId = task.AssigneeId,
                AssigneeName = task.Assignee != null ? GetDisplayName(task.Assignee) : null,
                AssigneeDisplay = null,
                AssigneeIds = await ResolveTaskAssigneeIdsAsync(task),
                MilestoneId = task.MilestoneId,
                MilestoneName = task.Milestone?.Name,
                Priority = task.Priority,
                PriorityName = GetPriorityName(task.Priority),
                Status = task.Status,
                StatusName = GetStatusName(task.Status),
                StartDate = task.StartDate,
                DueDate = task.DueDate,
                CompletedAt = task.CompletedAt,
                Progress = task.Progress,
                OverdueReason = task.OverdueReason,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                IsOverdue = task.DueDate.HasValue && task.DueDate.Value < AppTime.Now && task.Status != 2
            };

            var collaboratorNames = ExtractDefaultAssigneeNames(task.Description);
            var collaboratorDisplayMap = new Dictionary<string, string>();
            if (collaboratorNames.Any())
            {
                var collaboratorUsers = await _context.Users
                    .AsNoTracking()
                    .Where(u => collaboratorNames.Contains(u.Username))
                    .Select(u => new { u.Username, u.RealName })
                    .ToListAsync();

                collaboratorDisplayMap = collaboratorUsers.ToDictionary(
                    u => u.Username,
                    u => !string.IsNullOrWhiteSpace(u.RealName) ? u.RealName! : u.Username);
            }

            dto.AssigneeDisplay = BuildAssigneeDisplay(
                dto.AssigneeName,
                collaboratorNames.Select(name => collaboratorDisplayMap.TryGetValue(name, out var displayName) ? displayName : name).ToList());

            return dto;
        }

        public async Task<TaskDto> CreateTaskAsync(CreateTaskRequest request, int userId)
        {
            ValidateDateRange(request.StartDate, request.DueDate, "任务预计开始时间", "任务预计截止时间");

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
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

            if (project == null)
            {
                throw new KeyNotFoundException("项目不存在");
            }

            if (currentUser.Role.Name != "管理员" && project.ManagerId != userId)
            {
                var isProjectMember = await _context.ProjectMembers
                    .AsNoTracking()
                    .AnyAsync(pm => pm.ProjectId == request.ProjectId && pm.UserId == userId);

                if (!isProjectMember)
                {
                    throw new UnauthorizedAccessException("暂无该项目访问权限");
                }
            }

            var assigneeIds = NormalizeAssigneeIds(request.AssigneeIds, request.AssigneeId);
            var assigneeNames = assigneeIds.Any()
                ? await GetActiveUserNamesByIdsAsync(assigneeIds)
                : new List<string>();
            var primaryAssigneeId = assigneeIds.FirstOrDefault();
            var initialStatus = request.StartDate.HasValue && request.StartDate.Value.Date < AppTime.Today ? 1 : 0;

            var task = new TaskEntity
            {
                ProjectId = request.ProjectId,
                Title = request.Title,
                Description = UpsertDefaultAssigneeLine(request.Description, assigneeNames),
                AssigneeId = primaryAssigneeId == 0 ? null : primaryAssigneeId,
                MilestoneId = request.MilestoneId,
                Priority = project.Priority,
                Status = initialStatus,
                StartDate = request.StartDate,
                DueDate = request.DueDate,
                Progress = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            await SyncProjectStatusByTasksAsync(task.ProjectId);

            // 记录任务日志
            await AddTaskLogAsync(task.Id, userId, "创建任务", null, task.Title);

            return await GetTaskByIdAsync(task.Id);
        }

        public async Task<TaskDto> UpdateTaskAsync(int id, UpdateTaskRequest request, int userId)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.Assignee)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (task == null)
            {
                throw new KeyNotFoundException("任务不存在");
            }

            var nextStartDate = request.StartDate ?? task.StartDate;
            var nextDueDate = request.DueDate ?? task.DueDate;
            ValidateDateRange(nextStartDate, nextDueDate, "任务预计开始时间", "任务预计截止时间");

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            var hasGeneralEdit = request.Title != null
                || request.Description != null
                || request.AssigneeIds != null
                || request.AssigneeId.HasValue
                || request.MilestoneId.HasValue
                || request.Priority.HasValue
                || request.Status.HasValue
                || request.StartDate.HasValue
                || request.DueDate.HasValue
                || request.Progress.HasValue;
            var isAdmin = currentUser.Role.Name == "管理员";
            var isProjectManagerOfTask = task.Project.ManagerId == userId;
            var isTaskAssignee = task.AssigneeId.HasValue && task.AssigneeId.Value == userId;
            var canEditTask = isAdmin || isProjectManagerOfTask || isTaskAssignee;

            if (hasGeneralEdit && !canEditTask)
            {
                throw new UnauthorizedAccessException("项目成员仅可修改自己负责的任务");
            }

            var shouldAdvanceNextTask = false;
            var shouldAdjustNextTaskSchedule = false;
            var oldValues = new List<string>();
            var newValues = new List<string>();

            void TrackChange(string field, string? oldValue, string? newValue)
            {
                oldValues.Add($"{field}：{(string.IsNullOrWhiteSpace(oldValue) ? "-" : oldValue)}");
                newValues.Add($"{field}：{(string.IsNullOrWhiteSpace(newValue) ? "-" : newValue)}");
            }

            if (request.Title != null)
            {
                if (!string.Equals(task.Title, request.Title, StringComparison.Ordinal))
                {
                    TrackChange("标题", task.Title, request.Title);
                }
                task.Title = request.Title;
            }

            if (request.Description != null)
            {
                var newDescription = request.Description;

                if (request.AssigneeIds == null && !request.AssigneeId.HasValue)
                {
                    var currentAssigneeIds = await ResolveTaskAssigneeIdsAsync(task);
                    var currentAssigneeNames = currentAssigneeIds.Any()
                        ? await GetActiveUserNamesByIdsAsync(currentAssigneeIds)
                        : new List<string>();
                    newDescription = UpsertDefaultAssigneeLine(newDescription, currentAssigneeNames);
                }

                if (!string.Equals(task.Description, newDescription, StringComparison.Ordinal))
                {
                    TrackChange("描述", task.Description, newDescription);
                }

                task.Description = newDescription;
            }

            if (request.AssigneeIds != null)
            {
                var assigneeIds = NormalizeAssigneeIds(request.AssigneeIds, request.AssigneeId);
                var assigneeNames = assigneeIds.Any()
                    ? await GetActiveUserNamesByIdsAsync(assigneeIds)
                    : new List<string>();

                var oldAssignee = await BuildAssigneeDisplayForTaskAsync(task.AssigneeId, task.Description);
                var assigneeDisplayNames = assigneeIds.Any()
                    ? await GetActiveUserDisplayNamesByIdsAsync(assigneeIds)
                    : new List<string>();
                var newAssignee = assigneeDisplayNames.Any() ? string.Join("、", assigneeDisplayNames) : null;
                if (!string.Equals(oldAssignee, newAssignee, StringComparison.Ordinal))
                {
                    TrackChange("责任人", oldAssignee, newAssignee);
                }

                var primaryAssigneeId = assigneeIds.FirstOrDefault();
                task.AssigneeId = primaryAssigneeId == 0 ? null : primaryAssigneeId;
                task.Description = UpsertDefaultAssigneeLine(task.Description, assigneeNames);
            }
            else if (request.AssigneeId.HasValue)
            {
                if (!await _context.Users.AnyAsync(u => u.Id == request.AssigneeId.Value && u.IsActive))
                {
                    throw new KeyNotFoundException("责任人不存在或已禁用");
                }

                var oldAssignee = task.AssigneeId.HasValue
                    ? GetDisplayName(await _context.Users.AsNoTracking().FirstAsync(u => u.Id == task.AssigneeId.Value))
                    : null;
                var newAssignee = GetDisplayName(await _context.Users.AsNoTracking().FirstAsync(u => u.Id == request.AssigneeId.Value));
                if (!string.Equals(oldAssignee, newAssignee, StringComparison.Ordinal))
                {
                    TrackChange("责任人", oldAssignee, newAssignee);
                }
                task.AssigneeId = request.AssigneeId.Value;
                task.Description = UpsertDefaultAssigneeLine(task.Description, string.IsNullOrWhiteSpace(newAssignee) ? new List<string>() : new List<string> { newAssignee });
            }

            if (request.MilestoneId.HasValue)
            {
                if (task.MilestoneId != request.MilestoneId.Value)
                {
                    TrackChange("里程碑", task.MilestoneId?.ToString(), request.MilestoneId.Value.ToString());
                }
                task.MilestoneId = request.MilestoneId.Value;
            }

            if (request.Priority.HasValue)
            {
                if (task.Priority != request.Priority.Value)
                {
                    TrackChange("优先级", GetPriorityName(task.Priority), GetPriorityName(request.Priority.Value));
                    task.Priority = request.Priority.Value;
                }
            }

            if (request.Status.HasValue)
            {
                if (task.Status != request.Status.Value)
                {
                    if (request.Status.Value == 2)
                    {
                        await EnsureTaskHasWorkSubmissionAsync(task.Id);
                    }

                    TrackChange("状态", GetStatusName(task.Status), GetStatusName(request.Status.Value));
                    task.Status = request.Status.Value;

                    if (request.Status.Value == 2)
                    {
                        task.CompletedAt = DateTime.UtcNow;
                        shouldAdvanceNextTask = true;
                        shouldAdjustNextTaskSchedule = false;
                    }
                    else if (task.CompletedAt.HasValue)
                    {
                        task.CompletedAt = null;
                    }
                }
            }

            if (request.StartDate.HasValue)
            {
                if (task.StartDate?.Date != request.StartDate.Value.Date)
                {
                    TrackChange("预计开始时间", task.StartDate?.ToString("yyyy-MM-dd"), request.StartDate.Value.ToString("yyyy-MM-dd"));
                    task.StartDate = request.StartDate.Value;
                }
            }

            if (request.DueDate.HasValue)
            {
                if (task.DueDate?.Date != request.DueDate.Value.Date)
                {
                    TrackChange("预计截止时间", task.DueDate?.ToString("yyyy-MM-dd"), request.DueDate.Value.ToString("yyyy-MM-dd"));
                    shouldAdvanceNextTask = true;
                    shouldAdjustNextTaskSchedule = true;
                    task.DueDate = request.DueDate.Value;
                }
            }

            if (request.Progress.HasValue)
            {
                if (task.Progress != request.Progress.Value)
                {
                    TrackChange("进度", task.Progress.ToString(), request.Progress.Value.ToString());
                    task.Progress = request.Progress.Value;
                }
            }

            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (oldValues.Any())
            {
                await AddTaskLogAsync(id, userId, "编辑任务", string.Join("；", oldValues), string.Join("；", newValues));
            }

            if (shouldAdvanceNextTask)
            {
                await ActivateNextTaskAsync(task, userId, shouldAdjustNextTaskSchedule);
            }

            await SyncProjectStatusByTasksAsync(task.ProjectId);

            return await GetTaskByIdAsync(id);
        }

        public async Task<TaskDto> ClaimTaskAsync(int id, int userId, DateTime? dueDate)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.Assignee)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                throw new KeyNotFoundException("任务不存在");
            }

            if (task.Status == 2 || task.Status == 3)
            {
                throw new InvalidOperationException("已完成或已取消任务不可认领");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            var projectMember = await _context.ProjectMembers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(pm => pm.ProjectId == task.ProjectId && pm.UserId == userId);

            if (projectMember == null)
            {
                _context.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = task.ProjectId,
                    UserId = userId,
                    Role = "成员",
                    JoinedAt = DateTime.UtcNow,
                    IsDeleted = false
                });
            }
            else if (projectMember.IsDeleted)
            {
                projectMember.IsDeleted = false;
                if (string.IsNullOrWhiteSpace(projectMember.Role))
                {
                    projectMember.Role = "成员";
                }
                projectMember.JoinedAt = DateTime.UtcNow;
            }

            var oldAssigneeDisplay = await BuildAssigneeDisplayForTaskAsync(task.AssigneeId, task.Description);
            var isFirstClaim = !task.AssigneeId.HasValue;

            task.AssigneeId = userId;

            if (isFirstClaim)
            {
                var startDate = AppTime.Today;
                if (!dueDate.HasValue)
                {
                    throw new InvalidOperationException("首次认领任务请先填写预计截止日期");
                }

                ValidateDateRange(startDate, dueDate.Value.Date, "任务预计开始时间", "任务预计截止时间");
                task.StartDate = startDate;
                task.DueDate = dueDate.Value.Date;

                if (task.Status != 1)
                {
                    await AddTaskLogAsync(id, userId, "更新状态", GetStatusName(task.Status), GetStatusName(1));
                    task.Status = 1;
                }
            }

            var assigneeNames = ExtractDefaultAssigneeNames(task.Description);
            if (!string.IsNullOrWhiteSpace(currentUser.Username) && !assigneeNames.Contains(currentUser.Username))
            {
                assigneeNames.Insert(0, currentUser.Username);
            }

            task.Description = UpsertDefaultAssigneeLine(task.Description, assigneeNames);
            var newAssigneeDisplay = await BuildAssigneeDisplayForTaskAsync(task.AssigneeId, task.Description);

            await AddTaskLogAsync(id, userId, "认领任务", oldAssigneeDisplay, newAssigneeDisplay);

            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await SyncProjectStatusByTasksAsync(task.ProjectId);

            return await GetTaskByIdAsync(id);
        }

        public async Task<bool> DeleteTaskAsync(int id)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            if (task == null)
            {
                throw new KeyNotFoundException("任务不存在");
            }

            task.IsDeleted = true;
            task.UpdatedAt = DateTime.UtcNow;

            var taskLogs = await _context.TaskLogs.Where(log => log.TaskId == id).ToListAsync();
            foreach (var log in taskLogs)
            {
                log.IsDeleted = true;
            }

            var alerts = await _context.Alerts.Where(a => a.TaskId == id).ToListAsync();
            foreach (var alert in alerts)
            {
                alert.IsDeleted = true;
            }

            await _context.SaveChangesAsync();

            await SyncProjectStatusByTasksAsync(task.ProjectId);

            return true;
        }

        public async Task<TaskDto> UpdateTaskProgressAsync(int id, UpdateTaskProgressRequest request, int userId)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            if (task == null)
            {
                throw new KeyNotFoundException("任务不存在");
            }

            await AddTaskLogAsync(id, userId, "更新进度", task.Progress.ToString(), request.Progress.ToString());
            task.Progress = request.Progress;

            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await GetTaskByIdAsync(id);
        }

        public async Task<TaskDto> UpdateTaskStatusAsync(int id, UpdateTaskStatusRequest request, int userId)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (task == null)
            {
                throw new KeyNotFoundException("任务不存在");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            var canUpdateStatus = currentUser.Role.Name == "管理员"
                || task.Project.ManagerId == userId
                || IsTaskAssignee(task, currentUser);

            if (!canUpdateStatus)
            {
                throw new UnauthorizedAccessException("项目成员仅可修改自己负责的任务");
            }

            if (request.Status == 2 && task.Status != 2)
            {
                await EnsureTaskHasWorkSubmissionAsync(id);
            }

            var shouldAdvanceNextTask = false;

            await AddTaskLogAsync(id, userId, "更新状态", GetStatusName(task.Status), GetStatusName(request.Status));
            task.Status = request.Status;
            if (request.Status == 2)
            {
                task.CompletedAt = DateTime.UtcNow;
                shouldAdvanceNextTask = true;
            }
            else if (task.CompletedAt.HasValue)
            {
                task.CompletedAt = null;
            }
            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (shouldAdvanceNextTask)
            {
                await ActivateNextTaskAsync(task, userId, false);
            }

            await SyncProjectStatusByTasksAsync(task.ProjectId);

            return await GetTaskByIdAsync(id);
        }

        public async Task<List<TaskLogDto>> GetTaskLogsAsync(int taskId)
        {
            return await _context.TaskLogs
                .Include(tl => tl.User)
                .Where(tl => tl.TaskId == taskId)
                .OrderByDescending(tl => tl.CreatedAt)
                .Select(tl => new TaskLogDto
                {
                    Id = tl.Id,
                    TaskId = tl.TaskId,
                    UserId = tl.UserId,
                    Username = !string.IsNullOrWhiteSpace(tl.User.RealName) ? tl.User.RealName : tl.User.Username,
                    Action = tl.Action,
                    OldValue = tl.OldValue,
                    NewValue = tl.NewValue,
                    CreatedAt = tl.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<int> ImportMicrogridTemplateAsync(int projectId, int userId, int? defaultAssigneeId, int? templateId)
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

            if (currentUser.Role.Name != "管理员" && project.ManagerId != userId)
            {
                var isProjectMember = await _context.ProjectMembers
                    .AsNoTracking()
                    .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);

                if (!isProjectMember)
                {
                    throw new UnauthorizedAccessException("暂无该项目访问权限");
                }
            }

            if (defaultAssigneeId.HasValue && !await _context.Users.AnyAsync(u => u.Id == defaultAssigneeId.Value))
            {
                throw new KeyNotFoundException("默认责任人不存在");
            }

            if (await _context.Tasks.AnyAsync(t => t.ProjectId == projectId))
            {
                throw new InvalidOperationException("该项目已有任务，暂不支持重复导入模板");
            }

            if (templateId.HasValue && templateId.Value > 0)
            {
                var importedCount = await _processTemplateService.ApplyDefaultTemplateToProjectAsync(projectId, defaultAssigneeId, userId, templateId.Value);
                if (importedCount <= 0)
                {
                    throw new InvalidOperationException("所选模板没有可导入步骤");
                }

                return importedCount;
            }

            var now = AppTime.Now;
            var baseDate = project.StartDate?.Date ?? now.Date;
            var templateTasks = await LoadMicrogridTemplateTasksAsync();
            var entities = templateTasks.Select((template, index) => new TaskEntity
            {
                ProjectId = projectId,
                Title = template.Title,
                Description = template.Description,
                AssigneeId = defaultAssigneeId,
                Priority = project.Priority,
                Status = 0,
                StartDate = baseDate.AddDays(template.StartOffsetDays),
                DueDate = baseDate.AddDays(template.DueOffsetDays),
                Progress = 0,
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();

            _context.Tasks.AddRange(entities);
            await _context.SaveChangesAsync();

            foreach (var task in entities)
            {
                await AddTaskLogAsync(task.Id, userId, "导入标准工序", null, task.Title);
            }

            return entities.Count;
        }

        public async Task<int> ExportMicrogridTemplateAsync(int projectId, int userId, string? templateName)
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

            if (currentUser.Role.Name != "管理员" && project.ManagerId != userId)
            {
                var isProjectMember = await _context.ProjectMembers
                    .AsNoTracking()
                    .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);

                if (!isProjectMember)
                {
                    throw new UnauthorizedAccessException("暂无该项目访问权限");
                }
            }

            var tasks = await _context.Tasks
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .OrderBy(t => t.StartDate ?? DateTime.MaxValue)
                .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                .ThenBy(t => t.CreatedAt)
                .ThenBy(t => t.Id)
                .ToListAsync();

            if (!tasks.Any())
            {
                throw new InvalidOperationException("当前项目暂无任务，无法导出标准工序");
            }

            var baseDate = tasks
                .Where(t => t.StartDate.HasValue)
                .Select(t => t.StartDate!.Value.Date)
                .DefaultIfEmpty(AppTime.Today)
                .Min();

            var templateTasks = tasks.Select(task =>
            {
                var startDate = task.StartDate?.Date ?? baseDate;
                var dueDate = task.DueDate?.Date ?? startDate.AddDays(1);
                if (dueDate < startDate)
                {
                    dueDate = startDate;
                }

                var startOffset = (int)(startDate - baseDate).TotalDays;
                var dueOffset = (int)(dueDate - baseDate).TotalDays;

                if (startOffset < 0)
                {
                    startOffset = 0;
                }

                if (dueOffset < startOffset)
                {
                    dueOffset = startOffset;
                }

                return new MicrogridTemplateTaskDefinition
                {
                    Title = task.Title,
                    Description = task.Description,
                    Priority = task.Priority,
                    StartOffsetDays = startOffset,
                    DueOffsetDays = dueOffset
                };
            }).ToList();

            var normalizedTemplateName = templateName?.Trim();
            var processTemplateName = string.IsNullOrWhiteSpace(normalizedTemplateName)
                ? $"{project.Name}-导出工序-{AppTime.Now:yyyyMMddHHmmss}"
                : normalizedTemplateName;
            var processTemplateRequest = new CreateProcessTemplateRequest
            {
                Name = processTemplateName,
                Description = $"由项目“{project.Name}”任务导出，基准日期：{baseDate:yyyy-MM-dd}",
                IsDefault = false,
                Steps = tasks.Select((task, index) =>
                {
                    var startDate = task.StartDate?.Date ?? baseDate;
                    var dueDate = task.DueDate?.Date ?? startDate;
                    var estimatedDays = (dueDate - startDate).Days;
                    if (estimatedDays <= 0)
                    {
                        estimatedDays = 1;
                    }

                    var priority = task.Priority;
                    if (priority < 1 || priority > 4)
                    {
                        priority = 2;
                    }

                    return new ProcessStepRequest
                    {
                        SortOrder = index + 1,
                        Stage = "导出工序",
                        Name = task.Title,
                        Description = task.Description,
                        Priority = priority,
                        DefaultAssigneeId = null,
                        DefaultAssigneeIds = new List<int>(),
                        EstimatedDays = estimatedDays
                    };
                }).ToList()
            };

            await _processTemplateService.CreateTemplateAsync(processTemplateRequest);

            await SaveMicrogridTemplateTasksAsync(templateTasks);

            foreach (var task in tasks)
            {
                await AddTaskLogAsync(task.Id, userId, "导出标准工序", null, task.Title);
            }

            return templateTasks.Count;
        }

        public async Task<TaskDto> SubmitTaskWorkAsync(int id, SubmitTaskWorkRequest request, int userId)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (task == null)
            {
                throw new KeyNotFoundException("任务不存在");
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            var canSubmitWork = currentUser.Role.Name == "管理员"
                || task.Project.ManagerId == userId
                || IsTaskAssignee(task, currentUser);

            if (!canSubmitWork)
            {
                throw new UnauthorizedAccessException("仅项目负责人或任务责任人可提交工作记录");
            }

            var validAssigneeIds = await ResolveTaskAssigneeIdsAsync(task);
            var validAssigneeSet = validAssigneeIds.ToHashSet();
            string? nextAssigneesDisplay = null;

            if (request.NextAssigneeIds != null && request.NextAssigneeIds.Count > 0)
            {
                if (!validAssigneeIds.Any())
                {
                    throw new InvalidOperationException("当前工序未配置负责人，请先在项目任务模板中配置负责人");
                }

                if (request.NextAssigneeIds.Any(id => !validAssigneeSet.Contains(id)))
                {
                    throw new InvalidOperationException("下一步任务负责人必须从当前工序负责人中选择");
                }

                var validAssigneeMap = await _context.Users
                    .AsNoTracking()
                    .Where(u => validAssigneeIds.Contains(u.Id))
                    .Select(u => new
                    {
                        AssigneeId = u.Id,
                        AssigneeName = !string.IsNullOrWhiteSpace(u.RealName) ? u.RealName : u.Username
                    })
                    .ToListAsync();

                var selectedAssigneeNames = validAssigneeMap
                    .Where(x => request.NextAssigneeIds.Contains(x.AssigneeId))
                    .Select(x => x.AssigneeName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                nextAssigneesDisplay = selectedAssigneeNames.Any()
                    ? string.Join("、", selectedAssigneeNames)
                    : string.Join("、", request.NextAssigneeIds);
            }

            var workSummary = $"完成内容：{request.WorkContent}";
            if (!string.IsNullOrWhiteSpace(request.Deliverables))
            {
                workSummary += $"\n交付物：{request.Deliverables}";
            }
            if (!string.IsNullOrWhiteSpace(request.Blockers))
            {
                workSummary += $"\n阻塞问题：{request.Blockers}";
            }
            if (!string.IsNullOrWhiteSpace(request.NextPlan))
            {
                workSummary += $"\n下一步：{request.NextPlan}";
            }
            if (!string.IsNullOrWhiteSpace(nextAssigneesDisplay))
            {
                workSummary += $"\n下一步负责人：{nextAssigneesDisplay}";
            }

            await AddTaskLogAsync(id, userId, "工作提交", null, workSummary);
            if (!string.IsNullOrWhiteSpace(nextAssigneesDisplay))
            {
                await AddTaskLogAsync(id, userId, "设置下一步负责人", null, nextAssigneesDisplay);
            }

            if (task.Status == 0)
            {
                task.Status = 1;
            }

            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await SyncProjectStatusByTasksAsync(task.ProjectId);

            return await GetTaskByIdAsync(id);
        }

        public async Task<TaskDto> ReviewTaskWorkAsync(int id, ReviewTaskWorkRequest request, int userId)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            if (task == null)
            {
                throw new KeyNotFoundException("任务不存在");
            }

            var shouldAdvanceNextTask = false;

            if (request.Approved)
            {
                await AddTaskLogAsync(id, userId, "验收通过", null, string.IsNullOrWhiteSpace(request.Comment) ? "已通过" : request.Comment);

                if (request.MarkAsCompleted)
                {
                    await AddTaskLogAsync(id, userId, "更新状态", GetStatusName(task.Status), GetStatusName(2));
                    task.Status = 2;
                    task.CompletedAt = DateTime.UtcNow;
                    shouldAdvanceNextTask = true;
                }
            }
            else
            {
                await AddTaskLogAsync(id, userId, "验收驳回", null, string.IsNullOrWhiteSpace(request.Comment) ? "请补充完善后再提交" : request.Comment);
                if (task.Status == 2)
                {
                    await AddTaskLogAsync(id, userId, "更新状态", GetStatusName(task.Status), GetStatusName(1));
                    task.Status = 1;
                    task.CompletedAt = null;
                }
            }

            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (shouldAdvanceNextTask)
            {
                await ActivateNextTaskAsync(task, userId, false);
            }

            await SyncProjectStatusByTasksAsync(task.ProjectId);

            return await GetTaskByIdAsync(id);
        }

        private async System.Threading.Tasks.Task SyncProjectStatusByTasksAsync(int projectId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null || project.Status == 3)
            {
                return;
            }

            var taskStatuses = await _context.Tasks
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .Select(t => t.Status)
                .ToListAsync();

            if (!taskStatuses.Any())
            {
                return;
            }

            var hasStarted = taskStatuses.Any(status => status == 1 || status == 2);
            var allCompleted = taskStatuses.All(status => status == 2);

            var targetStatus = allCompleted
                ? 2
                : (hasStarted ? 1 : 0);

            if (project.Status == targetStatus)
            {
                return;
            }

            project.Status = targetStatus;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private async System.Threading.Tasks.Task ActivateNextTaskAsync(TaskEntity currentTask, int operatorUserId, bool adjustSchedule)
        {
            var orderedTasks = await _context.Tasks
                .Where(t => t.ProjectId == currentTask.ProjectId)
                .OrderBy(t => t.StartDate ?? DateTime.MaxValue)
                .ThenBy(t => t.CreatedAt)
                .ThenBy(t => t.Id)
                .ToListAsync();

            var currentIndex = orderedTasks.FindIndex(t => t.Id == currentTask.Id);
            if (currentIndex < 0)
            {
                return;
            }

            var remainingTasks = orderedTasks
                .Skip(currentIndex + 1)
                .Where(t => t.Status != 2 && t.Status != 3)
                .ToList();

            var nextTask = remainingTasks.FirstOrDefault();
            if (nextTask == null)
            {
                return;
            }

            var lastNextAssigneeLog = await _context.TaskLogs
                .Where(l => l.TaskId == currentTask.Id && l.Action == "设置下一步负责人")
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync();

            var nextAssigneeNames = ParseAssigneeNames(lastNextAssigneeLog?.NewValue);
            if (nextAssigneeNames.Any())
            {
                var users = await _context.Users
                    .AsNoTracking()
                    .Where(u => nextAssigneeNames.Contains(u.Username) && u.IsActive)
                    .ToListAsync();

                var orderedUserNames = nextAssigneeNames
                    .Where(name => users.Any(u => u.Username == name))
                    .Distinct()
                    .ToList();

                if (orderedUserNames.Any())
                {
                    var primaryUser = users.First(u => u.Username == orderedUserNames[0]);
                    var oldAssignee = nextTask.AssigneeId.HasValue
                        ? (await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == nextTask.AssigneeId.Value))?.Username
                        : null;

                    if (nextTask.AssigneeId != primaryUser.Id)
                    {
                        nextTask.AssigneeId = primaryUser.Id;
                        await AddTaskLogAsync(nextTask.Id, operatorUserId, "更新责任人", oldAssignee, primaryUser.Username);
                    }

                    nextTask.Description = UpsertDefaultAssigneeLine(nextTask.Description, orderedUserNames);
                }
            }

            if (adjustSchedule)
            {
                var previousEndDate = currentTask.CompletedAt?.Date
                    ?? currentTask.DueDate?.Date
                    ?? AppTime.Today;

                var dateChanges = new List<(int taskId, string oldSummary, string newSummary)>();
                var chainStart = previousEndDate;

                foreach (var task in remainingTasks)
                {
                    if (task.Status == 1)
                    {
                        chainStart = task.DueDate?.Date ?? chainStart;
                        continue;
                    }

                    var durationDays = 1;
                    if (task.StartDate.HasValue && task.DueDate.HasValue)
                    {
                        var spanDays = (task.DueDate.Value.Date - task.StartDate.Value.Date).Days;
                        durationDays = spanDays <= 0 ? 1 : spanDays;
                    }

                    var oldStartDate = task.StartDate?.ToString("yyyy-MM-dd");
                    var oldDueDate = task.DueDate?.ToString("yyyy-MM-dd");

                    task.StartDate = chainStart;
                    task.DueDate = chainStart.AddDays(durationDays);
                    task.UpdatedAt = DateTime.UtcNow;

                    var newStartDate = task.StartDate?.ToString("yyyy-MM-dd");
                    var newDueDate = task.DueDate?.ToString("yyyy-MM-dd");
                    if (oldStartDate != newStartDate || oldDueDate != newDueDate)
                    {
                        dateChanges.Add((
                            task.Id,
                            $"预计开始时间：{oldStartDate ?? "-"}；预计截止时间：{oldDueDate ?? "-"}",
                            $"预计开始时间：{newStartDate ?? "-"}；预计截止时间：{newDueDate ?? "-"}"));
                    }

                    chainStart = task.DueDate?.Date ?? chainStart;
                }

                await _context.SaveChangesAsync();

                foreach (var change in dateChanges)
                {
                    await AddTaskLogAsync(change.taskId, operatorUserId, "动态排期调整", change.oldSummary, change.newSummary);
                }

                return;
            }

            await _context.SaveChangesAsync();
        }

        private async System.Threading.Tasks.Task<List<MicrogridTemplateTaskDefinition>> LoadMicrogridTemplateTasksAsync()
        {
            var filePath = GetMicrogridTemplateFilePath();
            if (!System.IO.File.Exists(filePath))
            {
                return GetDefaultMicrogridTemplateTasks();
            }

            try
            {
                var json = await System.IO.File.ReadAllTextAsync(filePath);
                var list = JsonSerializer.Deserialize<List<MicrogridTemplateTaskDefinition>>(json);
                if (list == null || list.Count == 0)
                {
                    return GetDefaultMicrogridTemplateTasks();
                }

                return list
                    .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                    .Select(item => new MicrogridTemplateTaskDefinition
                    {
                        Title = item.Title.Trim(),
                        Description = item.Description,
                        Priority = item.Priority,
                        StartOffsetDays = item.StartOffsetDays,
                        DueOffsetDays = item.DueOffsetDays
                    })
                    .ToList();
            }
            catch
            {
                return GetDefaultMicrogridTemplateTasks();
            }
        }

        private async System.Threading.Tasks.Task SaveMicrogridTemplateTasksAsync(List<MicrogridTemplateTaskDefinition> tasks)
        {
            var filePath = GetMicrogridTemplateFilePath();
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await System.IO.File.WriteAllTextAsync(filePath, json);
        }

        private string GetMicrogridTemplateFilePath()
        {
            return Path.Combine(_environment.ContentRootPath, "Data", "microgrid-template.json");
        }

        private static List<MicrogridTemplateTaskDefinition> GetDefaultMicrogridTemplateTasks()
        {
            return new List<MicrogridTemplateTaskDefinition>
            {
                new() { Title = "售前需求澄清", Description = "梳理业主目标、边界条件与关键约束", Priority = 3, StartOffsetDays = 0, DueOffsetDays = 3 },
                new() { Title = "现场踏勘与负荷调研", Description = "采集负荷曲线、用能结构与接入条件", Priority = 4, StartOffsetDays = 1, DueOffsetDays = 7 },
                new() { Title = "方案初设与容量配置", Description = "完成光储柴/充电负荷的容量组合建议", Priority = 4, StartOffsetDays = 4, DueOffsetDays = 12 },
                new() { Title = "经济性测算与投资回报", Description = "形成CAPEX/OPEX与收益测算模型", Priority = 3, StartOffsetDays = 6, DueOffsetDays = 14 },
                new() { Title = "投标技术文件编制", Description = "输出技术方案、实施计划和交付清单", Priority = 3, StartOffsetDays = 10, DueOffsetDays = 18 },
                new() { Title = "合同技术条款确认", Description = "确认范围、接口、验收标准和违约条款", Priority = 4, StartOffsetDays = 15, DueOffsetDays = 20 },
                new() { Title = "项目启动与里程碑发布", Description = "建立RACI、风险台账、主计划", Priority = 3, StartOffsetDays = 20, DueOffsetDays = 23 },
                new() { Title = "初步设计评审", Description = "完成一次系统、通讯架构和保护方案评审", Priority = 4, StartOffsetDays = 22, DueOffsetDays = 30 },
                new() { Title = "施工图与BOM冻结", Description = "冻结施工图纸、材料清单和采购需求", Priority = 4, StartOffsetDays = 28, DueOffsetDays = 38 },
                new() { Title = "关键设备采购下单", Description = "完成核心设备选型、下单与交付计划", Priority = 4, StartOffsetDays = 35, DueOffsetDays = 45 },
                new() { Title = "工厂FAT测试", Description = "组织储能PCS、EMS等关键设备工厂验收", Priority = 3, StartOffsetDays = 42, DueOffsetDays = 50 },
                new() { Title = "现场土建与基础施工", Description = "完成设备基础、线缆通道和防雷接地", Priority = 3, StartOffsetDays = 45, DueOffsetDays = 58 },
                new() { Title = "设备安装与接线", Description = "完成一次设备、二次设备安装与接线", Priority = 4, StartOffsetDays = 55, DueOffsetDays = 68 },
                new() { Title = "系统联调与保护定值", Description = "完成EMS联调、保护整定与故障演练", Priority = 4, StartOffsetDays = 66, DueOffsetDays = 75 },
                new() { Title = "并网/并离网切换测试", Description = "验证并网、孤岛、黑启动等关键场景", Priority = 4, StartOffsetDays = 72, DueOffsetDays = 80 },
                new() { Title = "试运行与性能考核", Description = "按合同指标完成试运行及性能验证", Priority = 4, StartOffsetDays = 78, DueOffsetDays = 90 },
                new() { Title = "问题整改闭环", Description = "汇总缺陷清单并完成整改销项", Priority = 3, StartOffsetDays = 86, DueOffsetDays = 94 },
                new() { Title = "竣工资料与培训移交", Description = "提交竣工文档并完成业主运维培训", Priority = 3, StartOffsetDays = 92, DueOffsetDays = 98 },
                new() { Title = "最终验收与结算", Description = "完成最终验收签字、结算与回款节点", Priority = 4, StartOffsetDays = 96, DueOffsetDays = 103 },
                new() { Title = "项目复盘与运维交接", Description = "沉淀经验教训并转入运维阶段", Priority = 2, StartOffsetDays = 102, DueOffsetDays = 108 }
            };
        }

        private sealed class MicrogridTemplateTaskDefinition
        {
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
            public int Priority { get; set; } = 1;
            public int StartOffsetDays { get; set; }
            public int DueOffsetDays { get; set; }
        }

        private async System.Threading.Tasks.Task AddTaskLogAsync(int taskId, int userId, string action, string? oldValue, string? newValue)
        {
            var taskLog = new TaskLog
            {
                TaskId = taskId,
                UserId = userId,
                Action = action,
                OldValue = oldValue,
                NewValue = newValue,
                CreatedAt = DateTime.UtcNow
            };

            _context.TaskLogs.Add(taskLog);
            await _context.SaveChangesAsync();
        }

        private async System.Threading.Tasks.Task EnsureTaskHasWorkSubmissionAsync(int taskId)
        {
            var hasWorkSubmission = await _context.TaskLogs
                .AsNoTracking()
                .AnyAsync(log => log.TaskId == taskId && log.Action == "工作提交" && !string.IsNullOrWhiteSpace(log.NewValue));

            if (!hasWorkSubmission)
            {
                throw new InvalidOperationException("请先至少提交一次工作内容后再完成任务");
            }
        }

        private static string GetPriorityName(int priority)
        {
            return priority switch
            {
                1 => "低",
                2 => "中",
                3 => "高",
                4 => "紧急",
                _ => "未知"
            };
        }

        private static string GetStatusName(int status)
        {
            return status switch
            {
                0 => "待办",
                1 => "进行中",
                2 => "已完成",
                3 => "已取消",
                _ => "未知"
            };
        }

        private static string? BuildAssigneeDisplay(string? primaryAssigneeName, List<string> collaboratorNames)
        {
            if (!collaboratorNames.Any())
            {
                return primaryAssigneeName;
            }

            if (!string.IsNullOrWhiteSpace(primaryAssigneeName) && !collaboratorNames.Contains(primaryAssigneeName))
            {
                collaboratorNames.Insert(0, primaryAssigneeName);
            }

            return string.Join("、", collaboratorNames);
        }

        private async Task<string?> BuildAssigneeDisplayForTaskAsync(int? primaryAssigneeId, string? description)
        {
            string? primaryAssigneeName = null;
            if (primaryAssigneeId.HasValue)
            {
                var primaryAssignee = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == primaryAssigneeId.Value);
                if (primaryAssignee != null)
                {
                    primaryAssigneeName = GetDisplayName(primaryAssignee);
                }
            }

            var collaboratorNames = ExtractDefaultAssigneeNames(description);
            if (!collaboratorNames.Any())
            {
                return primaryAssigneeName;
            }

            var collaboratorUsers = await _context.Users
                .AsNoTracking()
                .Where(u => collaboratorNames.Contains(u.Username))
                .ToListAsync();

            var displayNameMap = collaboratorUsers.ToDictionary(u => u.Username, GetDisplayName);
            var collaboratorDisplayNames = collaboratorNames
                .Select(name => displayNameMap.TryGetValue(name, out var displayName) ? displayName : name)
                .Distinct()
                .ToList();

            return BuildAssigneeDisplay(primaryAssigneeName, collaboratorDisplayNames);
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

        private static List<string> ParseAssigneeNames(string? namesText)
        {
            if (string.IsNullOrWhiteSpace(namesText))
            {
                return new List<string>();
            }

            return namesText
                .Split(new[] { '、', '，', ',', ';', '；' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();
        }

        private static bool IsTaskAssignee(TaskEntity task, User currentUser)
        {
            if (task.AssigneeId.HasValue && task.AssigneeId.Value == currentUser.Id)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(currentUser.Username))
            {
                return false;
            }

            return ExtractDefaultAssigneeNames(task.Description)
                .Contains(currentUser.Username);
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

        private static List<int> NormalizeAssigneeIds(List<int>? assigneeIds, int? assigneeId)
        {
            var ids = assigneeIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!ids.Any() && assigneeId.HasValue && assigneeId.Value > 0)
            {
                ids.Add(assigneeId.Value);
            }

            return ids;
        }

        private async Task<List<string>> GetActiveUserNamesByIdsAsync(List<int> assigneeIds)
        {
            var users = await _context.Users
                .AsNoTracking()
                .Where(u => assigneeIds.Contains(u.Id) && u.IsActive)
                .ToListAsync();

            if (users.Count != assigneeIds.Count)
            {
                throw new KeyNotFoundException("责任人不存在或已禁用");
            }

            return assigneeIds
                .Select(id => users.First(u => u.Id == id).Username)
                .Distinct()
                .ToList();
        }

        private async Task<List<string>> GetActiveUserDisplayNamesByIdsAsync(List<int> assigneeIds)
        {
            var users = await _context.Users
                .AsNoTracking()
                .Where(u => assigneeIds.Contains(u.Id) && u.IsActive)
                .ToListAsync();

            if (users.Count != assigneeIds.Count)
            {
                throw new KeyNotFoundException("责任人不存在或已禁用");
            }

            return assigneeIds
                .Select(id => GetDisplayName(users.First(u => u.Id == id)))
                .Distinct()
                .ToList();
        }

        private static string GetDisplayName(User user)
        {
            return !string.IsNullOrWhiteSpace(user.RealName) ? user.RealName! : user.Username;
        }

        private static string? UpsertDefaultAssigneeLine(string? description, List<string> assigneeNames)
        {
            var keptLines = (description ?? string.Empty)
                .Replace("\r\n", "\n")
                .Split('\n', StringSplitOptions.None)
                .Select(line => line.TrimEnd())
                .Where(line => !line.StartsWith("默认负责人：", StringComparison.Ordinal))
                .ToList();

            while (keptLines.Any() && string.IsNullOrWhiteSpace(keptLines.Last()))
            {
                keptLines.RemoveAt(keptLines.Count - 1);
            }

            var cleaned = string.Join("\n", keptLines).Trim();
            var uniqueNames = assigneeNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToList();

            if (!uniqueNames.Any())
            {
                return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
            }

            var line = $"默认负责人：{string.Join("、", uniqueNames)}";
            return string.IsNullOrWhiteSpace(cleaned) ? line : $"{cleaned}\n{line}";
        }

        private static void ValidateDateRange(DateTime? startDate, DateTime? endDate, string startLabel, string endLabel)
        {
            if (startDate.HasValue && endDate.HasValue && endDate.Value.Date < startDate.Value.Date)
            {
                throw new InvalidOperationException($"{endLabel}不能早于{startLabel}");
            }
        }
    }
}
