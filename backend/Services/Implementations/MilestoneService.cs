using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Models.Entities;
using ProjectManagementSystem.Services.Interfaces;

namespace ProjectManagementSystem.Services.Implementations
{
    public class MilestoneService : IMilestoneService
    {
        private readonly ApplicationDbContext _context;

        public MilestoneService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<MilestoneDto>> GetMilestonesByProjectIdAsync(int projectId)
        {
            return await _context.Milestones
                .Where(m => m.ProjectId == projectId)
                .OrderBy(m => m.DueDate)
                .Select(m => new MilestoneDto
                {
                    Id = m.Id,
                    ProjectId = m.ProjectId,
                    Name = m.Name,
                    Description = m.Description,
                    DueDate = m.DueDate,
                    Status = m.Status,
                    StatusName = GetStatusName(m.Status),
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<MilestoneDto> GetMilestoneByIdAsync(int id)
        {
            var milestone = await _context.Milestones.FirstOrDefaultAsync(m => m.Id == id);
            if (milestone == null)
            {
                throw new KeyNotFoundException("里程碑不存在");
            }

            return new MilestoneDto
            {
                Id = milestone.Id,
                ProjectId = milestone.ProjectId,
                Name = milestone.Name,
                Description = milestone.Description,
                DueDate = milestone.DueDate,
                Status = milestone.Status,
                StatusName = GetStatusName(milestone.Status),
                CreatedAt = milestone.CreatedAt
            };
        }

        public async Task<MilestoneDto> CreateMilestoneAsync(CreateMilestoneRequest request)
        {
            if (!await _context.Projects.AnyAsync(p => p.Id == request.ProjectId))
            {
                throw new KeyNotFoundException("项目不存在");
            }

            var milestone = new Milestone
            {
                ProjectId = request.ProjectId,
                Name = request.Name,
                Description = request.Description,
                DueDate = request.DueDate,
                Status = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.Milestones.Add(milestone);
            await _context.SaveChangesAsync();

            return await GetMilestoneByIdAsync(milestone.Id);
        }

        public async Task<MilestoneDto> UpdateMilestoneAsync(int id, UpdateMilestoneRequest request)
        {
            var milestone = await _context.Milestones.FirstOrDefaultAsync(m => m.Id == id);
            if (milestone == null)
            {
                throw new KeyNotFoundException("里程碑不存在");
            }

            if (request.Name != null)
            {
                milestone.Name = request.Name;
            }

            if (request.Description != null)
            {
                milestone.Description = request.Description;
            }

            if (request.DueDate.HasValue)
            {
                milestone.DueDate = request.DueDate.Value;
            }

            if (request.Status.HasValue)
            {
                milestone.Status = request.Status.Value;
            }

            await _context.SaveChangesAsync();

            return await GetMilestoneByIdAsync(id);
        }

        public async Task<bool> DeleteMilestoneAsync(int id)
        {
            var milestone = await _context.Milestones.FirstOrDefaultAsync(m => m.Id == id);
            if (milestone == null)
            {
                throw new KeyNotFoundException("里程碑不存在");
            }

            milestone.IsDeleted = true;
            await _context.SaveChangesAsync();

            return true;
        }

        private string GetStatusName(int status)
        {
            return status switch
            {
                0 => "未开始",
                1 => "进行中",
                2 => "已完成",
                _ => "未知"
            };
        }
    }
}
