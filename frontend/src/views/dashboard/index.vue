<template>
  <div class="dashboard">
    <!-- 欢迎栏 -->
    <div class="welcome-bar">
      <span class="welcome-text"><strong>{{ getWelcomeText() }}</strong></span>
      <span class="welcome-date">{{ formatDateFull(new Date()) }}</span>
    </div>

    <!-- 统计卡片 -->
    <div class="stats-grid">
      <div class="stat-card" @click="router.push('/projects')">
        <div class="stat-icon icon-primary">
          <el-icon>
            <Folder />
          </el-icon>
        </div>
        <div class="stat-info">
          <div class="stat-value">{{ stats.totalProjects }}</div>
          <div class="stat-label">总项目数</div>
        </div>
      </div>

      <div class="stat-card" @click="router.push('/projects')">
        <div class="stat-icon icon-success">
          <el-icon>
            <CircleCheck />
          </el-icon>
        </div>
        <div class="stat-info">
          <div class="stat-value">{{ stats.activeProjects }}</div>
          <div class="stat-label">进行中项目</div>
        </div>
      </div>

      <div class="stat-card" @click="router.push('/tasks')">
        <div class="stat-icon icon-warning">
          <el-icon>
            <List />
          </el-icon>
        </div>
        <div class="stat-info">
          <div class="stat-value">{{ stats.totalTasks }}</div>
          <div class="stat-label">总任务数</div>
        </div>
      </div>

      <div class="stat-card" :class="{ 'has-alert': stats.overdueTasks > 0 }" @click="viewOverdueTasks">
        <div class="stat-icon icon-danger">
          <el-icon>
            <Warning />
          </el-icon>
        </div>
        <div class="stat-info">
          <div class="stat-value">{{ stats.overdueTasks }}</div>
          <div class="stat-label">超期任务</div>
        </div>
        <span v-if="stats.overdueTasks > 0" class="alert-badge">{{ stats.overdueTasks }}</span>
      </div>
    </div>

    <!-- 图表区域 -->
    <div class="charts-grid">
      <div class="chart-card">
        <div class="card-header">
          <h3 class="card-title">
            <el-icon>
              <PieChart />
            </el-icon>
            项目状态分布
          </h3>
        </div>
        <div ref="projectChartRef" class="chart-container"></div>
      </div>

      <div class="chart-card">
        <div class="card-header">
          <h3 class="card-title">
            <el-icon>
              <Histogram />
            </el-icon>
            任务状态统计
          </h3>
        </div>
        <div ref="taskChartRef" class="chart-container"></div>
      </div>
    </div>

    <!-- 任务和告警 -->
    <div class="content-grid">
      <!-- 我的任务 -->
      <div class="content-card">
        <div class="card-header">
          <h3 class="card-title">
            <el-icon>
              <Calendar />
            </el-icon>
            我的任务
            <span class="task-count">{{ myTasks.length }}</span>
          </h3>
          <el-button type="primary" text @click="viewMyTaskScopeAll">
            查看全部
            <el-icon class="el-icon--right">
              <ArrowRight />
            </el-icon>
          </el-button>
        </div>

        <div class="card-body">
          <div v-if="myTasks.length > 0" class="task-list">
            <div v-for="task in myTasks.slice(0, 5)" :key="task.id" class="task-item" @click="viewTask(task.id)">
              <div class="task-priority" :class="getPriorityClass(task.priority)"></div>
              <div class="task-content">
                <div class="task-title">{{ task.title }}</div>
                <div class="task-meta">
                  <span class="task-project">{{ task.projectName || '未分配项目' }}</span>
                  <span class="task-date" :class="{ 'overdue': task.isOverdue }">{{ formatDate(task.dueDate) }}</span>
                </div>
              </div>
              <el-tag :type="getPriorityType(task.priority)" size="small">
                {{ getPriorityName(task.priority) }}
              </el-tag>
            </div>
          </div>
          <el-empty v-else description="暂无任务" :image-size="80" />
        </div>
      </div>

      <!-- 超期告警 -->
      <div class="content-card">
        <div class="card-header">
          <h3 class="card-title">
            <el-icon>
              <Bell />
            </el-icon>
            超期告警
            <span v-if="recentAlerts.filter(a => !a.isRead).length > 0" class="alert-count">
              {{recentAlerts.filter(a => !a.isRead).length}}
            </span>
          </h3>
          <el-button type="primary" text @click="router.push('/alerts')">全部</el-button>
        </div>

        <div class="card-body">
          <div v-if="recentAlerts.length > 0" class="alert-list">
            <div v-for="alert in recentAlerts.slice(0, 6)" :key="alert.id" class="alert-item"
              :class="{ 'unread': !alert.isRead }">
              <div class="alert-icon" :class="getAlertBgClass(alert.alertType)">
                <el-icon size="14">
                  <Warning v-if="alert.alertType === 1" />
                  <CircleClose v-else-if="alert.alertType === 2" />
                  <InfoFilled v-else />
                </el-icon>
              </div>
              <div class="alert-content">
                <div class="alert-message">{{ alert.message }}</div>
                <div class="alert-time">{{ formatRelativeTime(alert.createdAt) }}</div>
              </div>
            </div>
          </div>
          <el-empty v-else description="暂无告警" :image-size="80" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { request } from '@/api/request'
