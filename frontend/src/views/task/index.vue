<template>
  <div class="task-page">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>任务列表</span>
          <div class="header-actions">
            <el-button :disabled="!canOperateTemplateActions" @click="importMicrogridTemplate">
              导入微电网标准工序
            </el-button>
            <el-button :disabled="!canOperateTemplateActions" @click="exportMicrogridTemplate">
              导出微电网标准工序
            </el-button>
            <el-button type="primary" :disabled="!canCreateTask" @click="showCreateDialog">
              <el-icon>
                <Plus />
              </el-icon>
              新建任务
            </el-button>
          </div>
        </div>
      </template>

      <div class="explorer-layout">
        <aside class="left-tree-panel">
          <div class="panel-title">
            <span>项目目录</span>
            <el-input v-model="projectSearchKeyword" size="small" clearable placeholder="搜索项目"
              class="project-search-input" />
          </div>
          <el-tree v-if="filteredProjectTreeData.length" :data="filteredProjectTreeData" node-key="id"
            :current-node-key="searchForm.projectId" :expand-on-click-node="false" highlight-current default-expand-all
            @node-click="handleProjectNodeClick">
            <template #default="{ data }">
              <span class="project-tree-node">
                <span class="project-tree-name" :title="data.name">{{ data.name }}</span>
                <span class="project-tree-meta">
                  <el-tag size="small" class="project-manager-tag" type="info" :title="data.managerDisplay">
                    负责人 · {{ data.managerDisplay }}
                  </el-tag>
                  <span class="project-tree-badge" :class="data.statusClass">{{ data.statusName }}</span>
                  <span v-if="data.projectOverdue" class="project-tree-badge danger">项目超期</span>
                  <span v-if="data.taskOverdue" class="project-tree-badge danger">任务超期</span>
                </span>
              </span>
            </template>
          </el-tree>
          <el-empty v-else description="暂无项目" :image-size="60" />
        </aside>

        <main class="right-content-panel">
          <div class="search-bar">
            <el-input v-model="searchForm.keyword" placeholder="搜索任务名称" clearable style="width: 200px;"
              @keyup.enter="handleSearch" />
            <el-button type="primary" @click="handleSearch">搜索</el-button>
            <el-button @click="resetSearch">重置</el-button>
            <el-tooltip
              :content="searchForm.sortBy === 'urgency' ? '当前：按任务紧急排序；点击后切换为按任务顺序排序' : '当前：按任务顺序排序；点击后切换为按任务紧急排序'"
              placement="top">
              <el-button :type="searchForm.sortBy === 'urgency' ? 'danger' : 'primary'" @click="togglePrioritySort">
                {{ searchForm.sortBy === 'urgency' ? '按任务紧急排序' : '按任务顺序排序' }}
              </el-button>
            </el-tooltip>
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
            <el-button type="warning" :disabled="!selectedTask || !canClaimTask(selectedTask)"
              @click="claimSelectedTask">
              认领任务
            </el-button>
            <el-button type="primary" :disabled="!selectedTask || !canOperateTask(selectedTask)"
              @click="openDueDateDialog">
              修改预计截止日期
            </el-button>
          </div>

          <div class="content-area">
            <div v-if="searchForm.projectId">
              <el-table ref="taskTableRef" :data="tasks" style="width: 100%;" v-loading="loading" :fit="true"
                :row-class-name="getTaskRowClass" @selection-change="handleSelectionChange" @row-click="handleRowClick">
                <el-table-column type="selection" width="50" />
                <el-table-column prop="title" label="任务名称" min-width="160" show-overflow-tooltip>
                  <template #default="{ row }">
                    <el-link type="primary" @click="viewTask(row.id)">
                      <span class="task-title-text" :class="{ 'task-title-overdue': isTaskOverdueRow(row) }">
                        <span v-if="isTaskOverdueRow(row)" class="task-overdue-dot">!</span>
                        {{ row.title }}
                      </span>
                    </el-link>
                  </template>
                </el-table-column>
                <el-table-column label="责任人" min-width="130" show-overflow-tooltip>
                  <template #default="{ row }">
                    {{ row.assigneeDisplay || row.assigneeName || '-' }}
                  </template>
                </el-table-column>
                <el-table-column prop="priority" label="优先级" min-width="126" class-name="priority-cell">
                  <template #default="{ row }">
                    <el-select v-if="canOperateTask(row)" :model-value="row.priority" size="small"
                      :class="['biz-inline-select', getPriorityVisualClass(row.priority)]"
                      popper-class="task-inline-select-popper" :fit-input-width="false"
                      :disabled="rowPriorityUpdatingId === row.id || rowStatusUpdatingId === row.id"
                      @change="(value: number) => handleRowPriorityChange(row, Number(value))">
                      <template #prefix>
                        <span class="biz-select-dot" :class="getPriorityVisualClass(row.priority)"></span>
                      </template>
                      <el-option v-for="item in priorityOptions" :key="item.value" :label="item.label"
                        :value="item.value">
                        <span class="biz-option-row" :class="getPriorityVisualClass(item.value)">
                          <span class="biz-option-dot"></span>
                          <span>{{ item.label }}</span>
                        </span>
                      </el-option>
                    </el-select>
                    <el-tag v-else :type="getPriorityType(row.priority)" size="small" class="inline-cell-tag"
                      :class="getPriorityVisualClass(row.priority)">
                      {{ getPriorityName(row.priority) }}
                    </el-tag>
                  </template>
                </el-table-column>
                <el-table-column prop="status" label="状态" min-width="132" class-name="status-cell">
                  <template #default="{ row }">
                    <el-select v-if="canOperateTask(row)" :model-value="row.status" size="small"
                      :class="['biz-inline-select', getStatusVisualClass(row.status)]"
                      popper-class="task-inline-select-popper" :fit-input-width="false"
                      :disabled="rowPriorityUpdatingId === row.id || rowStatusUpdatingId === row.id"
                      @change="(value: number) => handleRowStatusChange(row, Number(value))">
                      <template #prefix>
                        <span class="biz-select-dot" :class="getStatusVisualClass(row.status)"></span>
                      </template>
                      <el-option v-for="item in statusOptions" :key="item.value" :label="item.label"
                        :value="item.value">
                        <span class="biz-option-row" :class="getStatusVisualClass(item.value)">
                          <span class="biz-option-dot"></span>
                          <span>{{ item.label }}</span>
                        </span>
                      </el-option>
                    </el-select>
                    <el-tag v-else :type="getStatusType(row.status)" class="inline-cell-tag"
                      :class="getStatusVisualClass(row.status)">
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
                    <span :class="{ 'overdue': isTaskOverdueRow(row) }">{{ formatDate(row.dueDate) }}</span>
                  </template>
                </el-table-column>
                <el-table-column prop="completedAt" label="实际结束时间" min-width="110">
                  <template #default="{ row }">
                    <span :class="{ 'completed-late': isCompletedLate(row) }">{{ formatDate(row.completedAt) }}</span>
                  </template>
                </el-table-column>
                <el-table-column label="操作" width="230" fixed="right">
                  <template #default="{ row }">
                    <div class="action-inline">
                      <el-button type="primary" link @click="viewTask(row.id)">查看</el-button>
                      <el-button v-if="canClaimTask(row)" type="warning" link @click="claimTask(row)">认领</el-button>
                      <el-popconfirm title="确定要删除这个任务吗？" @confirm="deleteTask(row.id)">
                        <template #reference>
                          <el-button type="danger" link>删除</el-button>
                        </template>
                      </el-popconfirm>
                    </div>
                  </template>
                </el-table-column>
              </el-table>
            </div>
            <el-empty v-else description="请先在左侧选择项目" />
          </div>

          <div class="pagination">
            <el-pagination v-model:current-page="pagination.page" v-model:page-size="pagination.pageSize"
              :page-sizes="[10, 20, 50, 100]" :total="pagination.total" layout="total, sizes, prev, pager, next, jumper"
              @size-change="handleSizeChange" @current-change="handleCurrentChange" />
          </div>
        </main>
      </div>
    </el-card>

    <!-- 创建/编辑任务对话框 -->
    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑任务' : '新建任务'" width="600px">
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="120px">
        <el-form-item label="所属项目" prop="projectId">
          <el-input v-if="!isEdit && searchForm.projectId" :model-value="selectedProjectNameForCreate" disabled />
          <el-select v-else v-model="form.projectId" placeholder="请选择项目" filterable
            @change="() => (form.assigneeIds = [])">
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
            :max-collapse-tags="3" collapse-tags-tooltip>
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

    <el-dialog v-model="dueDateDialogVisible" :title="claimDueDateRequired ? '认领任务并设置时间' : '修改预计截止日期'" width="420px"
      :show-close="!claimDueDateRequired" :close-on-click-modal="!claimDueDateRequired"
      :close-on-press-escape="!claimDueDateRequired">
      <el-form label-width="100px">
        <el-form-item label="任务名称">
          <div>{{ selectedTask?.title || '-' }}</div>
        </el-form-item>
        <el-form-item v-if="claimDueDateRequired" label="预计开始时间">
          <el-date-picker v-model="claimStartDate" type="date" value-format="YYYY-MM-DD" disabled
            style="width: 220px" />
        </el-form-item>
        <el-form-item label="预计截止日期">
          <el-date-picker v-model="dueDateForm.dueDate" type="date" value-format="YYYY-MM-DD" placeholder="请选择预计截止日期"
            style="width: 220px" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button v-if="!claimDueDateRequired" @click="dueDateDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submitSelectedTaskDueDate">{{ claimDueDateRequired ? '提交并认领' : '提交'
        }}</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="importTemplateDialogVisible" title="导入微电网标准工序" width="480px">
      <el-form label-width="110px">
        <el-form-item label="目标项目">
          <div>{{ selectedImportProjectName || '-' }}</div>
        </el-form-item>
        <el-form-item label="任务模板">
          <el-select v-model="selectedImportTemplateId" placeholder="请选择要导入的模板" filterable style="width: 100%">
            <el-option v-for="template in processTemplates" :key="template.id"
              :label="template.isDefault ? `${template.name}（默认）` : template.name" :value="template.id">
              <div class="template-option-row">
                <span class="template-option-name">{{ template.isDefault ? `${template.name}（默认）` : template.name
                }}</span>
                <el-button link type="primary" @click.stop.prevent="openTemplatePreview(template)">预览</el-button>
              </div>
            </el-option>
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="importTemplateDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submitImportMicrogridTemplate">确认导入</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="templatePreviewDialogVisible" :title="`模板预览：${selectedPreviewTemplate?.name || ''}`"
      width="760px">
      <el-table :data="previewTemplateSteps" max-height="420" style="width: 100%">
        <el-table-column label="序号" width="70">
          <template #default="{ $index }">{{ $index + 1 }}</template>
        </el-table-column>
        <el-table-column prop="stage" label="阶段" width="140" show-overflow-tooltip />
        <el-table-column prop="name" label="工序名称" min-width="240" show-overflow-tooltip />
        <el-table-column label="优先级" width="100">
          <template #default="{ row }">{{ getPriorityName(row.priority) }}</template>
        </el-table-column>
        <el-table-column prop="estimatedDays" label="预计工期(天)" width="130" />
      </el-table>
      <template #footer>
        <el-button type="primary" @click="templatePreviewDialogVisible = false">关闭</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="exportTemplateDialogVisible" title="导出微电网标准工序" width="460px">
      <el-form label-width="110px">
        <el-form-item label="目标项目">
          <div>{{ selectedExportProjectName || '-' }}</div>
        </el-form-item>
        <el-form-item label="模板名称">
          <el-input v-model="exportTemplateForm.templateName" placeholder="请输入导出模板名称" maxlength="100" show-word-limit />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="exportTemplateDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submitExportMicrogridTemplate">确认导出</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed, nextTick } from 'vue'
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
const claimDueDateRequired = ref(false)
const claimStartDate = ref('')
const importTemplateDialogVisible = ref(false)
const selectedImportTemplateId = ref<number | undefined>(undefined)
const selectedImportProjectName = ref('')
const templatePreviewDialogVisible = ref(false)
const selectedPreviewTemplate = ref<any | null>(null)
const exportTemplateDialogVisible = ref(false)
const selectedExportProjectName = ref('')
const projectSearchKeyword = ref('')
const projectAccessDenied = ref(false)
const projectAccessWarningShown = ref(false)
const lastDeniedProjectId = ref<number | null>(null)
const pendingFocusTaskId = ref<number | null>(null)
const rowPriorityUpdatingId = ref<number | null>(null)
const rowStatusUpdatingId = ref<number | null>(null)
const SHARED_FOLDER_PROJECT_NAME = '共享文件夹'

