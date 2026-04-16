<template>
  <div class="project-detail-page">
    <el-card v-loading="loading">
      <template #header>
        <div class="card-header">
          <span>项目详情</span>
          <el-button link type="primary" @click="goBack">返回项目列表</el-button>
        </div>
      </template>

      <el-descriptions v-if="project" :column="2" border>
        <el-descriptions-item label="项目ID">{{ project.id }}</el-descriptions-item>
        <el-descriptions-item label="项目名称">{{ project.name }}</el-descriptions-item>
        <el-descriptions-item label="负责人">{{ project.managerName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="优先级">
          <el-tag :type="getPriorityType(project.priority)">{{ project.priorityName || getPriorityName(project.priority)
          }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="状态">
          <el-tag :type="getStatusType(project.status)">{{ project.statusName }}</el-tag>
          <el-tag class="status-extra-tag" :type="projectOverallTagType">{{ projectOverallTagText }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="整体拖期">
          <el-tag :type="isProjectOverallOverdue(project) ? 'danger' : 'success'">
            {{ isProjectOverallOverdue(project) ? '是' : '否' }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="开始日期">{{ formatDate(project.startDate) }}</el-descriptions-item>
        <el-descriptions-item label="结束日期">{{ formatDate(project.endDate) }}</el-descriptions-item>
        <el-descriptions-item label="整体进度">
          <el-progress :percentage="Number(project.progress || 0)" :stroke-width="10" />
        </el-descriptions-item>
        <el-descriptions-item label="任务数">{{ project.taskCount }}</el-descriptions-item>
        <el-descriptions-item label="描述" :span="2">{{ project.description || '-' }}</el-descriptions-item>
      </el-descriptions>

      <el-divider content-position="left">任务甘特图</el-divider>
      <div v-if="ganttRows.length" class="gantt-wrapper">
        <div class="gantt-legend">
          <div class="gantt-legend-item"><span class="legend-dot normal-completed" />绿色：正常完成</div>
          <div class="gantt-legend-item"><span class="legend-dot delayed-completed" />黄色：拖期完成</div>
          <div class="gantt-legend-item"><span class="legend-dot delayed-unfinished" />红色：拖期未完成</div>
          <div class="gantt-legend-item"><span class="legend-dot normal-progress" />蓝色：正常进度/未来任务</div>
        </div>
        <div class="gantt-chart" :style="{ '--now-ratio': `${nowRatio}` }">
          <el-tooltip v-if="showNowLine" :content="`当前时间：${currentTimeText}`" placement="top" effect="dark">
            <div class="gantt-now-line" />
          </el-tooltip>
          <div class="gantt-table">
            <div class="gantt-table-head">
              <div class="gantt-head-cell">任务信息</div>
              <div class="gantt-head-cell">时间轴</div>
              <div class="gantt-head-cell">状态信息</div>
            </div>
            <div class="gantt-axis-row">
              <div class="gantt-axis-side" />
              <div class="gantt-axis">
                <div v-for="tick in timelineTicks" :key="tick.label" class="gantt-axis-tick"
                  :style="{ left: `${tick.left}%` }">
                  <span class="gantt-axis-label">{{ tick.label }}</span>
                </div>
              </div>
              <div class="gantt-axis-side" />
            </div>
            <div class="gantt-body">
              <div v-for="item in ganttRows" :key="item.id" class="gantt-row">
                <div class="gantt-info gantt-info-clickable" @click="goToTaskDetail(item.id)">
                  <div class="gantt-label" :title="item.title">{{ item.title }}</div>
                  <div class="gantt-click-hint">点击查看任务详情</div>
                  <div class="gantt-subtext">责任人：{{ item.assigneeText }}</div>
                </div>
                <div class="gantt-track">
                  <el-tooltip :content="getGanttTimeTooltip(item)" placement="top" effect="dark">
                    <div class="gantt-bar" :class="getGanttBarClass(item)"
                      :style="{ left: `${item.left}%`, width: `${item.width}%` }" />
                  </el-tooltip>
                </div>
                <div class="gantt-meta">
                  <div class="gantt-date">{{ formatDate(item.startDate) }} ~ {{ formatDate(item.dueDate) }}</div>
                  <div class="gantt-tags">
                    <el-tag size="small" :type="getTaskStatusType(item.status)">{{ getTaskStatusName(item.status)
                    }}</el-tag>
                    <el-tag size="small" :type="item.isStarted ? 'primary' : 'info'">{{ item.isStarted ? '已开始' : '未开始'
                    }}</el-tag>
                    <el-tag size="small" :type="item.isCompleted ? 'success' : 'warning'">{{ item.isCompleted ? '已完成' :
                      '未完成'
                    }}</el-tag>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
      <el-empty v-else description="暂无可展示的任务时间数据" />

      <div class="member-section-header">
        <el-divider content-position="left">项目成员</el-divider>
        <el-button v-if="canManageMembers" type="primary" @click="openMemberDialog">添加项目成员</el-button>
      </div>
      <el-table v-if="sortedMembers.length" :data="sortedMembers" style="width: 100%">
        <el-table-column prop="username" label="用户名" width="180" />
        <el-table-column prop="phone" label="电话" width="160">
          <template #default="{ row }">{{ row.phone || '-' }}</template>
        </el-table-column>
        <el-table-column prop="role" label="项目角色" width="120" />
        <el-table-column prop="joinedAt" label="加入时间" width="180">
          <template #default="{ row }">{{ formatDateTime(row.joinedAt) }}</template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="暂无项目成员" />

      <el-dialog v-model="memberDialogVisible" title="添加项目成员" width="460px">
        <el-form ref="memberFormRef" :model="memberForm" :rules="memberRules" label-width="90px">
          <el-form-item label="选择成员" prop="userId">
            <el-select v-model="memberForm.userId" placeholder="请选择成员" filterable clearable style="width: 100%">
              <el-option
                v-for="user in availableUsers"
                :key="user.id"
                :label="getUserDisplayName(user)"
                :value="user.id"
              />
            </el-select>
          </el-form-item>
          <el-form-item label="项目角色" prop="role">
            <el-input v-model="memberForm.role" maxlength="50" placeholder="请输入角色，如：成员" />
          </el-form-item>
        </el-form>
        <template #footer>
          <el-button @click="memberDialogVisible = false">取消</el-button>
          <el-button type="primary" :loading="addingMember" @click="submitAddMember">确定</el-button>
        </template>
      </el-dialog>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { request } from '@/api/request'
import dayjs from 'dayjs'
import { ElMessage } from 'element-plus'

const route = useRoute()
const router = useRouter()

const projectId = computed(() => Number(route.params.id))
const loading = ref(false)
const project = ref<any>(null)
const members = ref<any[]>([])
const tasks = ref<any[]>([])
const userOptions = ref<any[]>([])
const memberDialogVisible = ref(false)
const addingMember = ref(false)
const permissionWarningShown = ref(false)
const memberFormRef = ref()
const memberForm = ref({
  userId: undefined as number | undefined,
  role: '成员'
})
const memberRules = {
  userId: [{ required: true, message: '请选择成员', trigger: 'change' }],
  role: [{ required: true, message: '请输入项目角色', trigger: 'blur' }]
}

const currentUser = computed(() => {
  try {
    const raw = localStorage.getItem('user')
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
})

const canManageMembers = computed(() => {
  if (!project.value || !currentUser.value) return false
  if (currentUser.value.roleName === '管理员') return true
  return Number(project.value.managerId) === Number(currentUser.value.id)
})

const timelineStart = computed(() => {
  const starts = tasks.value
    .map((item) => dayjs(item.startDate || item.createdAt).startOf('day'))
    .filter((item) => item.isValid())
  if (!starts.length) return null
  return starts.reduce((min, current) => (current.isBefore(min) ? current : min), starts[0])
})

const timelineEnd = computed(() => {
  const ends = tasks.value
    .map((item) => dayjs(item.dueDate || item.completedAt || item.startDate || item.createdAt).endOf('day'))
    .filter((item) => item.isValid())
  if (!ends.length) return null
  return ends.reduce((max, current) => (current.isAfter(max) ? current : max), ends[0])
})

const totalDays = computed(() => {
  if (!timelineStart.value || !timelineEnd.value) return 1
  const value = timelineEnd.value.diff(timelineStart.value, 'day') + 1
  return value > 0 ? value : 1
})

const timelineTicks = computed(() => {
  if (!timelineStart.value || !timelineEnd.value) return []

  const tickCount = 6
  const daySpan = Math.max(1, timelineEnd.value.diff(timelineStart.value, 'day'))

  return Array.from({ length: tickCount + 1 }, (_, index) => {
    const ratio = index / tickCount
    const offset = Math.round(daySpan * ratio)
    return {
      left: ratio * 100,
      label: timelineStart.value!.add(offset, 'day').format('MM-DD')
    }
  })
})

const nowRatio = computed(() => {
  if (!timelineStart.value || !timelineEnd.value) return 0

  const start = timelineStart.value.valueOf()
  const end = timelineEnd.value.valueOf()
  if (end <= start) return 0

  const ratio = (dayjs().valueOf() - start) / (end - start)
  return Math.max(0, Math.min(1, ratio))
})

const showNowLine = computed(() => !!timelineStart.value && !!timelineEnd.value)

const isProjectOverallDelayed = computed(() => {
  if (!project.value?.endDate) return false
  if (project.value.status === 2 || project.value.status === 3) return false
  return dayjs().isAfter(dayjs(project.value.endDate).endOf('day'))
})

const projectOverallTagText = computed(() => (isProjectOverallDelayed.value ? '整体拖期' : '正常运行'))
const projectOverallTagType = computed(() => (isProjectOverallDelayed.value ? 'danger' : 'success'))

const ganttRows = computed(() => {
  if (!timelineStart.value || !timelineEnd.value) return []

  const now = dayjs()
  const baseline = timelineStart.value
  const days = totalDays.value

  return [...tasks.value]
    .filter((item) => !!(item.startDate || item.createdAt) && !!(item.dueDate || item.completedAt || item.startDate || item.createdAt))
    .sort((left, right) => {
      const leftPriority = Number(left.priority || 0)
      const rightPriority = Number(right.priority || 0)
      if (leftPriority !== rightPriority) {
        return rightPriority - leftPriority
      }
      const leftDue = dayjs(left.dueDate || left.completedAt || left.startDate || left.createdAt)
      const rightDue = dayjs(right.dueDate || right.completedAt || right.startDate || right.createdAt)
      if (!leftDue.isValid() && !rightDue.isValid()) return 0
      if (!leftDue.isValid()) return 1
      if (!rightDue.isValid()) return -1
      return leftDue.valueOf() - rightDue.valueOf()
    })
    .map((item) => {
      const start = dayjs(item.startDate || item.createdAt).startOf('day')
      const end = dayjs(item.dueDate || item.completedAt || item.startDate || item.createdAt).endOf('day')
      const safeEnd = end.isBefore(start) ? start.endOf('day') : end

      const offsetDays = start.diff(baseline, 'day')
      const durationDays = safeEnd.diff(start, 'day') + 1
      const left = Math.max(0, Math.min(100, (offsetDays / days) * 100))
      const width = Math.max(1.2, Math.min(100 - left, (durationDays / days) * 100))

      const due = dayjs(item.dueDate)
      const completedAt = dayjs(item.completedAt)
      const isOverdue = due.isValid() && due.endOf('day').isBefore(now)
      const isDelayedCompleted = item.status === 2 && due.isValid() && completedAt.isValid() && completedAt.isAfter(due.endOf('day'))
      const isDelayedUnfinished = item.status !== 2 && item.status !== 3 && isOverdue
      const isNormalCompleted = item.status === 2 && !isDelayedCompleted

      return {
        ...item,
        assigneeText: item.assigneeDisplay || item.assigneeName || '-',
        isStarted: item.status !== 0,
        isCompleted: item.status === 2,
        isNormalCompleted,
        isDelayedCompleted,
        isDelayedUnfinished,
        left,
        width,
        isOverdue
      }
    })
})

const sortedMembers = computed(() => {
  const list = members.value || []
  const managerId = project.value?.managerId

  if (!managerId || !list.length) {
    return list
  }

  return [...list].sort((left, right) => {
    const leftIsManager = left.userId === managerId
    const rightIsManager = right.userId === managerId

    if (leftIsManager && !rightIsManager) return -1
    if (!leftIsManager && rightIsManager) return 1
    return 0
  })
})

const fetchProjectDetail = async () => {
  loading.value = true
  try {
    const res = await request.get(`/projects/${projectId.value}`, {
      headers: { 'X-Silent-Error': '1' }
    })
    project.value = res.data
  } catch (error) {
    const status = (error as any)?.response?.status
    if (status === 403 && !permissionWarningShown.value) {
      permissionWarningShown.value = true
      ElMessage.warning('暂无该项目访问权限')
    }
    console.error('获取项目详情失败：', error)
  } finally {
    loading.value = false
  }
}

const fetchMembers = async () => {
  try {
    const res = await request.get(`/projects/${projectId.value}/members`, {
      headers: { 'X-Silent-Error': '1' }
    })
    members.value = res.data || []
  } catch (error) {
    const status = (error as any)?.response?.status
    if (status === 403 && !permissionWarningShown.value) {
      permissionWarningShown.value = true
      ElMessage.warning('暂无该项目访问权限')
    }
    console.error('获取项目成员失败：', error)
  }
}

const fetchUsers = async () => {
  try {
    const res = await request.get('/users', { params: { page: 1, pageSize: 200 } })
    userOptions.value = Array.isArray(res.data?.items) ? res.data.items : []
  } catch (error) {
    console.error('获取用户列表失败：', error)
  }
}

const resetMemberForm = () => {
  memberForm.value = {
    userId: undefined,
    role: '成员'
  }
  memberFormRef.value?.clearValidate?.()
}

const openMemberDialog = () => {
  if (!canManageMembers.value) {
    ElMessage.warning('暂无项目成员管理权限')
    return
  }
  resetMemberForm()
  memberDialogVisible.value = true
}

const availableUsers = computed(() => {
  const existingIds = new Set((members.value || []).map((item) => Number(item.userId)))
  return userOptions.value.filter((user) => !existingIds.has(Number(user.id)))
})

const getUserDisplayName = (user: any) => {
  if (!user) return '-'
  return user.realName ? `${user.realName} (${user.username})` : user.username
}

const submitAddMember = async () => {
  if (!canManageMembers.value) {
    ElMessage.warning('暂无项目成员管理权限')
    return
  }

  if (!memberFormRef.value) return
  await memberFormRef.value.validate(async (valid: boolean) => {
    if (!valid) return
    addingMember.value = true
    try {
      await request.post(`/projects/${projectId.value}/members`, {
        userId: memberForm.value.userId,
        role: memberForm.value.role || '成员'
      })
      ElMessage.success('添加成员成功')
      memberDialogVisible.value = false
      await fetchMembers()
    } catch (error) {
      const status = (error as any)?.response?.status
      if (status === 403) {
        ElMessage.warning('暂无项目成员管理权限')
      }
      console.error('添加项目成员失败：', error)
    } finally {
      addingMember.value = false
    }
  })
}

const fetchProjectTasks = async () => {
  try {
    const allTasks: any[] = []
    const pageSize = 200
    let page = 1
    let totalCount = 0

    do {
      const res = await request.get('/tasks', {
        params: {
          projectId: projectId.value,
          page,
          pageSize
        },
        headers: { 'X-Silent-Error': '1' }
      })

      const items = Array.isArray(res.data?.items) ? res.data.items : []
      totalCount = Number(res.data?.totalCount || 0)
      allTasks.push(...items)

      if (!items.length) {
        break
      }

      page += 1
    } while (allTasks.length < totalCount)

    tasks.value = allTasks
  } catch (error) {
    const status = (error as any)?.response?.status
    if (status === 403 && !permissionWarningShown.value) {
      permissionWarningShown.value = true
      ElMessage.warning('暂无该项目访问权限')
    }
    console.error('获取项目任务失败：', error)
  }
}

const getStatusType = (status: number) => {
  const types: Record<number, string> = {
    0: 'info',
    1: 'primary',
    2: 'success',
    3: 'warning'
  }
  return types[status] || 'info'
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

const getTaskStatusType = (status: number) => {
  const types: Record<number, string> = {
    0: 'info',
    1: 'primary',
    2: 'success',
    3: 'danger'
  }
  return types[status] || 'info'
}

const getTaskStatusName = (status: number) => {
  const names: Record<number, string> = {
    0: '待办',
    1: '进行中',
    2: '已完成',
    3: '已取消'
  }
  return names[status] || '未知'
}

const isProjectOverallOverdue = (projectItem: any) => {
  if (!projectItem?.endDate) return false
  if (projectItem.status === 2 || projectItem.status === 3) return false
  return dayjs(projectItem.endDate).endOf('day').isBefore(dayjs())
}

const formatDate = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD')
}

const formatDateTime = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}

const currentTimeText = computed(() => dayjs().format('YYYY-MM-DD HH:mm:ss'))

const getGanttTimeTooltip = (item: any) => {
  const title = item?.title || '任务'
  const startText = formatDate(item?.startDate)
  const dueText = formatDate(item?.dueDate)
  const completedText = formatDate(item?.completedAt)
  return `${title}｜计划开始：${startText}｜计划截止：${dueText}｜实际完成：${completedText}`
}

const getGanttBarClass = (item: any) => {
  if (item?.isDelayedCompleted) return 'delayed-completed'
  if (item?.isDelayedUnfinished) return 'delayed-unfinished'
  if (item?.isNormalCompleted) return 'normal-completed'
  return 'normal-progress'
}

const goBack = () => {
  router.push('/projects')
}

const goToTaskDetail = (taskId: number) => {
  if (!Number.isFinite(taskId) || taskId <= 0) {
    return
  }

  router.push(`/tasks/${taskId}`)
}

onMounted(async () => {
  await Promise.all([fetchProjectDetail(), fetchMembers(), fetchProjectTasks(), fetchUsers()])
})
</script>

<style scoped>
.project-detail-page {
  padding: 10px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.status-extra-tag {
  margin-left: 8px;
}

.member-section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.member-section-header :deep(.el-divider) {
  margin: 18px 0;
  flex: 1;
}

.gantt-wrapper {
  width: 100%;
}

.gantt-legend {
  margin-left: 29%;
  display: flex;
  flex-wrap: wrap;
  gap: 12px 18px;
  margin-bottom: 10px;
}

.gantt-legend-item {
  display: inline-flex;
  align-items: center;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.legend-dot {
  width: 10px;
  height: 10px;
  border-radius: 2px;
  margin-right: 6px;
}

.gantt-header {
  margin-bottom: 12px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.gantt-body {
  display: flex;
  flex-direction: column;
  gap: 0;
  margin-bottom: 18px;
  position: relative;
}

.gantt-chart {
  position: relative;
  --gantt-info-width: 220px;
  --gantt-meta-width: 200px;
  --gantt-col-gap: 0px;
}

.gantt-table {
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  overflow: hidden;
  background: var(--el-bg-color);
}

.gantt-table-head,
.gantt-axis-row,
.gantt-row {
  display: grid;
  grid-template-columns: var(--gantt-info-width) 1fr var(--gantt-meta-width);
}

.gantt-table-head {
  background: var(--el-fill-color-light);
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.gantt-head-cell {
  padding: 10px 12px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  border-right: 1px solid var(--el-border-color-lighter);
}

.gantt-head-cell:last-child {
  border-right: none;
}

.gantt-axis-row {
  align-items: stretch;
  gap: var(--gantt-col-gap);
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: var(--el-bg-color);
}

.gantt-axis {
  position: relative;
  height: 38px;
  background: var(--el-fill-color-light);
  border-right: 1px solid var(--el-border-color-lighter);
  border-left: 1px solid var(--el-border-color-lighter);
}

.gantt-axis-side {
  min-height: 1px;
  border-right: 1px solid var(--el-border-color-lighter);
}

.gantt-axis-side:last-child {
  border-right: none;
}

.gantt-axis-tick {
  position: absolute;
  top: 0;
  bottom: 0;
  width: 0;
  border-left: 1px solid var(--el-border-color);
}

.gantt-axis-label {
  position: absolute;
  top: 9px;
  left: 4px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
  background: var(--el-fill-color-light);
  padding: 0 2px;
}

.gantt-now-line {
  position: absolute;
  top: 0;
  bottom: 0;
  width: 3px;
  background: var(--el-color-danger);
  left: calc(var(--gantt-info-width) + var(--gantt-col-gap) + (100% - var(--gantt-info-width) - var(--gantt-meta-width) - (var(--gantt-col-gap) * 2)) * var(--now-ratio));
  z-index: 3;
  pointer-events: auto;
  cursor: pointer;
}

.gantt-row {
  align-items: stretch;
  gap: var(--gantt-col-gap);
  border-bottom: 1px solid var(--el-border-color-lighter);
  min-height: 58px;
}

.gantt-row:nth-child(even) {
  background: var(--el-fill-color-lighter);
}

.gantt-row:last-child {
  border-bottom: none;
}

.gantt-info {
  min-width: 0;
  padding: 10px 12px;
  border-right: 1px solid var(--el-border-color-lighter);
}

.gantt-info-clickable {
  cursor: pointer;
  background: var(--el-color-primary-light-9);
  transition: background-color 0.2s ease, box-shadow 0.2s ease;
}

.gantt-info-clickable:hover {
  background: var(--el-color-primary-light-8);
  box-shadow: inset 0 0 0 1px var(--el-color-primary-light-5);
}

.gantt-label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--el-color-primary);
  font-weight: 600;
  text-decoration: underline;
  text-underline-offset: 2px;
  font-size: 13px;
}

.gantt-click-hint {
  margin-top: 3px;
  color: var(--el-color-primary);
  font-size: 11px;
  line-height: 1.2;
}

.gantt-subtext {
  margin-top: 3px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.gantt-track {
  position: relative;
  height: auto;
  min-height: 58px;
  border-radius: 0;
  background: var(--el-fill-color-light);
  overflow: hidden;
  border-left: 1px solid var(--el-border-color-lighter);
  border-right: 1px solid var(--el-border-color-lighter);
}

.gantt-bar {
  position: absolute;
  top: 1px;
  bottom: 1px;
  height: auto;
  border-radius: 3px;
  background: var(--el-color-primary);
  box-shadow: inset 0 -1px 0 rgba(0, 0, 0, 0.08);
  cursor: pointer;
}

.gantt-bar.normal-completed,
.legend-dot.normal-completed {
  background: var(--el-color-success);
}

.gantt-bar.delayed-completed,
.legend-dot.delayed-completed {
  background: var(--el-color-warning);
}

.gantt-bar.delayed-unfinished,
.legend-dot.delayed-unfinished {
  background: var(--el-color-danger);
}

.gantt-bar.normal-progress,
.legend-dot.normal-progress {
  background: var(--el-color-primary);
}

.gantt-meta {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 10px 12px;
  border-left: 1px solid var(--el-border-color-lighter);
}

.gantt-date {
  line-height: 1.2;
}

.gantt-tags {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
</style>