import dayjs from 'dayjs'
import * as echarts from 'echarts'

const router = useRouter()

interface DashboardStats {
  totalProjects: number
  activeProjects: number
  totalTasks: number
  overdueTasks: number
  projectStatusCounts: Record<string, number>
  taskStatusCounts: Record<string, number>
}

const stats = ref<DashboardStats>({
  totalProjects: 0,
  activeProjects: 0,
  totalTasks: 0,
  overdueTasks: 0,
  projectStatusCounts: {},
  taskStatusCounts: {}
})

const myTasks = ref<any[]>([])
const recentAlerts = ref<any[]>([])
const projectChartRef = ref<HTMLElement>()
const taskChartRef = ref<HTMLElement>()
const projectChart = ref<echarts.ECharts | null>(null)
const taskChart = ref<echarts.ECharts | null>(null)

const fetchDashboardData = async () => {
  try {
    const [statsRes, tasksRes, alertsRes] = await Promise.all([
      request.get('/dashboard/overview'),
      request.get('/dashboard/my-tasks'),
      request.get('/alerts?pageSize=6&alertStatus=0')
    ])

    stats.value = {
      totalProjects: statsRes.data?.totalProjects ?? 0,
      activeProjects: statsRes.data?.activeProjects ?? 0,
      totalTasks: statsRes.data?.totalTasks ?? 0,
      overdueTasks: statsRes.data?.overdueTasks ?? 0,
      projectStatusCounts: statsRes.data?.projectStatusCounts ?? {},
      taskStatusCounts: statsRes.data?.taskStatusCounts ?? {}
    }
    myTasks.value = tasksRes.data || []
    recentAlerts.value = alertsRes.data.items || []

    await nextTick()
    initCharts()
  } catch (error) {
    console.error('获取仪表盘数据失败：', error)
  }
}

