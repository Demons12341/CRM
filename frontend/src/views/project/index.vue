<template>
  <div class="project-page">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>项目列表</span>
          <el-button type="primary" v-permission="'projects.create'" @click="showCreateDialog">
            <el-icon>
              <Plus />
            </el-icon>
            新建项目
          </el-button>
        </div>
        <div class="search-bar">
          <el-input v-model="searchForm.keyword" placeholder="搜索项目名称" clearable style="width: 200px;"
            @keyup.enter="handleSearch" />
          <el-select v-model="searchForm.businessLine" placeholder="业务线" clearable style="width: 180px;"
            @change="handleSearch">
            <el-option v-for="bl in businessLines" :key="bl.id" :label="bl.name" :value="bl.name" />
          </el-select>
          <el-select v-model="searchForm.status" placeholder="项目状态" clearable style="width: 150px;"
            @change="handleSearch">
            <el-option label="售前阶段" :value="0" />
            <el-option label="已中标，待签合同" :value="2" />
            <el-option label="需求确定阶段" :value="3" />
            <el-option label="设计阶段" :value="4" />
            <el-option label="采购生产阶段" :value="5" />
            <el-option label="装配阶段" :value="6" />
            <el-option label="测试阶段" :value="7" />
            <el-option label="已发货" :value="8" />
            <el-option label="现场调试" :value="9" />
            <el-option label="已完成" :value="10" />
          </el-select>
          <el-button type="primary" @click="handleSearch">搜索</el-button>
          <el-button @click="resetSearch">重置</el-button>
          <el-button type="success" v-permission="'projects.edit'"
            :disabled="!selectedProject || !canEditProject(selectedProject)" @click="openStatusDialog">
            修改项目状态
          </el-button>
        </div>
      </template>

      <div v-if="!treeReady" class="tree-loading-placeholder">
        <el-icon class="is-loading">
          <Loading />
        </el-icon>
        <span>加载中...</span>
      </div>
      <el-collapse v-else v-model="expandedBusinessLines" class="bl-collapse">
        <el-collapse-item v-for="group in groupedProjects" :key="group.name" :name="group.name">
          <template #title>
            <span class="bl-title">{{ group.name }}</span>
            <el-tag size="small" type="info" class="bl-count">{{ group.projects.length }} 个项目</el-tag>
            <el-tag v-if="group.projectOverdueCount > 0" size="small" type="danger" class="bl-overdue-tag">
              {{ group.projectOverdueCount }} 个项目超期
            </el-tag>
            <el-tag v-if="group.taskOverdueCount > 0" size="small" type="warning" class="bl-overdue-tag">
              {{ group.taskOverdueCount }} 个项目有任务超期
            </el-tag>
          </template>
          <el-table :data="group.projects" style="width: 100%;" v-loading="loading" :row-class-name="getRowClassName"
            @row-click="handleRowClick">
            <el-table-column width="50">
              <template #default="{ row }">
                <el-checkbox :model-value="selectedProject?.id === row.id" @click.stop
                  @change="(val: boolean) => { selectedProject = val ? row : null }" />
              </template>
            </el-table-column>
            <el-table-column prop="name" label="项目名称" min-width="200" show-overflow-tooltip>
              <template #default="{ row }">
                <el-link type="primary" @click="viewProject(row.id)">
                  {{ row.name }}
                </el-link>
                <el-tag v-if="isProjectOverdue(row) || hasOverdueTask(row)" class="project-alert-tag" type="danger"
                  size="small">
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
                  <el-tag :style="getStatusStyle(row.status)">
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
                <el-button type="primary" link v-permission="'projects.edit'" @click="editProject(row)">
                  编辑
                </el-button>
                <el-popconfirm v-permission="'projects.delete'" v-if="isAdmin && !isSharedFolderProject(row)"
                  title="确定要删除这个项目吗？" @confirm="deleteProject(row.id)">
                  <template #reference>
                    <el-button type="danger" link>删除</el-button>
                  </template>
                </el-popconfirm>
              </template>
            </el-table-column>
          </el-table>
        </el-collapse-item>
      </el-collapse>

      <el-empty v-if="!loading && projects.length === 0" description="暂无项目" />

      <el-empty v-if="!loading && projects.length === 0" description="暂无项目" />
    </el-card>

    <!-- 创建/编辑项目对话框 -->
    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑项目' : '新建项目'" width="600px">
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="100px">
        <el-form-item label="业务线" prop="businessLine">
          <el-select v-model="form.businessLine" placeholder="请选择业务线">
            <el-option v-for="bl in businessLines" :key="bl.id" :label="bl.name" :value="bl.name" />
          </el-select>
        </el-form-item>
        <el-form-item label="项目名称" prop="name">
          <el-input v-model="form.name" placeholder="请输入项目名称" />
        </el-form-item>
        <el-form-item label="项目描述" prop="description">
          <el-input v-model="form.description" type="textarea" :rows="3" placeholder="请输入项目描述" />
        </el-form-item>
        <el-form-item label="负责人" prop="managerId">
          <el-select v-model="form.managerId" placeholder="请选择负责人" filterable @change="handleManagerChange">
            <el-option v-for="user in users" :key="user.id" :label="getUserLabel(user)" :value="user.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="项目成员" prop="memberIds">
          <el-select v-model="form.memberIds" placeholder="请选择项目成员（可多选）" filterable multiple clearable collapse-tags
            :max-collapse-tags="3" collapse-tags-tooltip>
            <el-option v-for="user in users" :key="user.id" :label="getUserLabel(user)" :value="user.id"
              :disabled="user.id === form.managerId" />
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
          <el-select v-model="form.processTemplateId" placeholder="请选择项目任务模板（不选则使用默认模板）" clearable filterable>
            <el-option v-for="template in processTemplates" :key="template.id"
              :label="template.isDefault ? `${template.name}（默认）` : template.name" :value="template.id" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="isEdit" label="状态" prop="status">
          <el-select v-model="form.status" placeholder="请选择状态">
            <el-option label="售前阶段" :value="0" />
            <el-option label="已中标，待签合同" :value="2" />
            <el-option label="需求确定阶段" :value="3" />
            <el-option label="设计阶段" :value="4" />
            <el-option label="采购生产阶段" :value="5" />
            <el-option label="装配阶段" :value="6" />
            <el-option label="测试阶段" :value="7" />
            <el-option label="已发货" :value="8" />
            <el-option label="现场调试" :value="9" />
            <el-option label="已完成" :value="10" />
          </el-select>
        </el-form-item>
        <el-form-item label="开始日期" prop="startDate" :required="!isEdit">
          <el-date-picker v-model="form.startDate" type="date" placeholder="选择开始日期" value-format="YYYY-MM-DD" />
        </el-form-item>
        <el-form-item label="结束日期" prop="endDate" :required="!isEdit">
          <el-date-picker v-model="form.endDate" type="date" placeholder="选择结束日期" value-format="YYYY-MM-DD" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="submitting">
          确定
        </el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="statusDialogVisible" title="修改项目状态" width="420px">
      <el-form label-width="90px">
        <el-form-item label="项目名称">
          <span>{{ selectedProject?.name || '-' }}</span>
        </el-form-item>
        <el-form-item label="当前状态">
          <el-tag :style="getStatusStyle(selectedProject?.status)">{{ getStatusName(selectedProject?.status) }}</el-tag>
        </el-form-item>
        <el-form-item label="新状态">
          <el-select v-model="newStatus" placeholder="请选择新状态" style="width: 100%;">
            <el-option label="售前阶段" :value="0" />
            <el-option label="已中标，待签合同" :value="2" />
            <el-option label="需求确定阶段" :value="3" />
            <el-option label="设计阶段" :value="4" />
            <el-option label="采购生产阶段" :value="5" />
            <el-option label="装配阶段" :value="6" />
            <el-option label="测试阶段" :value="7" />
            <el-option label="已发货" :value="8" />
            <el-option label="现场调试" :value="9" />
            <el-option label="已完成" :value="10" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="statusDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="statusSubmitting" @click="submitStatusChange">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed, nextTick } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { request } from '@/api/request'
