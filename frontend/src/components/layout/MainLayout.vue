<template>
  <el-container class="main-layout">
    <el-aside :width="isCollapse ? '64px' : '220px'" class="aside" :class="{ 'is-collapse': isCollapse }">
      <div class="logo">
        <el-icon size="28"><Folder /></el-icon>
        <span v-show="showLogoText" class="logo-text">项目管理</span>
      </div>
      
      <el-menu
        :default-active="activeMenu"
        :collapse="isCollapse"
        :router="true"
        background-color="#ffffff"
        text-color="#475569"
        active-text-color="#2563eb"
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

    <div class="tabs-bar">
      <el-tabs v-model="activeTab" @tab-click="handleTabClick">
        <el-tab-pane
          v-for="tab in openTabs"
          :key="tab.path"
          :label="tab.title"
          :name="tab.path"
        >
          <template #label>
            <div class="tab-label" @contextmenu.prevent="openTabContextMenu($event, tab.path)">
              <span class="tab-title" @click="handleTabLabelClick(tab.path)">{{ tab.title }}</span>
              <el-icon class="tab-close" size="14" @click.stop="closeTab(tab.path)">
                <Close />
              </el-icon>
            </div>
          </template>
        </el-tab-pane>
      </el-tabs>

      <div
        v-if="tabContextMenu.visible"
        class="tab-context-menu"
        :style="{ left: `${tabContextMenu.x}px`, top: `${tabContextMenu.y}px` }"
      >
        <div class="menu-item" @click="handleTabMenuCommand('reload')">重新加载</div>
        <div class="menu-item" :class="{ disabled: closeCurrentDisabled }" @click="handleTabMenuCommand('closeCurrent')">关闭标签页</div>
        <div class="menu-item" :class="{ disabled: closeLeftDisabled }" @click="handleTabMenuCommand('closeLeft')">关闭左侧标签页</div>
        <div class="menu-item" :class="{ disabled: closeRightDisabled }" @click="handleTabMenuCommand('closeRight')">关闭右侧标签页</div>
        <div class="menu-divider"></div>
        <div class="menu-item" :class="{ disabled: closeOthersDisabled }" @click="handleTabMenuCommand('closeOthers')">关闭其他标签页</div>
        <div class="menu-item" :class="{ disabled: closeAllDisabled }" @click="handleTabMenuCommand('closeAll')">关闭全部标签页</div>
      </div>
    </div>
      
      <el-main class="main">
        <div class="main-view">
          <router-view :key="currentViewKey" />
        </div>
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, provide } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import type { RouteLocationNormalizedLoaded } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { TabsPaneContext } from 'element-plus'
import { useAlertUnread } from '../../composables/useAlertUnread'

const router = useRouter()
const currentRoute = useRoute()

const isCollapse = ref(false)
const showLogoText = ref(true)
const user = ref<any>(null)
const { unreadCount, fetchUnreadCount, clearUnreadCount } = useAlertUnread()
let unreadCountTimer: number | null = null
let logoTextTimer: number | null = null

interface OpenTab {
  title: string
  path: string
  fullPath: string
}

const openTabs = ref<OpenTab[]>([])
const activeTab = ref('')
const viewRefreshSeed = ref(0)
const tabContextMenu = ref({
  visible: false,
  x: 0,
  y: 0,
  tabPath: ''
})

const activeMenu = computed(() => currentRoute.path)
const currentViewKey = computed(() => `${currentRoute.fullPath}__${viewRefreshSeed.value}`)
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
  if (permissions.includes('*') || permissions.includes(permission)) return true

  return permissions.some((p: string) => p.startsWith(`${permission}.`))
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

const dynamicTitleMap = ref<Record<string, string>>({})

const getRouteTitle = (route: RouteLocationNormalizedLoaded) => {
  // 如果有动态标题，优先使用
  const dynamicTitle = dynamicTitleMap.value[route.fullPath]
  if (dynamicTitle) {
    return dynamicTitle
  }

  const metaTitle = route.meta?.title
  if (typeof metaTitle === 'string' && metaTitle.trim()) {
    return metaTitle.trim()
  }

  if (typeof route.name === 'string' && route.name.trim()) {
    return route.name.trim()
  }

  return '未命名页面'
}

const setDynamicTitle = (path: string, title: string) => {
  dynamicTitleMap.value[path] = title
  // 更新已有标签的标题
  const tab = openTabs.value.find(t => t.fullPath === path || t.path === path)
  if (tab) {
    tab.title = title
  }
}

const syncOpenTabs = (route: RouteLocationNormalizedLoaded) => {
  const path = route.path
  const fullPath = route.fullPath
  const title = getRouteTitle(route)
  const existingTab = openTabs.value.find(tab => tab.path === path)

  if (!existingTab) {
    openTabs.value.push({
      title,
      path,
      fullPath
    })
  } else {
    existingTab.title = title
    existingTab.fullPath = fullPath
  }

  activeTab.value = path
}

