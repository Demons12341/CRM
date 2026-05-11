import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import { ElMessage } from 'element-plus'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/login/index.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/',
    component: () => import('@/components/layout/MainLayout.vue'),
    redirect: '/dashboard',
    meta: { requiresAuth: true },
    children: [
      {
        path: 'dashboard',
        name: 'Dashboard',
        component: () => import('@/views/dashboard/index.vue'),
        meta: { title: '仪表盘', icon: 'Odometer', permission: 'dashboard' }
      },
      {
        path: 'projects',
        name: 'Projects',
        component: () => import('@/views/project/index.vue'),
        meta: { title: '项目管理', icon: 'Folder', permission: 'projects' }
      },
      {
        path: 'projects/:id',
        name: 'ProjectDetail',
        component: () => import('@/views/project/detail.vue'),
        meta: { title: '项目详情', hidden: true }
      },
      {
        path: 'tasks',
        name: 'Tasks',
        component: () => import('@/views/task/index.vue'),
        meta: { title: '任务管理', icon: 'List', permission: 'tasks' }
      },
      {
        path: 'tasks/:id',
        name: 'TaskDetail',
        component: () => import('@/views/task/detail.vue'),
        meta: { title: '任务详情', hidden: true }
      },
      {
        path: 'files',
        name: 'Files',
        component: () => import('@/views/file/index.vue'),
        meta: { title: '文件管理', icon: 'Document', permission: 'files' }
      },
      {
        path: 'alerts',
        name: 'Alerts',
        component: () => import('@/views/alert/index.vue'),
        meta: { title: '超期告警', icon: 'Bell', permission: 'alerts' }
      },
      {
        path: 'processes',
        name: 'ProcessTemplates',
        component: () => import('@/views/setting/processes.vue'),
        meta: { title: '项目任务模板', icon: 'Operation', permission: 'processes' }
      },
      {
        path: 'business-lines',
        name: 'BusinessLines',
        component: () => import('@/views/setting/business-lines.vue'),
        meta: { title: '业务线管理', icon: 'Connection', permission: 'business-lines' }
      },
      {
        path: 'settings',
        name: 'Settings',
        redirect: '/settings/users',
        meta: { title: '系统设置', icon: 'Setting', permission: 'settings' },
        children: [
          {
            path: 'users',
            name: 'Users',
            component: () => import('@/views/setting/users.vue'),
            meta: { title: '用户管理', permission: 'settings.users' }
          },
          {
            path: 'roles',
            name: 'Roles',
            component: () => import('@/views/setting/roles.vue'),
            meta: { title: '角色管理', permission: 'settings.roles' }
          },
          {
            path: 'menus',
            name: 'MenuPermissions',
            component: () => import('@/views/setting/menus.vue'),
            meta: { title: '权限管理', permission: 'settings.menu' }
          },
          {
            path: 'processes',
            name: 'SettingsProcesses',
            redirect: '/processes',
            meta: { title: '项目任务模板', hidden: true }
          },
          {
            path: 'profile',
            name: 'Profile',
            component: () => import('@/views/setting/profile.vue'),
            meta: { title: '个人设置', permission: 'settings.profile' }
          }
        ]
      }
    ]
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'NotFound',
    component: () => import('@/views/404.vue')
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

// 路由守卫
router.beforeEach((to, _from, next) => {
  const token = localStorage.getItem('token')
  const userStr = localStorage.getItem('user')
  const user = userStr ? JSON.parse(userStr) : null
  const permission = to.meta?.permission as string | undefined
  const hasPermission = !permission
    || user?.roleName === '管理员'
    || (Array.isArray(user?.permissions) && (user.permissions.includes('*') || user.permissions.includes(permission) || user.permissions.some((p: string) => p.startsWith(`${permission}.`))))
  
  if (to.meta.requiresAuth !== false && !token) {
    next('/login')
  } else if (to.path === '/login' && token) {
    next('/')
  } else if (to.meta.requiresAuth !== false && !hasPermission) {
    ElMessage.error('您没有权限访问此页面')
    next('/dashboard')
  } else {
    next()
  }
})

export default router
