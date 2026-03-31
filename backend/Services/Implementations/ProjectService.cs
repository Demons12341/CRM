using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Models.Entities;
using ProjectManagementSystem.Services.Interfaces;

namespace ProjectManagementSystem.Services.Implementations
{
    public class ProjectService : IProjectService
    {
        private const string SharedFolderProjectName = "共享文件夹";
        private readonly ApplicationDbContext _context;
        private readonly IProcessTemplateService _processTemplateService;

        public ProjectService(ApplicationDbContext context, IProcessTemplateService processTemplateService)
        {
            _context = context;
            _processTemplateService = processTemplateService;
        }

        public async Task<PaginatedResult<ProjectDto>> GetProjectsAsync(ProjectListRequest request)
        {
            var query = _context.Projects
                .Include(p => p.Manager)
                .Include(p => p.Members)
                .Include(p => p.Tasks)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                query = query.Where(p => p.Name.Contains(request.Keyword) || (p.Description != null && p.Description.Contains(request.Keyword)));
            }

            if (request.Status.HasValue)
            {
                query = query.Where(p => p.Status == request.Status.Value);
            }

            if (request.ManagerId.HasValue)
            {
                query = query.Where(p => p.ManagerId == request.ManagerId.Value);
            }

            if (request.ExcludeSharedFolder)
            {
                query = query.Where(p => p.Name != SharedFolderProjectName);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

            var projects = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    ManagerId = p.ManagerId,
                    ManagerName = !string.IsNullOrWhiteSpace(p.Manager.RealName) ? p.Manager.RealName! : p.Manager.Username,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    Status = p.Status,
                    StatusName = GetStatusName(p.Status),
                    Priority = p.Priority,
                    PriorityName = GetPriorityName(p.Priority),
                    Budget = p.Budget,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    MemberCount = p.Members.Count,
                    TaskCount = p.Tasks.Count,
                    Progress = p.Tasks.Count > 0
                        ? Math.Round((decimal)p.Tasks.Count(t => t.Status == 2) * 100m / p.Tasks.Count, 2)
                        : 0
                })
                .ToListAsync();

