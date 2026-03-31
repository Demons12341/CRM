using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Models.Entities;
using ProjectManagementSystem.Services.Interfaces;
using TaskEntity = ProjectManagementSystem.Models.Entities.Task;

namespace ProjectManagementSystem.Services.Implementations
{
    public class TaskService : ITaskService
    {
        private const string SharedFolderProjectName = "共享文件夹";
        private readonly ApplicationDbContext _context;

        public TaskService(ApplicationDbContext context)
        {
            _context = context;
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
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow.Date && t.Status != 2 && t.Status != 3);
            }

            if (request.ProjectId.HasValue)
            {
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

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                query = query.Where(t => t.Title.Contains(request.Keyword) || (t.Description != null && t.Description.Contains(request.Keyword)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
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
                    Priority = t.Project.Priority,
                    PriorityName = GetPriorityName(t.Project.Priority),
                    Status = t.Status,
                    StatusName = GetStatusName(t.Status),
                    StartDate = t.StartDate,
                    DueDate = t.DueDate,
                    CompletedAt = t.CompletedAt,
                    Progress = t.Progress,
                    OverdueReason = t.OverdueReason,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    IsOverdue = t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Status != 2
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
                Priority = task.Project.Priority,
                PriorityName = GetPriorityName(task.Project.Priority),
                Status = task.Status,
                StatusName = GetStatusName(task.Status),
                StartDate = task.StartDate,
                DueDate = task.DueDate,
                CompletedAt = task.CompletedAt,
                Progress = task.Progress,
                OverdueReason = task.OverdueReason,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                IsOverdue = task.DueDate.HasValue && task.DueDate.Value < DateTime.UtcNow && task.Status != 2
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

            var project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId);

            if (project == null)
            {
                throw new KeyNotFoundException("项目不存在");
            }

            var assigneeIds = NormalizeAssigneeIds(request.AssigneeIds, request.AssigneeId);
            var assigneeNames = assigneeIds.Any()
                ? await GetActiveUserNamesByIdsAsync(assigneeIds)
                : new List<string>();
            var primaryAssigneeId = assigneeIds.FirstOrDefault();

            var task = new TaskEntity
            {
                ProjectId = request.ProjectId,
                Title = request.Title,
                Description = UpsertDefaultAssigneeLine(request.Description, assigneeNames),
                AssigneeId = primaryAssigneeId == 0 ? null : primaryAssigneeId,
                MilestoneId = request.MilestoneId,
                Priority = project.Priority,
                Status = 0,
                StartDate = request.StartDate,
                DueDate = request.DueDate,
                Progress = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

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

            return await GetTaskByIdAsync(id);
        }

        public async Task<TaskDto> ClaimTaskAsync(int id, int userId)
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

            var isAdmin = currentUser.Role.Name == "管理员";
            var isProjectManager = task.Project.ManagerId == userId;
            var isProjectMember = await _context.ProjectMembers
                .AsNoTracking()
                .AnyAsync(pm => pm.ProjectId == task.ProjectId && pm.UserId == userId);

            if (!isAdmin && !isProjectManager && !isProjectMember)
            {
                throw new UnauthorizedAccessException("仅项目负责人或项目成员可认领任务");
            }

            var oldAssigneeDisplay = await BuildAssigneeDisplayForTaskAsync(task.AssigneeId, task.Description);

            task.AssigneeId = userId;

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

        public async Task<int> ImportMicrogridTemplateAsync(int projectId, int userId, int? defaultAssigneeId)
        {
            var project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                throw new KeyNotFoundException("项目不存在");
            }

            if (defaultAssigneeId.HasValue && !await _context.Users.AnyAsync(u => u.Id == defaultAssigneeId.Value))
            {
                throw new KeyNotFoundException("默认责任人不存在");
            }

            if (await _context.Tasks.AnyAsync(t => t.ProjectId == projectId))
            {
                throw new InvalidOperationException("该项目已有任务，暂不支持重复导入模板");
            }

            var now = DateTime.UtcNow;
            var baseDate = project.StartDate?.Date ?? now.Date;
            var templateTasks = GetMicrogridTemplateTasks();
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

            return await GetTaskByIdAsync(id);
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
                    ?? DateTime.UtcNow.Date;

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

        private static List<(string Title, string Description, int Priority, int StartOffsetDays, int DueOffsetDays)> GetMicrogridTemplateTasks()
        {
            return new List<(string, string, int, int, int)>
            {
                ("售前需求澄清", "梳理业主目标、边界条件与关键约束", 3, 0, 3),
                ("现场踏勘与负荷调研", "采集负荷曲线、用能结构与接入条件", 4, 1, 7),
                ("方案初设与容量配置", "完成光储柴/充电负荷的容量组合建议", 4, 4, 12),
                ("经济性测算与投资回报", "形成CAPEX/OPEX与收益测算模型", 3, 6, 14),
                ("投标技术文件编制", "输出技术方案、实施计划和交付清单", 3, 10, 18),
                ("合同技术条款确认", "确认范围、接口、验收标准和违约条款", 4, 15, 20),
                ("项目启动与里程碑发布", "建立RACI、风险台账、主计划", 3, 20, 23),
                ("初步设计评审", "完成一次系统、通讯架构和保护方案评审", 4, 22, 30),
                ("施工图与BOM冻结", "冻结施工图纸、材料清单和采购需求", 4, 28, 38),
                ("关键设备采购下单", "完成核心设备选型、下单与交付计划", 4, 35, 45),
                ("工厂FAT测试", "组织储能PCS、EMS等关键设备工厂验收", 3, 42, 50),
                ("现场土建与基础施工", "完成设备基础、线缆通道和防雷接地", 3, 45, 58),
                ("设备安装与接线", "完成一次设备、二次设备安装与接线", 4, 55, 68),
                ("系统联调与保护定值", "完成EMS联调、保护整定与故障演练", 4, 66, 75),
                ("并网/并离网切换测试", "验证并网、孤岛、黑启动等关键场景", 4, 72, 80),
                ("试运行与性能考核", "按合同指标完成试运行及性能验证", 4, 78, 90),
                ("问题整改闭环", "汇总缺陷清单并完成整改销项", 3, 86, 94),
                ("竣工资料与培训移交", "提交竣工文档并完成业主运维培训", 3, 92, 98),
                ("最终验收与结算", "完成最终验收签字、结算与回款节点", 4, 96, 103),
                ("项目复盘与运维交接", "沉淀经验教训并转入运维阶段", 2, 102, 108)
            };
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