import dayjs from 'dayjs'
import { hasPermission } from '@/directives/permission'

const router = useRouter()
const route = useRoute()

const loading = ref(false)
const submitting = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const editId = ref<number | null>(null)
const formRef = ref<FormInstance>()

const projects = ref<any[]>([])
const users = ref<any[]>([])
const processTemplates = ref<any[]>([])
const businessLines = ref<any[]>([])
const selectedProject = ref<any | null>(null)
const currentUser = ref<any | null>(null)
const isAdmin = ref(false)
const SHARED_FOLDER_PROJECT_NAME = '共享文件夹'
const statusDialogVisible = ref(false)
const statusSubmitting = ref(false)
const newStatus = ref<number | undefined>(undefined)
const expandedBusinessLines = ref<string[]>([])
const treeReady = ref(false)

const groupedProjects = computed(() => {
  const groups: Record<string, any[]> = {}
  for (const project of projects.value) {
    const bl = project.businessLine || '未分类业务线'
    if (!groups[bl]) groups[bl] = []
    groups[bl].push(project)
  }
  const orderedLines = [...businessLines.value.map((bl: any) => bl.name), '未分类业务线'].filter(line => groups[line]?.length)
  return orderedLines.map(line => {
    const projects = groups[line]
    const projectOverdueCount = projects.filter(p => isProjectOverdue(p)).length
    const taskOverdueCount = projects.filter(p => hasOverdueTask(p)).length
    return {
      name: line,
      projects,
      projectOverdueCount,
      taskOverdueCount
    }
  })
})

