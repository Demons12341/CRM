<template>
  <div class="project-page">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>项目列表</span>
          <el-button type="primary" @click="showCreateDialog">
            <el-icon><Plus /></el-icon>
            新建项目
          </el-button>
        </div>
      </template>
      
      <div class="search-bar">
        <el-input
          v-model="searchForm.keyword"
          placeholder="搜索项目名称"
          clearable
          style="width: 200px;"
          @keyup.enter="handleSearch"
        />
        <el-select v-model="searchForm.status" placeholder="项目状态" clearable style="width: 150px;">
          <el-option label="规划中" :value="0" />
          <el-option label="进行中" :value="1" />
          <el-option label="已完成" :value="2" />
          <el-option label="已暂停" :value="3" />
        </el-select>
        <el-button type="primary" @click="handleSearch">搜索</el-button>
        <el-button @click="resetSearch">重置</el-button>
        <el-button type="primary" :disabled="!selectedProject" @click="jumpToProjectTasks">
          跳转任务
        </el-button>
        <el-popconfirm
          title="确认将所选项目标记为已完成吗？"
          @confirm="completeSelectedProject"
        >
          <template #reference>
            <el-button type="success" :disabled="!canCompleteSelectedProject">
              完成项目
            </el-button>
          </template>
        </el-popconfirm>
      </div>
      
      <el-table
        ref="projectTableRef"
        :data="projects"
        style="width: 100%;"
        v-loading="loading"
        highlight-current-row
        @selection-change="handleSelectionChange"
        @row-click="handleRowClick"
      >
        <el-table-column type="selection" width="50" />
        <el-table-column prop="name" label="项目名称" min-width="200">
          <template #default="{ row }">
            <el-link type="primary" @click="viewProject(row.id)">
              {{ row.name }}
            </el-link>
            <el-tag v-if="isProjectOverdue(row) || hasOverdueTask(row)" class="project-alert-tag" type="danger" size="small">
              预警
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="managerName" label="负责人" width="120" />
        <el-table-column prop="priority" label="优先级" width="100">
          <template #default="{ row }">
            <el-tag :type="getPriorityType(row.priority)">
              {{ row.priorityName || getPriorityName(row.priority) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="status" label="状态 / 拖期" min-width="280">
          <template #default="{ row }">
            <div class="status-cell">
              <el-tag :type="getStatusType(row.status)">
                {{ getStatusName(row.status) }}
              </el-tag>
              <el-tag v-if="isProjectOverdue(row)" type="danger">
                项目拖期 {{ getProjectOverdueDays(row) }} 天
              </el-tag>
              <el-tag v-if="hasOverdueTask(row)" type="danger">
                任务超期
              </el-tag>
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="progress" label="进度" width="150">
          <template #default="{ row }">
            <el-progress :percentage="row.progress" :stroke-width="10" />
          </template>
        </el-table-column>
        <el-table-column prop="startDate" label="开始日期" width="120">
          <template #default="{ row }">
            {{ formatDate(row.startDate) }}
          </template>
        </el-table-column>
        <el-table-column prop="endDate" label="结束日期" width="120">
          <template #default="{ row }">
            {{ formatDate(row.endDate) }}
          </template>
        </el-table-column>
        <el-table-column prop="taskCount" label="任务数" width="80" />
        <el-table-column label="操作" width="200" fixed="right">
          <template #default="{ row }">
            <el-button type="primary" link @click="viewProject(row.id)">
              查看
            </el-button>
            <el-button type="primary" link @click="editProject(row)">
              编辑
            </el-button>
            <el-popconfirm
              v-if="isAdmin && !isSharedFolderProject(row)"
              title="确定要删除这个项目吗？"
              @confirm="deleteProject(row.id)"
            >
              <template #reference>
                <el-button type="danger" link>删除</el-button>
              </template>
            </el-popconfirm>
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
    
    <!-- 创建/编辑项目对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="isEdit ? '编辑项目' : '新建项目'"
      width="600px"
    >
      <el-form
        ref="formRef"
        :model="form"
        :rules="formRules"
        label-width="100px"
      >
        <el-form-item label="项目名称" prop="name">
          <el-input v-model="form.name" placeholder="请输入项目名称" />
        </el-form-item>
        <el-form-item label="项目描述" prop="description">
          <el-input
            v-model="form.description"
            type="textarea"
            :rows="3"
            placeholder="请输入项目描述"
          />
        </el-form-item>
        <el-form-item label="负责人" prop="managerId">
          <el-select v-model="form.managerId" placeholder="请选择负责人" filterable @change="handleManagerChange">
            <el-option
              v-for="user in users"
              :key="user.id"
              :label="getUserLabel(user)"
              :value="user.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="项目成员" prop="memberIds">
          <el-select
            v-model="form.memberIds"
            placeholder="请选择项目成员（可多选）"
            filterable
            multiple
            clearable
            collapse-tags
            :max-collapse-tags="3"
            collapse-tags-tooltip
          >
            <el-option
              v-for="user in users"
              :key="user.id"
              :label="getUserLabel(user)"
              :value="user.id"
              :disabled="user.id === form.managerId"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="优先级" prop="priority">
          <el-select v-model="form.priority" placeholder="请选择优先级">
            <el-option label="低" :value="1" />
            <el-option label="中" :value="2" />
            <el-option label="高" :value="3" />
            <el-option label="紧急" :value="4" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="!isEdit" label="项目任务模板" prop="processTemplateId">
          <el-select
            v-model="form.processTemplateId"
            placeholder="请选择项目任务模板（不选则使用默认模板）"
            clearable
            filterable
          >
            <el-option
              v-for="template in processTemplates"
              :key="template.id"
              :label="template.isDefault ? `${template.name}（默认）` : template.name"
              :value="template.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item v-if="isEdit" label="状态" prop="status">
          <el-select v-model="form.status" placeholder="请选择状态">
            <el-option label="规划中" :value="0" />
            <el-option label="进行中" :value="1" />
            <el-option label="已完成" :value="2" />
            <el-option label="已暂停" :value="3" />
          </el-select>
        </el-form-item>
        <el-form-item label="开始日期" prop="startDate" :required="!isEdit">
          <el-date-picker
            v-model="form.startDate"
            type="date"
            placeholder="选择开始日期"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
        <el-form-item label="结束日期" prop="endDate" :required="!isEdit">
          <el-date-picker
            v-model="form.endDate"
            type="date"
            placeholder="选择结束日期"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="submitting">
          确定
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules, TableInstance } from 'element-plus'
import { request } from '@/api/request'
import dayjs from 'dayjs'

const router = useRouter()

const loading = ref(false)
const submitting = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const editId = ref<number | null>(null)
const formRef = ref<FormInstance>()
const projectTableRef = ref<TableInstance>()

const projects = ref<any[]>([])
const users = ref<any[]>([])
const processTemplates = ref<any[]>([])
const selectedProject = ref<any | null>(null)
const currentUser = ref<any | null>(null)
const isAdmin = ref(false)
const SHARED_FOLDER_PROJECT_NAME = '共享文件夹'

const searchForm = reactive({
  keyword: '',
  status: undefined as number | undefined
})

const pagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

const canCompleteSelectedProject = computed(() => {
  if (!selectedProject.value) {
    return false
  }

  if (Number(selectedProject.value.status) === 2) {
    return false
  }

  return canEditProject(selectedProject.value)
})

const form = reactive({
  name: '',
  description: '',
  managerId: undefined as number | undefined,
  memberIds: [] as number[],
  status: 0,
  priority: 2,
  processTemplateId: undefined as number | undefined,
  startDate: '',
  endDate: ''
})

const formRules: FormRules = {
  name: [
    { required: true, message: '请输入项目名称', trigger: 'blur' }
  ],
  managerId: [
    { required: true, message: '请选择负责人', trigger: 'change' }
  ],
  priority: [
    { required: true, message: '请选择优先级', trigger: 'change' }
  ],
  startDate: [
    {
      validator: (_rule, value, callback) => {
        if (!isEdit.value && !value) {
          callback(new Error('新建项目时请选择开始日期'))
          return
        }
        callback()
      },
      trigger: 'change'
    }
  ],
  endDate: [
    {
      validator: (_rule, value, callback) => {
        if (!isEdit.value && !value) {
          callback(new Error('新建项目时请选择结束日期'))
          return
        }
        callback()
      },
      trigger: 'change'
    }
  ]
}

const fetchProjects = async () => {
  loading.value = true
  try {
    const params: any = {
      page: pagination.page,
      pageSize: pagination.pageSize,
      excludeSharedFolder: true
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.status !== undefined) params.status = searchForm.status
    
    const res = await request.get('/projects', { params })
    projects.value = res.data.items
    selectedProject.value = null
    projectTableRef.value?.clearSelection()
    pagination.total = res.data.totalCount
  } catch (error) {
    console.error('获取项目列表失败：', error)
  } finally {
    loading.value = false
  }
}

const fetchUsers = async () => {
  try {
    const res = await request.get('/users', { params: { pageSize: 200 } })
    users.value = res.data.items
  } catch (error) {
    console.error('获取用户列表失败：', error)
  }
}

const fetchProcessTemplates = async () => {
  try {
    const res = await request.get('/process-templates')
    processTemplates.value = res.data || []
  } catch (error) {
    console.error('获取项目任务模板失败：', error)
  }
}

const handleSearch = () => {
  pagination.page = 1
  fetchProjects()
}

const resetSearch = () => {
  searchForm.keyword = ''
  searchForm.status = undefined
  handleSearch()
}

const handleSizeChange = (val: number) => {
  pagination.pageSize = val
  fetchProjects()
}

const handleCurrentChange = (val: number) => {
  pagination.page = val
  fetchProjects()
}

const showCreateDialog = () => {
  isEdit.value = false
  editId.value = null
  resetForm()
  dialogVisible.value = true
}

const editProject = async (row: any) => {
  if (!canEditProject(row)) {
    ElMessage.warning('仅项目负责人或管理员可编辑项目')
    return
  }

  isEdit.value = true
  editId.value = row.id
  Object.assign(form, {
    name: row.name,
    description: row.description,
    managerId: row.managerId,
    memberIds: [],
    status: row.status,
    priority: row.priority ?? 2,
    processTemplateId: undefined,
    startDate: row.startDate,
    endDate: row.endDate
  })

  try {
    const res = await request.get(`/projects/${row.id}/members`)
    const members = (res.data || []) as any[]
    form.memberIds = members
      .map(member => member.userId)
      .filter(userId => userId !== row.managerId)
  } catch (error) {
    console.error('获取项目成员失败：', error)
  }

  dialogVisible.value = true
}

const resetForm = () => {
  Object.assign(form, {
    name: '',
    description: '',
    managerId: undefined,
    memberIds: [],
    status: 0,
    priority: 2,
    processTemplateId: undefined,
    startDate: '',
    endDate: ''
  })
  formRef.value?.resetFields()
}

const handleManagerChange = (managerId: number | undefined) => {
  if (!managerId) return
  form.memberIds = form.memberIds.filter(id => id !== managerId)
}

const handleSubmit = async () => {
  if (!formRef.value) return
  
  await formRef.value.validate(async (valid) => {
    if (valid) {
      submitting.value = true
      try {
        if (isEdit.value && editId.value) {
          const updatePayload = {
            name: form.name,
            description: form.description,
            managerId: form.managerId,
            memberIds: form.memberIds,
            status: form.status,
            priority: form.priority,
            startDate: form.startDate,
            endDate: form.endDate
          }
          await request.put(`/projects/${editId.value}`, updatePayload)
          ElMessage.success('更新成功')
        } else {
          const createPayload = {
            name: form.name,
            description: form.description,
            managerId: form.managerId,
            priority: form.priority,
            startDate: form.startDate,
            endDate: form.endDate,
            memberIds: form.memberIds,
            processTemplateId: form.processTemplateId
          }
          await request.post('/projects', createPayload)
          ElMessage.success('创建成功')
        }
        dialogVisible.value = false
        fetchProjects()
      } catch (error) {
        console.error('操作失败：', error)
      } finally {
        submitting.value = false
      }
    }
  })
}

const deleteProject = async (id: number) => {
  const targetProject = projects.value.find(project => project.id === id)
  if (isSharedFolderProject(targetProject)) {
    ElMessage.warning('共享文件夹项目禁止删除')
    return
  }

  try {
    await request.delete(`/projects/${id}`)
    ElMessage.success('删除成功')
    fetchProjects()
  } catch (error) {
    console.error('删除失败：', error)
  }
}

const completeProject = async (row: any) => {
  if (!canEditProject(row)) {
    ElMessage.warning('仅项目负责人或管理员可完成项目')
    return
  }

  try {
    await request.put(`/projects/${row.id}`, { status: 2 })
    ElMessage.success('项目已完成')
    fetchProjects()
  } catch (error) {
    console.error('项目完成失败：', error)
  }
}

const completeSelectedProject = async () => {
  if (!selectedProject.value) {
    ElMessage.warning('请先选择一个项目')
    return
  }

  if (!canEditProject(selectedProject.value)) {
    ElMessage.warning('仅项目负责人或管理员可完成项目')
    return
  }

  await completeProject(selectedProject.value)
}

const handleSelectionChange = (rows: any[]) => {
  if (!rows || rows.length === 0) {
    selectedProject.value = null
    return
  }

  const last = rows[rows.length - 1]
  if (rows.length > 1) {
    projectTableRef.value?.clearSelection()
    projectTableRef.value?.toggleRowSelection(last, true)
  }

  selectedProject.value = last
}

const handleRowClick = (row: any) => {
  projectTableRef.value?.clearSelection()
  projectTableRef.value?.toggleRowSelection(row, true)
  selectedProject.value = row
}

const jumpToProjectTasks = () => {
  if (!selectedProject.value) {
    ElMessage.warning('请先选择一个项目')
    return
  }

  router.push({
    path: '/tasks',
    query: { projectId: String(selectedProject.value.id) }
  })
}

const viewProject = (id: number) => {
  router.push(`/projects/${id}`)
}

const canEditProject = (project: any) => {
  if (!currentUser.value || !project) {
    return false
  }

  if (isAdmin.value) {
    return true
  }

  return Number(project.managerId) === Number(currentUser.value.id)
}

const getUserLabel = (user: any) => {
  return user.realName ? `${user.realName} (${user.username})` : user.username
}

const getProjectOverdueTaskFlag = (project: any) => {
  if (!project) return false
  if (typeof project.hasOverdueTask === 'boolean') return project.hasOverdueTask
  if (typeof project.HasOverdueTask === 'boolean') return project.HasOverdueTask
  return false
}

const isSharedFolderProject = (project: any) => {
  return `${project?.name || ''}`.trim() ===   SHARED_FOLDER_PROJECT_NAME
}

const hasOverdueTask = (project: any) => {
  return getProjectOverdueTaskFlag(project)
}

const isProjectOverdue = (project: any) => {
  if (!project?.endDate) {
    return false
  }

  const status = Number(project?.status)
  if (status === 2 || status === 3) {
    return false
  }

  return dayjs().isAfter(dayjs(project.endDate).endOf('day'))
}

const getProjectOverdueDays = (project: any) => {
  if (!isProjectOverdue(project)) {
    return 0
  }

  const overdueDays = dayjs().startOf('day').diff(dayjs(project.endDate).startOf('day'), 'day')
  return overdueDays > 0 ? overdueDays : 1
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
    3: 'warning'
  }
  return types[status] || 'info'
}

