using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.Common;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Services.Interfaces;
using TaskEntity = ProjectManagementSystem.Models.Entities.Task;

namespace ProjectManagementSystem.Services.Implementations
{
    public class DashboardService : IDashboardService
    {
        private const string SharedFolderProjectName = "共享文件夹";
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissionService;

        public DashboardService(ApplicationDbContext context, IPermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }

        public async Task<DashboardOverviewDto> GetOverviewAsync(int userId)
        {
            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                return new DashboardOverviewDto();
            }

            IQueryable<Models.Entities.Project> projectQuery = _context.Projects.Where(p => p.Name != SharedFolderProjectName);
            IQueryable<TaskEntity> taskQuery = _context.Tasks.Where(t => t.Project.Name != SharedFolderProjectName);

            if (_permissionService.HasPermission(currentUser, "dashboard.view_all"))
            {
            }
            else
            {
                var scopedProjectIds = await _context.Projects
                    .AsNoTracking()
                    .Where(p => p.ManagerId == userId || p.Members.Any(pm => pm.UserId == userId))
                    .Select(p => p.Id)
                    .Distinct()
                    .ToListAsync();

                projectQuery = projectQuery.Where(p => scopedProjectIds.Contains(p.Id));
                taskQuery = taskQuery.Where(t => scopedProjectIds.Contains(t.ProjectId));
            }

            var today = AppTime.Today;

            var totalProjects = await projectQuery.CountAsync();
            var activeProjects = await projectQuery.CountAsync(p => p.Status != 10);
            var totalTasks = await taskQuery.CountAsync();
            var projectStatusCounts = await projectQuery
                .GroupBy(p => p.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(item => item.Status, item => item.Count);
            var taskStatusCounts = await taskQuery
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(item => item.Status, item => item.Count);

            var pendingContractProjects = await projectQuery
                .Where(p => p.Status == 2)
                .OrderByDescending(p => p.UpdatedAt)
                .Select(p => new PendingContractProjectDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    BusinessLine = p.BusinessLine,
                    ManagerName = !string.IsNullOrWhiteSpace(p.Manager.RealName) ? p.Manager.RealName! : p.Manager.Username,
                    Budget = p.Budget,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();

            var threeDaysLater = today.AddDays(3);

            var progressStart = today.AddDays(-30);

            var progressProjects = await projectQuery
                .Where(p => p.StatusChangedAt.HasValue
                    && p.StatusChangedAt.Value >= progressStart
                    && p.Status > 0)
                .OrderByDescending(p => p.StatusChangedAt)
                .Select(p => new ProgressProjectDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    BusinessLine = p.BusinessLine,
                    Status = p.Status,
                    StatusName = GetProjectStatusName(p.Status),
                    StatusChangedAt = p.StatusChangedAt,
                    ManagerName = !string.IsNullOrWhiteSpace(p.Manager.RealName) ? p.Manager.RealName! : p.Manager.Username
                })
                .ToListAsync();

            var upcomingOverdueTasks = await taskQuery
                .Include(t => t.Project)
                .Include(t => t.Assignee)
                .Where(t => t.DueDate.HasValue
                    && t.Status != 2 && t.Status != 3
                    && t.DueDate.Value.Date >= today
                    && t.DueDate.Value.Date <= threeDaysLater)
                .OrderBy(t => t.DueDate)
                .ToListAsync();

            var allCollaboratorUsernames = upcomingOverdueTasks
                .SelectMany(t => ParseAssigneeNames(t.Description))
                .Distinct()
                .ToList();

            var usernameToDisplay = new Dictionary<string, string>();
            if (allCollaboratorUsernames.Any())
            {
                var users = await _context.Users
                    .AsNoTracking()
                    .Where(u => allCollaboratorUsernames.Contains(u.Username))
                    .ToListAsync();

                foreach (var u in users)
                {
                    var displayName = !string.IsNullOrWhiteSpace(u.RealName) ? u.RealName! : u.Username;
                    usernameToDisplay[u.Username] = displayName;
                }
            }

            var upcomingOverdueTaskDtos = upcomingOverdueTasks.Select(t => new UpcomingOverdueItemDto
            {
                Id = t.Id,
                Title = t.Title,
                ProjectName = t.Project.Name,
                ProjectId = t.ProjectId,
                ItemType = "任务",
                DueDate = t.DueDate,
                DaysLeft = CalcDaysLeft(t.DueDate!.Value, today),
                IsOverdue = t.DueDate!.Value.Date < today,
                AssigneeName = BuildAssigneeDisplayWithLookup(
                    t.Assignee != null
                        ? (!string.IsNullOrWhiteSpace(t.Assignee.RealName) ? t.Assignee.RealName! : t.Assignee.Username)
                        : "",
                    t.Description,
                    usernameToDisplay)
            }).ToList();

            var upcomingOverdueProjects = await projectQuery
                .Include(p => p.Manager)
                .Where(p => p.EndDate.HasValue
                    && p.Status != 10
                    && p.EndDate.Value.Date >= today
                    && p.EndDate.Value.Date <= threeDaysLater)
                .OrderBy(p => p.EndDate)
                .Select(p => new UpcomingOverdueItemDto
                {
                    Id = p.Id,
                    Title = p.Name,
                    ProjectName = p.Name,
                    ProjectId = p.Id,
                    ItemType = "项目",
                    DueDate = p.EndDate,
                    DaysLeft = CalcDaysLeft(p.EndDate!.Value, today),
                    IsOverdue = p.EndDate!.Value.Date < today,
                    AssigneeName = !string.IsNullOrWhiteSpace(p.Manager.RealName) ? p.Manager.RealName! : p.Manager.Username
                })
                .ToListAsync();

            var allUpcomingOverdue = upcomingOverdueTaskDtos
                .Concat(upcomingOverdueProjects)
                .OrderBy(x => x.IsOverdue ? 0 : 1)
                .ThenBy(x => x.DueDate)
                .Take(10)
                .ToList();

            return new DashboardOverviewDto
            {
                TotalProjects = totalProjects,
                ActiveProjects = activeProjects,
                TotalTasks = totalTasks,
                ProjectStatusCounts = projectStatusCounts,
                TaskStatusCounts = taskStatusCounts,
                PendingContractProjects = pendingContractProjects,
                ProgressProjects = progressProjects,
                UpcomingOverdueItems = allUpcomingOverdue
            };
        }