const handleTabClick = (tab: TabsPaneContext) => {
  const targetPath = tab.paneName
  if (typeof targetPath === 'string' && targetPath && targetPath !== currentRoute.path) {
    router.push(targetPath)
  }
}

const handleTabLabelClick = (path: string) => {
  if (path !== currentRoute.path) {
    router.push(path)
  }
}

const openTabContextMenu = (event: MouseEvent, path: string) => {
  tabContextMenu.value = {
    visible: true,
    x: event.clientX,
    y: event.clientY,
    tabPath: path
  }
}

const hideTabContextMenu = () => {
  tabContextMenu.value.visible = false
}

const currentContextTabIndex = computed(() => openTabs.value.findIndex(tab => tab.path === tabContextMenu.value.tabPath))
const closeCurrentDisabled = computed(() => !tabContextMenu.value.tabPath || openTabs.value.length <= 1)
const closeLeftDisabled = computed(() => currentContextTabIndex.value <= 0)
const closeRightDisabled = computed(() => {
  const idx = currentContextTabIndex.value
  return idx === -1 || idx >= openTabs.value.length - 1
})
const closeOthersDisabled = computed(() => !tabContextMenu.value.tabPath || openTabs.value.length <= 1)
const closeAllDisabled = computed(() => openTabs.value.length === 0)

const closeTab = (path: string) => {
  const tabIndex = openTabs.value.findIndex(tab => tab.path === path)
  if (tabIndex === -1) return

  const isActiveTab = activeTab.value === path
  openTabs.value.splice(tabIndex, 1)

  if (isActiveTab) {
    if (openTabs.value.length > 0) {
      const nextTab = openTabs.value[Math.min(tabIndex, openTabs.value.length - 1)]
      activeTab.value = nextTab.path
      router.replace(nextTab.path)
    } else {
      router.replace('/')
    }
  }
}

const closeTabsByCondition = (condition: (tab: OpenTab, index: number) => boolean) => {
  const nextTabs = openTabs.value.filter((tab, index) => !condition(tab, index))
  openTabs.value = nextTabs

  if (nextTabs.length === 0) {
    activeTab.value = '/dashboard'
    router.replace('/dashboard')
    return
  }

  if (!nextTabs.some(tab => tab.path === activeTab.value)) {
    const fallbackTab = nextTabs[nextTabs.length - 1]
    activeTab.value = fallbackTab.path
    router.replace(fallbackTab.path)
  }
}

const handleTabMenuCommand = (command: 'reload' | 'closeCurrent' | 'closeLeft' | 'closeRight' | 'closeOthers' | 'closeAll') => {
  const targetPath = tabContextMenu.value.tabPath
  const targetIndex = openTabs.value.findIndex(tab => tab.path === targetPath)

  if (!targetPath || targetIndex === -1) {
    hideTabContextMenu()
    return
  }

  if (command === 'reload') {
    hideTabContextMenu()
    if (currentRoute.path !== targetPath) {
      router.push(targetPath).then(() => {
        viewRefreshSeed.value += 1
      })
      return
    }
    viewRefreshSeed.value += 1
    return
  }

  if (command === 'closeCurrent' && !closeCurrentDisabled.value) {
    closeTab(targetPath)
  }

  if (command === 'closeLeft' && !closeLeftDisabled.value) {
    closeTabsByCondition((_tab, index) => index < targetIndex)
  }

  if (command === 'closeRight' && !closeRightDisabled.value) {
    closeTabsByCondition((_tab, index) => index > targetIndex)
  }

  if (command === 'closeOthers' && !closeOthersDisabled.value) {
    closeTabsByCondition(tab => tab.path !== targetPath)
    activeTab.value = targetPath
    if (currentRoute.path !== targetPath) {
      router.replace(targetPath)
    }
  }

  if (command === 'closeAll' && !closeAllDisabled.value) {
    openTabs.value = []
    activeTab.value = '/dashboard'
    router.replace('/dashboard')
  }

  hideTabContextMenu()
}

provide('setDynamicTitle', setDynamicTitle)

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
  window.addEventListener('click', hideTabContextMenu)
})

watch(
  () => currentRoute.fullPath,
  () => {
    syncOpenTabs(currentRoute)
  },
  { immediate: true }
)

watch(
  () => isCollapse.value,
  (collapsed) => {
    if (logoTextTimer !== null) {
      window.clearTimeout(logoTextTimer)
      logoTextTimer = null
    }

    if (collapsed) {
      showLogoText.value = false
      return
    }

    logoTextTimer = window.setTimeout(() => {
      showLogoText.value = true
      logoTextTimer = null
    }, 220)
  },
  { immediate: true }
)

onUnmounted(() => {
  if (unreadCountTimer !== null) {
    window.clearInterval(unreadCountTimer)
    unreadCountTimer = null
  }
  if (logoTextTimer !== null) {
    window.clearTimeout(logoTextTimer)
    logoTextTimer = null
  }
  window.removeEventListener('click', hideTabContextMenu)
})
</script>