const initCharts = () => {
  const getStatusCount = (source: Record<string, number>, status: number) => {
    const value = source[String(status)]
    return typeof value === 'number' ? value : 0
  }

  // 项目状态分布饼图
  if (projectChartRef.value) {
    if (!projectChart.value) {
      projectChart.value = echarts.init(projectChartRef.value)
    }

    const projectOption = {
      tooltip: {
        trigger: 'item',
        formatter: '{b}: {c} ({d}%)'
      },
      legend: {
        bottom: '0',
        left: 'center',
        itemWidth: 10,
        itemHeight: 10,
        textStyle: {
          fontSize: 12,
          color: '#666'
        }
      },
      series: [
        {
          name: '项目状态',
          type: 'pie',
          radius: ['45%', '70%'],
          center: ['50%', '42%'],
          avoidLabelOverlap: false,
          itemStyle: {
            borderRadius: 4,
            borderColor: '#fff',
            borderWidth: 2
          },
          label: {
            show: false
          },
          emphasis: {
            label: {
              show: true,
              fontSize: 14,
              fontWeight: 'bold'
            }
          },
          labelLine: {
            show: false
          },
          data: [
            { value: getStatusCount(stats.value.projectStatusCounts, 0), name: '规划中', itemStyle: { color: '#94a3b8' } },
            { value: getStatusCount(stats.value.projectStatusCounts, 1), name: '进行中', itemStyle: { color: '#3b82f6' } },
            { value: getStatusCount(stats.value.projectStatusCounts, 2), name: '已完成', itemStyle: { color: '#22c55e' } },
            { value: getStatusCount(stats.value.projectStatusCounts, 3), name: '已暂停', itemStyle: { color: '#f59e0b' } }
          ]
        }
      ]
    }
    projectChart.value.setOption(projectOption, true)
  }

  // 任务状态统计柱状图
  if (taskChartRef.value) {
    if (!taskChart.value) {
      taskChart.value = echarts.init(taskChartRef.value)
    }

    const taskOption = {
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' }
      },
      grid: {
        left: '3%',
        right: '4%',
        bottom: '3%',
        top: '10%',
        containLabel: true
      },
      xAxis: [
        {
          type: 'category',
          data: ['待办', '进行中', '已完成', '已取消'],
          axisTick: { alignWithLabel: true },
          axisLine: { lineStyle: { color: '#e5e7eb' } },
          axisLabel: { color: '#666' }
        }
      ],
      yAxis: [
        {
          type: 'value',
          axisLine: { show: false },
          axisTick: { show: false },
          splitLine: { lineStyle: { color: '#f3f4f6' } },
          axisLabel: { color: '#666' }
        }
      ],
      series: [
        {
          name: '任务数',
          type: 'bar',
          barWidth: '36%',
          data: [
            { value: getStatusCount(stats.value.taskStatusCounts, 0), itemStyle: { color: '#94a3b8' } },
            { value: getStatusCount(stats.value.taskStatusCounts, 1), itemStyle: { color: '#3b82f6' } },
            { value: getStatusCount(stats.value.taskStatusCounts, 2), itemStyle: { color: '#22c55e' } },
            { value: getStatusCount(stats.value.taskStatusCounts, 3), itemStyle: { color: '#ef4444' } }
          ],
          itemStyle: { borderRadius: [4, 4, 0, 0] }
        }
      ]
    }
    taskChart.value.setOption(taskOption, true)
  }
}

const viewOverdueTasks = () => {
  router.push({ path: '/tasks', query: { overdueOnly: 'true' } })
}

const viewMyTaskScopeAll = () => {
  router.push({ path: '/tasks', query: { myOpenScope: 'true' } })
}

const viewTask = (id: number) => {
  router.push(`/tasks/${id}`)
}

const getPriorityClass = (priority: number) => {
  const classes: Record<number, string> = {
    1: 'priority-low',
    2: 'priority-medium',
    3: 'priority-high',
    4: 'priority-urgent'
  }
  return classes[priority] || 'priority-low'
}

const getPriorityType = (priority: number) => {
  const types: Record<number, string> = {
    1: 'info',
    2: 'warning',
    3: 'danger',
    4: 'danger'
  }
  return types[priority] || 'info'
}

const getPriorityName = (priority: number) => {
  const names: Record<number, string> = {
    1: '低',
    2: '中',
    3: '高',
    4: '紧急'
  }
  return names[priority] || '未知'
}

