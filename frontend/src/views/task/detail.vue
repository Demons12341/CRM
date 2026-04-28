<template>
  <div class="task-detail-page">
    <el-card v-loading="loading">
      <template #header>
        <div class="card-header">
          <span>任务详情</span>
          <el-button link type="primary" @click="goBack">返回任务列表</el-button>
        </div>
      </template>

    <el-descriptions v-if="task" :column="2" border>
      <el-descriptions-item label="任务名称" :span="2">{{ task.title }}</el-descriptions-item>
        <el-descriptions-item label="所属项目">{{ task.projectName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="责任人">{{ task.assigneeDisplay || task.assigneeName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="优先级">
          <el-tag :type="getPriorityType(task.priority)">{{ task.priorityName }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="状态">
          <el-tag :type="getStatusType(task.status)">{{ task.statusName }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="预计开始时间">{{ formatDate(task.startDate) }}</el-descriptions-item>
        <el-descriptions-item label="预计截止日期">{{ formatDate(task.dueDate) }}</el-descriptions-item>
        <el-descriptions-item label="超期原因" :span="2">{{ task.overdueReason || '-' }}</el-descriptions-item>
        <el-descriptions-item label="描述" :span="2">{{ task.description || '-' }}</el-descriptions-item>
      </el-descriptions>

      <div class="work-submission-section">
        <div class="work-submission-title">任务提交记录</div>
        <el-timeline v-if="workSubmissionLogs.length">
          <el-timeline-item
            v-for="item in workSubmissionLogs"
            :key="item.id"
            :timestamp="formatDateTime(item.createdAt)">
            <div class="work-submission-item">
              <div class="work-submission-meta">{{ item.username }}</div>
              <div class="work-submission-content">
                <div
                  v-for="(line, lineIndex) in item.summaryLines"
                  :key="`${item.id}-line-${lineIndex}`"
                  class="work-summary-line">
                  <template v-if="line.type === 'deliverables'">
                    <span>交付物：</span>
                    <template v-if="line.deliverableNames.length">
                      <span v-for="(name, index) in line.deliverableNames" :key="`${item.id}-${name}-${index}`">
                        <template v-if="isImageName(name)">
                          <a class="deliverable-thumb-link" href="#" @click.prevent="handleDeliverableClick(name)">
                            <img
                              v-if="deliverableThumbnailMap[normalizeDeliverableKey(name)]"
                              :src="deliverableThumbnailMap[normalizeDeliverableKey(name)]"
                              :alt="name"
                              class="deliverable-thumb-image" />
                            <span v-else class="deliverable-thumb-fallback">图片</span>
                          </a>
                        </template>
                        <el-link v-else type="primary" @click="handleDeliverableClick(name)">{{ name }}</el-link>
                        <span v-if="Number(index) < line.deliverableNames.length - 1">；</span>
                      </span>
                    </template>
                    <span v-else>{{ line.rawValue }}</span>
                  </template>
                  <template v-else>
                    {{ line.raw }}
                  </template>
                </div>
              </div>
            </div>
          </el-timeline-item>
        </el-timeline>
        <el-empty v-else description="暂无任务提交记录" />
      </div>

      <div class="submit-actions">
        <el-button v-if="canClaimTask" type="warning" :loading="claimLoading" @click="claimTask">认领任务</el-button>
        <el-button v-if="canEditTask" type="primary" @click="openWorkDialog">提交工作记录</el-button>
        <el-button v-if="canEditTask" type="success" :loading="completeLoading"
          :disabled="task?.status === 2 || task?.status === 3" @click="completeTask">
          完成任务
        </el-button>
      </div>

      <div class="log-actions">
        <el-button @click="openLogDialog">查看操作日志</el-button>
      </div>
    </el-card>

    <el-dialog v-model="workDialogVisible" title="提交工作记录" width="700px">
      <el-form :model="workSubmitForm" label-width="95px">
        <el-form-item label="完成内容">
          <el-input v-model="workSubmitForm.workContent" type="textarea" :rows="3" placeholder="请填写本次具体完成了什么" />
        </el-form-item>
        <el-form-item label="交付物">
          <div class="deliverables-row">
            <el-input v-model="workSubmitForm.deliverables" placeholder="如：施工图V2、调试报告、现场照片" />
            <el-upload :show-file-list="false" :before-upload="beforeDeliverableUpload"
              :http-request="uploadDeliverableFile" :disabled="uploadDeliverableLoading">
              <el-button :loading="uploadDeliverableLoading">上传文件</el-button>
            </el-upload>
          </div>
          <div v-if="uploadDeliverableLoading" class="deliverables-progress">
            <div class="deliverables-progress-text">正在上传：{{ uploadingDeliverableName || '交付物文件' }}（{{ uploadDeliverableProgress }}%）</div>
            <el-progress :percentage="uploadDeliverableProgress" :stroke-width="8" />
          </div>
          <div class="deliverables-hint">{{ deliverablesHintText }}</div>
        </el-form-item>
        <el-form-item label="阻塞问题">
          <el-input v-model="workSubmitForm.blockers" placeholder="如：设备未到货、接口人未确认" />
        </el-form-item>
        <el-form-item label="下一步计划">
          <el-input v-model="workSubmitForm.nextPlan" placeholder="下一步计划工作" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="workDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitLoading" @click="submitWork">提交工作记录</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="logDialogVisible" title="操作日志" width="900px">
      <el-table v-if="logs.length" :data="logs" style="width: 100%">
        <el-table-column prop="username" label="操作人" width="140" />
        <el-table-column prop="action" label="操作" width="140" />
        <el-table-column prop="oldValue" label="原值" min-width="220" show-overflow-tooltip>
          <template #default="{ row }">{{ row.oldValue || '-' }}</template>
        </el-table-column>
        <el-table-column prop="newValue" label="新值" min-width="220" show-overflow-tooltip>
          <template #default="{ row }">{{ row.newValue || '-' }}</template>
        </el-table-column>
        <el-table-column prop="createdAt" label="时间" width="180">
          <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="暂无操作日志" />
    </el-dialog>

  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, inject } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import axios from 'axios'
import { request } from '@/api/request'
import dayjs from 'dayjs'

const route = useRoute()
const router = useRouter()

const taskId = computed(() => Number(route.params.id))
const loading = ref(false)
const submitLoading = ref(false)
const completeLoading = ref(false)
const claimLoading = ref(false)
const uploadDeliverableLoading = ref(false)
const uploadDeliverableProgress = ref(0)
const uploadingDeliverableName = ref('')
const workDialogVisible = ref(false)
const logDialogVisible = ref(false)
const deliverableThumbnailMap = ref<Record<string, string>>({})

const task = ref<any>(null)
const logs = ref<any[]>([])
const currentUser = ref<any>(null)
const taskFolderId = ref<number | null>(null)

const extractDeliverableNames = (summary: string) => {
  const lines = `${summary || ''}`
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)

  const deliverableLine = lines.find((line) => line.startsWith('交付物：'))
  if (!deliverableLine) {
    return [] as string[]
  }

  return deliverableLine
    .replace('交付物：', '')
    .split(/[；;，,、]/)
    .map((name) => name.trim())
    .filter(Boolean)
}

const buildWorkSummaryLines = (summary: string) => {
  return `${summary || ''}`
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      if (line.startsWith('交付物：')) {
        const rawValue = line.replace('交付物：', '').trim()
        return {
          type: 'deliverables',
          raw: line,
          rawValue,
          deliverableNames: rawValue
            .split(/[；;，,、]/)
            .map((name) => name.trim())
            .filter(Boolean)
        }
      }

      return {
        type: 'text',
        raw: line,
        rawValue: '',
        deliverableNames: [] as string[]
      }
    })
}

const workSubmissionLogs = computed(() => {
  return [...logs.value]
    .filter((item: any) => item?.action === '工作提交' && `${item?.newValue || ''}`.trim())
    .map((item: any) => ({
      ...item,
      deliverableNames: extractDeliverableNames(item?.newValue || ''),
      summaryLines: buildWorkSummaryLines(item?.newValue || '')
    }))
    .sort((a: any, b: any) => dayjs(a.createdAt).valueOf() - dayjs(b.createdAt).valueOf())
})

const isCurrentUserAssignee = computed(() => {
  if (!task.value || !currentUser.value?.id) return false

  const currentUserId = Number(currentUser.value.id)
  const assigneeIds = Array.isArray(task.value.assigneeIds)
    ? task.value.assigneeIds.map((id: any) => Number(id))
    : []

  if (assigneeIds.length > 0) {
    return assigneeIds.includes(currentUserId)
  }

  if (task.value.assigneeId !== null && task.value.assigneeId !== undefined) {
    return Number(task.value.assigneeId) === currentUserId
  }

  return false
})

const canEditTask = computed(() => {
  if (currentUser.value?.roleName === '管理员') return true
  if (currentUser.value?.id && task.value?.projectManagerId && Number(task.value.projectManagerId) === Number(currentUser.value.id)) {
    return true
  }
  return isCurrentUserAssignee.value
})

const canClaimTask = computed(() => {
  if (!task.value || !currentUser.value?.id) return false
  if (task.value.status === 2 || task.value.status === 3) return false
  return !isCurrentUserAssignee.value
})
const workSubmitForm = ref({
  workContent: '',
  deliverables: '',
  blockers: '',
  nextPlan: ''
})

const deliverablesHintText = computed(() => {
  const projectName = task.value?.projectName || '所属项目'
  const folderName = getTaskFolderName()
  return `提交后可在 文件管理 > ${projectName} > ${folderName} 查看上传内容`
})

const setDynamicTitle = inject<(path: string, title: string) => void>('setDynamicTitle')

const fetchTaskDetail = async () => {
  loading.value = true
  try {
    const res = await request.get(`/tasks/${taskId.value}`)
    task.value = res.data
    // 设置标签栏动态标题
    if (task.value?.title && setDynamicTitle) {
      setDynamicTitle(route.fullPath, task.value.title)
    }
  } catch (error) {
    console.error('获取任务详情失败：', error)
  } finally {
    loading.value = false
  }
}

const fetchTaskLogs = async () => {
  try {
    const res = await request.get(`/tasks/${taskId.value}/logs`)
    logs.value = res.data || []
    await refreshDeliverableThumbnails(logs.value)
  } catch (error) {
    console.error('获取任务日志失败：', error)
  }
}

const beforeDeliverableUpload = (file: File) => {
  if (!task.value?.projectId) {
    ElMessage.warning('任务未绑定项目，无法上传')
    return false
  }

  const isLt200M = file.size / 1024 / 1024 < 200
  if (!isLt200M) {
    ElMessage.error('文件大小不能超过 200MB')
    return false
  }

  return true
}

const getTaskFolderName = () => {
  const name = `${task.value?.title || ''}`.trim()
  return name || `任务-${taskId.value}`
}

const isImageName = (fileName: string) => {
  const name = `${fileName || ''}`.toLowerCase()
  return ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp'].some((ext) => name.endsWith(ext))
}

const normalizeDeliverableKey = (fileName: string) => `${fileName || ''}`.trim().toLowerCase()

const cleanupDeliverableThumbnailUrls = (keepKeys: string[] = []) => {
  const keepSet = new Set(keepKeys.map((key) => normalizeDeliverableKey(key)))
  Object.entries(deliverableThumbnailMap.value).forEach(([key, url]) => {
    if (!keepSet.has(key)) {
      URL.revokeObjectURL(url)
      delete deliverableThumbnailMap.value[key]
    }
  })
}

const extractDeliverableNamesFromSummary = (summary: string) => {
  const names: string[] = []
  const lines = `${summary || ''}`
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)

  lines.forEach((line) => {
    if (!line.startsWith('交付物：')) {
      return
    }

    const currentNames = line
      .replace('交付物：', '')
      .split(/[；;，,、]/)
      .map((name) => name.trim())
      .filter(Boolean)

    names.push(...currentNames)
  })

  return names
}

const ensureTaskFolderId = async () => {
  if (!task.value?.projectId) {
    throw new Error('任务未绑定项目')
  }

  if (taskFolderId.value) {
    return taskFolderId.value
  }

  const folderName = getTaskFolderName()
  const projectId = task.value.projectId

  const findFolder = async () => {
    const res = await request.get(`/projects/${projectId}/files`, {
      params: {
        parentId: null,
        keyword: folderName
      }
    })

    const items = res.data.items || []
    const exactFolder = items.find((item: any) => item.isFolder && item.fileName === folderName)
    return exactFolder?.id as number | undefined
  }

  let folderId = await findFolder()
  if (!folderId) {
    try {
      const createRes = await request.post(`/projects/${projectId}/folders`, {
        parentId: null,
        folderName
      })
      folderId = createRes.data?.id
    } catch (error: any) {
      const msg = error?.response?.data?.message || ''
      if (!`${msg}`.includes('同名')) {
        throw error
      }
      folderId = await findFolder()
    }
  }

  if (!folderId) {
    throw new Error('任务文件夹创建失败')
  }

  taskFolderId.value = folderId
  return folderId
}

const uploadDeliverableFile = async (options: any) => {
  if (!task.value?.projectId) {
    options.onError?.(new Error('任务未绑定项目'))
    return
  }

  uploadDeliverableLoading.value = true
  uploadDeliverableProgress.value = 0
  uploadingDeliverableName.value = options?.file?.name || ''
  try {
    const folderId = await ensureTaskFolderId()

    const formData = new FormData()
    formData.append('file', options.file)
    formData.append('description', `任务${taskId.value}交付物上传`)
    formData.append('parentId', String(folderId))

    await request.post(`/projects/${task.value.projectId}/files`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      },
      timeout: 0,
      onUploadProgress: (event: any) => {
        const loaded = Number(event?.loaded || 0)
        const total = Number(event?.total || 0)
        if (total > 0) {
          uploadDeliverableProgress.value = Math.min(100, Math.round((loaded / total) * 100))
        }
      }
    })

    const uploadedName = options.file?.name || ''
    if (uploadedName) {
      workSubmitForm.value.deliverables = workSubmitForm.value.deliverables
        ? `${workSubmitForm.value.deliverables}；${uploadedName}`
        : uploadedName
    }

    ElMessage.success(`文件已上传到任务文件夹：${getTaskFolderName()}`)
    options.onSuccess?.({ success: true })
  } catch (error) {
    options.onError?.(error)
  } finally {
    uploadDeliverableLoading.value = false
    uploadDeliverableProgress.value = 0
    uploadingDeliverableName.value = ''
  }
}

