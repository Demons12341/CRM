using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Services.Interfaces;
using TaskEntity = ProjectManagementSystem.Models.Entities.Task;

namespace ProjectManagementSystem.Services.Implementations
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
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

            IQueryable<Models.Entities.Project> projectQuery = _context.Projects;
            IQueryable<TaskEntity> taskQuery = _context.Tasks;

            if (currentUser.Role.Name == "管理员")
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

            var totalProjects = await projectQuery.CountAsync();
            var activeProjects = await projectQuery.CountAsync(p => p.Status == 1);
            var totalTasks = await taskQuery.CountAsync();
            var overdueTasks = await taskQuery
                .CountAsync(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Status != 2 && t.Status != 3);

            return new DashboardOverviewDto
            {
                TotalProjects = totalProjects,
                ActiveProjects = activeProjects,
                TotalTasks = totalTasks,
                OverdueTasks = overdueTasks
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

            return tasks
                .Where(t => t.AssigneeId == userId || ParseAssigneeNames(t.Description).Contains(currentUser.Username))
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    ProjectId = t.ProjectId,
                    ProjectName = t.Project.Name,
                    Title = t.Title,
                    Description = t.Description,
                    AssigneeId = t.AssigneeId,
                    AssigneeName = t.Assignee != null ? t.Assignee.Username : null,
                    AssigneeDisplay = BuildAssigneeDisplay(t.Assignee != null ? t.Assignee.Username : null, t.Description),
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
                    IsOverdue = t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Status != 2
                })
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .ToList();
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
    }
}