const priorityOptions = [
  { value: 1, label: '低' },
  { value: 2, label: '中' },
  { value: 3, label: '高' },
  { value: 4, label: '紧急' }
]

const statusOptions = [
  { value: 0, label: '待办' },
  { value: 1, label: '进行中' },
  { value: 2, label: '已完成' },
  { value: 3, label: '已取消' }
]

const dueDateForm = reactive({
  dueDate: ''
})

const exportTemplateForm = reactive({
  templateName: ''
})

const searchForm = reactive({
  keyword: '',
  projectId: undefined as number | undefined,
  status: undefined as number | undefined,
  overdueOnly: false,
  myOpenScope: false,
  sortBy: 'urgency' as 'urgency' | 'progress'
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
  ],
  startDate: [
    { required: true, message: '请选择预计开始时间', trigger: 'change' }
  ],
  dueDate: [
    { required: true, message: '请选择预计截止日期', trigger: 'change' },
    {
      validator: (_rule: any, value: string, callback: any) => {
        if (!value || !form.startDate) {
          callback()
          return
        }

        if (dayjs(value).isBefore(dayjs(form.startDate), 'day')) {
          callback(new Error('预计截止日期不能早于预计开始时间'))
          return
        }

        callback()
      },
      trigger: 'change'
    }
  ]
}