const getAlertBgClass = (alertType: number) => {
  const classes: Record<number, string> = {
    1: 'bg-danger',
    2: 'bg-warning',
    3: 'bg-info'
  }
  return classes[alertType] || 'bg-info'
}

const formatDate = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('MM-DD')
}

const formatDateFull = (date: Date) => {
  const weekdays = ['周日', '周一', '周二', '周三', '周四', '周五', '周六']
  const month = date.getMonth() + 1
  const day = date.getDate()
  const weekday = weekdays[date.getDay()]
  return `${month}月${day}日 ${weekday}`
}

const getWelcomeText = () => {
  const hour = new Date().getHours()
  if (hour < 12) return '上午好'
  if (hour < 18) return '下午好'
  return '晚上好'
}

const formatRelativeTime = (date: string) => {
  if (!date) return '-'
  const now = dayjs()
  const target = dayjs(date)
  const diffMinutes = now.diff(target, 'minute')
  const diffHours = now.diff(target, 'hour')
  const diffDays = now.diff(target, 'day')

  if (diffMinutes < 1) return '刚刚'
  if (diffMinutes < 60) return `${diffMinutes}分钟前`
  if (diffHours < 24) return `${diffHours}小时前`
  if (diffDays < 7) return `${diffDays}天前`
  return formatDate(date)
}

onMounted(() => {
  fetchDashboardData()
  window.addEventListener('resize', () => {
    projectChart.value?.resize()
    taskChart.value?.resize()
  })
})
</script>

<style scoped>
.dashboard {
  --biz-text-strong: #0f3b8c;
  --biz-text-muted: #5f6b7a;
  padding: 12px;
  background: #f5f8fc;
  min-height: 100%;
  box-sizing: border-box;
}

/* 欢迎栏 */
.welcome-bar {
  margin-bottom: 14px;
  padding: 14px 16px;
  border: 1px solid #dce8fb;
  border-radius: 16px;
  background: #f4f9ff;
  box-shadow: 0 10px 22px rgba(36, 66, 135, 0.08);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.welcome-text {
  font-size: 21px;
  color: var(--biz-text-muted);
}

.welcome-text strong {
  color: var(--biz-text-strong);
  font-weight: 800;
}

.welcome-date {
  font-size: 12px;
  color: #72839a;
}

/* 统计卡片网格 */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
  margin-bottom: 14px;
}

@media (max-width: 1200px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 640px) {
  .stats-grid {
    grid-template-columns: 1fr;
  }
}

.stat-card {
  background: #ffffff;
  border-radius: 14px;
  padding: 16px;
  display: flex;
  align-items: center;
  cursor: pointer;
  transition: all 0.2s ease;
  position: relative;
  border: 1px solid #dce8fb;
  box-shadow: 0 10px 20px rgba(31, 59, 115, 0.08);
}

.stat-card:hover {
  border-color: #8fb4ff;
  box-shadow: 0 14px 24px rgba(34, 70, 136, 0.14);
}

.stat-card.has-alert {
  border-color: #fecaca;
}

.stat-card.has-alert:hover {
  border-color: #fca5a5;
}

.stat-icon {
  width: 44px;
  height: 44px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 12px;
  font-size: 20px;
}

.icon-primary {
  background: #eff6ff;
  color: #3b82f6;
}

.icon-success {
  background: #f0fdf4;
  color: #22c55e;
}

.icon-warning {
  background: #fffbeb;
  color: #f59e0b;
}

.icon-danger {
  background: #fef2f2;
  color: #ef4444;
}

.stat-info {
  flex: 1;
}

.stat-value {
  font-size: 26px;
  font-weight: 700;
  color: #1d3250;
  line-height: 1;
  margin-bottom: 6px;
}

.stat-label {
  font-size: 13px;
  color: #60718a;
}