        public async Task<List<TaskDto>> GetMyTasksAsync(int userId)
        {
            var currentUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (currentUser == null)
            {
                return new List<TaskDto>();
            }

            var tasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.Assignee)
                .Include(t => t.Milestone)
                .Where(t =>
                    t.Status != 2 &&
                    t.Status != 3 &&
                    (t.AssigneeId == userId || (t.Description != null && EF.Functions.Like(t.Description, "%默认负责人：%"))))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var taskCollaboratorUsernames = tasks
                .SelectMany(t => ParseAssigneeNames(t.Description))
                .Distinct()
                .ToList();

            var taskUsernameToDisplay = new Dictionary<string, string>();
            if (taskCollaboratorUsernames.Any())
            {
                var collabUsers = await _context.Users
                    .AsNoTracking()
                    .Where(u => taskCollaboratorUsernames.Contains(u.Username))
                    .ToListAsync();

                foreach (var u in collabUsers)
                {
                    taskUsernameToDisplay[u.Username] = !string.IsNullOrWhiteSpace(u.RealName) ? u.RealName! : u.Username;
                }
            }

            return tasks
                .Where(t => t.AssigneeId == userId || ParseAssigneeNames(t.Description).Contains(currentUser.Username))
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    ProjectId = t.ProjectId,
                    ProjectName = t.Project.Name,
                    BusinessLine = t.Project.BusinessLine,
                    Title = t.Title,
                    Description = t.Description,
                    AssigneeId = t.AssigneeId,
                    AssigneeName = t.Assignee != null
                        ? (!string.IsNullOrWhiteSpace(t.Assignee.RealName) ? t.Assignee.RealName! : t.Assignee.Username)
                        : null,
                    AssigneeDisplay = BuildAssigneeDisplayWithLookup(
                        t.Assignee != null
                            ? (!string.IsNullOrWhiteSpace(t.Assignee.RealName) ? t.Assignee.RealName! : t.Assignee.Username)
                            : null,
                        t.Description,
                        taskUsernameToDisplay),
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
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    IsOverdue = t.DueDate.HasValue && t.DueDate.Value < AppTime.Today && t.Status != 2 && t.Status != 3,
                    DaysLeft = t.DueDate.HasValue ? (int)(t.DueDate.Value.Date - AppTime.Today).Days : (int?)null
                })
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .ToList();
        }

        public async Task<List<MyProjectDto>> GetMyProjectsAsync(int userId)
        {
            var memberProjectIds = await _context.ProjectMembers
                .AsNoTracking()
                .Where(pm => pm.UserId == userId)
                .Select(pm => pm.ProjectId)
                .Distinct()
                .ToListAsync();

            var managerProjectIds = await _context.Projects
                .AsNoTracking()
                .Where(p => p.ManagerId == userId)
                .Select(p => p.Id)
                .Distinct()
                .ToListAsync();

            var allProjectIds = memberProjectIds.Union(managerProjectIds).Distinct().ToList();

            var projects = await _context.Projects
                .AsNoTracking()
                .Include(p => p.Manager)
                .Include(p => p.Tasks)
                .Where(p => allProjectIds.Contains(p.Id)
                    && p.Name != SharedFolderProjectName)
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync();

            return projects.Select(p => new MyProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                BusinessLine = p.BusinessLine,
                Status = p.Status,
                StatusName = GetProjectStatusName(p.Status),
                Progress = p.Tasks.Count > 0
                    ? Math.Round((decimal)p.Tasks.Count(t => t.Status == 2) * 100m / p.Tasks.Count, 0)
                    : 0,
                ManagerName = !string.IsNullOrWhiteSpace(p.Manager.RealName) ? p.Manager.RealName! : p.Manager.Username,
                IsManager = p.ManagerId == userId
            }).ToList();
        }

        private static string? BuildAssigneeDisplay(string? primaryAssigneeName, string? description)
        {
            var collaboratorNames = ParseAssigneeNames(description);
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

        private static string? BuildAssigneeDisplayWithLookup(string? primaryAssigneeName, string? description, Dictionary<string, string> usernameToDisplay)
        {
            var collaboratorUsernames = ParseAssigneeNames(description);
            if (!collaboratorUsernames.Any())
            {
                return primaryAssigneeName;
            }

            var collaboratorDisplayNames = collaboratorUsernames
                .Select(u => usernameToDisplay.TryGetValue(u, out var name) ? name : u)
                .ToList();

            if (!string.IsNullOrWhiteSpace(primaryAssigneeName) && !collaboratorDisplayNames.Contains(primaryAssigneeName))
            {
                collaboratorDisplayNames.Insert(0, primaryAssigneeName);
            }

            return string.Join("、", collaboratorDisplayNames);
        }

        private static List<string> ParseAssigneeNames(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return new List<string>();
            }

            var line = description
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(item => item.Trim())
                .FirstOrDefault(item => item.StartsWith("默认负责人：", StringComparison.Ordinal));

            if (string.IsNullOrWhiteSpace(line))
            {
                return new List<string>();
            }

            return line.Replace("默认负责人：", string.Empty)
                .Split('、', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();
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

        private static string GetMilestoneStatusName(int status)
        {
            return status switch
            {
                0 => "未开始",
                1 => "进行中",
                2 => "已完成",
                _ => "未知"
            };
        }

        private static int CalcDaysLeft(DateTime dueDate, DateTime today)
        {
            return (dueDate.Date - today.Date).Days;
        }

        private static string GetProjectStatusName(int status)
        {
            return status switch
            {
                0 => "售前阶段",
                2 => "已中标，待签合同",
                3 => "需求确定阶段",
                4 => "设计阶段",
                5 => "采购生产阶段",
                6 => "装配阶段",
                7 => "测试阶段",
                8 => "已发货",
                9 => "现场调试",
                10 => "已完成",
                _ => "未知"
            };
        }
    }
}