const getStatusName = (status: number) => {
  const names: Record<number, string> = {
    0: '规划中',
    1: '进行中',
    2: '已完成',
    3: '已暂停'
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
    isAdmin.value = currentUser.value?.roleName === '管理员'
  }

  fetchProjects()
  fetchUsers()
  fetchProcessTemplates()
})
</script>

<style scoped>
.project-page {
  --biz-bg-top: #eff5ff;
  --biz-bg-bottom: #f6fbf8;
  --biz-card-bg: rgba(255, 255, 255, 0.94);
  --biz-card-border: #d7e4f8;
  --biz-text-strong: #0f3b8c;
  --biz-text-muted: #5f6b7a;
  --biz-accent-soft: #eaf1ff;
  padding: 12px;
  height: 100%;
  min-height: 0;
  display: flex;
  overflow: hidden;
  background: #f5f8fc;
}

.project-page :deep(.el-card) {
  width: 100%;
  display: flex;
  flex-direction: column;
  border-radius: 16px;
  border: 1px solid var(--biz-card-border);
  background: var(--biz-card-bg);
  box-shadow: 0 18px 40px rgba(36, 66, 135, 0.12);
  overflow: hidden;
}

.project-page :deep(.el-card__header) {
  background: #f4f9ff;
  border-bottom: 1px solid #dce8fb;
}

