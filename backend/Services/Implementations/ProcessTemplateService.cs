using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Models.Entities;
using ProjectManagementSystem.Services.Interfaces;
using System.Text.Json;
using TaskEntity = ProjectManagementSystem.Models.Entities.Task;

namespace ProjectManagementSystem.Services.Implementations
{
    public class ProcessTemplateService : IProcessTemplateService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly SemaphoreSlim _locker = new(1, 1);

        public ProcessTemplateService(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async System.Threading.Tasks.Task<List<ProcessTemplateDto>> GetTemplatesAsync()
        {
            var data = await LoadDataAsync();
            return await MapTemplatesAsync(data.Templates.OrderByDescending(t => t.IsDefault).ThenBy(t => t.Id).ToList());
        }

        public async System.Threading.Tasks.Task<ProcessTemplateDto> GetTemplateByIdAsync(int id)
        {
            var data = await LoadDataAsync();
            var template = data.Templates.FirstOrDefault(t => t.Id == id);
            if (template == null)
            {
                throw new KeyNotFoundException("项目任务模板不存在");
            }

            return await MapTemplateAsync(template);
        }

        public async System.Threading.Tasks.Task<ProcessTemplateDto> CreateTemplateAsync(CreateProcessTemplateRequest request)
        {
            await _locker.WaitAsync();
            try
            {
                var data = await LoadDataAsync();
                var now = DateTime.UtcNow;
                var newId = data.Templates.Any() ? data.Templates.Max(t => t.Id) + 1 : 1;

                if (request.IsDefault)
                {
                    foreach (var item in data.Templates)
                    {
                        item.IsDefault = false;
                        item.UpdatedAt = now;
                    }
                }

                var template = new ProcessTemplateStore
                {
                    Id = newId,
                    Name = request.Name,
                    Description = request.Description,
                    IsDefault = request.IsDefault,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Steps = request.Steps.Select((s, index) => new ProcessStepStore
                    {
                        Id = index + 1,
                        SortOrder = s.SortOrder > 0 ? s.SortOrder : index + 1,
                        Stage = s.Stage,
                        Name = s.Name,
                        Description = s.Description,
                        Priority = s.Priority,
                        DefaultAssigneeIds = NormalizeAssigneeIds(s.DefaultAssigneeIds, s.DefaultAssigneeId),
                        DefaultAssigneeId = NormalizeAssigneeIds(s.DefaultAssigneeIds, s.DefaultAssigneeId).Cast<int?>().FirstOrDefault(),
                        EstimatedDays = s.EstimatedDays
                    }).OrderBy(s => s.SortOrder).ToList()
                };

                data.Templates.Add(template);
                await SaveDataAsync(data);
                return await MapTemplateAsync(template);
            }
            finally
            {
                _locker.Release();
            }
        }

        public async System.Threading.Tasks.Task<ProcessTemplateDto> UpdateTemplateAsync(int id, UpdateProcessTemplateRequest request)
        {
            await _locker.WaitAsync();
            try
            {
                var data = await LoadDataAsync();
                var template = data.Templates.FirstOrDefault(t => t.Id == id);
                if (template == null)
                {
                    throw new KeyNotFoundException("项目任务模板不存在");
                }

                var now = DateTime.UtcNow;
                if (request.IsDefault)
                {
                    foreach (var item in data.Templates)
                    {
                        item.IsDefault = false;
                        item.UpdatedAt = now;
                    }
                }

                template.Name = request.Name;
                template.Description = request.Description;
                template.IsDefault = request.IsDefault;
                template.UpdatedAt = now;
                template.Steps = request.Steps.Select((s, index) => new ProcessStepStore
                {
                    Id = index + 1,
                    SortOrder = s.SortOrder > 0 ? s.SortOrder : index + 1,
                    Stage = s.Stage,
                    Name = s.Name,
                    Description = s.Description,
                    Priority = s.Priority,
                    DefaultAssigneeIds = NormalizeAssigneeIds(s.DefaultAssigneeIds, s.DefaultAssigneeId),
                    DefaultAssigneeId = NormalizeAssigneeIds(s.DefaultAssigneeIds, s.DefaultAssigneeId).Cast<int?>().FirstOrDefault(),
                    EstimatedDays = s.EstimatedDays
                }).OrderBy(s => s.SortOrder).ToList();

                await SaveDataAsync(data);
                return await MapTemplateAsync(template);
            }
            finally
            {
                _locker.Release();
            }
        }

        public async System.Threading.Tasks.Task<bool> DeleteTemplateAsync(int id)
        {
            await _locker.WaitAsync();
            try
            {
                var data = await LoadDataAsync();
                var template = data.Templates.FirstOrDefault(t => t.Id == id);
                if (template == null)
                {
                    throw new KeyNotFoundException("项目任务模板不存在");
                }

                data.Templates.Remove(template);
                if (!data.Templates.Any(t => t.IsDefault) && data.Templates.Any())
                {
                    data.Templates.OrderBy(t => t.Id).First().IsDefault = true;
                }

                await SaveDataAsync(data);
                return true;
            }
            finally
            {
                _locker.Release();
            }
        }

        public async System.Threading.Tasks.Task<ProcessTemplateDto> SetDefaultTemplateAsync(int id)
        {
            await _locker.WaitAsync();
            try
            {
                var data = await LoadDataAsync();
                var template = data.Templates.FirstOrDefault(t => t.Id == id);
                if (template == null)
                {
                    throw new KeyNotFoundException("项目任务模板不存在");
                }

                var now = DateTime.UtcNow;
                foreach (var item in data.Templates)
                {
                    item.IsDefault = item.Id == id;
                    item.UpdatedAt = now;
                }

                await SaveDataAsync(data);
                return await MapTemplateAsync(template);
            }
            finally
            {
                _locker.Release();
            }
        }

        public async System.Threading.Tasks.Task<int> ApplyDefaultTemplateToProjectAsync(int projectId, int? fallbackAssigneeId, int operatorUserId, int? templateId = null)
        {
            var project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                throw new KeyNotFoundException("项目不存在");
            }

            if (await _context.Tasks.AnyAsync(t => t.ProjectId == projectId))
            {
                return 0;
            }

            var data = await LoadDataAsync();
            ProcessTemplateStore? template;
            if (templateId.HasValue)
            {
                template = data.Templates.FirstOrDefault(t => t.Id == templateId.Value);
                if (template == null)
                {
                    throw new KeyNotFoundException("所选项目任务模板不存在");
                }
            }
            else
            {
                template = data.Templates.FirstOrDefault(t => t.IsDefault) ?? data.Templates.OrderBy(t => t.Id).FirstOrDefault();
            }

            if (template == null || template.Steps.Count == 0)
            {
                return 0;
            }

            var now = DateTime.UtcNow;
            var baseDate = project.StartDate?.Date ?? now.Date;
            DateTime? previousTaskEndDate = null;
            var entities = new List<TaskEntity>();

            foreach (var step in template.Steps.OrderBy(s => s.SortOrder))
            {
                var estimated = step.EstimatedDays <= 0 ? 1 : step.EstimatedDays;
                var startDate = previousTaskEndDate ?? baseDate;
                var dueDate = startDate.AddDays(estimated);
                previousTaskEndDate = dueDate;

                var preferredAssigneeIds = NormalizeAssigneeIds(step.DefaultAssigneeIds, step.DefaultAssigneeId);
                int? assigneeId = null;
                if (preferredAssigneeIds.Count > 0)
                {
                    var availableAssignees = await _context.Users
                        .AsNoTracking()
                        .Where(u => preferredAssigneeIds.Contains(u.Id))
                        .Select(u => u.Id)
                        .ToListAsync();

                    var availableSet = availableAssignees.ToHashSet();
                    var candidate = preferredAssigneeIds
                        .Where(id => availableSet.Contains(id))
                        .Cast<int?>()
                        .FirstOrDefault();

                    if (candidate.HasValue)
                    {
                        assigneeId = candidate.Value;
                    }
                }

                if (!assigneeId.HasValue && fallbackAssigneeId.HasValue && fallbackAssigneeId.Value > 0)
                {
                    if (await _context.Users.AnyAsync(u => u.Id == fallbackAssigneeId.Value))
                    {
                        assigneeId = fallbackAssigneeId.Value;
                    }
                }

                var assigneeNames = await _context.Users
                    .AsNoTracking()
                    .Where(u => preferredAssigneeIds.Contains(u.Id))
                    .Select(u => u.Username)
                    .ToListAsync();

                entities.Add(new TaskEntity
                {
                    ProjectId = projectId,
                    Title = step.Name,
                    Description = BuildDescription(step, assigneeNames),
                    AssigneeId = assigneeId,
                    Priority = step.Priority,
                    Status = 0,
                    StartDate = startDate,
                    DueDate = dueDate,
                    Progress = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            _context.Tasks.AddRange(entities);
            await _context.SaveChangesAsync();

            foreach (var task in entities)
            {
                _context.TaskLogs.Add(new TaskLog
                {
                    TaskId = task.Id,
                    UserId = operatorUserId,
                    Action = "自动生成",
                    OldValue = null,
                    NewValue = $"来自项目任务模板：{template.Name}",
                    CreatedAt = now
                });
            }
            await _context.SaveChangesAsync();

            return entities.Count;
        }

        private async System.Threading.Tasks.Task<List<ProcessTemplateDto>> MapTemplatesAsync(List<ProcessTemplateStore> templates)
        {
            var users = await _context.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Username);
            return templates.Select(t => MapTemplate(t, users)).ToList();
        }

        private async System.Threading.Tasks.Task<ProcessTemplateDto> MapTemplateAsync(ProcessTemplateStore template)
        {
            var users = await _context.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Username);
            return MapTemplate(template, users);
        }

        private static ProcessTemplateDto MapTemplate(ProcessTemplateStore template, Dictionary<int, string> users)
        {
            return new ProcessTemplateDto
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                IsDefault = template.IsDefault,
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt,
                Steps = template.Steps.OrderBy(s => s.SortOrder).Select(s => new ProcessStepDto
                {
                    Id = s.Id,
                    SortOrder = s.SortOrder,
                    Stage = s.Stage,
                    Name = s.Name,
                    Description = s.Description,
                    Priority = s.Priority,
                    DefaultAssigneeId = NormalizeAssigneeIds(s.DefaultAssigneeIds, s.DefaultAssigneeId).Cast<int?>().FirstOrDefault(),
                    DefaultAssigneeName = NormalizeAssigneeIds(s.DefaultAssigneeIds, s.DefaultAssigneeId)
                        .Select(id => users.TryGetValue(id, out var username) ? username : null)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                    DefaultAssigneeIds = NormalizeAssigneeIds(s.DefaultAssigneeIds, s.DefaultAssigneeId),
                    DefaultAssigneeNames = NormalizeAssigneeIds(s.DefaultAssigneeIds, s.DefaultAssigneeId)
                        .Select(id => users.TryGetValue(id, out var username) ? username : null)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Select(name => name!)
                        .ToList(),
                    EstimatedDays = s.EstimatedDays
                }).ToList()
            };
        }