const searchForm = reactive({
  keyword: '',
  status: undefined as number | undefined,
  businessLine: '' as string | undefined
})

const openStatusDialog = () => {
  if (!selectedProject.value) {
    ElMessage.warning('请先选择一个项目')
    return
  }
  if (!canEditProject(selectedProject.value)) {
    ElMessage.warning('仅项目负责人或管理员可修改项目状态')
    return
  }
  newStatus.value = Number(selectedProject.value.status)
  statusDialogVisible.value = true
}

const submitStatusChange = async () => {
  if (!selectedProject.value) return
  if (newStatus.value === undefined || newStatus.value === null) {
    ElMessage.warning('请选择新状态')
    return
  }
  statusSubmitting.value = true
  try {
    await request.put(`/projects/${selectedProject.value.id}`, { status: newStatus.value })
    ElMessage.success('状态修改成功')
    statusDialogVisible.value = false
    fetchProjects()
  } catch (error: any) {
    const msg = error?.response?.data?.message || '状态修改失败'
    ElMessage.error(msg)
  } finally {
    statusSubmitting.value = false
  }
}

const form = reactive({
  name: '',
  description: '',
  managerId: undefined as number | undefined,
  memberIds: [] as number[],
  status: 0,
  priority: 2,
  businessLine: '' as string | undefined,
  processTemplateId: undefined as number | undefined,
  startDate: '',
  endDate: ''
})

const formRules: FormRules = {
  businessLine: [
    { required: true, message: '请选择业务线', trigger: 'change' }
  ],
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
      pageSize: 200,
      excludeSharedFolder: true
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.status !== undefined) params.status = searchForm.status
    if (searchForm.businessLine) params.businessLine = searchForm.businessLine

    const res = await request.get('/projects', { params })
    projects.value = res.data.items
    selectedProject.value = null
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

const fetchBusinessLines = async () => {
  try {
    const res = await request.get('/business-lines')
    businessLines.value = res.data || []
  } catch (error) {
    console.error('获取业务线列表失败：', error)
  }
}

const handleSearch = () => {
  fetchProjects()
}