const openLogDialog = async () => {
  await fetchTaskLogs()
  logDialogVisible.value = true
}

const openWorkDialog = () => {
  if (!canEditTask.value) {
    ElMessage.warning('仅项目负责人或任务责任人可提交工作记录')
    return
  }

  workDialogVisible.value = true
}

const submitWork = async () => {
  if (!canEditTask.value) {
    ElMessage.warning('仅项目负责人或任务责任人可提交工作记录')
    return
  }

  if (!workSubmitForm.value.workContent.trim()) {
    ElMessage.warning('请填写完成内容')
    return
  }

  submitLoading.value = true
  try {
    await request.post(`/tasks/${taskId.value}/work-submit`, {
      workContent: workSubmitForm.value.workContent,
      deliverables: workSubmitForm.value.deliverables || null,
      blockers: workSubmitForm.value.blockers || null,
      nextPlan: workSubmitForm.value.nextPlan || null
    })
    ElMessage.success('工作记录提交成功')
    workSubmitForm.value.workContent = ''
    workSubmitForm.value.deliverables = ''
    workSubmitForm.value.blockers = ''
    workSubmitForm.value.nextPlan = ''
    workDialogVisible.value = false
    await Promise.all([fetchTaskDetail(), fetchTaskLogs()])
  } catch (error) {
    console.error('提交工作记录失败：', error)
  } finally {
    submitLoading.value = false
  }
}

