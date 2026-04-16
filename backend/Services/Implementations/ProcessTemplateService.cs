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
            await EnsureTemplateDataInitializedAsync();
            var templates = await _context.ProcessTemplates
                .AsNoTracking()
                .Include(t => t.Steps)
                .OrderByDescending(t => t.IsDefault)
                .ThenBy(t => t.Id)
                .ToListAsync();

            return await MapTemplatesAsync(templates);
        }

        public async System.Threading.Tasks.Task<ProcessTemplateDto> GetTemplateByIdAsync(int id)
        {
            await EnsureTemplateDataInitializedAsync();
            var template = await _context.ProcessTemplates
                .AsNoTracking()
                .Include(t => t.Steps)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (template == null)
            {
                throw new KeyNotFoundException("项目任务模板不存在");
            }

            return await MapTemplateAsync(template);
        }

        public async System.Threading.Tasks.Task<ProcessTemplateDto> CreateTemplateAsync(CreateProcessTemplateRequest request)
        {
            await EnsureTemplateDataInitializedAsync();
            await _locker.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var normalizedSteps = BuildStepEntities(request.Steps, now).ToList();

                if (request.IsDefault)
                {
                    var defaultTemplates = await _context.ProcessTemplates.Where(t => t.IsDefault).ToListAsync();
                    foreach (var current in defaultTemplates)
                    {
                        current.IsDefault = false;
                        current.UpdatedAt = now;
                    }
                }

                var template = new ProcessTemplate
                {
                    Name = request.Name,
                    Description = request.Description,
                    IsDefault = request.IsDefault,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Steps = normalizedSteps
                };

                _context.ProcessTemplates.Add(template);
                await _context.SaveChangesAsync();
                return await GetTemplateByIdAsync(template.Id);
            }
            finally
            {
                _locker.Release();
            }
        }

        public async System.Threading.Tasks.Task<ProcessTemplateDto> UpdateTemplateAsync(int id, UpdateProcessTemplateRequest request)
        {
            await EnsureTemplateDataInitializedAsync();
            await _locker.WaitAsync();
            try
            {
                var template = await _context.ProcessTemplates
                    .Include(t => t.Steps)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (template == null)
                {
                    throw new KeyNotFoundException("项目任务模板不存在");
                }

                var now = DateTime.UtcNow;
                if (request.IsDefault)
                {
                    var defaultTemplates = await _context.ProcessTemplates.Where(t => t.IsDefault && t.Id != id).ToListAsync();
                    foreach (var current in defaultTemplates)
                    {
                        current.IsDefault = false;
                        current.UpdatedAt = now;
                    }
                }

                template.Name = request.Name;
                template.Description = request.Description;
                template.IsDefault = request.IsDefault;
                template.UpdatedAt = now;

                if (template.Steps.Any())
                {
                    _context.ProcessTemplateSteps.RemoveRange(template.Steps);
                }

                var newSteps = BuildStepEntities(request.Steps, now).ToList();
                foreach (var step in newSteps)
                {
                    step.ProcessTemplateId = template.Id;
                }
                _context.ProcessTemplateSteps.AddRange(newSteps);

                await _context.SaveChangesAsync();
                return await GetTemplateByIdAsync(template.Id);
            }
            finally
            {
                _locker.Release();
            }
        }

        public async System.Threading.Tasks.Task<bool> DeleteTemplateAsync(int id)
        {
            await EnsureTemplateDataInitializedAsync();
            await _locker.WaitAsync();
            try
            {
                var template = await _context.ProcessTemplates
                    .Include(t => t.Steps)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (template == null)
                {
                    throw new KeyNotFoundException("项目任务模板不存在");
                }

                var removedDefault = template.IsDefault;
                var now = DateTime.UtcNow;
                template.IsDeleted = true;
                template.IsDefault = false;
                template.UpdatedAt = now;

                foreach (var step in template.Steps)
                {
                    step.IsDeleted = true;
                    step.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();

                if (removedDefault)
                {
                    var nextDefault = await _context.ProcessTemplates.OrderBy(t => t.Id).FirstOrDefaultAsync();
                    if (nextDefault != null)
                    {
                        nextDefault.IsDefault = true;
                        nextDefault.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }

                return true;
            }
            finally
            {
                _locker.Release();
            }
        }

        public async System.Threading.Tasks.Task<ProcessTemplateDto> SetDefaultTemplateAsync(int id)
        {
            await EnsureTemplateDataInitializedAsync();
            await _locker.WaitAsync();
            try
            {
                var template = await _context.ProcessTemplates.FirstOrDefaultAsync(t => t.Id == id);
                if (template == null)
                {
                    throw new KeyNotFoundException("项目任务模板不存在");
                }

                var now = DateTime.UtcNow;
                var allTemplates = await _context.ProcessTemplates.ToListAsync();
                foreach (var item in allTemplates)
                {
                    item.IsDefault = item.Id == id;
                    item.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();
                return await GetTemplateByIdAsync(template.Id);
            }
            finally
            {
                _locker.Release();
            }
        }

        public async System.Threading.Tasks.Task<int> ApplyDefaultTemplateToProjectAsync(int projectId, int? fallbackAssigneeId, int operatorUserId, int? templateId = null)
        {
            await EnsureTemplateDataInitializedAsync();

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

            ProcessTemplate? template;
            if (templateId.HasValue)
            {
                template = await _context.ProcessTemplates
                    .AsNoTracking()
                    .Include(t => t.Steps)
                    .FirstOrDefaultAsync(t => t.Id == templateId.Value);

                if (template == null)
                {
                    throw new KeyNotFoundException("所选项目任务模板不存在");
                }
            }
            else
            {
                template = await _context.ProcessTemplates
                    .AsNoTracking()
                    .Include(t => t.Steps)
                    .OrderByDescending(t => t.IsDefault)
                    .ThenBy(t => t.Id)
                    .FirstOrDefaultAsync();
            }

            if (template == null || !template.Steps.Any())
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

                var preferredAssigneeIds = ParseAssigneeIds(step.DefaultAssigneeIds, step.DefaultAssigneeId);
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

        private IEnumerable<ProcessTemplateStep> BuildStepEntities(List<ProcessStepRequest>? steps, DateTime now)
        {
            var source = steps ?? new List<ProcessStepRequest>();
            return source.Select((s, index) =>
            {
                var assigneeIds = NormalizeAssigneeIds(s.DefaultAssigneeIds, s.DefaultAssigneeId);
                return new ProcessTemplateStep
                {
                    SortOrder = s.SortOrder > 0 ? s.SortOrder : index + 1,
                    Stage = s.Stage,
                    Name = s.Name,
                    Description = s.Description,
                    Priority = s.Priority,
                    DefaultAssigneeId = assigneeIds.Cast<int?>().FirstOrDefault(),
                    DefaultAssigneeIds = SerializeAssigneeIds(assigneeIds),
                    EstimatedDays = s.EstimatedDays,
                    CreatedAt = now,
                    UpdatedAt = now
                };
            }).OrderBy(s => s.SortOrder).ToList();
        }

        private async System.Threading.Tasks.Task<List<ProcessTemplateDto>> MapTemplatesAsync(List<ProcessTemplate> templates)
        {
            var users = await _context.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Username);
            return templates.Select(t => MapTemplate(t, users)).ToList();
        }

        private async System.Threading.Tasks.Task<ProcessTemplateDto> MapTemplateAsync(ProcessTemplate template)
        {
            var users = await _context.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Username);
            return MapTemplate(template, users);
        }

        private static ProcessTemplateDto MapTemplate(ProcessTemplate template, Dictionary<int, string> users)
        {
            return new ProcessTemplateDto
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                IsDefault = template.IsDefault,
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt,
                Steps = template.Steps.OrderBy(s => s.SortOrder).Select(s =>
                {
                    var assigneeIds = ParseAssigneeIds(s.DefaultAssigneeIds, s.DefaultAssigneeId);
                    var assigneeNames = assigneeIds
                        .Select(id => users.TryGetValue(id, out var username) ? username : null)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Select(name => name!)
                        .ToList();

                    return new ProcessStepDto
                    {
                        Id = s.Id,
                        SortOrder = s.SortOrder,
                        Stage = s.Stage,
                        Name = s.Name,
                        Description = s.Description,
                        Priority = s.Priority,
                        DefaultAssigneeId = assigneeIds.Cast<int?>().FirstOrDefault(),
                        DefaultAssigneeName = assigneeNames.FirstOrDefault(),
                        DefaultAssigneeIds = assigneeIds,
                        DefaultAssigneeNames = assigneeNames,
                        EstimatedDays = s.EstimatedDays
                    };
                }).ToList()
            };
        }

        private async System.Threading.Tasks.Task EnsureTemplateDataInitializedAsync()
        {
            if (await _context.ProcessTemplates.IgnoreQueryFilters().AnyAsync())
            {
                return;
            }

            await _locker.WaitAsync();
            try
            {
                if (await _context.ProcessTemplates.IgnoreQueryFilters().AnyAsync())
                {
                    return;
                }

                var now = DateTime.UtcNow;
                var legacyData = await LoadLegacyDataAsync();
                var sourceTemplates = legacyData?.Templates?.Any() == true
                    ? legacyData.Templates.OrderBy(t => t.Id).ToList()
                    : CreateDefaultTemplateData().Templates;

                var entities = sourceTemplates.Select(template =>
                {
                    var templateEntity = new ProcessTemplate
                    {
                        Name = template.Name,
                        Description = template.Description,
                        IsDefault = template.IsDefault,
                        CreatedAt = template.CreatedAt == default ? now : template.CreatedAt,
                        UpdatedAt = template.UpdatedAt == default ? now : template.UpdatedAt,
                        Steps = template.Steps
                            .OrderBy(s => s.SortOrder)
                            .Select(step =>
                            {
                                var assigneeIds = NormalizeAssigneeIds(step.DefaultAssigneeIds, step.DefaultAssigneeId);
                                return new ProcessTemplateStep
                                {
                                    SortOrder = step.SortOrder,
                                    Stage = step.Stage,
                                    Name = step.Name,
                                    Description = step.Description,
                                    Priority = step.Priority,
                                    DefaultAssigneeId = assigneeIds.Cast<int?>().FirstOrDefault(),
                                    DefaultAssigneeIds = SerializeAssigneeIds(assigneeIds),
                                    EstimatedDays = step.EstimatedDays,
                                    CreatedAt = now,
                                    UpdatedAt = now
                                };
                            }).ToList()
                    };

                    return templateEntity;
                }).ToList();

                if (!entities.Any())
                {
                    return;
                }

                if (!entities.Any(t => t.IsDefault))
                {
                    entities[0].IsDefault = true;
                }

                _context.ProcessTemplates.AddRange(entities);
                await _context.SaveChangesAsync();
            }
            finally
            {
                _locker.Release();
            }
        }

        private async System.Threading.Tasks.Task<LegacyTemplateDataStore?> LoadLegacyDataAsync()
        {
            var path = GetLegacyStorePath();
            if (!System.IO.File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = await System.IO.File.ReadAllTextAsync(path);
                var data = JsonSerializer.Deserialize<LegacyTemplateDataStore>(json, JsonOptions());
                return data;
            }
            catch
            {
                return null;
            }
        }

        private string GetLegacyStorePath()
        {
            return Path.Combine(_environment.ContentRootPath, "Data", "process-templates.json");
        }

        private static string BuildDescription(ProcessTemplateStep step, List<string>? assigneeNames = null)
        {
            var description = step.Description ?? string.Empty;
            var details = $"阶段：{step.Stage}\n预计工期：{step.EstimatedDays} 天";

            if (assigneeNames != null && assigneeNames.Any())
            {
                details += $"\n默认负责人：{string.Join("、", assigneeNames.Distinct())}";
            }

            return string.IsNullOrWhiteSpace(description) ? details : $"{description}\n\n{details}";
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

        private static List<int> ParseAssigneeIds(string? idsText, int? fallbackId)
        {
            var ids = new List<int>();
            if (!string.IsNullOrWhiteSpace(idsText))
            {
                foreach (var part in idsText.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(part.Trim(), out var value) && value > 0)
                    {
                        ids.Add(value);
                    }
                }
            }

            ids = ids.Distinct().ToList();
            if (!ids.Any() && fallbackId.HasValue && fallbackId.Value > 0)
            {
                ids.Add(fallbackId.Value);
            }

            return ids;
        }

        private static string SerializeAssigneeIds(List<int> ids)
        {
            return string.Join(',', ids.Where(id => id > 0).Distinct());
        }

        private static JsonSerializerOptions JsonOptions() => new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static LegacyTemplateDataStore CreateDefaultTemplateData()
        {
            var now = DateTime.UtcNow;
            return new LegacyTemplateDataStore
            {
                Templates = new List<LegacyProcessTemplateStore>
                {
                    new()
                    {
                        Id = 1,
                        Name = "微电网标准项目任务模板",
                        Description = "售前到收尾全流程标准工序",
                        IsDefault = true,
                        CreatedAt = now,
                        UpdatedAt = now,
                        Steps = new List<LegacyProcessStepStore>
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

        private class LegacyTemplateDataStore
        {
            public List<LegacyProcessTemplateStore> Templates { get; set; } = new();
        }

        private class LegacyProcessTemplateStore
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public bool IsDefault { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public List<LegacyProcessStepStore> Steps { get; set; } = new();
        }

        private class LegacyProcessStepStore
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