        private static List<int> NormalizeAssigneeIds(List<int>? ids, int? fallbackId)
        {
            var result = ids?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!result.Any() && fallbackId.HasValue && fallbackId.Value > 0)
            {
                result.Add(fallbackId.Value);
            }

            return result;
        }

        private async System.Threading.Tasks.Task<TemplateDataStore> LoadDataAsync()
        {
            var path = GetStorePath();
            if (!System.IO.File.Exists(path))
            {
                var initial = CreateDefaultTemplateData();
                await SaveDataAsync(initial);
                return initial;
            }

            var json = await System.IO.File.ReadAllTextAsync(path);
            var data = JsonSerializer.Deserialize<TemplateDataStore>(json, JsonOptions());
            return data ?? new TemplateDataStore();
        }

        private async System.Threading.Tasks.Task SaveDataAsync(TemplateDataStore data)
        {
            var path = GetStorePath();
            var dir = Path.GetDirectoryName(path)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(data, JsonOptions());
            await System.IO.File.WriteAllTextAsync(path, json);
        }

        private string GetStorePath()
        {
            return Path.Combine(_environment.ContentRootPath, "Data", "process-templates.json");
        }

        private static JsonSerializerOptions JsonOptions() => new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static string BuildDescription(ProcessStepStore step, List<string>? assigneeNames = null)
        {
            var description = step.Description ?? string.Empty;
            var details = $"阶段：{step.Stage}\n预计工期：{step.EstimatedDays} 天";

            if (assigneeNames != null && assigneeNames.Any())
            {
                details += $"\n默认负责人：{string.Join("、", assigneeNames.Distinct())}";
            }

            return string.IsNullOrWhiteSpace(description) ? details : $"{description}\n\n{details}";
        }

        private static TemplateDataStore CreateDefaultTemplateData()
        {
            var now = DateTime.UtcNow;
            return new TemplateDataStore
            {
                Templates = new List<ProcessTemplateStore>
                {
                    new()
                    {
                        Id = 1,
                        Name = "微电网标准项目任务模板",
                        Description = "售前到收尾全流程标准工序",
                        IsDefault = true,
                        CreatedAt = now,
                        UpdatedAt = now,
                        Steps = new List<ProcessStepStore>
                        {
                            new() { Id = 1, SortOrder = 1, Stage = "售前", Name = "售前需求澄清", Priority = 3, EstimatedDays = 2 },
                            new() { Id = 2, SortOrder = 2, Stage = "售前", Name = "现场踏勘与负荷调研", Priority = 4, EstimatedDays = 4 },
                            new() { Id = 3, SortOrder = 3, Stage = "方案", Name = "方案初设与容量配置", Priority = 4, EstimatedDays = 5 },
                            new() { Id = 4, SortOrder = 4, Stage = "方案", Name = "经济性测算与投资回报", Priority = 3, EstimatedDays = 3 },
                            new() { Id = 5, SortOrder = 5, Stage = "合同", Name = "合同技术条款确认", Priority = 4, EstimatedDays = 2 },
                            new() { Id = 6, SortOrder = 6, Stage = "设计", Name = "初步设计评审", Priority = 4, EstimatedDays = 4 },
                            new() { Id = 7, SortOrder = 7, Stage = "设计", Name = "施工图与BOM冻结", Priority = 4, EstimatedDays = 5 },
                            new() { Id = 8, SortOrder = 8, Stage = "采购", Name = "关键设备采购下单", Priority = 4, EstimatedDays = 5 },
                            new() { Id = 9, SortOrder = 9, Stage = "采购", Name = "工厂FAT测试", Priority = 3, EstimatedDays = 3 },
                            new() { Id = 10, SortOrder = 10, Stage = "施工", Name = "现场土建与基础施工", Priority = 3, EstimatedDays = 6 },
                            new() { Id = 11, SortOrder = 11, Stage = "施工", Name = "设备安装与接线", Priority = 4, EstimatedDays = 7 },
                            new() { Id = 12, SortOrder = 12, Stage = "调试", Name = "系统联调与保护定值", Priority = 4, EstimatedDays = 5 },
                            new() { Id = 13, SortOrder = 13, Stage = "调试", Name = "并网/并离网切换测试", Priority = 4, EstimatedDays = 3 },
                            new() { Id = 14, SortOrder = 14, Stage = "交付", Name = "试运行与性能考核", Priority = 4, EstimatedDays = 6 },
                            new() { Id = 15, SortOrder = 15, Stage = "交付", Name = "问题整改闭环", Priority = 3, EstimatedDays = 3 },
                            new() { Id = 16, SortOrder = 16, Stage = "收尾", Name = "竣工资料与培训移交", Priority = 3, EstimatedDays = 2 },
                            new() { Id = 17, SortOrder = 17, Stage = "收尾", Name = "最终验收与结算", Priority = 4, EstimatedDays = 3 },
                            new() { Id = 18, SortOrder = 18, Stage = "收尾", Name = "项目复盘与运维交接", Priority = 2, EstimatedDays = 2 }
                        }
                    }
                }
            };
        }

        private class TemplateDataStore
        {
            public List<ProcessTemplateStore> Templates { get; set; } = new();
        }

        private class ProcessTemplateStore
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public bool IsDefault { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public List<ProcessStepStore> Steps { get; set; } = new();
        }

        private class ProcessStepStore
        {
            public int Id { get; set; }
            public int SortOrder { get; set; }
            public string Stage { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public int Priority { get; set; } = 2;
            public int? DefaultAssigneeId { get; set; }
            public List<int> DefaultAssigneeIds { get; set; } = new();
            public int EstimatedDays { get; set; } = 3;
        }
    }
}
