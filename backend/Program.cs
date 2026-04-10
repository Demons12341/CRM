using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProjectManagementSystem.Data;
using ProjectManagementSystem.Middleware;
using ProjectManagementSystem.Services.Implementations;
using ProjectManagementSystem.Services.Interfaces;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

const long maxUploadSize = 200L * 1024L * 1024L;

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadSize;
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = maxUploadSize;
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = maxUploadSize;
});

// 配置Serilog日志
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// 添加数据库上下文
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// 添加JWT认证
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtSecretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey 未配置");
var jwtIssuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JwtSettings:Issuer 未配置");
var jwtAudience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JwtSettings:Audience 未配置");
var secretKey = Encoding.UTF8.GetBytes(jwtSecretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };
});

// 添加授权
builder.Services.AddAuthorization();

// 添加服务
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProcessTemplateService, ProcessTemplateService>();
builder.Services.AddScoped<IMilestoneService, MilestoneService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddHostedService<FileCleanupHostedService>();

// 添加控制器
builder.Services.AddControllers();

// 添加Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "项目管理系统API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// 添加CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

var app = builder.Build();

// 配置HTTP请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("AllowVueApp");

// 使用自定义中间件
app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

// 确保数据库已创建
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();

    try
    {
        dbContext.Database.ExecuteSqlRaw("ALTER TABLE `Tasks` ADD COLUMN `OverdueReason` VARCHAR(1000) NULL;");
    }
    catch (Exception ex)
    {
        Log.Information("Tasks.OverdueReason 列初始化跳过：{Message}", ex.Message);
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("ALTER TABLE `Projects` ADD COLUMN `Priority` INT NOT NULL DEFAULT 2;");
    }
    catch (Exception ex)
    {
        Log.Information("Projects.Priority 列初始化跳过：{Message}", ex.Message);
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("ALTER TABLE `Users` ADD COLUMN `RealName` VARCHAR(50) NULL;");
    }
    catch (Exception ex)
    {
        Log.Information("Users.RealName 列初始化跳过：{Message}", ex.Message);
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("UPDATE `Tasks` t INNER JOIN `Projects` p ON t.`ProjectId` = p.`Id` SET t.`Priority` = p.`Priority`;");
    }
    catch (Exception ex)
    {
        Log.Information("Tasks.Priority 与 Projects.Priority 对齐跳过：{Message}", ex.Message);
    }

    var softDeleteColumnSql = new[]
    {
        "ALTER TABLE `Users` ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0;",
        "ALTER TABLE `Roles` ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0;",
        "ALTER TABLE `Projects` ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0;",
        "ALTER TABLE `ProjectMembers` ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0;",
        "ALTER TABLE `Tasks` ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0;",
        "ALTER TABLE `Milestones` ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0;",
        "ALTER TABLE `Files` ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0;",
        "ALTER TABLE `TaskLogs` ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0;",
        "ALTER TABLE `Alerts` ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0;"
    };

    foreach (var sql in softDeleteColumnSql)
    {
        try
        {
            dbContext.Database.ExecuteSqlRaw(sql);
        }
        catch (Exception ex)
        {
            Log.Information("逻辑删除列初始化跳过：{Message}", ex.Message);
        }
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("ALTER TABLE `Files` ADD COLUMN `ParentId` INT NULL;");
    }
    catch (Exception ex)
    {
        Log.Information("Files.ParentId 列初始化跳过：{Message}", ex.Message);
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("ALTER TABLE `Files` ADD COLUMN `IsFolder` TINYINT(1) NOT NULL DEFAULT 0;");
    }
    catch (Exception ex)
    {
        Log.Information("Files.IsFolder 列初始化跳过：{Message}", ex.Message);
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("ALTER TABLE `Files` ADD COLUMN `DeletedAt` DATETIME(6) NULL;");
    }
    catch (Exception ex)
    {
        Log.Information("Files.DeletedAt 列初始化跳过：{Message}", ex.Message);
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("ALTER TABLE `Files` ADD COLUMN `IsShared` TINYINT(1) NOT NULL DEFAULT 0;");
    }
    catch (Exception ex)
    {
        Log.Information("Files.IsShared 列初始化跳过：{Message}", ex.Message);
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("UPDATE `Files` f INNER JOIN `Projects` p ON f.`ProjectId` = p.`Id` SET f.`IsShared` = 1 WHERE p.`Name` = '共享文件夹';");
    }
    catch (Exception ex)
    {
        Log.Information("Files.IsShared 默认值回填跳过：{Message}", ex.Message);
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("CREATE INDEX `IX_Files_ProjectId_ParentId_IsDeleted` ON `Files` (`ProjectId`, `ParentId`, `IsDeleted`);");
    }
    catch (Exception ex)
    {
        Log.Information("Files 目录索引初始化跳过：{Message}", ex.Message);
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("CREATE INDEX `IX_Files_IsDeleted_IsFolder_DeletedAt` ON `Files` (`IsDeleted`, `IsFolder`, `DeletedAt`);");
    }
    catch (Exception ex)
    {
        Log.Information("Files 延迟清理索引初始化跳过：{Message}", ex.Message);
    }
}

app.Run();
