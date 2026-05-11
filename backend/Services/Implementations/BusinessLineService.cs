using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Models.DTOs;
using ProjectManagementSystem.Models.Entities;
using ProjectManagementSystem.Services.Interfaces;

namespace ProjectManagementSystem.Services.Implementations
{
    public class BusinessLineService : IBusinessLineService
    {
        private readonly ApplicationDbContext _context;

        public BusinessLineService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<BusinessLineDto>> GetBusinessLinesAsync()
        {
            return await _context.BusinessLines
                .OrderBy(b => b.SortOrder)
                .ThenBy(b => b.CreatedAt)
                .Select(b => new BusinessLineDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    Description = b.Description,
                    SortOrder = b.SortOrder,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<BusinessLineDto> CreateBusinessLineAsync(CreateBusinessLineRequest request)
        {
            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("业务线名称不能为空");
            }

            var exists = await _context.BusinessLines.AnyAsync(b => b.Name == name);
            if (exists)
            {
                throw new InvalidOperationException("业务线名称已存在");
            }

            var businessLine = new BusinessLine
            {
                Name = name,
                Description = request.Description?.Trim(),
                SortOrder = request.SortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.BusinessLines.Add(businessLine);
            await _context.SaveChangesAsync();

            return new BusinessLineDto
            {
                Id = businessLine.Id,
                Name = businessLine.Name,
                Description = businessLine.Description,
                SortOrder = businessLine.SortOrder,
                CreatedAt = businessLine.CreatedAt
            };
        }

        public async Task<BusinessLineDto> UpdateBusinessLineAsync(int id, UpdateBusinessLineRequest request)
        {
            var businessLine = await _context.BusinessLines.FirstOrDefaultAsync(b => b.Id == id);
            if (businessLine == null)
            {
                throw new KeyNotFoundException("业务线不存在");
            }

            if (request.Name != null)
            {
                var name = request.Name.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new InvalidOperationException("业务线名称不能为空");
                }

                var exists = await _context.BusinessLines.AnyAsync(b => b.Name == name && b.Id != id);
                if (exists)
                {
                    throw new InvalidOperationException("业务线名称已存在");
                }

                businessLine.Name = name;
            }

            if (request.Description != null)
            {
                businessLine.Description = request.Description.Trim();
            }

            if (request.SortOrder.HasValue)
            {
                businessLine.SortOrder = request.SortOrder.Value;
            }

            businessLine.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new BusinessLineDto
            {
                Id = businessLine.Id,
                Name = businessLine.Name,
                Description = businessLine.Description,
                SortOrder = businessLine.SortOrder,
                CreatedAt = businessLine.CreatedAt
            };
        }

        public async Task<bool> DeleteBusinessLineAsync(int id)
        {
            var businessLine = await _context.BusinessLines.FirstOrDefaultAsync(b => b.Id == id);
            if (businessLine == null)
            {
                throw new KeyNotFoundException("业务线不存在");
            }

            businessLine.IsDeleted = true;
            businessLine.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
