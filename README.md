# 项目管理系统

一个基于 Vue 3 + ASP.NET Core Web API + MySQL 的项目管理系统，支持文件存储、项目进度跟踪、责任人管理和超期告警功能。

## 技术栈

### 后端
- **框架**: ASP.NET Core 8.0 Web API
- **ORM**: Entity Framework Core 8.0
- **数据库**: MySQL 8.0
- **认证**: JWT Bearer Token
- **日志**: Serilog
- **API文档**: Swagger/OpenAPI

### 前端
- **框架**: Vue 3 + Composition API
- **语言**: TypeScript
- **构建工具**: Vite
- **UI组件库**: Element Plus
- **状态管理**: Pinia
- **路由**: Vue Router 4
- **HTTP客户端**: Axios

## 功能特性

### 核心功能
- ✅ 用户认证与授权（JWT）
- ✅ 角色权限管理（RBAC）
- ✅ 项目管理（创建、编辑、删除、查看）
- ✅ 任务管理（创建、编辑、删除、状态跟踪）
- ✅ 里程碑管理
- ✅ 文件上传与下载
- ✅ 超期告警系统
- ✅ 数据统计仪表盘

### 项目状态
- 规划中
- 进行中
- 已完成
- 已暂停

### 任务状态
- 待办
- 进行中
- 已完成
- 已取消

### 任务优先级
- 低
- 中
- 高
- 紧急

## 项目结构

```
├── backend/                    # 后端项目
│   ├── Controllers/           # API控制器
│   ├── Models/               # 数据模型
│   │   ├── Entities/         # 实体类
│   │   └── DTOs/            # 数据传输对象
│   ├── Services/            # 业务逻辑服务
│   │   ├── Interfaces/      # 服务接口
│   │   └── Implementations/ # 服务实现
│   ├── Data/                # 数据访问层
│   ├── Middleware/          # 中间件
│   ├── Program.cs           # 程序入口
│   └── appsettings.json     # 配置文件
│
├── frontend/                   # 前端项目
│   ├── src/
│   │   ├── api/            # API请求
│   │   ├── components/     # 公共组件
│   │   ├── views/          # 页面组件
│   │   ├── router/         # 路由配置
│   │   ├── stores/         # Pinia状态管理
│   │   ├── types/          # TypeScript类型定义
│   │   └── utils/          # 工具函数
│   ├── package.json
│   └── vite.config.ts
│
└── plans/                      # 项目文档
    └── project-management-system-plan.md
```

## 快速开始

### 环境要求
- Node.js 18+
- .NET 8.0 SDK
- MySQL 8.0+

### 后端配置

1. 进入后端目录
```bash
cd backend
```

2. 修改数据库连接字符串
编辑 `appsettings.json` 文件，修改 `ConnectionStrings.DefaultConnection`：
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ProjectManagementSystem;User=root;Password=your_password;CharSet=utf8mb4;"
  }
}
```

3. 还原依赖包
```bash
dotnet restore
```

4. 运行数据库迁移（可选，系统会自动创建数据库）
```bash
dotnet ef database update
```

5. 运行后端项目
```bash
dotnet run
```

后端将运行在 `https://localhost:5000`，Swagger文档地址：`https://localhost:5000/swagger`

### 前端配置

1. 进入前端目录
```bash
cd frontend
```

2. 安装依赖
```bash
npm install
```

3. 运行前端项目
```bash
npm run dev
```

前端将运行在 `http://localhost:5173`

### 默认账号
- 用户名: `admin`
- 密码: `admin123`

## API接口

### 认证模块
- `POST /api/auth/login` - 用户登录
- `POST /api/auth/logout` - 用户登出
- `GET /api/auth/current` - 获取当前用户信息
- `POST /api/auth/change-password` - 修改密码

