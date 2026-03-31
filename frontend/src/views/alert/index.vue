<template>
  <div class="alert-page">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>超期告警</span>
          <el-button type="primary" @click="markAllRead" :disabled="unreadCount === 0">
            全部标记已读
          </el-button>
        </div>
      </template>
      
      <div class="search-bar">
        <el-select v-model="searchForm.alertType" placeholder="告警类型" clearable style="width: 150px;">
          <el-option label="任务超期" :value="1" />
          <el-option label="项目超期" :value="2" />
          <el-option label="进度滞后" :value="3" />
        </el-select>
        <el-select v-model="searchForm.isRead" placeholder="阅读状态" clearable style="width: 150px;">
          <el-option label="未读" :value="false" />
          <el-option label="已读" :value="true" />
        </el-select>
        <el-button type="primary" @click="handleSearch">搜索</el-button>
        <el-button @click="resetSearch">重置</el-button>
        <el-button
          type="primary"
          :disabled="!canOperateOverdueReason"
          @click="handleOverdueReasonAction"
        >
          {{ overdueReasonActionText }}
        </el-button>
      </div>
      
      <div class="alert-stats">
        <el-statistic title="总告警数" :value="pagination.total" />
        <el-statistic title="未读告警" :value="unreadCount" />
      </div>
      
      <el-table
        ref="alertTableRef"
        :data="alerts"
        style="width: 100%;"
        v-loading="loading"
        highlight-current-row
        @selection-change="handleSelectionChange"
        @row-click="handleRowClick"
      >
        <el-table-column type="selection" width="50" />
        <el-table-column prop="alertType" label="告警类型" width="110">
          <template #default="{ row }">
            <el-tag :type="getAlertTypeTagType(row.alertType)" size="small">
              {{ row.alertTypeName || getAlertTypeName(row.alertType) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="message" label="告警信息" min-width="300">
          <template #default="{ row }">
            <div class="alert-message" :class="{ 'unread': !row.isRead }">
              {{ row.message }}
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="alertStatusName" label="告警状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.alertStatus === 1 ? 'success' : 'danger'" size="small">
              {{ row.alertStatusName || (row.alertStatus === 1 ? '已完成' : '超期中') }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="taskAssigneeName" label="任务负责人" width="130">
          <template #default="{ row }">
            {{ row.taskAssigneeName || '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="overdueReason" label="超期原因" min-width="220" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.overdueReason || '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="projectManagerName" label="项目负责人" width="130">
          <template #default="{ row }">
            {{ row.projectManagerName || '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="projectName" label="相关项目" width="150" />
        <el-table-column prop="taskName" label="相关任务" width="150" />
        <el-table-column prop="createdAt" label="告警时间" width="160">
          <template #default="{ row }">
            {{ formatDateTime(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column prop="isRead" label="状态" width="80">
          <template #default="{ row }">
            <el-tag :type="row.isRead ? 'info' : 'danger'" size="small">
              {{ row.isRead ? '已读' : '未读' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="220" fixed="right">
          <template #default="{ row }">
            <div class="action-inline">
              <el-button
                v-if="!row.isRead"
                type="primary"
                link
                @click="markRead(row.id)"
              >
                标记已读
              </el-button>
              <el-button
                v-if="row.taskId"
                type="primary"
                link
                @click="viewTask(row.taskId)"
              >
                查看任务
              </el-button>
            </div>
          </template>
        </el-table-column>
      </el-table>
      
      <div class="pagination">
        <el-pagination
          v-model:current-page="pagination.page"
          v-model:page-size="pagination.pageSize"
          :page-sizes="[10, 20, 50, 100]"
          :total="pagination.total"
          layout="total, sizes, prev, pager, next, jumper"
          @size-change="handleSizeChange"
          @current-change="handleCurrentChange"
        />
      </div>
    </el-card>

    <el-dialog v-model="overdueReasonDialogVisible" :title="overdueReasonDialogTitle" width="520px">
      <el-input
        v-if="canEditOverdueReason"
        v-model="overdueReasonForm.overdueReason"
        type="textarea"
        :rows="4"
        maxlength="1000"
        show-word-limit
        placeholder="请输入超期原因"
      />
      <div v-else class="overdue-reason-view">
        {{ overdueReasonForm.overdueReason || '暂无超期原因' }}
      </div>
      <template #footer>
        <el-button @click="overdueReasonDialogVisible = false">取消</el-button>
        <el-button v-if="canEditOverdueReason" type="primary" :loading="savingOverdueReason" @click="submitOverdueReason">提交</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { request } from '@/api/request'
import { useAlertUnread } from '../../composables/useAlertUnread'
import dayjs from 'dayjs'

const router = useRouter()

const loading = ref(false)
const alerts = ref<any[]>([])
const { unreadCount, fetchUnreadCount } = useAlertUnread()
const overdueReasonDialogVisible = ref(false)
const savingOverdueReason = ref(false)
const currentEditingAlertId = ref<number | null>(null)
const selectedAlert = ref<any | null>(null)
const currentUser = ref<any>(null)
const alertTableRef = ref<any>(null)

const overdueReasonForm = reactive({
  overdueReason: ''
})

const searchForm = reactive({
  alertType: undefined as number | undefined,
  isRead: undefined as boolean | undefined
})

const pagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

const normalizeAssigneeNames = (assigneeText: string) => {
  if (!assigneeText) return []
  return assigneeText
    .split(/[、,，;；]/)
    .map((item) => item.trim())
    .filter(Boolean)
}

const isSelectedTaskAssignee = computed(() => {
  if (!selectedAlert.value || !currentUser.value) return false

  const assigneeNames = normalizeAssigneeNames(selectedAlert.value.taskAssigneeName || '')
  const currentUsername = currentUser.value.username || ''
  const currentRealName = currentUser.value.realName || ''

  return assigneeNames.includes(currentUsername) || (!!currentRealName && assigneeNames.includes(currentRealName))
})

const isSelectedProjectManager = computed(() => {
  if (!selectedAlert.value || !currentUser.value) return false
  if (selectedAlert.value.alertType !== 2) return false
  return Number(selectedAlert.value.projectManagerId) === Number(currentUser.value.id)
})

const canOperateOverdueReason = computed(() => {
  const alertType = selectedAlert.value?.alertType
  return alertType === 1 || alertType === 2
})

const canEditOverdueReason = computed(() => {
  const isAdmin = currentUser.value?.roleName === '管理员'
  const alertType = selectedAlert.value?.alertType

  if (alertType === 1) {
    return isAdmin || isSelectedTaskAssignee.value
  }

  if (alertType === 2) {
    return isAdmin || isSelectedProjectManager.value
  }

  return false
})
const overdueReasonActionText = computed(() => (canEditOverdueReason.value ? '填写超期原因' : '查看超期原因'))
const overdueReasonDialogTitle = computed(() => (canEditOverdueReason.value ? '填写超期原因' : '查看超期原因'))

const fetchAlerts = async () => {
  loading.value = true
  try {
    const params: any = {
      page: pagination.page,
      pageSize: pagination.pageSize
    }
    if (searchForm.alertType !== undefined) params.alertType = searchForm.alertType
    if (searchForm.isRead !== undefined) params.isRead = searchForm.isRead
    
    const res = await request.get('/alerts', { params })
    alerts.value = res.data.items
    selectedAlert.value = null
    alertTableRef.value?.clearSelection()
    pagination.total = res.data.totalCount
  } catch (error) {
    console.error('获取告警列表失败：', error)
  } finally {
    loading.value = false
  }
}

const handleSearch = () => {
  pagination.page = 1
  fetchAlerts()
}

const resetSearch = () => {
  searchForm.alertType = undefined
  searchForm.isRead = undefined
  handleSearch()
}

const handleSizeChange = (val: number) => {
  pagination.pageSize = val
  fetchAlerts()
}

const handleCurrentChange = (val: number) => {
  pagination.page = val
  fetchAlerts()
}

const markRead = async (id: number) => {
  try {
    await request.put(`/alerts/${id}/read`)
    ElMessage.success('已标记为已读')
    await Promise.all([fetchAlerts(), fetchUnreadCount()])
  } catch (error) {
    console.error('标记已读失败：', error)
  }
}

const markAllRead = async () => {
  try {
    await request.put('/alerts/read-all')
    ElMessage.success('已全部标记为已读')
    await Promise.all([fetchAlerts(), fetchUnreadCount()])
  } catch (error) {
    console.error('标记全部已读失败：', error)
  }
}

const openOverdueReasonDialog = (row: any) => {
  currentEditingAlertId.value = row.id
  overdueReasonForm.overdueReason = row.overdueReason || ''
  overdueReasonDialogVisible.value = true
}

const handleOverdueReasonAction = () => {
  if (!selectedAlert.value || (selectedAlert.value.alertType !== 1 && selectedAlert.value.alertType !== 2)) {
    ElMessage.warning('请先选中一条任务超期或项目超期告警')
    return
  }

  openOverdueReasonDialog(selectedAlert.value)
}

const getAlertTypeName = (alertType: number) => {
  const names: Record<number, string> = {
    1: '任务超期',
    2: '项目超期',
    3: '进度滞后'
  }

  return names[alertType] || '未知'
}

const getAlertTypeTagType = (alertType: number) => {
  const types: Record<number, string> = {
    1: 'danger',
    2: 'warning',
    3: 'info'
  }

  return types[alertType] || 'info'
}

const handleSelectionChange = (rows: any[]) => {
  if (!rows || rows.length === 0) {
    selectedAlert.value = null
    return
  }

  const last = rows[rows.length - 1]
  if (rows.length > 1) {
    alertTableRef.value?.clearSelection()
    alertTableRef.value?.toggleRowSelection(last, true)
  }

  selectedAlert.value = last
}

const handleRowClick = (row: any) => {
  alertTableRef.value?.clearSelection()
  alertTableRef.value?.toggleRowSelection(row, true)
  selectedAlert.value = row
}

const submitOverdueReason = async () => {
  if (!currentEditingAlertId.value) return

  savingOverdueReason.value = true
  try {
    await request.put(`/alerts/${currentEditingAlertId.value}/overdue-reason`, {
      overdueReason: overdueReasonForm.overdueReason || null
    })
    ElMessage.success('超期原因已更新')
    overdueReasonDialogVisible.value = false
    fetchAlerts()
  } catch (error) {
    console.error('更新超期原因失败：', error)
  } finally {
    savingOverdueReason.value = false
  }
}

const viewTask = (taskId: number) => {
  router.push(`/tasks/${taskId}`)
}

const formatDateTime = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}

onMounted(() => {
  const userStr = localStorage.getItem('user')
  if (userStr) {
    currentUser.value = JSON.parse(userStr)
  }

  fetchAlerts()
  fetchUnreadCount()
})
</script>

<style scoped>
.alert-page {
  padding: 10px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.search-bar {
  display: flex;
  gap: 10px;
  margin-bottom: 20px;
}

.alert-stats {
  display: flex;
  gap: 40px;
  margin-bottom: 20px;
  padding: 20px;
  background: #f5f7fa;
  border-radius: 4px;
}

.alert-message {
  font-size: 14px;
}

.alert-message.unread {
  font-weight: bold;
  color: #303133;
}

.pagination {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
}

.action-inline {
  display: flex;
  align-items: center;
  white-space: nowrap;
}

.action-inline :deep(.el-button) {
  margin-left: 0;
  margin-right: 8px;
}

.overdue-reason-view {
  min-height: 112px;
  max-height: 240px;
  padding: 10px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 4px;
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
  overflow-y: auto;
}
</style>