const goToDeliverableFile = (fileName: string) => {
  if (!task.value?.projectId) {
    ElMessage.warning('任务未绑定项目，无法定位文件')
    return
  }

  const normalizedName = `${fileName || ''}`.trim()
  if (!normalizedName) {
    ElMessage.warning('交付物名称为空，无法定位文件')
    return
  }

  router.push({
    path: '/files',
    query: {
      projectId: String(task.value.projectId),
      taskFolder: getTaskFolderName(),
      fileName: normalizedName,
      fromTaskId: String(taskId.value)
    }
  })
}

const findDeliverableFile = async (fileName: string) => {
  const projectId = Number(task.value?.projectId || 0)
  if (!projectId) {
    return null
  }

  const folderName = getTaskFolderName()
  const folderRes = await request.get(`/projects/${projectId}/files`, {
    params: {
      parentId: null,
      keyword: folderName
    },
    headers: {
      'X-Silent-Error': '1'
    }
  })

  const folderItems = Array.isArray(folderRes?.data?.items) ? folderRes.data.items : []
  const taskFolder = folderItems.find((item: any) => item?.isFolder && `${item?.fileName || ''}` === folderName)
  if (!taskFolder?.id) {
    return null
  }

  const fileRes = await request.get(`/projects/${projectId}/files`, {
    params: {
      parentId: taskFolder.id,
      keyword: fileName,
      recursive: false
    },
    headers: {
      'X-Silent-Error': '1'
    }
  })

  const fileItems = Array.isArray(fileRes?.data?.items) ? fileRes.data.items : []
  const normalizedName = `${fileName || ''}`.trim().toLowerCase()
  const exactFiles = fileItems.filter((item: any) => {
    if (item?.isFolder) {
      return false
    }

    return `${item?.fileName || ''}`.trim().toLowerCase() === normalizedName
  })

  if (!exactFiles.length) {
    return null
  }

  return [...exactFiles].sort((a: any, b: any) => dayjs(b?.uploadedAt).valueOf() - dayjs(a?.uploadedAt).valueOf())[0]
}