const resetSearch = () => {
  searchForm.keyword = ''
  searchForm.status = undefined
  searchForm.businessLine = ''
  handleSearch()
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
    businessLine: row.businessLine || '',
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
    businessLine: '',
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
            businessLine: form.businessLine || null,
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
            businessLine: form.businessLine || null,
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

const handleRowClick = (row: any, _column: any, event: MouseEvent) => {
  const target = event.target as HTMLElement | null
  if (target?.closest('.el-checkbox') || target?.closest('.el-link') || target?.closest('.el-button')) {
    return
  }

  if (selectedProject.value?.id === row.id) {
    selectedProject.value = null
    return
  }

  selectedProject.value = row
}

const getRowClassName = ({ row }: { row: any }) => {
  return selectedProject.value?.id === row.id ? 'is-selected-row' : ''
}

const viewProject = (id: number) => {
  router.push(`/projects/${id}`)
}

const canEditProject = (project: any) => {
  if (!currentUser.value || !project) {
    return false
  }

  if (hasPermission('project.edit_all')) {
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
  return `${project?.name || ''}`.trim() === SHARED_FOLDER_PROJECT_NAME
}

const hasOverdueTask = (project: any) => {
  return getProjectOverdueTaskFlag(project)
}

const isProjectOverdue = (project: any) => {
  if (!project?.endDate) {
    return false
  }

  const status = Number(project?.status)
  if (status === 10) {
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

const getStatusStyle = (status: number) => {
  const styles: Record<number, { color: string; background: string; border: string }> = {
    0: { color: '#78716c', background: '#f5f5f4', border: '1px solid #e7e5e4' },
    2: { color: '#1d4ed8', background: '#dbeafe', border: '1px solid #bfdbfe' },
    3: { color: '#6d28d9', background: '#ede9fe', border: '1px solid #ddd6fe' },
    4: { color: '#7c3aed', background: '#f3e8ff', border: '1px solid #e9d5ff' },
    5: { color: '#b45309', background: '#fef3c7', border: '1px solid #fde68a' },
    6: { color: '#c2410c', background: '#ffedd5', border: '1px solid #fed7aa' },
    7: { color: '#be185d', background: '#fce7f3', border: '1px solid #fbcfe8' },
    8: { color: '#b7791f', background: '#fefce8', border: '1px solid #fef9c3' },
    9: { color: '#0e7490', background: '#ecfeff', border: '1px solid #cffafe' },
    10: { color: '#15803d', background: '#dcfce7', border: '1px solid #bbf7d0' }
  }
  return styles[status] ?? styles[0]
}

const getStatusName = (status: number) => {
  const names: Record<number, string> = {
    0: '售前阶段',
    2: '已中标，待签合同',
    3: '需求确定阶段',
    4: '设计阶段',
    5: '采购生产阶段',
    6: '装配阶段',
    7: '测试阶段',
    8: '已发货',
    9: '现场调试',
    10: '已完成'
  }
  return names[status] || '未知'
}

const formatDate = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD')
}

onMounted(async () => {
  const userStr = localStorage.getItem('user')
  if (userStr) {
    currentUser.value = JSON.parse(userStr)
    isAdmin.value = hasPermission('project.view_all')
  }

  await Promise.all([fetchProjects(), fetchBusinessLines()])
  treeReady.value = true

  const queryFocusProjectId = Number(route.query.focusProjectId)
  const queryBusinessLine = String(route.query.businessLine || '')
  if (!Number.isNaN(queryFocusProjectId) && queryFocusProjectId > 0) {
    const targetProject = projects.value.find((p: any) => p.id === queryFocusProjectId)
    if (targetProject) {
      const blToExpand = queryBusinessLine || targetProject.businessLine || '未分类业务线'
      expandedBusinessLines.value = [blToExpand]
      nextTick(() => {
        selectedProject.value = targetProject
      })
    }
  }

  fetchUsers()
  fetchProcessTemplates()
})
</script>

<style scoped>
.bl-collapse {
  border: none;
}

.tree-loading-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 40px 0;
  color: #909399;
  font-size: 13px;
}

.bl-collapse :deep(.el-collapse-item__header) {
  background: #f4f9ff;
  border-radius: 8px;
  padding: 0 12px;
  margin-bottom: 8px;
  font-weight: 600;
  color: #0f3b8c;
  min-height: 42px;
}

.bl-collapse :deep(.el-collapse-item__wrap) {
  border-bottom: none;
}

.bl-collapse :deep(.el-collapse-item__content) {
  padding-bottom: 12px;
}

.bl-title {
  font-size: 14px;
  font-weight: 600;
  color: #0f3b8c;
  margin-right: 8px;
}

.bl-count {
  font-weight: 400;
  margin-left: 8px;
}

.bl-overdue-tag {
  margin-left: 6px;
}

.bl-collapse :deep(.el-table) {
  border-radius: 8px;
  border: 1px solid #dce8fb;
  overflow: hidden;
}

.bl-collapse :deep(.el-table th.el-table__cell) {
  background: #f3f8ff;
  color: #2e4566;
  font-weight: 700;
}

.bl-collapse :deep(.el-table tr:hover > td.el-table__cell) {
  background: #f5f9ff;
}

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
  overflow-y: auto;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
}

.card-header>span {
  font-size: 20px;
  color: var(--biz-text-strong);
  font-weight: 800;
  letter-spacing: 0.3px;
}

.search-bar {
  display: flex;
  gap: 10px;
  margin-top: 10px;
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

.project-name-cell {
  overflow: hidden;
}

.project-name-cell :deep(.el-link) {
  display: block;
  width: 100%;
}

.project-name-cell :deep(.el-link__inner) {
  display: block;
  width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  text-align: left;
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

.project-page :deep(.el-table .is-selected-row > td.el-table__cell) {
  background: #eef4ff;
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

  .card-header>span {
    font-size: 17px;
  }
}
</style>