### 用户管理
- `GET /api/users` - 获取用户列表
 后端默认运行在 `https://localhost:62388`（HTTP 端口 `http://localhost:62389`），Swagger 文档地址：`https://localhost:62388/swagger`
- `POST /api/users` - 创建用户
- `PUT /api/users/{id}` - 更新用户
- `DELETE /api/users/{id}` - 删除用户

### 项目管理
- `GET /api/projects` - 获取项目列表
- `GET /api/projects/{id}` - 获取项目详情
- `POST /api/projects` - 创建项目
- `PUT /api/projects/{id}` - 更新项目
- `DELETE /api/projects/{id}` - 删除项目
- `GET /api/projects/{id}/members` - 获取项目成员
- `POST /api/projects/{id}/members` - 添加项目成员

 3. 配置前端代理（可选但推荐）
 ```bash
 cp .env.development.example .env.development
 ```

 可在 `frontend/.env.development` 中修改：
 ```env
 VITE_API_TARGET=https://localhost:62388
 ```
### 任务管理
- `GET /api/tasks` - 获取任务列表
- `GET /api/tasks/{id}` - 获取任务详情
- `POST /api/tasks` - 创建任务
- `PUT /api/tasks/{id}` - 更新任务
- `DELETE /api/tasks/{id}` - 删除任务
- `PUT /api/tasks/{id}/progress` - 更新任务进度
- `PUT /api/tasks/{id}/status` - 更新任务状态

### 文件管理
- `GET /api/projects/{projectId}/files` - 获取项目文件列表
- `POST /api/projects/{projectId}/files` - 上传文件
- `GET /api/files/{id}/download` - 下载文件
- `DELETE /api/files/{id}` - 删除文件

### 告警管理
- `GET /api/alerts` - 获取告警列表
- `PUT /api/alerts/{id}/read` - 标记告警已读
- `PUT /api/alerts/read-all` - 标记所有告警已读
- `GET /api/alerts/unread-count` - 获取未读告警数量

### 仪表盘
- `GET /api/dashboard/overview` - 获取概览数据
- `GET /api/dashboard/my-tasks` - 获取我的任务

## 数据库设计

### 核心表
- `Users` - 用户表
- `Roles` - 角色表
- `Projects` - 项目表
- `ProjectMembers` - 项目成员表
- `Tasks` - 任务表
- `Milestones` - 里程碑表
- `Files` - 文件表
- `TaskLogs` - 任务日志表
- `Alerts` - 告警表

## 开发计划

### 第一阶段：基础架构搭建 ✅
- [x] 创建后端项目结构
- [x] 配置数据库连接
- [x] 实现用户认证（JWT）
- [x] 实现基础中间件
- [x] 创建前端项目结构
- [x] 配置路由和状态管理
- `POST /api/projects/{projectId}/files` - 上传文件（REST风格）
- `POST /api/files/upload` - 上传文件（兼容旧前端）

### 第二阶段：核心功能开发 ✅
- [x] 实现用户管理模块
- [x] 实现角色权限管理
- [x] 实现项目管理模块
- [x] 实现任务管理模块
- [x] 实现里程碑管理
- [x] 实现文件上传下载

### 第三阶段：高级功能开发 ✅
- [x] 实现告警系统
- [x] 实现仪表盘
- [x] 实现任务日志
- [x] 实现数据统计图表

### 第四阶段：优化和完善
- [ ] 性能优化
- [ ] 安全加固
- [ ] 用户体验优化
- [ ] 测试和Bug修复

## 注意事项

1. **数据库配置**：确保MySQL服务已启动，并且连接字符串配置正确
2. **文件存储**：上传的文件默认存储在 `backend/wwwroot/uploads` 目录
3. **跨域配置**：后端已配置CORS，允许前端 `http://localhost:5173` 和 `http://localhost:3000` 访问
4. **JWT密钥**：生产环境请修改 `appsettings.json` 中的 `JwtSettings.SecretKey`

## 许可证

MIT License