            return new PaginatedResult<ProjectDto>
            {
                Items = projects,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = totalPages
            };
        }

        public async Task<ProjectDto> GetProjectByIdAsync(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Manager)
                .Include(p => p.Members)
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                throw new KeyNotFoundException("项目不存在");
            }

            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                ManagerId = project.ManagerId,
                ManagerName = GetDisplayName(project.Manager),
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Status = project.Status,
                StatusName = GetStatusName(project.Status),
                Priority = project.Priority,
                PriorityName = GetPriorityName(project.Priority),
                Budget = project.Budget,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt,
                MemberCount = project.Members.Count,
                TaskCount = project.Tasks.Count,
                Progress = project.Tasks.Count > 0
                    ? Math.Round((decimal)project.Tasks.Count(t => t.Status == 2) * 100m / project.Tasks.Count, 2)
                    : 0
            };
        }

        public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request)
        {
            if (!request.StartDate.HasValue || !request.EndDate.HasValue)
            {
                throw new InvalidOperationException("新建项目时开始日期和结束日期为必填项");
            }

            await EnsureProjectNameUniqueAsync(request.Name);

            await EnsureProjectManagerRoleAsync(request.ManagerId);
            ValidateDateRange(request.StartDate, request.EndDate, "项目开始时间", "项目结束时间");

            var initialStatus = request.StartDate.Value.Date <= DateTime.Today ? 1 : 0;

            var project = new Project
            {
                Name = request.Name,
                Description = request.Description,
                ManagerId = request.ManagerId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Priority = request.Priority,
                Budget = request.Budget,
                Status = initialStatus,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            var membersToAdd = new List<ProjectMember>
            {
                new ProjectMember
                {
                    ProjectId = project.Id,
                    UserId = request.ManagerId,
                    Role = "负责人",
                    JoinedAt = DateTime.UtcNow
                }
            };

            var extraMemberIds = (request.MemberIds ?? new List<int>())
                .Where(id => id > 0 && id != request.ManagerId)
                .Distinct()
                .ToList();

            if (extraMemberIds.Any())
            {
                var validMemberIds = await _context.Users
                    .AsNoTracking()
                    .Where(u => extraMemberIds.Contains(u.Id) && u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync();

                var invalidIds = extraMemberIds.Except(validMemberIds).ToList();
                if (invalidIds.Any())
                {
                    throw new InvalidOperationException($"以下成员不存在或已禁用：{string.Join(",", invalidIds)}");
                }

                membersToAdd.AddRange(validMemberIds.Select(memberId => new ProjectMember
                {
                    ProjectId = project.Id,
                    UserId = memberId,
                    Role = "成员",
                    JoinedAt = DateTime.UtcNow
                }));
            }

            _context.ProjectMembers.AddRange(membersToAdd);
            await _context.SaveChangesAsync();

            await _processTemplateService.ApplyDefaultTemplateToProjectAsync(project.Id, request.ManagerId, request.ManagerId, request.ProcessTemplateId);

            var createdProjectTasks = await _context.Tasks
                .Where(t => t.ProjectId == project.Id)
                .ToListAsync();

            foreach (var task in createdProjectTasks)
            {
                task.Priority = project.Priority;
                task.UpdatedAt = DateTime.UtcNow;
            }

            if (createdProjectTasks.Any())
            {
                await _context.SaveChangesAsync();
            }

            return await GetProjectByIdAsync(project.Id);
        }

        public async Task<ProjectDto> UpdateProjectAsync(int id, UpdateProjectRequest request)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
            {
                throw new KeyNotFoundException("项目不存在");
            }

            var nextStartDate = request.StartDate ?? project.StartDate;
            var nextEndDate = request.EndDate ?? project.EndDate;
            ValidateDateRange(nextStartDate, nextEndDate, "项目开始时间", "项目结束时间");

            if (request.Name != null)
            {
                await EnsureProjectNameUniqueAsync(request.Name, id);
                project.Name = request.Name;
            }

            if (request.Description != null)
            {
                project.Description = request.Description;
            }

            if (request.ManagerId.HasValue)
            {
                await EnsureProjectManagerRoleAsync(request.ManagerId.Value);
                project.ManagerId = request.ManagerId.Value;
            }

            if (request.StartDate.HasValue)
            {
                project.StartDate = request.StartDate.Value;
            }

            if (request.EndDate.HasValue)
            {
                project.EndDate = request.EndDate.Value;
            }

            if (request.Status.HasValue)
            {
                if (request.Status.Value == 2)
                {
                    var hasIncompleteTasks = await _context.Tasks
                        .AnyAsync(t => t.ProjectId == id && t.Status != 2);

                    if (hasIncompleteTasks)
                    {
                        throw new InvalidOperationException("项目下仍有未完成任务，不能标记为已完成");
                    }
                }

                project.Status = request.Status.Value;
            }

            if (request.Priority.HasValue)
            {
                project.Priority = request.Priority.Value;

                var projectTasks = await _context.Tasks
                    .Where(t => t.ProjectId == id)
                    .ToListAsync();

                foreach (var task in projectTasks)
                {
                    task.Priority = request.Priority.Value;
                    task.UpdatedAt = DateTime.UtcNow;
                }
            }

            if (request.Budget.HasValue)
            {
                project.Budget = request.Budget.Value;
            }

            if (request.MemberIds != null || request.ManagerId.HasValue)
            {
                await SyncProjectMembersAsync(project.Id, project.ManagerId, request.MemberIds);
            }

            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await GetProjectByIdAsync(id);
        }

        private async System.Threading.Tasks.Task EnsureProjectNameUniqueAsync(string name, int? excludeProjectId = null)
        {
            var normalizedName = name?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new InvalidOperationException("项目名称不能为空");
            }

            var query = _context.Projects.Where(p => p.Name == normalizedName);
            if (excludeProjectId.HasValue)
            {
                query = query.Where(p => p.Id != excludeProjectId.Value);
            }

            var exists = await query.AnyAsync();
            if (exists)
            {
                throw new InvalidOperationException("项目名称不能重复");
            }
        }
        public async Task<bool> DeleteProjectAsync(int id, int currentUserId)
        {
            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);

            if (currentUser == null)
            {
                throw new UnauthorizedAccessException("用户不存在或已禁用");
            }

            if (currentUser.Role.Name != "管理员")
            {
                throw new UnauthorizedAccessException("只有管理员可以删除项目");
            }

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
            {
                throw new KeyNotFoundException("项目不存在");
            }

            if (string.Equals(project.Name?.Trim(), SharedFolderProjectName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("共享文件夹项目禁止删除");
            }

            project.IsDeleted = true;
            project.UpdatedAt = DateTime.UtcNow;

            var projectTasks = await _context.Tasks.Where(t => t.ProjectId == id).ToListAsync();
            foreach (var task in projectTasks)
            {
                task.IsDeleted = true;
                task.UpdatedAt = DateTime.UtcNow;
            }

            var taskIds = projectTasks.Select(t => t.Id).ToList();
            if (taskIds.Any())
            {
                var taskLogs = await _context.TaskLogs.Where(log => taskIds.Contains(log.TaskId)).ToListAsync();
                foreach (var log in taskLogs)
                {
                    log.IsDeleted = true;
                }
            }

            var milestones = await _context.Milestones.Where(m => m.ProjectId == id).ToListAsync();
            foreach (var milestone in milestones)
            {
                milestone.IsDeleted = true;
            }

            var files = await _context.Files.Where(f => f.ProjectId == id).ToListAsync();
            foreach (var file in files)
            {
                file.IsDeleted = true;
            }

            var alerts = await _context.Alerts.Where(a => a.ProjectId == id).ToListAsync();
            foreach (var alert in alerts)
            {
                alert.IsDeleted = true;
            }

            var members = await _context.ProjectMembers.Where(pm => pm.ProjectId == id).ToListAsync();
            foreach (var member in members)
            {
                member.IsDeleted = true;
            }

            await _context.SaveChangesAsync();

            return true;
        }

        private async System.Threading.Tasks.Task SyncProjectMembersAsync(int projectId, int managerId, List<int>? memberIds)
        {
            var targetMemberIds = (memberIds ?? new List<int>())
                .Where(id => id > 0 && id != managerId)
                .Distinct()
                .ToList();

            if (targetMemberIds.Any())
            {
                var validMemberIds = await _context.Users
                    .AsNoTracking()
                    .Where(u => targetMemberIds.Contains(u.Id) && u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync();

                var invalidIds = targetMemberIds.Except(validMemberIds).ToList();
                if (invalidIds.Any())
                {
                    throw new InvalidOperationException($"以下成员不存在或已禁用：{string.Join(",", invalidIds)}");
                }
            }

            var desiredIds = targetMemberIds.Append(managerId).Distinct().ToHashSet();

            var allMembers = await _context.ProjectMembers
                .IgnoreQueryFilters()
                .Where(pm => pm.ProjectId == projectId)
                .ToListAsync();

            foreach (var pm in allMembers)
            {
                if (!desiredIds.Contains(pm.UserId))
                {
                    pm.IsDeleted = true;
                    continue;
                }

                pm.IsDeleted = false;
                pm.Role = pm.UserId == managerId ? "负责人" : "成员";
            }

            var existingUserIds = allMembers
                .Where(pm => !pm.IsDeleted)
                .Select(pm => pm.UserId)
                .ToHashSet();

            var now = DateTime.UtcNow;
            var missingIds = desiredIds.Where(userId => !existingUserIds.Contains(userId));
            foreach (var userId in missingIds)
            {
                _context.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = userId,
                    Role = userId == managerId ? "负责人" : "成员",
                    JoinedAt = now,
                    IsDeleted = false
                });
            }
        }

        public async Task<bool> CanUserAccessProjectAsync(int projectId, int userId)
        {
            var currentUser = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                return false;
            }

            if (currentUser.Role.Name == "管理员")
            {
                return await _context.Projects.AnyAsync(p => p.Id == projectId);
            }

            var isProjectManager = await _context.Projects
                .AsNoTracking()
                .AnyAsync(p => p.Id == projectId && p.ManagerId == userId);

            if (isProjectManager)
            {
                return true;
            }

            return await _context.ProjectMembers
                .AsNoTracking()
                .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
        }

        public async Task<List<ProjectMemberDto>> GetProjectMembersAsync(int projectId)
        {
            return await _context.ProjectMembers
                .Include(pm => pm.User)
                .Where(pm => pm.ProjectId == projectId)
                .Select(pm => new ProjectMemberDto
                {
                    Id = pm.Id,
                    ProjectId = pm.ProjectId,
                    UserId = pm.UserId,
                    Username = !string.IsNullOrWhiteSpace(pm.User.RealName) ? pm.User.RealName! : pm.User.Username,
                    Phone = pm.User.Phone,
                    Role = pm.Role,
                    JoinedAt = pm.JoinedAt
                })
                .ToListAsync();
        }

        public async Task<ProjectMemberDto> AddProjectMemberAsync(int projectId, AddProjectMemberRequest request)
        {
            if (!await _context.Projects.AnyAsync(p => p.Id == projectId))
            {
                throw new KeyNotFoundException("项目不存在");
            }

            if (!await _context.Users.AnyAsync(u => u.Id == request.UserId))
            {
                throw new KeyNotFoundException("用户不存在");
            }

            if (await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == request.UserId))
            {
                throw new InvalidOperationException("用户已是项目成员");
            }

            var projectMember = new ProjectMember
            {
                ProjectId = projectId,
                UserId = request.UserId,
                Role = request.Role,
                JoinedAt = DateTime.UtcNow
            };

            _context.ProjectMembers.Add(projectMember);
            await _context.SaveChangesAsync();

            return (await GetProjectMembersAsync(projectId)).First(pm => pm.UserId == request.UserId);
        }

        public async Task<bool> RemoveProjectMemberAsync(int projectId, int userId)
        {
            var projectMember = await _context.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);

            if (projectMember == null)
            {
                throw new KeyNotFoundException("项目成员不存在");
            }

            projectMember.IsDeleted = true;
            await _context.SaveChangesAsync();

            return true;
        }

        private async System.Threading.Tasks.Task EnsureProjectManagerRoleAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (user == null)
            {
                throw new KeyNotFoundException("项目负责人用户不存在或已禁用");
            }

            return;
        }

        private static string GetStatusName(int status)
        {
            return status switch
            {
                0 => "规划中",
                1 => "进行中",
                2 => "已完成",
                3 => "已暂停",
                _ => "未知"
            };
        }

        private static void ValidateDateRange(DateTime? startDate, DateTime? endDate, string startLabel, string endLabel)
        {
            if (startDate.HasValue && endDate.HasValue && endDate.Value.Date < startDate.Value.Date)
            {
                throw new InvalidOperationException($"{endLabel}不能早于{startLabel}");
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

        private static string GetDisplayName(User user)
        {
            return !string.IsNullOrWhiteSpace(user.RealName) ? user.RealName! : user.Username;
        }
    }
}