const fetchDeliverableThumbnail = async (fileName: string) => {
  const key = normalizeDeliverableKey(fileName)
  if (!key || !isImageName(fileName) || deliverableThumbnailMap.value[key]) {
    return
  }

  try {
    const token = localStorage.getItem('token')
    if (!token) {
      return
    }

    const deliverableFile = await findDeliverableFile(fileName)
    if (!deliverableFile?.id) {
      return
    }

    const response = await axios.get(`/api/files/${deliverableFile.id}/download`, {
      responseType: 'blob',
      headers: {
        Authorization: `Bearer ${token}`
      }
    })

    deliverableThumbnailMap.value[key] = URL.createObjectURL(response.data)
  } catch (error) {
    console.error('交付物缩略图加载失败：', error)
  }
}

const refreshDeliverableThumbnails = async (list: any[]) => {
  const names = list
    .filter((item: any) => item?.action === '工作提交')
    .flatMap((item: any) => extractDeliverableNamesFromSummary(`${item?.newValue || ''}`))
    .filter((name: string) => isImageName(name))

  cleanupDeliverableThumbnailUrls(names)

  const uniqueNames = [...new Set(names.map((name: string) => normalizeDeliverableKey(name)))]
  for (const key of uniqueNames) {
    const originalName = names.find((name: string) => normalizeDeliverableKey(name) === key)
    if (originalName) {
      await fetchDeliverableThumbnail(originalName)
    }
  }
}

