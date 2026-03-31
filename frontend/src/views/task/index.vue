<template>
  <div class="task-page">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>任务列表</span>
          <div class="header-actions">
            <el-button @click="importMicrogridTemplate">
              导入微电网标准工序
            </el-button>
            <el-button type="primary" @click="showCreateDialog">
              <el-icon>
                <Plus />
              </el-icon>
              新建任务
            </el-button>
          </div>
        </div>
      </template>

      <div class="search-bar">
        <el-input v-model="searchForm.keyword" placeholder="搜索任务名称" clearable style="width: 200px;"
          @keyup.enter="handleSearch" />
        <el-select v-model="searchForm.projectId" placeholder="所属项目" clearable filterable style="width: 200px;">
          <el-option v-for="project in projects" :key="project.id" :label="project.name" :value="project.id" />
        </el-select>
        <el-select v-model="searchForm.status" placeholder="任务状态" clearable style="width: 150px;">
          <el-option label="待办" :value="0" />
          <el-option label="进行中" :value="1" />
          <el-option label="已完成" :value="2" />
          <el-option label="已取消" :value="3" />
        </el-select>
        <el-button type="primary" @click="handleSearch">搜索</el-button>
        <el-button @click="resetSearch">重置</el-button>
        <el-button type="success"
          :disabled="!selectedTask || !canOperateTask(selectedTask) || selectedTask.status !== 0"
          @click="updateSelectedTaskStatus(1)">
          开始
        </el-button>
        <el-button type="success"
          :disabled="!selectedTask || !canOperateTask(selectedTask) || selectedTask.status === 2 || selectedTask.status === 3"
          @click="updateSelectedTaskStatus(2)">
          完成
        </el-button>
        <el-button type="warning" :disabled="!selectedTask || !canClaimTask(selectedTask)" @click="claimSelectedTask">
          认领任务
        </el-button>
        <el-button type="primary" :disabled="!selectedTask || !canOperateTask(selectedTask)" @click="openDueDateDialog">
          修改预计截止日期
        </el-button>
      </div>

      <el-table ref="taskTableRef" :data="tasks" style="width: 100%;" v-loading="loading" :fit="true"
        @selection-change="handleSelectionChange" @row-click="handleRowClick">
        <el-table-column type="selection" width="50" />
        <el-table-column prop="title" label="任务名称" min-width="160" show-overflow-tooltip>
          <template #default="{ row }">
            <el-link type="primary" @click="viewTask(row.id)">
              {{ row.title }}
            </el-link>
          </template>
        </el-table-column>
        <el-table-column prop="projectName" label="所属项目" min-width="110" show-overflow-tooltip />
        <el-table-column label="责任人" min-width="130" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.assigneeDisplay || row.assigneeName || '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="priority" label="优先级" min-width="80">
          <template #default="{ row }">
            <el-tag :type="getPriorityType(row.priority)" size="small">
              {{ getPriorityName(row.priority) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="status" label="状态" min-width="90">
          <template #default="{ row }">
            <el-tag :type="getStatusType(row.status)">
              {{ getStatusName(row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="startDate" label="预计开始时间" min-width="110">
          <template #default="{ row }">
            {{ formatDate(row.startDate) }}
          </template>
        </el-table-column>
        <el-table-column prop="dueDate" label="预计截止日期" min-width="110">
          <template #default="{ row }">
            <span :class="{ 'overdue': row.isOverdue }">{{ formatDate(row.dueDate) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="230" fixed="right">
          <template #default="{ row }">
            <div class="action-inline">
              <el-button type="primary" link @click="viewTask(row.id)">查看</el-button>
              <el-button v-if="canClaimTask(row)" type="warning" link @click="claimTask(row)">认领</el-button>
              <el-button type="primary" link @click="editTask(row)">编辑</el-button>
              <el-popconfirm title="确定要删除这个任务吗？" @confirm="deleteTask(row.id)">
                <template #reference>
                  <el-button type="danger" link>删除</el-button>
                </template>
              </el-popconfirm>
            </div>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination">
        <el-pagination v-model:current-page="pagination.page" v-model:page-size="pagination.pageSize"
          :page-sizes="[10, 20, 50, 100]" :total="pagination.total" layout="total, sizes, prev, pager, next, jumper"
          @size-change="handleSizeChange" @current-change="handleCurrentChange" />
      </div>
    </el-card>

    <!-- 创建/编辑任务对话框 -->
    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑任务' : '新建任务'" width="600px">
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="100px">
        <el-form-item label="所属项目" prop="projectId">
          <el-select v-model="form.projectId" placeholder="请选择项目" filterable @change="() => (form.assigneeIds = [])">
            <el-option v-for="project in projects" :key="project.id" :label="project.name" :value="project.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="任务名称" prop="title">
          <el-input v-model="form.title" placeholder="请输入任务名称" />
        </el-form-item>
        <el-form-item label="任务描述" prop="description">
          <el-input v-model="form.description" type="textarea" :rows="3" placeholder="请输入任务描述" />
        </el-form-item>
        <el-form-item label="责任人" prop="assigneeIds">
          <el-select v-model="form.assigneeIds" placeholder="请选择责任人（可多选）" filterable clearable multiple collapse-tags
            collapse-tags-tooltip>
            <el-option v-for="user in users" :key="user.id" :label="getUserLabel(user)" :value="user.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="预计开始时间" prop="startDate">
          <el-date-picker v-model="form.startDate" type="date" placeholder="选择预计开始时间" value-format="YYYY-MM-DD" />
        </el-form-item>
        <el-form-item label="预计截止日期" prop="dueDate">
          <el-date-picker v-model="form.dueDate" type="date" placeholder="选择预计截止日期" value-format="YYYY-MM-DD" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="submitting">
          确定
        </el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="dueDateDialogVisible" title="修改预计截止日期" width="420px">
      <el-form label-width="100px">
        <el-form-item label="任务名称">
          <div>{{ selectedTask?.title || '-' }}</div>
        </el-form-item>
        <el-form-item label="预计截止日期">
          <el-date-picker v-model="dueDateForm.dueDate" type="date" value-format="YYYY-MM-DD" placeholder="请选择预计截止日期"
            style="width: 220px" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dueDateDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submitSelectedTaskDueDate">提交</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { FormInstance, FormRules, TableInstance } from 'element-plus'
import { request } from '@/api/request'
import dayjs from 'dayjs'

const router = useRouter()
const route = useRoute()

const loading = ref(false)
const submitting = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const editId = ref<number | null>(null)
const formRef = ref<FormInstance>()
const taskTableRef = ref<TableInstance>()

const tasks = ref<any[]>([])
const projects = ref<any[]>([])
const users = ref<any[]>([])
const currentUser = ref<any>(null)
const originalEditForm = ref<any | null>(null)
const selectedTask = ref<any | null>(null)
const dueDateDialogVisible = ref(false)
const SHARED_FOLDER_PROJECT_NAME = '共享文件夹'

const dueDateForm = reactive({
  dueDate: ''
})

const searchForm = reactive({
  keyword: '',
  projectId: undefined as number | undefined,
  status: undefined as number | undefined,
  overdueOnly: false
})

const pagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

const form = reactive({
  projectId: undefined as number | undefined,
  title: '',
  description: '',
  assigneeIds: [] as number[],
  startDate: '',
  dueDate: ''
})

const formRules: FormRules = {
  projectId: [
    { required: true, message: '请选择所属项目', trigger: 'change' }
  ],
  title: [
    { required: true, message: '请输入任务名称', trigger: 'blur' }
  ]
}

const getUserLabel = (user: any) => {
  return user.realName ? `${user.realName} (${user.username})` : user.username
}

const fetchTasks = async () => {
  loading.value = true
  try {
    const params: any = {
      page: pagination.page,
      pageSize: pagination.pageSize
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.projectId) params.projectId = searchForm.projectId
    if (searchForm.status !== undefined) params.status = searchForm.status
    if (searchForm.overdueOnly) params.overdueOnly = true

    const res = await request.get('/tasks', { params })
    tasks.value = res.data.items || []
    selectedTask.value = null
    taskTableRef.value?.clearSelection()
    pagination.total = res.data.totalCount
  } catch (error) {
    console.error('获取任务列表失败：', error)
  } finally {
    loading.value = false
  }
}

const fetchProjects = async () => {
  try {
    const res = await request.get('/projects', { params: { pageSize: 200 } })
    const items = Array.isArray(res.data.items) ? res.data.items : []
    projects.value = items.filter((project: any) => `${project?.name || ''}`.trim() !== SHARED_FOLDER_PROJECT_NAME)

    if (searchForm.projectId && !projects.value.some((project: any) => project.id === searchForm.projectId)) {
      searchForm.projectId = undefined
    }

    if (form.projectId && !projects.value.some((project: any) => project.id === form.projectId)) {
      form.projectId = undefined
    }
  } catch (error) {
    console.error('获取项目列表失败：', error)
  }
}

const fetchUsers = async () => {
  try {
    const res = await request.get('/users', { params: { page: 1, pageSize: 200, isActive: true } })
    users.value = res.data.items || []
  } catch (error) {
    console.error('获取用户列表失败：', error)
  }
}

const handleSearch = () => {
  pagination.page = 1
  fetchTasks()
}

const resetSearch = () => {
  searchForm.keyword = ''
  searchForm.projectId = undefined
  searchForm.status = undefined
  searchForm.overdueOnly = false
  handleSearch()
}

const handleSizeChange = (val: number) => {
  pagination.pageSize = val
  fetchTasks()
}

const handleCurrentChange = (val: number) => {
  pagination.page = val
  fetchTasks()
}

const showCreateDialog = () => {
  isEdit.value = false
  editId.value = null
  originalEditForm.value = null
  resetForm()
  dialogVisible.value = true
}

const isCurrentUserAssignee = (row: any) => {
  if (!currentUser.value?.id || !row) return false
  return !!row.assigneeId && Number(row.assigneeId) === Number(currentUser.value.id)
}

const canOperateTask = (row: any) => {
  if (!row) return false
  if (currentUser.value?.roleName === '管理员') return true
  if (currentUser.value?.id && row.projectManagerId && Number(row.projectManagerId) === Number(currentUser.value.id)) return true
  return isCurrentUserAssignee(row)
}

const canClaimTask = (row: any) => {
  if (!row || !currentUser.value?.id) return false
  if (row.status === 2 || row.status === 3) return false
  return !isCurrentUserAssignee(row)
}

const editTask = (row: any) => {
  if (!canOperateTask(row)) {
    ElMessage.warning('项目成员仅可修改自己负责的任务')
    return
  }

  isEdit.value = true
  editId.value = row.id
  Object.assign(form, {
    projectId: row.projectId,
    title: row.title,
    description: row.description,
    assigneeIds: (row.assigneeIds && row.assigneeIds.length) ? [...row.assigneeIds] : (row.assigneeId ? [row.assigneeId] : []),
    startDate: row.startDate,
    dueDate: row.dueDate
  })
  originalEditForm.value = {
    projectId: form.projectId,
    title: form.title,
    description: form.description,
    assigneeIds: [...form.assigneeIds],
    startDate: form.startDate,
    dueDate: form.dueDate
  }
  dialogVisible.value = true
}

const resetForm = () => {
  Object.assign(form, {
    projectId: undefined,
    title: '',
    description: '',
    assigneeIds: [],
    startDate: '',
    dueDate: ''
  })
  originalEditForm.value = null
  formRef.value?.resetFields()
}

const isSameArray = (a: number[], b: number[]) => {
  if (a.length !== b.length) return false
  const sortedA = [...a].sort((x, y) => x - y)
  const sortedB = [...b].sort((x, y) => x - y)
  return sortedA.every((value, index) => value === sortedB[index])
}

const handleSubmit = async () => {
  if (!formRef.value) return

  await formRef.value.validate(async (valid) => {
    if (valid) {
      submitting.value = true
      try {
        if (isEdit.value && editId.value) {
          const payload: any = {}
          const origin = originalEditForm.value

          if (!origin || origin.title !== form.title) payload.title = form.title
          if (!origin || origin.description !== form.description) payload.description = form.description
          if (!origin || !isSameArray(origin.assigneeIds || [], form.assigneeIds || [])) {
            payload.assigneeIds = form.assigneeIds
            payload.assigneeId = form.assigneeIds.length ? form.assigneeIds[0] : null
          }
          if (!origin || origin.startDate !== form.startDate) payload.startDate = form.startDate || null
          if (!origin || origin.dueDate !== form.dueDate) payload.dueDate = form.dueDate || null

          if (Object.keys(payload).length === 0) {
            ElMessage.warning('未检测到修改内容')
            submitting.value = false
            return
          }

          await request.put(`/tasks/${editId.value}`, payload)
          ElMessage.success('更新成功')
        } else {
          await request.post('/tasks', {
            ...form,
            assigneeId: form.assigneeIds.length ? form.assigneeIds[0] : null,
            assigneeIds: form.assigneeIds
          })
          ElMessage.success('创建成功')
        }
        dialogVisible.value = false
        fetchTasks()
      } catch (error) {
        console.error('操作失败：', error)
      } finally {
        submitting.value = false
      }
    }
  })
}

const deleteTask = async (id: number) => {
  const row = tasks.value.find((item: any) => item.id === id)
  if (!canOperateTask(row)) {
    ElMessage.warning('项目成员仅可修改自己负责的任务')
    return
  }

  try {
    await request.delete(`/tasks/${id}`)
    ElMessage.success('删除成功')
    fetchTasks()
  } catch (error) {
    console.error('删除失败：', error)
  }
}

const claimTask = async (row: any) => {
  try {
    await request.put(`/tasks/${row.id}/claim`)
    ElMessage.success('任务认领成功')
    fetchTasks()
  } catch (error) {
    const message = (error as any)?.response?.data?.message || '任务认领失败'
    ElMessage.warning(message)
  }
}

const claimSelectedTask = async () => {
  if (!selectedTask.value) {
    ElMessage.warning('请先选择一个任务')
    return
  }

  if (!canClaimTask(selectedTask.value)) {
    ElMessage.warning('当前任务不可认领')
    return
  }

  await claimTask(selectedTask.value)
}

const quickUpdateStatus = async (id: number, status: number) => {
  try {
    await request.put(`/tasks/${id}/status`, { status }, { headers: { 'X-Silent-Error': '1' } })
    ElMessage.success(status === 1 ? '任务已开始' : '任务已完成')
    fetchTasks()
  } catch (error) {
    const message = (error as any)?.response?.data?.message || (error as any)?.message || ''
    if (status === 2 && `${message}`.includes('提交一次工作内容')) {
      ElMessage.warning('请先填写并提交工作内容')
      router.push({
        path: `/tasks/${id}`,
        query: { needWorkContent: 'true' }
      })
      return
    }
    ElMessage.warning(message || '更新任务状态失败')
  }
}

const handleSelectionChange = (rows: any[]) => {
  if (!rows || rows.length === 0) {
    selectedTask.value = null
    return
  }

  const last = rows[rows.length - 1]
  if (rows.length > 1) {
    taskTableRef.value?.clearSelection()
    taskTableRef.value?.toggleRowSelection(last, true)
  }

  selectedTask.value = last
}

const handleRowClick = (row: any) => {
  taskTableRef.value?.clearSelection()
  taskTableRef.value?.toggleRowSelection(row, true)
  selectedTask.value = row
}

const updateSelectedTaskStatus = async (status: number) => {
  if (!selectedTask.value) {
    ElMessage.warning('请先选择一个任务')
    return
  }

  if (!canOperateTask(selectedTask.value)) {
    ElMessage.warning('项目成员仅可修改自己负责的任务')
    return
  }

  const actionName = status === 1 ? '开始' : '完成'
  const targetStatusName = status === 1 ? '进行中' : '已完成'
  try {
    await ElMessageBox.confirm(`确认将任务“${selectedTask.value.title}”设为${targetStatusName}吗？`, `${actionName}任务`, {
      confirmButtonText: '确认',
      cancelButtonText: '取消',
      type: 'warning'
    })

    await quickUpdateStatus(selectedTask.value.id, status)
  } catch {
  }
}

const openDueDateDialog = () => {
  if (!selectedTask.value) {
    ElMessage.warning('请先选择一个任务')
    return
  }

  if (!canOperateTask(selectedTask.value)) {
    ElMessage.warning('项目成员仅可修改自己负责的任务')
    return
  }

  dueDateForm.dueDate = selectedTask.value.dueDate || ''
  dueDateDialogVisible.value = true
}

const submitSelectedTaskDueDate = async () => {
  if (!selectedTask.value) {
    ElMessage.warning('请先选择一个任务')
    return
  }

  if (!canOperateTask(selectedTask.value)) {
    ElMessage.warning('项目成员仅可修改自己负责的任务')
    return
  }

  if (!dueDateForm.dueDate) {
    ElMessage.warning('请选择预计截止日期')
    return
  }

  const nextDueDate = dueDateForm.dueDate
  const currentDueDate = selectedTask.value.dueDate || null
  if (nextDueDate === currentDueDate) {
    ElMessage.warning('预计截止时间未变化')
    return
  }

  try {
    await request.put(`/tasks/${selectedTask.value.id}`, { dueDate: nextDueDate })
    ElMessage.success('预计截止时间已更新')
    dueDateDialogVisible.value = false
    fetchTasks()
  } catch (error) {
    console.error('更新预计截止时间失败：', error)
  }
}

const importMicrogridTemplate = async () => {
  if (!searchForm.projectId) {
    ElMessage.warning('请先在筛选区选择要导入模板的项目')
    return
  }

  try {
    await request.post(`/projects/${searchForm.projectId}/tasks/microgrid-template`, {})
    ElMessage.success('微电网标准工序导入成功')
    fetchTasks()
  } catch (error) {
    console.error('导入模板失败：', error)
  }
}

const viewTask = (id: number) => {
  router.push(`/tasks/${id}`)
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

const formatDate = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD')
}

onMounted(() => {
  const userStr = localStorage.getItem('user')
  if (userStr) {
    currentUser.value = JSON.parse(userStr)
  }

  searchForm.overdueOnly = route.query.overdueOnly === 'true'
  const queryProjectId = Number(route.query.projectId)
  if (!Number.isNaN(queryProjectId) && queryProjectId > 0) {
    searchForm.projectId = queryProjectId
  }

  fetchTasks()
  fetchProjects()
  fetchUsers()
})
</script>

<style scoped>
.task-page {
  padding: 10px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header-actions {
  display: flex;
  gap: 8px;
}

.search-bar {
  display: flex;
  gap: 10px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.pagination {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
}

.overdue {
  color: #f56c6c;
}

.action-inline {
  display: flex;
  align-items: center;
  flex-wrap: nowrap;
  white-space: nowrap;
}

.action-inline :deep(.el-button) {
  margin-left: 0;
  margin-right: 8px;
}

.action-inline :deep(.el-button:last-child) {
  margin-right: 0;
}
</style>
