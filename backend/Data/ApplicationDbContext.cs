using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ProjectManagementSystem.Common;
using ProjectManagementSystem.Models.Entities;
using Task = ProjectManagementSystem.Models.Entities.Task;
using File = ProjectManagementSystem.Models.Entities.File;

namespace ProjectManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectMember> ProjectMembers { get; set; }
        public DbSet<Task> Tasks { get; set; }
        public DbSet<Milestone> Milestones { get; set; }
        public DbSet<File> Files { get; set; }
        public DbSet<TaskLog> TaskLogs { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<ProcessTemplate> ProcessTemplates { get; set; }
        public DbSet<ProcessTemplateStep> ProcessTemplateSteps { get; set; }
        public DbSet<BusinessLine> BusinessLines { get; set; }

        public override int SaveChanges()
        {
            NormalizeDateTimeValues();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            NormalizeDateTimeValues();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            NormalizeDateTimeValues();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override System.Threading.Tasks.Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            NormalizeDateTimeValues();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void NormalizeDateTimeValues()
        {
            var trackedEntries = ChangeTracker.Entries()
                .Where(entry => entry.State == EntityState.Added || entry.State == EntityState.Modified)
                .ToList();

            foreach (var entry in trackedEntries)
            {
                NormalizeEntryDateTime(entry);
            }
        }

        private static void NormalizeEntryDateTime(EntityEntry entry)
        {
            foreach (var property in entry.Properties)
            {
                var propertyType = property.Metadata.ClrType;
                if (propertyType == typeof(DateTime))
                {
                    if (property.CurrentValue is DateTime dateTime)
                    {
                        property.CurrentValue = ConvertToAppTime(dateTime);
                    }
                }
                else if (propertyType == typeof(DateTime?))
                {
                    if (property.CurrentValue is DateTime nullableDateTime)
                    {
                        property.CurrentValue = ConvertToAppTime(nullableDateTime);
                    }
                }
            }
        }

        private static DateTime ConvertToAppTime(DateTime value)
        {
            return AppTime.ConvertToChinaTime(value);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置User实体
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasOne(e => e.Role)
                    .WithMany(r => r.Users)
                    .HasForeignKey(e => e.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // 配置Project实体
            modelBuilder.Entity<Project>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasOne(e => e.Manager)
                    .WithMany(u => u.ManagedProjects)
                    .HasForeignKey(e => e.ManagerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // 配置ProjectMember实体
            modelBuilder.Entity<ProjectMember>(entity =>
            {
                entity.HasIndex(e => new { e.ProjectId, e.UserId, e.IsDeleted }).IsUnique();
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasOne(e => e.Project)
                    .WithMany(p => p.Members)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.ProjectMemberships)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // 配置Task实体
            modelBuilder.Entity<Task>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasOne(e => e.Project)
                    .WithMany(p => p.Tasks)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Assignee)
                    .WithMany(u => u.AssignedTasks)
                    .HasForeignKey(e => e.AssigneeId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Milestone)
                    .WithMany(m => m.Tasks)
                    .HasForeignKey(e => e.MilestoneId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // 配置Milestone实体
            modelBuilder.Entity<Milestone>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasOne(e => e.Project)
                    .WithMany(p => p.Milestones)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 配置File实体
            modelBuilder.Entity<File>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasOne(e => e.Project)
                    .WithMany(p => p.Files)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Uploader)
                    .WithMany(u => u.UploadedFiles)
                    .HasForeignKey(e => e.UploadedBy)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Children)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // 配置TaskLog实体
            modelBuilder.Entity<TaskLog>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasOne(e => e.Task)
                    .WithMany(t => t.TaskLogs)
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.TaskLogs)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // 配置Alert实体
            modelBuilder.Entity<Alert>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasOne(e => e.Project)
                    .WithMany(p => p.Alerts)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Task)
                    .WithMany(t => t.Alerts)
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Alerts)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // 配置ProcessTemplate实体
            modelBuilder.Entity<ProcessTemplate>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasIndex(e => e.IsDefault);
                entity.HasIndex(e => new { e.IsDeleted, e.IsDefault });
            });

            // 配置ProcessTemplateStep实体
            modelBuilder.Entity<ProcessTemplateStep>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasIndex(e => new { e.ProcessTemplateId, e.SortOrder });
                entity.HasOne(e => e.ProcessTemplate)
                    .WithMany(t => t.Steps)
                    .HasForeignKey(e => e.ProcessTemplateId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 配置BusinessLine实体
            modelBuilder.Entity<BusinessLine>(entity =>
            {
                entity.HasQueryFilter(e => !e.IsDeleted);
                entity.HasIndex(e => e.Name).IsUnique();
            });

            // 初始化角色数据
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "管理员", Description = "系统管理员，拥有所有权限", CreatedAt = DateTime.UtcNow },
                new Role { Id = 2, Name = "项目经理", Description = "项目经理，可以管理项目和任务", CreatedAt = DateTime.UtcNow },
                new Role { Id = 3, Name = "普通成员", Description = "普通成员，可以查看和更新任务", CreatedAt = DateTime.UtcNow }
            );

            // 初始化管理员用户（密码：admin123）
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    RoleId = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                }
            );
        }
    }
}