const handleDeliverableClick = (fileName: string) => {
  const normalizedName = `${fileName || ''}`.trim()
  if (!normalizedName) {
    ElMessage.warning('交付物名称为空，无法定位文件')
    return
  }

  goToDeliverableFile(normalizedName)
}

const completeTask = async () => {
  if (!canEditTask.value) {
    ElMessage.warning('仅项目负责人或任务责任人可完成任务')
    return
  }

  if (task.value?.status === 2 || task.value?.status === 3) {
    return
  }

  completeLoading.value = true
  try {
    await request.put(`/tasks/${taskId.value}/status`, { status: 2 }, { headers: { 'X-Silent-Error': '1' } })
    ElMessage.success('任务已完成')
    await Promise.all([fetchTaskDetail(), fetchTaskLogs()])
  } catch (error) {
    const message = (error as any)?.response?.data?.message || (error as any)?.message || ''
    if (`${message}`.includes('提交一次工作内容')) {
      ElMessage.warning('请先提交一次工作记录')
      workDialogVisible.value = true
      return
    }
    console.error('完成任务失败：', error)
  } finally {
    completeLoading.value = false
  }
}

const claimTask = async () => {
  if (!canClaimTask.value) {
    ElMessage.warning('当前任务不可认领')
    return
  }

  claimLoading.value = true
  try {
    await request.put(`/tasks/${taskId.value}/claim`)
    ElMessage.success('任务认领成功')
    await Promise.all([fetchTaskDetail(), fetchTaskLogs()])
  } catch (error) {
    const message = (error as any)?.response?.data?.message || '任务认领失败'
    ElMessage.warning(message)
  } finally {
    claimLoading.value = false
  }
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

const getStatusType = (status: number) => {
  const types: Record<number, string> = {
    0: 'info',
    1: 'primary',
    2: 'success',
    3: 'danger'
  }
  return types[status] || 'info'
}

const formatDate = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD')
}