<style scoped>
.main-layout {
  height: 100vh;
}

.aside {
  background-color: #ffffff;
  border-right: 1px solid #e5e7eb;
  transition: width 0.3s;
  box-sizing: border-box;
  overflow-x: hidden;
}

.logo {
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #2563eb;
  font-size: 18px;
  font-weight: 600;
  gap: 10px;
  border-bottom: 1px solid #f1f5f9;
  overflow: hidden;
  white-space: nowrap;
}

.logo-text {
  white-space: nowrap;
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
  overflow: auto;
  display: flex;
  flex-direction: column;
}

.main-view {
  flex: 1;
  min-height: 100%;
}

.tabs-bar {
  background: #f5f7fa;
  border-bottom: 1px solid #d1d5db;
  padding: 6px 16px 0;
}

.tabs-bar :deep(.el-tabs__header) {
  margin: 0;
}

.tabs-bar :deep(.el-tabs__nav-wrap::after) {
  display: none;
}

.tabs-bar :deep(.el-tabs__nav) {
  border: none !important;
}

.tabs-bar :deep(.el-tabs__item) {
  padding: 0 !important;
  height: 36px;
  line-height: 36px;
  border: none !important;
}

.tabs-bar :deep(.el-tabs__item.is-active) {
  background: transparent;
}

.tabs-bar :deep(.el-tabs__item.is-active .tab-label) {
  background: #fff;
  border-color: #409eff;
  color: #409eff;
  font-weight: 500;
}

.tabs-bar :deep(.el-tabs__item.is-active .tab-close) {
  color: #409eff;
}

.tabs-bar :deep(.el-tabs__item.is-active .tab-close:hover) {
  color: #fff;
}

.tabs-bar :deep(.el-tabs__active-bar) {
  display: none;
}

.tab-label {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 14px;
  height: 32px;
  line-height: 32px;
  margin-top: 2px;
  background: #fff;
  border: 1px solid #d1d5db;
  border-bottom: none;
  border-radius: 6px 6px 0 0;
  font-size: 13px;
  color: #606266;
  cursor: pointer;
  transition: all 0.2s ease;
}

.tab-label:hover {
  color: #409eff;
  border-color: #c6e2ff;
}

.tab-title {
  max-width: 120px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  user-select: none;
}

.tab-close {
  width: 16px;
  height: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  color: #909399;
  transition: all 0.2s ease;
  flex-shrink: 0;
}

.tab-close:hover {
  color: #fff;
  background-color: #f56c6c;
}

.el-menu {
  border-right: none;
  background: transparent;
  width: 100%;
  overflow-x: hidden;
}

.aside :deep(.el-menu--collapse) {
  width: 100%;
}

.el-menu :deep(.el-menu-item),
.el-menu :deep(.el-sub-menu__title) {
  margin: 6px 10px;
  border-radius: 8px;
  transition: all 0.2s ease;
}

.el-menu :deep(.el-menu-item:hover),
.el-menu :deep(.el-sub-menu__title:hover) {
  background: #f1f5f9;
  color: #1e293b;
}

.el-menu :deep(.el-menu-item.is-active) {
  background: #eff6ff;
  color: #2563eb;
  font-weight: 600;
}

.el-menu :deep(.el-sub-menu .el-menu) {
  background: transparent;
}

.aside.is-collapse .logo {
  justify-content: center;
  padding: 0;
}

.aside.is-collapse .el-menu :deep(.el-menu-item),
.aside.is-collapse .el-menu :deep(.el-sub-menu__title) {
  width: 44px;
  height: 44px;
  margin: 6px auto;
  padding: 0 !important;
  justify-content: center;
}

.aside.is-collapse .el-menu :deep(.el-sub-menu__icon-arrow),
.aside.is-collapse .el-menu :deep(.el-menu-item .el-icon + span),
.aside.is-collapse .el-menu :deep(.el-sub-menu__title span) {
  display: none;
}

.aside.is-collapse .el-menu :deep(.el-sub-menu__title .el-icon),
.aside.is-collapse .el-menu :deep(.el-menu-item .el-icon) {
  margin-right: 0;
}

.tab-context-menu {
  position: fixed;
  z-index: 3000;
  min-width: 168px;
  background: #fff;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
  padding: 6px 0;
}

.menu-item {
  padding: 8px 14px;
  font-size: 13px;
  color: #303133;
  cursor: pointer;
  user-select: none;
}

.menu-item:hover {
  background: #ecf5ff;
  color: #409eff;
}

.menu-item.disabled {
  color: #c0c4cc;
  cursor: not-allowed;
}

.menu-item.disabled:hover {
  background: transparent;
  color: #c0c4cc;
}

.menu-divider {
  height: 1px;
  margin: 4px 0;
  background: #ebeef5;
}
</style>