const processTemplates = ref<any[]>([])
const previewTemplateSteps = computed(() => {
  const steps = selectedPreviewTemplate.value?.steps || []
  return [...steps].sort((a: any, b: any) => (a.sortOrder || 0) - (b.sortOrder || 0))
})

const selectedProjectNameForCreate = computed(() => {
  const target = projects.value.find((item: any) => Number(item.id) === Number(searchForm.projectId))
  return target?.name || '-'
})

const getProjectOverdueTaskFlag = (project: any) => {
  if (!project) return false
  if (typeof project.hasOverdueTask === 'boolean') return project.hasOverdueTask
  if (typeof project.HasOverdueTask === 'boolean') return project.HasOverdueTask
  return false
}

const canCreateTask = computed(() => {
  return !!searchForm.projectId && !projectAccessDenied.value
})

const canOperateTemplateActions = computed(() => {
  return !!searchForm.projectId && !projectAccessDenied.value
})

const getProjectStatusName = (project: any) => {
  const fromApi = `${project?.statusName || ''}`.trim()
  if (fromApi) {
    return fromApi
  }

  const statusMap: Record<number, string> = {
    0: '规划中',
    1: '进行中',
    2: '已完成',
    3: '已暂停'
  }

  return statusMap[Number(project?.status)] || '未知'
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

const buildProjectStatusSummary = (project: any) => {
  const statusName = getProjectStatusName(project)
  const hasTaskOverdue = getProjectOverdueTaskFlag(project)
  const hasProjectOverdue = isProjectOverdue(project)

  const statusClassMap: Record<string, string> = {
    '规划中': 'planning',
    '进行中': 'active',
    '已完成': 'done',
    '已暂停': 'paused'
  }

  return {
    statusName,
    statusClass: statusClassMap[statusName] || 'planning',
    projectOverdue: hasProjectOverdue,
    taskOverdue: hasTaskOverdue
  }
}

const filteredProjectTreeData = computed(() => {
  const keyword = `${projectSearchKeyword.value || ''}`.trim().toLowerCase()
  const source = projects.value || []

  const matched = keyword
    ? source.filter((item: any) => `${item?.name || ''}`.toLowerCase().includes(keyword))
    : source

  return matched.map((item: any) => {
    const statusMeta = buildProjectStatusSummary(item)
    const managerName = `${item?.managerName || item?.projectManagerName || '-'}`.trim() || '-'
    return {
      id: item.id,
      label: item.name,
      name: item.name,
      managerDisplay: managerName,
      statusName: statusMeta.statusName,
      statusClass: statusMeta.statusClass,
      projectOverdue: statusMeta.projectOverdue,
      taskOverdue: statusMeta.taskOverdue
    }
  })
})

const getUserLabel = (user: any) => {
  return user.realName ? `${user.realName} (${user.username})` : user.username
}

const getApiErrorMessage = (error: any, fallback: string) => {
  const responseData = error?.response?.data

  if (responseData?.errors && typeof responseData.errors === 'object') {
    const firstFieldErrors: string[] = []
    for (const current of Object.values(responseData.errors) as any[]) {
      if (!Array.isArray(current)) {
        continue
      }

      for (const item of current) {
        if (typeof item === 'string') {
          firstFieldErrors.push(item)
        }
      }
    }

    if (firstFieldErrors.length > 0) {
      return firstFieldErrors[0]
    }
  }

  if (responseData?.message) {
    return responseData.message
  }

  return fallback
}

const normalizeQuery = (query: Record<string, any>) => {
  const normalized: Record<string, string> = {}
  Object.keys(query || {}).forEach((key) => {
    const value = query[key]
    if (value === undefined || value === null || value === '') return
    if (Array.isArray(value)) {
      if (value.length > 0 && value[0] !== undefined && value[0] !== null) {
        normalized[key] = String(value[0])
      }
      return
    }
    normalized[key] = String(value)
  })
  return normalized
}

const isSameQuery = (left: Record<string, any>, right: Record<string, any>) => {
  const leftNormalized = normalizeQuery(left)
  const rightNormalized = normalizeQuery(right)
  const leftKeys = Object.keys(leftNormalized).sort()
  const rightKeys = Object.keys(rightNormalized).sort()
  if (leftKeys.length !== rightKeys.length) return false
  return leftKeys.every((key, index) => key === rightKeys[index] && leftNormalized[key] === rightNormalized[key])
}

const syncListRouteQuery = (options?: { focusTaskId?: number | null }) => {
  const nextQuery: Record<string, any> = {
    ...route.query
  }

  if (searchForm.projectId) {
    nextQuery.projectId = String(searchForm.projectId)
  } else {
    delete nextQuery.projectId
  }

  if (searchForm.overdueOnly) {
    nextQuery.overdueOnly = 'true'
  } else {
    delete nextQuery.overdueOnly
  }

  if (searchForm.myOpenScope) {
    nextQuery.myOpenScope = 'true'
  } else {
    delete nextQuery.myOpenScope
  }

  if (searchForm.sortBy === 'urgency') {
    nextQuery.sortBy = 'urgency'
  } else if (searchForm.sortBy === 'progress') {
    nextQuery.sortBy = 'progress'
  } else {
    delete nextQuery.sortBy
  }

  if (options && Object.prototype.hasOwnProperty.call(options, 'focusTaskId')) {
    const nextFocusTaskId = Number(options.focusTaskId || 0)
    if (nextFocusTaskId > 0) {
      nextQuery.focusTaskId = String(nextFocusTaskId)
    } else {
      delete nextQuery.focusTaskId
    }
  }

  if (!isSameQuery(route.query as Record<string, any>, nextQuery)) {
    router.replace({
      path: '/tasks',
      query: nextQuery
    })
    return true
  }

  return false
}

const fetchTasks = async () => {
  if (!searchForm.projectId) {
    tasks.value = []
    pagination.total = 0
    selectedTask.value = null
    taskTableRef.value?.clearSelection()
    projectAccessDenied.value = false
    return
  }

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
    if (searchForm.myOpenScope) params.myOpenScope = true
    if (searchForm.sortBy === 'urgency') params.sortBy = 'urgency'
    if (searchForm.sortBy === 'progress') params.sortBy = 'progress'

    const res = await request.get('/tasks', { params, headers: { 'X-Silent-Error': '1' } })
    tasks.value = res.data.items || []
    selectedTask.value = null
    taskTableRef.value?.clearSelection()
    pagination.total = res.data.totalCount
    projectAccessDenied.value = false
    projectAccessWarningShown.value = false
    lastDeniedProjectId.value = null

    const focusTaskIdFromRoute = Number(route.query.focusTaskId)
    const focusTaskId = pendingFocusTaskId.value || (focusTaskIdFromRoute > 0 ? focusTaskIdFromRoute : null)
    if (focusTaskId) {
      const targetTask = tasks.value.find((item: any) => Number(item.id) === focusTaskId)
      if (targetTask) {
        await nextTick()
        taskTableRef.value?.toggleRowSelection(targetTask, true)
        selectedTask.value = targetTask
      }

      pendingFocusTaskId.value = null
      syncListRouteQuery({ focusTaskId: null })
    }
  } catch (error) {
    tasks.value = []
    pagination.total = 0
    selectedTask.value = null
    taskTableRef.value?.clearSelection()
    const statusCode = (error as any)?.response?.status
    projectAccessDenied.value = statusCode === 403
    if (statusCode === 403) {
      const currentProjectId = Number(searchForm.projectId || 0)
      const shouldWarn = !projectAccessWarningShown.value || lastDeniedProjectId.value !== currentProjectId
      if (shouldWarn) {
        ElMessage.warning(getApiErrorMessage(error, '暂无该项目访问权限'))
        projectAccessWarningShown.value = true
        lastDeniedProjectId.value = currentProjectId
      }
    } else {
      ElMessage.warning(getApiErrorMessage(error, '获取任务列表失败，请稍后重试'))
    }
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

const fetchProcessTemplates = async () => {
  try {
    const res = await request.get('/process-templates')
    processTemplates.value = res.data || []
  } catch (error) {
    console.error('获取项目任务模板失败：', error)
  }
}

const handleProjectNodeClick = (node: any) => {
  const nextProjectId = Number(node?.id || 0)
  if (!nextProjectId) {
    return
  }

  if (Number(searchForm.projectId) === nextProjectId) {
    return
  }

  searchForm.projectId = nextProjectId
  selectedTask.value = null
  handleSearch()
}

const handleSearch = () => {
  pagination.page = 1
  const replaced = syncListRouteQuery({ focusTaskId: null })
  if (!replaced) {
    fetchTasks()
  }
}

const resetSearch = () => {
  searchForm.keyword = ''
  searchForm.status = undefined
  searchForm.overdueOnly = false
  searchForm.myOpenScope = false
  searchForm.sortBy = 'urgency'
  handleSearch()
}

const togglePrioritySort = () => {
  searchForm.sortBy = searchForm.sortBy === 'urgency' ? 'progress' : 'urgency'
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
  if (!searchForm.projectId) {
    ElMessage.warning('请先在左侧选择项目')
    return
  }

  if (projectAccessDenied.value) {
    ElMessage.warning('暂无该项目访问权限，不能新建任务')
    return
  }

  isEdit.value = false
  editId.value = null
  originalEditForm.value = null
  resetForm()
  if (searchForm.projectId) {
    form.projectId = Number(searchForm.projectId)
  }
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
        ElMessage.error(getApiErrorMessage(error, '操作失败，请稍后重试'))
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
  const isFirstClaim = !row?.assigneeId
  if (isFirstClaim) {
    selectedTask.value = row
    claimStartDate.value = dayjs().format('YYYY-MM-DD')
    dueDateForm.dueDate = row?.dueDate || ''
    claimDueDateRequired.value = true
    dueDateDialogVisible.value = true
    return
  }

  try {
    await request.put(`/tasks/${row.id}/claim`, {})
    ElMessage.success('任务认领成功')
    await fetchTasks()
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

const applyTaskRowUpdate = (updatedTask: any) => {
  if (!updatedTask?.id) {
    return
  }

  const targetId = Number(updatedTask.id)
  const index = tasks.value.findIndex((item: any) => Number(item.id) === targetId)
  if (index >= 0) {
    tasks.value[index] = {
      ...tasks.value[index],
      ...updatedTask
    }
  }

  if (selectedTask.value && Number(selectedTask.value.id) === targetId) {
    selectedTask.value = {
      ...selectedTask.value,
      ...updatedTask
    }
  }
}

const handleRowPriorityChange = async (row: any, nextPriority: number) => {
  if (!row || !row.id) {
    return
  }

  const oldPriority = Number(row.priority)
  if (oldPriority === nextPriority) {
    return
  }

  applyTaskRowUpdate({ id: row.id, priority: nextPriority })
  rowPriorityUpdatingId.value = Number(row.id)
  try {
    const res = await request.put(`/tasks/${row.id}`, { priority: nextPriority }, { headers: { 'X-Silent-Error': '1' } })
    applyTaskRowUpdate(res?.data || { id: row.id, priority: nextPriority })
    ElMessage.success('优先级已更新')
  } catch (error) {
    applyTaskRowUpdate({ id: row.id, priority: oldPriority })
    const message = (error as any)?.response?.data?.message || '优先级更新失败'
    ElMessage.warning(message)
  } finally {
    rowPriorityUpdatingId.value = null
  }
}

const handleRowStatusChange = async (row: any, nextStatus: number) => {
  if (!row || !row.id) {
    return
  }

  const oldStatus = Number(row.status)
  if (oldStatus === nextStatus) {
    return
  }

  applyTaskRowUpdate({ id: row.id, status: nextStatus })
  rowStatusUpdatingId.value = Number(row.id)
  try {
    const res = await request.put(`/tasks/${row.id}/status`, { status: nextStatus }, { headers: { 'X-Silent-Error': '1' } })
    applyTaskRowUpdate(res?.data || { id: row.id, status: nextStatus })
    ElMessage.success('状态已更新')
  } catch (error) {
    applyTaskRowUpdate({ id: row.id, status: oldStatus })
    const message = (error as any)?.response?.data?.message || ''
    if (nextStatus === 2 && `${message}`.includes('提交一次工作内容')) {
      ElMessage.warning('请先填写并提交工作内容')
      router.push({
        path: `/tasks/${row.id}`,
        query: {
          needWorkContent: 'true',
          projectId: searchForm.projectId ? String(searchForm.projectId) : undefined,
          overdueOnly: searchForm.overdueOnly ? 'true' : undefined,
          myOpenScope: searchForm.myOpenScope ? 'true' : undefined,
          focusTaskId: String(row.id)
        }
      })
      return
    }

    ElMessage.warning(message || '状态更新失败')
  } finally {
    rowStatusUpdatingId.value = null
  }
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
        query: {
          needWorkContent: 'true',
          projectId: searchForm.projectId ? String(searchForm.projectId) : undefined,
          overdueOnly: searchForm.overdueOnly ? 'true' : undefined,
          myOpenScope: searchForm.myOpenScope ? 'true' : undefined,
          focusTaskId: String(id)
        }
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

  claimDueDateRequired.value = false
  claimStartDate.value = ''
  dueDateForm.dueDate = selectedTask.value.dueDate || ''
  dueDateDialogVisible.value = true
}

const submitSelectedTaskDueDate = async () => {
  if (!selectedTask.value) {
    ElMessage.warning('请先选择一个任务')
    return
  }

  if (!claimDueDateRequired.value && !canOperateTask(selectedTask.value)) {
    ElMessage.warning('项目成员仅可修改自己负责的任务')
    return
  }

  if (!dueDateForm.dueDate) {
    ElMessage.warning('请选择预计截止日期')
    return
  }

  const nextDueDate = dueDateForm.dueDate
  const currentDueDate = selectedTask.value.dueDate || null
  if (!claimDueDateRequired.value && nextDueDate === currentDueDate) {
    ElMessage.warning('预计截止时间未变化')
    return
  }

  try {
    if (claimDueDateRequired.value) {
      await request.put(`/tasks/${selectedTask.value.id}/claim`, { dueDate: nextDueDate })
      ElMessage.success('任务认领成功')
    } else {
      await request.put(`/tasks/${selectedTask.value.id}`, { dueDate: nextDueDate })
      ElMessage.success('预计截止时间已更新')
    }
    dueDateDialogVisible.value = false
    claimDueDateRequired.value = false
    claimStartDate.value = ''
    await fetchTasks()
  } catch (error) {
    const message = (error as any)?.response?.data?.message || (claimDueDateRequired.value ? '任务认领失败' : '更新预计截止时间失败')
    ElMessage.warning(message)
    console.error('提交失败：', error)
  }
}

const importMicrogridTemplate = async () => {
  if (!searchForm.projectId) {
    ElMessage.warning('请先在筛选区选择要导入模板的项目')
    return
  }

  const targetProject = projects.value.find((item: any) => Number(item.id) === Number(searchForm.projectId))
  selectedImportProjectName.value = targetProject?.name || ''

  if (!processTemplates.value.length) {
    await fetchProcessTemplates()
  }

  if (!processTemplates.value.length) {
    ElMessage.warning('暂无可导入的任务模板')
    return
  }

  const defaultTemplate = processTemplates.value.find((item: any) => item.isDefault)
  selectedImportTemplateId.value = defaultTemplate?.id ?? processTemplates.value[0]?.id
  importTemplateDialogVisible.value = true
}

const openTemplatePreview = (template: any) => {
  selectedPreviewTemplate.value = template
  templatePreviewDialogVisible.value = true
}

const submitImportMicrogridTemplate = async () => {
  if (!searchForm.projectId) {
    ElMessage.warning('请先选择项目')
    return
  }

  if (!selectedImportTemplateId.value) {
    ElMessage.warning('请选择要导入的模板')
    return
  }

  try {
    await request.post(`/projects/${searchForm.projectId}/tasks/microgrid-template`, {
      templateId: selectedImportTemplateId.value
    })
    ElMessage.success('微电网标准工序导入成功')
    importTemplateDialogVisible.value = false
    fetchTasks()
  } catch (error) {
    console.error('导入模板失败：', error)
  }
}

const exportMicrogridTemplate = async () => {
  if (!searchForm.projectId) {
    ElMessage.warning('请先在筛选区选择要导出模板的项目')
    return
  }

  const targetProject = projects.value.find((item: any) => Number(item.id) === Number(searchForm.projectId))
  selectedExportProjectName.value = targetProject?.name || ''
  exportTemplateForm.templateName = `${selectedExportProjectName.value || '项目'}-导出工序-${dayjs().format('YYYYMMDDHHmmss')}`
  exportTemplateDialogVisible.value = true
}

const submitExportMicrogridTemplate = async () => {
  if (!searchForm.projectId) {
    ElMessage.warning('请先选择项目')
    return
  }

  const templateName = `${exportTemplateForm.templateName || ''}`.trim()
  if (!templateName) {
    ElMessage.warning('请输入模板名称')
    return
  }

  try {
    const res = await request.post(`/projects/${searchForm.projectId}/tasks/microgrid-template/export`, {
      templateName
    })
    ElMessage.success(res.message || '微电网标准工序导出成功')
    exportTemplateDialogVisible.value = false
  } catch (error) {
    console.error('导出模板失败：', error)
  }
}

const viewTask = (id: number) => {
  router.push({
    path: `/tasks/${id}`,
    query: {
      projectId: searchForm.projectId ? String(searchForm.projectId) : undefined,
      overdueOnly: searchForm.overdueOnly ? 'true' : undefined,
      myOpenScope: searchForm.myOpenScope ? 'true' : undefined,
      sortBy: searchForm.sortBy === 'urgency' ? 'urgency' : (searchForm.sortBy === 'progress' ? 'progress' : undefined),
      focusTaskId: String(id)
    }
  })
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

const getPriorityVisualClass = (priority: number) => {
  const classMap: Record<number, string> = {
    1: 'is-low',
    2: 'is-medium',
    3: 'is-high',
    4: 'is-urgent'
  }

  return classMap[priority] || 'is-low'
}

const getStatusVisualClass = (status: number) => {
  const classMap: Record<number, string> = {
    0: 'is-todo',
    1: 'is-progress',
    2: 'is-done',
    3: 'is-cancelled'
  }

  return classMap[status] || 'is-todo'
}

const formatDate = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD')
}

const isCompletedLate = (row: any) => {
  if (!row || row.status !== 2) return false
  if (!row.completedAt || !row.dueDate) return false
  return dayjs(row.completedAt).isAfter(dayjs(row.dueDate), 'day')
}

const isTaskOverdueRow = (row: any) => {
  if (!row?.dueDate) return false
  const status = Number(row.status)
  if (status === 2 || status === 3) return false
  return dayjs().isAfter(dayjs(row.dueDate), 'day')
}

const getTaskRowClass = ({ row }: { row: any }) => {
  if (isTaskOverdueRow(row)) {
    return 'task-row-overdue'
  }

  if (isCompletedLate(row)) {
    return 'task-row-completed-late'
  }

  return ''
}

onMounted(async () => {
  const userStr = localStorage.getItem('user')
  if (userStr) {
    currentUser.value = JSON.parse(userStr)
  }

  searchForm.overdueOnly = route.query.overdueOnly === 'true'
  searchForm.myOpenScope = route.query.myOpenScope === 'true'
  searchForm.sortBy = route.query.sortBy === 'progress'
    ? 'progress'
    : (route.query.sortBy === 'urgency' || route.query.sortBy === 'priority' ? 'urgency' : 'urgency')
  const queryProjectId = Number(route.query.projectId)
  const queryFocusTaskId = Number(route.query.focusTaskId)
  if (!Number.isNaN(queryProjectId) && queryProjectId > 0) {
    searchForm.projectId = queryProjectId
  }
  if (!Number.isNaN(queryFocusTaskId) && queryFocusTaskId > 0) {
    pendingFocusTaskId.value = queryFocusTaskId
  }

  await fetchProjects()
  if (!searchForm.projectId && projects.value.length > 0) {
    searchForm.projectId = projects.value[0].id
  }
  const replaced = syncListRouteQuery({ focusTaskId: pendingFocusTaskId.value })
  if (!replaced) {
    await fetchTasks()
  }
  fetchUsers()
  fetchProcessTemplates()
})
</script>

<style scoped>
.task-page {
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

.task-page :deep(.el-card) {
  width: 100%;
  display: flex;
  flex-direction: column;
  border-radius: 16px;
  border: 1px solid var(--biz-card-border);
  background: var(--biz-card-bg);
  box-shadow: 0 18px 40px rgba(36, 66, 135, 0.12);
  overflow: hidden;
}

.task-page :deep(.el-card__header) {
  background: #f4f9ff;
  border-bottom: 1px solid #dce8fb;
}

.task-page :deep(.el-card__body) {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
  padding: 14px;
}

.explorer-layout {
  flex: 1;
  min-height: 0;
  display: flex;
  gap: 14px;
}

.left-tree-panel {
  width: 264px;
  border: 1px solid #dce8fb;
  border-radius: 12px;
  padding: 12px;
  display: flex;
  flex-direction: column;
  min-height: 0;
  background: #f7fbff;
  box-shadow: 0 8px 20px rgba(22, 58, 123, 0.08);
}

.panel-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  font-size: 13px;
  color: var(--biz-text-muted);
  margin-bottom: 10px;
  font-weight: 700;
}

.project-search-input {
  width: 136px;
}

.left-tree-panel :deep(.el-tree) {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  background: transparent;
}

.left-tree-panel :deep(.el-tree-node__content) {
  min-height: 36px;
  height: auto;
  align-items: flex-start;
  border-radius: 8px;
  padding: 6px;
}

.left-tree-panel :deep(.el-tree-node__content:hover) {
  background: #edf5ff;
}

.left-tree-panel :deep(.el-tree--highlight-current .el-tree-node.is-current > .el-tree-node__content) {
  background: #dceaff;
  color: #1248a6;
  font-weight: 700;
}

.project-tree-node {
  display: flex;
  flex-direction: column;
  gap: 3px;
  max-width: calc(100% - 24px);
  width: 100%;
  padding-right: 6px;
}

.project-tree-name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
  line-height: 1.2;
}

.project-manager-tag {
  max-width: 100%;
  border-radius: 999px;
  border-color: #d2e2fc;
  background: #f4f8ff;
  color: #395171;
  font-weight: 500;
}

.project-manager-tag :deep(.el-tag__content) {
  display: inline-block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.project-tree-meta {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-wrap: wrap;
  min-height: 18px;
  line-height: 1.2;
}

.project-tree-badge {
  font-size: 11px;
  line-height: 1;
  padding: 2px 6px;
  border-radius: 10px;
  background: #edf3ff;
  color: #516884;
}

.project-tree-badge.planning {
  color: var(--el-text-color-secondary);
}

.project-tree-badge.active {
  color: #245cbc;
  background: #e6efff;
}

.project-tree-badge.done {
  color: #137657;
  background: #e8f8f2;
}

.project-tree-badge.paused {
  color: #7f5800;
  background: #fff4da;
}

.project-tree-badge.danger {
  color: #bf3a3a;
  background: #fdeeee;
}

.right-content-panel {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
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

.header-actions {
  display: flex;
  gap: 8px;
}

.search-bar {
  display: flex;
  gap: 10px;
  margin-bottom: 12px;
  flex-wrap: wrap;
  align-items: center;
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid #dbe8ff;
  background: #f8fbff;
}

.content-area {
  flex: 1;
  min-height: 0;
  overflow: auto;
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

.overdue {
  color: #c23a3a;
  font-weight: 700;
}

.completed-late {
  color: #f56c6c;
}

.task-title-text {
  display: inline-flex;
  align-items: center;
  gap: 1px;
  max-width: 100%;
}

.task-title-overdue {
  color: #a62d2d;
  font-weight: 700;
}

.task-overdue-dot {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: #d63f3f;
  color: #fff;
  font-size: 12px;
  font-weight: 700;
  line-height: 1;
  flex-shrink: 0;
}

.task-page :deep(.el-table .task-row-overdue > td.el-table__cell) {
  background: #fff2f2;
}

.task-page :deep(.el-table .task-row-overdue > td.el-table__cell:first-child) {
  box-shadow: inset 4px 0 0 #d63f3f;
}

.task-page :deep(.el-table .task-row-overdue:hover > td.el-table__cell) {
  background: #ffe8e8;
}

.task-page :deep(.el-table .task-row-completed-late > td.el-table__cell) {
  background: #fff7ef;
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

.task-page :deep(.priority-cell .cell),
.task-page :deep(.status-cell .cell) {
  display: flex;
  align-items: center;
}

.biz-inline-select {
  width: 104px;
}

.biz-inline-select :deep(.el-select__wrapper) {
  min-height: 30px;
  border-radius: 999px;
  border: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
  box-shadow: none;
  padding-left: 10px;
  padding-right: 22px;
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
}

.biz-inline-select :deep(.el-select__wrapper:hover) {
  border-color: var(--el-color-primary-light-5);
}

.biz-inline-select :deep(.el-select__wrapper.is-focused) {
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 2px var(--el-color-primary-light-9);
}

.biz-inline-select :deep(.el-select__selected-item) {
  font-size: 12px;
  font-weight: 700;
  color: var(--el-text-color-primary);
}

.biz-inline-select :deep(.el-select__caret) {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.biz-select-dot,
.biz-option-dot {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  background: currentColor;
  flex-shrink: 0;
}

.biz-option-row {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  line-height: 1.2;
}

.inline-cell-tag {
  border: 1px solid transparent;
  border-radius: 999px;
  font-weight: 700;
  min-width: 104px;
  justify-content: center;
  padding: 0 10px;
  box-sizing: border-box;
}

.biz-inline-select.is-low :deep(.el-select__wrapper),
.inline-cell-tag.is-low {
  border-color: var(--el-color-info-light-4);
  background: var(--el-color-info-light-8);
}

.biz-inline-select.is-medium :deep(.el-select__wrapper),
.inline-cell-tag.is-medium {
  border-color: var(--el-color-warning-light-4);
  background: var(--el-color-warning-light-8);
}

.biz-inline-select.is-high :deep(.el-select__wrapper),
.inline-cell-tag.is-high {
  border-color: var(--el-color-danger-light-4);
  background: var(--el-color-danger-light-8);
}

.biz-inline-select.is-urgent :deep(.el-select__wrapper),
.inline-cell-tag.is-urgent {
  border-color: var(--el-color-danger-light-3);
  background: var(--el-color-danger-light-7);
}

.biz-inline-select.is-todo :deep(.el-select__wrapper),
.inline-cell-tag.is-todo {
  border-color: var(--el-color-info-light-4);
  background: var(--el-color-info-light-8);
}

.biz-inline-select.is-progress :deep(.el-select__wrapper),
.inline-cell-tag.is-progress {
  border-color: var(--el-color-primary-light-4);
  background: var(--el-color-primary-light-8);
}

.biz-inline-select.is-done :deep(.el-select__wrapper),
.inline-cell-tag.is-done {
  border-color: var(--el-color-success-light-4);
  background: var(--el-color-success-light-8);
}

.biz-inline-select.is-cancelled :deep(.el-select__wrapper),
.inline-cell-tag.is-cancelled {
  border-color: var(--el-color-danger-light-4);
  background: var(--el-color-danger-light-8);
}

.biz-select-dot.is-low,
.inline-cell-tag.is-low,
.biz-option-row.is-low {
  color: var(--el-color-info-dark-2);
}

.biz-select-dot.is-medium,
.inline-cell-tag.is-medium,
.biz-option-row.is-medium {
  color: var(--el-color-warning-dark-2);
}

.biz-select-dot.is-high,
.inline-cell-tag.is-high,
.biz-option-row.is-high {
  color: var(--el-color-danger-dark-2);
}

.biz-select-dot.is-urgent,
.inline-cell-tag.is-urgent,
.biz-option-row.is-urgent {
  color: var(--el-color-danger-dark-2);
}

.biz-select-dot.is-todo,
.inline-cell-tag.is-todo,
.biz-option-row.is-todo {
  color: var(--el-color-info-dark-2);
}

.biz-select-dot.is-progress,
.inline-cell-tag.is-progress,
.biz-option-row.is-progress {
  color: var(--el-color-primary-dark-2);
}

.biz-select-dot.is-done,
.inline-cell-tag.is-done,
.biz-option-row.is-done {
  color: var(--el-color-success-dark-2);
}

.biz-select-dot.is-cancelled,
.inline-cell-tag.is-cancelled,
.biz-option-row.is-cancelled {
  color: var(--el-color-danger-dark-2);
}

.task-page :deep(.task-inline-select-popper.el-select__popper) {
  z-index: 3000;
}

.task-page :deep(.task-inline-select-popper .el-select-dropdown) {
  min-width: 136px !important;
  border-radius: 12px;
  border: 1px solid #dbe6f8;
  background: #fff;
  box-shadow: 0 16px 24px rgba(14, 40, 94, 0.16);
}

.task-page :deep(.task-inline-select-popper .el-select-dropdown__item) {
  height: 34px;
  line-height: 34px;
  border-radius: 8px;
  margin: 2px 6px;
  padding: 0 10px;
}

.task-page :deep(.task-inline-select-popper .el-select-dropdown__item:hover),
.task-page :deep(.task-inline-select-popper .el-select-dropdown__item.is-hovering) {
  background: #edf4ff;
}

.task-page :deep(.task-inline-select-popper .el-select-dropdown__item.is-selected) {
  font-weight: 700;
  background: #e4eeff;
}

.task-page :deep(.el-table) {
  border-radius: 12px;
  border: 1px solid #dce8fb;
  overflow: hidden;
}

.task-page :deep(.el-table th.el-table__cell) {
  background: #f3f8ff;
  color: #2e4566;
  font-weight: 700;
}

.task-page :deep(.el-table tr:hover > td.el-table__cell) {
  background: #f5f9ff;
}

.task-page :deep(.el-button) {
  border-radius: 999px;
}

.task-page :deep(.el-input__wrapper),
.task-page :deep(.el-select__wrapper),
.task-page :deep(.el-textarea__inner) {
  border-radius: 999px;
}

.task-page :deep(.el-textarea__inner) {
  border-radius: 12px;
}

.template-option-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.template-option-name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  margin-right: 12px;
}

@media (max-width: 1200px) {
  .left-tree-panel {
    width: 236px;
  }
}

@media (max-width: 900px) {
  .task-page {
    padding: 8px;
  }

  .task-page :deep(.el-card__body) {
    padding: 10px;
  }

  .card-header>span {
    font-size: 17px;
  }

  .explorer-layout {
    flex-direction: column;
  }

  .left-tree-panel {
    width: 100%;
    max-height: 240px;
  }
}
</style>
