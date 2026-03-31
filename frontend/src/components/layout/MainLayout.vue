<template>
  <el-container class="main-layout">
    <el-aside :width="isCollapse ? '64px' : '220px'" class="aside">
      <div class="logo">
        <el-icon size="28"><Folder /></el-icon>
        <span v-show="!isCollapse">项目管理</span>
      </div>
      
      <el-menu
        :default-active="activeMenu"
        :collapse="isCollapse"
        :router="true"
        background-color="#304156"
        text-color="#bfcbd9"
        active-text-color="#409eff"
      >
        <template v-for="route in menuRoutes">
          <el-sub-menu v-if="route.children" :key="`sub-${route.path}`" :index="resolveMenuPath(route.path)">
            <template #title>
              <el-icon><component :is="route.meta?.icon" /></el-icon>
              <span>{{ route.meta?.title }}</span>
            </template>
            <el-menu-item
              v-for="child in route.children"
              :key="child.path"
              :index="resolveMenuPath(child.path, route.path)"
            >
              {{ child.meta?.title }}
            </el-menu-item>
          </el-sub-menu>
          
          <el-menu-item v-else :key="`item-${route.path}`" :index="resolveMenuPath(route.path)">
            <el-icon><component :is="route.meta?.icon" /></el-icon>
            <span>{{ route.meta?.title }}</span>
          </el-menu-item>
        </template>
      </el-menu>
    </el-aside>
    
    <el-container>
      <el-header class="header">
        <div class="header-left">
          <el-icon
            class="collapse-btn"
            size="20"
            @click="isCollapse = !isCollapse"
          >
            <Fold v-if="!isCollapse" />
            <Expand v-else />
          </el-icon>
          <el-breadcrumb separator="/">
            <el-breadcrumb-item :to="{ path: '/' }">首页</el-breadcrumb-item>
            <el-breadcrumb-item v-if="currentRoute.meta?.title">
              {{ currentRoute.meta.title }}
            </el-breadcrumb-item>
          </el-breadcrumb>
        </div>
        
        <div class="header-right">
          <el-badge :value="unreadCount" :hidden="unreadCount === 0" class="alert-badge">
            <el-icon size="20" @click="router.push('/alerts')">
              <Bell />
            </el-icon>
          </el-badge>
          
          <el-dropdown @command="handleCommand">
            <span class="user-info">
              <el-icon><User /></el-icon>
              {{ userDisplayName }}
              <el-icon><ArrowDown /></el-icon>
            </span>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="profile">个人设置</el-dropdown-item>
                <el-dropdown-item command="logout" divided>退出登录</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>
      
      <el-main class="main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useAlertUnread } from '../../composables/useAlertUnread'

const router = useRouter()
const currentRoute = useRoute()

const isCollapse = ref(false)
const user = ref<any>(null)
const { unreadCount, fetchUnreadCount, clearUnreadCount } = useAlertUnread()
let unreadCountTimer: number | null = null

const activeMenu = computed(() => currentRoute.path)
const userDisplayName = computed(() => {
  const realName = user.value?.realName
  if (typeof realName === 'string' && realName.trim()) {
    return realName.trim()
  }

  return '未设置姓名'
})

const hasPermission = (permission?: string) => {
  if (!permission) return true
  if (user.value?.roleName === '管理员') return true

  if (!Array.isArray(user.value?.permissions)) return false

  const permissions = user.value.permissions
  return permissions.includes(permission)
}

const canShowRoute = (route: any) => {
  if (route.meta?.hidden) return false
  return hasPermission(route.meta?.permission)
}

const menuRoutes = computed(() => {
  const routes = router.getRoutes()
  const mainRoute = routes.find(r => r.path === '/')
  if (!mainRoute || !mainRoute.children) return []

  return mainRoute.children
    .filter(r => canShowRoute(r))
    .map((route: any) => {
      if (!route.children || route.children.length === 0) {
        return route
      }

      const visibleChildren = route.children.filter((child: any) => canShowRoute(child))
      return {
        ...route,
        children: visibleChildren
      }
    })
    .filter((route: any) => !route.children || route.children.length > 0)
})

const resolveMenuPath = (path?: string, parentPath?: string) => {
  if (!path) return '/'
  if (path.startsWith('/')) return path

  if (parentPath) {
    const normalizedParent = parentPath.startsWith('/') ? parentPath : `/${parentPath}`
    return `${normalizedParent}/${path}`.replace(/\/+/g, '/')
  }

  return `/${path}`
}

const handleCommand = async (command: string) => {
  if (command === 'profile') {
    router.push('/settings/profile')
  } else if (command === 'logout') {
    await ElMessageBox.confirm('确定要退出登录吗？', '提示', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
    clearUnreadCount()
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    ElMessage.success('已退出登录')
    router.push('/login')
  }
}

onMounted(() => {
  const userStr = localStorage.getItem('user')
  if (userStr) {
    user.value = JSON.parse(userStr)
  }
  fetchUnreadCount()
  unreadCountTimer = window.setInterval(() => {
    fetchUnreadCount()
  }, 30000)
})

onUnmounted(() => {
  if (unreadCountTimer !== null) {
    window.clearInterval(unreadCountTimer)
    unreadCountTimer = null
  }
})
</script>

<style scoped>
.main-layout {
  height: 100vh;
}

.aside {
  background-color: #304156;
  transition: width 0.3s;
}

.logo {
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 18px;
  font-weight: bold;
  gap: 10px;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: #fff;
  box-shadow: 0 1px 4px rgba(0, 21, 41, 0.08);
  padding: 0 20px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 20px;
}

.collapse-btn {
  cursor: pointer;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 20px;
}

.alert-badge {
  cursor: pointer;
}

.user-info {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}

.main {
  background: #f0f2f5;
  padding: 20px;
}

.el-menu {
  border-right: none;
}
</style>
