<template>
  <div class="dashboard">
    <el-row :gutter="20">
      <el-col :span="6">
        <el-card class="stat-card">
          <div class="stat-icon" style="background: #409eff;">
            <el-icon size="32">
              <Folder />
            </el-icon>
          </div>
          <div class="stat-info">
            <div class="stat-value">{{ stats.totalProjects }}</div>
            <div class="stat-label">总项目数</div>
          </div>
        </el-card>
      </el-col>

      <el-col :span="6">
        <el-card class="stat-card">
          <div class="stat-icon" style="background: #67c23a;">
            <el-icon size="32">
              <CircleCheck />
            </el-icon>
          </div>
          <div class="stat-info">
            <div class="stat-value">{{ stats.activeProjects }}</div>
            <div class="stat-label">进行中项目</div>
          </div>
        </el-card>
      </el-col>

      <el-col :span="6">
        <el-card class="stat-card">
          <div class="stat-icon" style="background: #e6a23c;">
            <el-icon size="32">
              <List />
            </el-icon>
          </div>
          <div class="stat-info">
            <div class="stat-value">{{ stats.totalTasks }}</div>
            <div class="stat-label">总任务数</div>
          </div>
        </el-card>
      </el-col>

      <el-col :span="6">
        <el-card class="stat-card clickable" @click="viewOverdueTasks">
          <div class="stat-icon" style="background: #f56c6c;">
            <el-icon size="32">
              <Warning />
            </el-icon>
          </div>
          <div class="stat-info">
            <div class="stat-value">{{ stats.overdueTasks }}</div>
            <div class="stat-label">超期任务</div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <el-row :gutter="20" style="margin-top: 20px;">
      <el-col :span="16">
        <el-card>
          <template #header>
            <div class="card-header">
              <span>我的任务</span>
              <el-button type="primary" link @click="viewMyTaskScopeAll">
                查看全部
              </el-button>
            </div>
          </template>

          <el-table :data="myTasks" style="width: 100%;">
            <el-table-column prop="title" label="任务名称" />
            <el-table-column prop="projectName" label="所属项目" width="150" />
            <el-table-column prop="priority" label="优先级" width="100">
              <template #default="{ row }">
                <el-tag :type="getPriorityType(row.priority)">
                  {{ getPriorityName(row.priority) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="status" label="状态" width="100">
              <template #default="{ row }">
                <el-tag :type="getStatusType(row.status)">
                  {{ getStatusName(row.status) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="dueDate" label="截止日期" width="120">
              <template #default="{ row }">
                <span :class="{ 'overdue': row.isOverdue }">
                  {{ formatDate(row.dueDate) }}
                </span>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-col>

      <el-col :span="8">
        <el-card>
          <template #header>
            <div class="card-header">
              <span>超期告警</span>
              <el-button type="primary" link @click="router.push('/alerts')">
                查看全部
              </el-button>
            </div>
          </template>

          <div class="alert-list">
            <div v-for="alert in recentAlerts" :key="alert.id" class="alert-item" :class="{ 'unread': !alert.isRead }">
              <el-icon :type="getAlertIconType(alert.alertType)">
                <Warning v-if="alert.alertType === 1" />
                <CircleClose v-else-if="alert.alertType === 2" />
                <InfoFilled v-else />
              </el-icon>
              <div class="alert-content">
                <div class="alert-message">
                  <el-tag :type="getAlertTagType(alert.alertType)" size="small" class="alert-type-tag">
                    {{ getAlertTypeName(alert.alertType) }}
                  </el-tag>
                  <span>{{ alert.message }}</span>
                </div>
                <div class="alert-time">{{ formatDate(alert.createdAt) }}</div>
              </div>
            </div>

            <el-empty v-if="recentAlerts.length === 0" description="暂无告警" />
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { request } from '@/api/request'
import dayjs from 'dayjs'

const router = useRouter()

const stats = ref({
  totalProjects: 0,
  activeProjects: 0,
  totalTasks: 0,
  overdueTasks: 0
})

const myTasks = ref<any[]>([])
const recentAlerts = ref<any[]>([])

const fetchDashboardData = async () => {
  try {
    const [statsRes, tasksRes, alertsRes] = await Promise.all([
      request.get('/dashboard/overview'),
      request.get('/dashboard/my-tasks'),
      request.get('/alerts?pageSize=5&alertStatus=0')
    ])

    stats.value = statsRes.data
    myTasks.value = tasksRes.data
    recentAlerts.value = alertsRes.data.items
  } catch (error) {
    console.error('获取仪表盘数据失败：', error)
  }
}

const viewOverdueTasks = () => {
  router.push({ path: '/tasks', query: { overdueOnly: 'true' } })
}

const viewMyTaskScopeAll = () => {
  router.push({ path: '/tasks', query: { myOpenScope: 'true' } })
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

const getStatusType = (status: number) => {
  const types: Record<number, string> = {
    0: 'info',
    1: 'primary',
    2: 'success',
    3: 'danger'
  }
  return types[status] || 'info'
}

const getStatusName = (status: number) => {
  const names: Record<number, string> = {
    0: '待办',
    1: '进行中',
    2: '已完成',
    3: '已取消'
  }
  return names[status] || '未知'
}

const getAlertIconType = (alertType: number) => {
  const types: Record<number, string> = {
    1: 'warning',
    2: 'error',
    3: 'info'
  }
  return types[alertType] || 'info'
}

const getAlertTypeName = (alertType: number) => {
  const names: Record<number, string> = {
    1: '任务超期',
    2: '项目超期',
    3: '进度滞后'
  }

  return names[alertType] || '未知'
}

const getAlertTagType = (alertType: number) => {
  const types: Record<number, string> = {
    1: 'danger',
    2: 'warning',
    3: 'info'
  }

  return types[alertType] || 'info'
}

const formatDate = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD')
}

onMounted(() => {
  fetchDashboardData()
})
</script>

<style scoped>
.dashboard {
  padding: 10px;
}

.stat-card {
  display: flex;
  align-items: center;
  padding: 20px;
}

.stat-card.clickable {
  cursor: pointer;
}

.stat-icon {
  width: 60px;
  height: 60px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  margin-right: 20px;
}

.stat-info {
  flex: 1;
}

.stat-value {
  font-size: 28px;
  font-weight: bold;
  color: #303133;
}

.stat-label {
  font-size: 14px;
  color: #909399;
  margin-top: 5px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.alert-list {
  max-height: 400px;
  overflow-y: auto;
}

.alert-item {
  display: flex;
  align-items: flex-start;
  padding: 12px 0;
  border-bottom: 1px solid #ebeef5;
}

.alert-item:last-child {
  border-bottom: none;
}

.alert-item.unread {
  background: #f5f7fa;
}

.alert-type-tag {
  margin-right: 8px;
}

.alert-content {
  flex: 1;
  margin-left: 12px;
}

.alert-message {
  font-size: 14px;
  color: #303133;
}

.alert-time {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}

.overdue {
  color: #f56c6c;
}
</style>