.alert-badge {
  position: absolute;
  top: 12px;
  right: 12px;
  min-width: 18px;
  height: 18px;
  padding: 0 5px;
  background: #ef4444;
  color: #fff;
  font-size: 11px;
  font-weight: 500;
  border-radius: 9px;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 图表区域 */
.charts-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
  margin-bottom: 14px;
}

@media (max-width: 900px) {
  .charts-grid {
    grid-template-columns: 1fr;
  }
}

.chart-card {
  background: #ffffff;
  border-radius: 14px;
  border: 1px solid #dce8fb;
  overflow: hidden;
  box-shadow: 0 10px 20px rgba(31, 59, 115, 0.08);
}

.chart-container {
  height: 260px;
  padding: 0 14px 14px;
}

/* 内容区域 */
.content-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}

@media (max-width: 900px) {
  .content-grid {
    grid-template-columns: 1fr;
  }
}

.content-card {
  background: #ffffff;
  border-radius: 14px;
  border: 1px solid #dce8fb;
  display: flex;
  flex-direction: column;
  box-shadow: 0 10px 20px rgba(31, 59, 115, 0.08);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 16px;
  border-bottom: 1px solid #e4ecfa;
}

.card-title {
  font-size: 14px;
  font-weight: 700;
  color: #1d3250;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.card-title .el-icon {
  color: #476791;
}

.task-count,
.alert-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 20px;
  height: 20px;
  padding: 0 6px;
  background: #edf3ff;
  color: #395171;
  font-size: 12px;
  font-weight: 500;
  border-radius: 10px;
  margin-left: 4px;
}

.alert-count {
  background: #fef2f2;
  color: #ef4444;
}

.card-body {
  flex: 1;
  padding: 12px;
  min-height: 286px;
}

/* 任务列表 */
.task-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.task-item {
  display: flex;
  align-items: center;
  padding: 12px;
  border-radius: 10px;
  cursor: pointer;
  transition: background 0.15s ease, border-color 0.15s ease;
  gap: 12px;
  border: 1px solid transparent;
}

.task-item:hover {
  background: #f3f8ff;
  border-color: #dce8fb;
}

.task-priority {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex-shrink: 0;
}

.priority-low {
  background: #94a3b8;
}

.priority-medium {
  background: #f59e0b;
}

.priority-high {
  background: #ef4444;
}

.priority-urgent {
  background: #dc2626;
}

.task-content {
  flex: 1;
  min-width: 0;
}

.task-title {
  font-size: 14px;
  color: #1d3250;
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.task-meta {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: #71839b;
}

.task-project {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 120px;
}

.task-date.overdue {
  color: #ef4444;
}

/* 告警列表 */
.alert-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.alert-item {
  display: flex;
  align-items: flex-start;
  padding: 12px;
  border-radius: 10px;
  cursor: pointer;
  transition: background 0.15s ease, border-color 0.15s ease;
  gap: 12px;
  border: 1px solid transparent;
}

.alert-item:hover {
  background: #f3f8ff;
  border-color: #dce8fb;
}

.alert-item.unread {
  background: #fef2f2;
}

.alert-item.unread:hover {
  background: #fee2e2;
}

.alert-icon {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  color: #fff;
}

.bg-danger {
  background: #ef4444;
}

.bg-warning {
  background: #f59e0b;
}

.bg-info {
  background: #3b82f6;
}

.alert-content {
  flex: 1;
  min-width: 0;
}

.alert-message {
  font-size: 13px;
  color: #28405f;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.alert-time {
  font-size: 11px;
  color: #71839b;
  margin-top: 4px;
}

.dashboard :deep(.el-button) {
  border-radius: 999px;
}

/* 响应式调整 */
@media (max-width: 768px) {
  .dashboard {
    padding: 8px;
  }

  .stat-value {
    font-size: 24px;
  }

  .card-header {
    padding: 12px 14px;
  }

  .card-body {
    padding: 10px;
  }

  .welcome-bar {
    padding: 12px;
  }
}
</style>