const formatDateTime = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}

const goBack = () => {
  router.push({
    path: '/tasks',
    query: {
      projectId: route.query.projectId,
      focusTaskId: route.query.focusTaskId,
      overdueOnly: route.query.overdueOnly,
      myOpenScope: route.query.myOpenScope
    }
  })
}

onMounted(async () => {
  const userStr = localStorage.getItem('user')
  if (userStr) {
    currentUser.value = JSON.parse(userStr)
  }

  await Promise.all([fetchTaskDetail(), fetchTaskLogs()])

  if (route.query.needWorkContent === 'true') {
    if (canEditTask.value) {
      workDialogVisible.value = true
    }
  }
})

onBeforeUnmount(() => {
  cleanupDeliverableThumbnailUrls()
})
</script>

<style scoped>
.task-detail-page {
  padding: 10px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.log-actions {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}

.work-submission-section {
  margin-top: 16px;
}

.work-submission-title {
  margin-bottom: 10px;
  font-size: 14px;
  font-weight: 600;
}

.work-submission-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.work-submission-meta {
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.work-submission-content {
  white-space: normal;
  line-height: 1.6;
}

.work-summary-line {
  white-space: pre-wrap;
}

.deliverables-row {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 12px;
}

.deliverables-row .el-input {
  flex: 1;
}

.deliverables-hint {
  margin-top: 6px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  line-height: 1.4;
}

.deliverables-progress {
  margin-top: 8px;
}

.deliverables-progress-text {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-bottom: 6px;
}

.submit-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.deliverable-thumb-link {
  display: inline-flex;
  vertical-align: middle;
  text-decoration: none;
}

.deliverable-thumb-image {
  width: 56px;
  height: 56px;
  border-radius: 6px;
  border: 1px solid var(--el-border-color-light);
  object-fit: cover;
}

.deliverable-thumb-fallback {
  width: 56px;
  height: 56px;
  border-radius: 6px;
  border: 1px dashed var(--el-border-color);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
</style>