.project-page :deep(.el-card__body) {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  padding: 14px;
  overflow: hidden;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
}

.card-header > span {
  font-size: 20px;
  color: var(--biz-text-strong);
  font-weight: 800;
  letter-spacing: 0.3px;
}

.search-bar {
  display: flex;
  gap: 10px;
  margin-bottom: 12px;
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid #dbe8ff;
  background: #f8fbff;
  flex-wrap: wrap;
  align-items: center;
}

.status-cell {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}

.project-alert-tag {
  margin-left: 8px;
}

.project-page :deep(.el-table) {
  border-radius: 12px;
  border: 1px solid #dce8fb;
  overflow: hidden;
}

.project-page :deep(.el-table th.el-table__cell) {
  background: #f3f8ff;
  color: #2e4566;
  font-weight: 700;
}

.project-page :deep(.el-table tr:hover > td.el-table__cell) {
  background: #f5f9ff;
}

.project-page :deep(.el-button) {
  border-radius: 999px;
}

.project-page :deep(.el-input__wrapper),
.project-page :deep(.el-select__wrapper),
.project-page :deep(.el-textarea__inner) {
  border-radius: 999px;
}

.project-page :deep(.el-textarea__inner) {
  border-radius: 12px;
}

.pagination {
  margin-top: 12px;
  display: flex;
  justify-content: flex-end;
  margin-top: auto;
  padding: 12px 0;
  border-top: 1px solid #e4ecfa;
  background: transparent;
}

@media (max-width: 900px) {
  .project-page {
    padding: 8px;
  }

  .project-page :deep(.el-card__body) {
    padding: 10px;
  }

  .card-header > span {
    font-size: 17px;
  }
}
</style>
