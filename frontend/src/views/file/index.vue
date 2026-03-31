<template>
  <div class="file-page">
    <el-card class="explorer-card">
      <template #header>
        <div class="card-header">
          <span class="header-title">文件资源管理器</span>
          <span class="header-tip">“共享文件夹”项目支持全员查看与编辑全部文件</span>
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
            @node-click="handleProjectNodeClick" />
          <el-empty v-else description="暂无项目" :image-size="60" />
        </aside>

        <main class="right-content-panel">
          <div class="toolbar">
            <div v-if="!recycleMode" class="path-bar">
              <el-button class="path-back-btn" type="primary" size="small"
                :disabled="!searchForm.projectId || !currentFolderId" @click="goToParentFolder">
                ← 返回上级
              </el-button>
              <el-breadcrumb separator="/">
                <el-breadcrumb-item>
                  <span class="path-node path-project-node" @click="goToRootFolder">
                    项目
                  </span>
                </el-breadcrumb-item>
                <el-breadcrumb-item v-if="currentProjectName">
                  <span class="path-node path-project-name" :class="{ 'path-drop-active': dragOverRoot }"
                    @click="goToRootFolder" @dragenter.prevent="handleBreadcrumbRootDragEnter($event)"
                    @dragover.prevent="handleBreadcrumbRootDragOver($event)" @dragleave="handleBreadcrumbRootDragLeave"
                    @drop.prevent="handleBreadcrumbRootDrop">
                    {{ currentProjectName }}
                  </span>
                </el-breadcrumb-item>
                <el-breadcrumb-item v-for="(folder, index) in folderPath" :key="folder.id">
                  <span class="path-node" :class="{ 'path-drop-active': dragOverFolderId === folder.id }"
                    @click="goToPathFolder(index)" @dragenter.prevent="handleBreadcrumbDragEnter(folder, $event)"
                    @dragover.prevent="handleBreadcrumbDragOver(folder, $event)"
                    @dragleave="handleBreadcrumbDragLeave(folder)" @drop.prevent="handleBreadcrumbDrop(folder)">
                    {{ folder.fileName }}
                  </span>
                </el-breadcrumb-item>
              </el-breadcrumb>
            </div>
            <div v-else class="path-bar recycle-hint">当前为回收站视图（保留 7 天，可恢复）</div>
            <div class="toolbar-actions">
              <el-button class="recycle-toggle-btn" size="small" :disabled="!searchForm.projectId" @click="toggleRecycleMode">
                {{ recycleMode ? '返回文件列表' : '回收站' }}
              </el-button>
              <el-button size="small" :disabled="!searchForm.projectId || !currentFolderId" @click="goToRootFolder">
                回到根目录
              </el-button>
              <el-button v-if="!recycleMode" type="success" size="small" :disabled="!searchForm.projectId"
                @click="openCreateFolderDialog">
                <el-icon>
                  <FolderAdd />
                </el-icon>
                新建文件夹
              </el-button>
              <span v-if="selectedCount" class="selection-count">已选 {{ selectedCount }} 个</span>
              <el-button v-if="!recycleMode" type="danger" size="small" :disabled="!selectedCount"
                @click="handleBulkDelete">
                批量删除
              </el-button>
              <el-button v-else type="success" size="small" :disabled="!selectedCount"
                @click="handleBulkRestore">
                批量恢复
              </el-button>
              <el-button v-if="recycleMode" type="danger" size="small" :disabled="!selectedCount"
                @click="handleBulkPermanentDelete">
                彻底删除
              </el-button>
              <el-upload v-if="!recycleMode" :action="uploadUrl" :headers="uploadHeaders" :data="uploadData"
                :before-upload="beforeUpload"
                :on-success="handleUploadSuccess" :on-error="handleUploadError" :on-progress="handleUploadProgress" :show-file-list="false">
                <el-button type="primary">
                  <el-icon>
                    <Upload />
                  </el-icon>
                  上传文件
                </el-button>
              </el-upload>
            </div>
          </div>

          <div v-if="uploading" class="upload-progress">
            <div class="upload-progress-meta">
              <span class="upload-progress-name">正在上传：{{ uploadFileName || '文件' }}</span>
              <span class="upload-progress-value">{{ uploadProgress }}%</span>
            </div>
            <el-progress :percentage="uploadProgress" :stroke-width="8" />
          </div>

          <div class="search-bar">
            <el-input v-model="searchForm.keyword" :placeholder="recycleMode ? '搜索回收站文件名' : '搜索当前项目文件名'" clearable style="width: 240px;"
              @keyup.enter="handleSearch" />
            <el-button type="primary" @click="handleSearch">搜索</el-button>
            <el-button @click="resetSearch">重置</el-button>
          </div>

          <div class="content-area">
            <div v-if="searchForm.projectId" v-loading="loading" class="icon-view">
              <div v-if="files.length" class="icon-grid">
                <div v-for="file in files" :key="file.id" class="icon-card"
                  :class="{ 'drop-target-active': dragOverFolderId === file.id && file.isFolder, 'folder-card': file.isFolder, 'is-selected': isSelected(file) }"
                  :draggable="!recycleMode" @click="handleCardClick(file)" @dragstart="handleDragStart(file, $event)"
                  @dragend="handleDragEnd" @dragover.prevent="handleDragOver(file, $event)"
                  @dragleave="handleDragLeave(file)" @drop.prevent="handleDropToFolder(file)"
                  @contextmenu.prevent="openEntryContextMenu(file, $event)">
                  <el-checkbox v-if="isSelectable(file)" class="icon-select"
                    :model-value="isSelected(file)" @change="(value: boolean) => handleSelectionChange(file, value)"
                    @click.stop />
                  <div class="icon-card-main">
                    <div class="thumb-box">
                      <div v-if="file.isFolder" class="folder-thumb">
                        <div class="folder-tab" />
                        <div class="folder-body" />
                      </div>
                      <img v-if="isImage(file.fileType) && thumbnailUrlMap[file.id]" :src="thumbnailUrlMap[file.id]"
                        :alt="file.fileName" class="thumb-image" />
                      <div v-else-if="!file.isFolder" class="file-badge" :class="getFileBadgeClass(file.fileType)">
                        <span class="file-badge-ext">{{ getFileExt(file.fileType) }}</span>
                      </div>
                    </div>
                    <div class="icon-name" :title="file.fileName">{{ file.fileName }}</div>
                    <div class="icon-meta">
                      {{ recycleMode ? `删除时间：${formatDeletedAt(file.deletedAt)}` : (file.isFolder ? '文件夹' : formatFileSize(file.fileSize)) }}
                    </div>
                    <div class="icon-uploader" :title="file.uploaderName || '-'">上传人：{{ file.uploaderName || '-' }}
                    </div>
                    <div class="icon-uploader" :title="formatUploadedAt(file.uploadedAt)">
                      上传时间：{{ formatUploadedAt(file.uploadedAt) }}
                    </div>
                    <div v-if="recycleMode && getRemainingDays(file.deletedAt) > 0" class="icon-uploader">
                      剩余恢复天数：{{ getRemainingDays(file.deletedAt) }}
                    </div>
                  </div>
                  <div class="icon-actions">
                    <template v-if="recycleMode">
                      <el-button class="action-btn action-restore" type="success" link size="small" @click.stop="restoreEntry(file)">恢复</el-button>
                      <el-button class="action-btn action-permanent" type="danger" link size="small" @click.stop="permanentlyDeleteEntry(file)">彻底删除</el-button>
                    </template>
                    <template v-else-if="!file.isFolder">
                      <el-button class="action-btn action-preview" type="primary" link size="small" @click.stop="openPreview(file)">预览</el-button>
                      <el-button class="action-btn action-download" type="primary" link size="small" @click.stop="downloadFile(file)">下载</el-button>
                    </template>
                  </div>
                </div>
              </div>
              <div v-else class="empty-content-center">
                <el-empty :description="recycleMode ? '回收站暂无内容' : '当前目录暂无内容'" />
              </div>
            </div>
            <el-empty v-else description="请先在左侧选择项目" />
          </div>

          <el-dialog v-model="previewVisible" :title="previewTitle" width="70%" destroy-on-close @closed="closePreview">
            <div class="preview-wrap" v-loading="previewLoading">
              <img v-if="previewMode === 'image' && previewUrl" :src="previewUrl" :alt="previewTitle"
                class="preview-image" />
              <video v-else-if="previewMode === 'video' && previewUrl" :src="previewUrl" controls
                class="preview-video" />
              <iframe v-else-if="previewMode === 'iframe' && previewUrl" :src="previewUrl" class="preview-frame" />
              <div v-else-if="previewMode === 'html'" class="preview-html" v-html="previewHtml" />
              <el-empty v-else description="当前文件类型暂不支持在线预览" />
            </div>
          </el-dialog>

          <el-dialog v-model="createFolderVisible" title="新建文件夹" width="420px" destroy-on-close>
            <el-form label-width="90px" @submit.prevent>
              <el-form-item label="文件夹名称">
                <el-input v-model="createFolderForm.folderName" maxlength="100" placeholder="请输入文件夹名称"
                  @keydown.enter.prevent="handleCreateFolder" />
              </el-form-item>
            </el-form>
            <template #footer>
              <el-button @click="createFolderVisible = false">取消</el-button>
              <el-button type="primary" :loading="creatingFolder" @click="handleCreateFolder">确定</el-button>
            </template>
          </el-dialog>

          <div v-if="entryContextMenu.visible && !recycleMode" class="folder-context-menu"
            :style="{ left: `${entryContextMenu.x}px`, top: `${entryContextMenu.y}px` }" @click.stop>
            <div class="context-item" @click="handleRenameEntry">重命名</div>
            <div class="context-item danger" @click="handleDeleteEntry">删除</div>
          </div>
        </main>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed, onBeforeUnmount } from 'vue'
import axios from 'axios'
import { ElMessage, ElMessageBox } from 'element-plus'
import { request } from '@/api/request'

const loading = ref(false)
const files = ref<any[]>([])
const projects = ref<any[]>([])
const thumbnailUrlMap = ref<Record<number, string>>({})
const currentFolderId = ref<number | undefined>(undefined)
const folderPath = ref<any[]>([])
const draggingFile = ref<any | null>(null)
const dragOverFolderId = ref<number | null>(null)
const dragOverRoot = ref(false)
const previewVisible = ref(false)
const previewLoading = ref(false)
const previewTitle = ref('文件预览')
const previewMode = ref<'image' | 'video' | 'iframe' | 'html' | 'unsupported'>('unsupported')
const previewUrl = ref('')
const previewHtml = ref('')
const createFolderVisible = ref(false)
const creatingFolder = ref(false)
const navigatingFolder = ref(false)
const createFolderForm = reactive({
  folderName: ''
})
const entryContextMenu = reactive({
  visible: false,
  x: 0,
  y: 0,
  entry: null as any
})

const searchForm = reactive({
  projectId: undefined as number | undefined,
  keyword: ''
})

const projectSearchKeyword = ref('')
const recycleMode = ref(false)
const SHARED_FOLDER_PROJECT_NAME = '共享文件夹'
const uploadProgress = ref(0)
const uploading = ref(false)
const uploadFileName = ref('')
const selectedFileIds = ref<number[]>([])

const normalizeProjectId = (project: any) => {
  const id = Number(project?.id)
  return Number.isFinite(id) ? id : 0
}

const getProjectName = (project: any) => {
  const candidates = [project?.name, project?.projectName]
  for (const candidate of candidates) {
    if (typeof candidate === 'string' && candidate.trim()) {
      return candidate.trim()
    }
  }
  return ''
}

const getProjectManagerName = (project: any) => {
  const candidates = [project?.managerName, project?.projectManagerName, project?.ownerName]
  for (const candidate of candidates) {
    if (typeof candidate === 'string' && candidate.trim()) {
      return candidate.trim()
    }
  }
  return '-'
}

const uploadUrl = '/api/files/upload'
const uploadHeaders = {
  Authorization: `Bearer ${localStorage.getItem('token')}`
}
const uploadData = computed(() => {
  const data: Record<string, number | undefined> = {
    projectId: searchForm.projectId
  }

  if (typeof currentFolderId.value === 'number') {
    data.parentId = currentFolderId.value
  }

  return data
})

const projectTreeData = computed(() =>
  projects.value.map((project: any) => ({
    id: normalizeProjectId(project),
    label: `${getProjectName(project)}（${getProjectManagerName(project)}）`
  }))
)

const filteredProjectTreeData = computed(() => {
  const keyword = projectSearchKeyword.value.trim().toLowerCase()
  if (!keyword) {
    return projectTreeData.value
  }

  return projectTreeData.value.filter((project: any) =>
    `${project.label || ''}`.toLowerCase().includes(keyword)
  )
})

const currentProjectName = computed(() => {
  const current = projects.value.find((p: any) => normalizeProjectId(p) === searchForm.projectId)
  return getProjectName(current)
})

const selectedCount = computed(() => selectedFileIds.value.length)

const getSelectableIds = (list: any[]) => {
  if (recycleMode.value) {
    return list.map(item => item?.id).filter(Boolean)
  }

  return list.filter(item => !item?.isFolder).map(item => item.id)
}

const reconcileSelection = (list: any[]) => {
  const keepIds = new Set(getSelectableIds(list))
  selectedFileIds.value = selectedFileIds.value.filter(id => keepIds.has(id))
}

const isSelectable = (file: any) => {
  if (!file) {
    return false
  }

  if (recycleMode.value) {
    return true
  }

  return !file.isFolder
}

const isSelected = (file: any) => {
  if (!file || !isSelectable(file)) {
    return false
  }
  return selectedFileIds.value.includes(file.id)
}

const handleSelectionChange = (file: any, checked: boolean) => {
  if (!file || !isSelectable(file)) {
    return
  }

  const id = file.id
  if (!id) {
    return
  }

  if (checked) {
    if (!selectedFileIds.value.includes(id)) {
      selectedFileIds.value = [...selectedFileIds.value, id]
    }
    return
  }

  selectedFileIds.value = selectedFileIds.value.filter(itemId => itemId !== id)
}

const toggleSelection = (file: any) => {
  if (!file || !isSelectable(file)) {
    return
  }

  const id = file.id
  if (!id) {
    return
  }

  const isChecked = selectedFileIds.value.includes(id)
  handleSelectionChange(file, !isChecked)
}

const deleteFilesByIds = async (ids: number[]) => {
  if (!ids.length) {
    return { failedCount: 0 }
  }

  const results = await Promise.allSettled(
    ids.map(id => request.delete(`/files/${id}`))
  )

  const failedCount = results.filter(result => result.status === 'rejected').length
  return { failedCount }
}

const fetchFiles = async () => {
  if (!searchForm.projectId) {
    files.value = []
    cleanupThumbnailUrls([])
    selectedFileIds.value = []
    return true
  }

  if (recycleMode.value) {
    return fetchRecycleBinFiles()
  }

  loading.value = true
  try {
    const params: any = {
      parentId: currentFolderId.value
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword

    const res = await request.get(`/projects/${searchForm.projectId}/files`, { params })
    files.value = res.data.items
    reconcileSelection(files.value)
    refreshImageThumbnails(files.value)
    return true
  } catch (error) {
    console.error('获取文件列表失败：', error)
    return false
  } finally {
    loading.value = false
  }
}

const fetchRecycleBinFiles = async () => {
  if (!searchForm.projectId) {
    files.value = []
    cleanupThumbnailUrls([])
    selectedFileIds.value = []
    return true
  }

  loading.value = true
  try {
    const params: any = {}
    if (searchForm.keyword) params.keyword = searchForm.keyword

    const res = await request.get(`/projects/${searchForm.projectId}/files/recycle-bin`, { params })
    files.value = res.data.items
    selectedFileIds.value = []
    cleanupThumbnailUrls([])
    return true
  } catch (error) {
    console.error('获取回收站列表失败：', error)
    return false
  } finally {
    loading.value = false
  }
}

const fetchProjects = async () => {
  try {
    const res = await request.get('/projects', { params: { pageSize: 200 } })
    const items = Array.isArray(res.data.items) ? [...res.data.items] : []
    items.sort((left: any, right: any) => {
      const leftIsShared = getProjectName(left) === SHARED_FOLDER_PROJECT_NAME
      const rightIsShared = getProjectName(right) === SHARED_FOLDER_PROJECT_NAME

      if (leftIsShared && !rightIsShared) return -1
      if (!leftIsShared && rightIsShared) return 1
      return 0
    })

    projects.value = items
    if (projects.value.length > 0) {
      searchForm.projectId = normalizeProjectId(projects.value[0])
      fetchFiles()
    }
  } catch (error) {
    console.error('获取项目列表失败：', error)
  }
}

const handleProjectNodeClick = (node: any) => {
  const nextProjectId = Number(node?.id)
  if (!Number.isFinite(nextProjectId) || nextProjectId <= 0) {
    return
  }

  if (searchForm.projectId === nextProjectId) {
    if (recycleMode.value || currentFolderId.value) {
      handleProjectChange()
    }
    return
  }

  searchForm.projectId = nextProjectId
  handleProjectChange()
}

const handleProjectChange = () => {
  recycleMode.value = false
  currentFolderId.value = undefined
  folderPath.value = []
  selectedFileIds.value = []
  fetchFiles()
}

const openFolder = async (folder: any) => {
  if (!folder?.isFolder) {
    return
  }

  if (navigatingFolder.value) {
    return
  }

  navigatingFolder.value = true
  try {
    const previousFolderId = currentFolderId.value
    const previousPath = [...folderPath.value]

    currentFolderId.value = folder.id
    folderPath.value.push({ id: folder.id, fileName: folder.fileName })

    const loaded = await fetchFiles()
    if (!loaded) {
      currentFolderId.value = previousFolderId
      folderPath.value = previousPath
    }
  } finally {
    navigatingFolder.value = false
  }
}

const goToPathFolder = (index: number) => {
  const target = folderPath.value[index]
  if (!target) {
    return
  }

  folderPath.value = folderPath.value.slice(0, index + 1)
  currentFolderId.value = target.id
  fetchFiles()
}

const handleCardClick = (item: any) => {
  if (recycleMode.value) {
    toggleSelection(item)
    return
  }

  if (item?.isFolder) {
    void openFolder(item)
    return
  }

  toggleSelection(item)
}

const openEntryContextMenu = (entry: any, event: MouseEvent) => {
  if (recycleMode.value) {
    return
  }

  if (!entry) {
    return
  }

  entryContextMenu.visible = true
  entryContextMenu.x = event.clientX
  entryContextMenu.y = event.clientY
  entryContextMenu.entry = entry
}

const closeEntryContextMenu = () => {
  entryContextMenu.visible = false
  entryContextMenu.entry = null
}

const bindPromptEnterToConfirm = () => {
  setTimeout(() => {
    const input = document.querySelector('.el-message-box__input input') as HTMLInputElement | null
    if (!input) {
      return
    }

    input.addEventListener('keydown', (event: KeyboardEvent) => {
      if (event.key !== 'Enter') {
        return
      }

      event.preventDefault()
      const confirmButton = document.querySelector('.el-message-box .el-message-box__btns .el-button--primary') as HTMLButtonElement | null
      confirmButton?.click()
    })
  }, 0)
}

const handleRenameEntry = async () => {
  const entry = entryContextMenu.entry
  if (!entry) {
    return
  }

  closeEntryContextMenu()

  const isFolder = !!entry.isFolder
  const entryLabel = isFolder ? '文件夹' : '文件'

  try {
    const renamePrompt = ElMessageBox.prompt(`请输入新的${entryLabel}名称`, `重命名${entryLabel}`, {
      inputValue: entry.fileName,
      inputPlaceholder: `${entryLabel}名称`,
      confirmButtonText: '确定',
      cancelButtonText: '取消'
    })

    bindPromptEnterToConfirm()
    const result = await renamePrompt

    const name = result.value?.trim()
    if (!name) {
      ElMessage.warning(`${entryLabel}名称不能为空`)
      return
    }

    await request.put(`/files/${entry.id}/rename`, { name })

    if (currentFolderId.value === entry.id) {
      const current = folderPath.value[folderPath.value.length - 1]
      if (current && current.id === entry.id) {
        current.fileName = name
      }
    }

    folderPath.value = folderPath.value.map(pathFolder =>
      pathFolder.id === entry.id
        ? { ...pathFolder, fileName: name }
        : pathFolder
    )

    ElMessage.success('重命名成功')
    fetchFiles()
  } catch (error: any) {
    if (error === 'cancel') {
      return
    }
    const msg = error?.response?.data?.message || '重命名失败'
    ElMessage.error(msg)
  }
}

const handleDeleteEntry = async () => {
  const entry = entryContextMenu.entry
  if (!entry) {
    return
  }

  closeEntryContextMenu()

  const entryLabel = entry.isFolder ? '文件夹' : '文件'

  try {
    await ElMessageBox.confirm(`确定删除${entryLabel}“${entry.fileName}”吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消'
    })

    await deleteFile(entry.id)
  } catch (error) {
  }
}

const handleDragStart = (item: any, event: DragEvent) => {
  if (recycleMode.value) {
    return
  }

  if (!item) {
    return
  }

  draggingFile.value = item
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', String(item.id))
  }
}

const handleDragEnd = () => {
  draggingFile.value = null
  dragOverFolderId.value = null
  dragOverRoot.value = false
}

const handleDragOver = (target: any, event: DragEvent) => {
  if (!target?.isFolder || !draggingFile.value || draggingFile.value.id === target.id) {
    return
  }

  dragOverFolderId.value = target.id
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move'
  }
}

const handleDragLeave = (target: any) => {
  if (dragOverFolderId.value === target?.id) {
    dragOverFolderId.value = null
  }
}

const handleDropToFolder = async (target: any) => {
  if (!target?.isFolder || !draggingFile.value) {
    return
  }

  await moveFileToFolder(target)
}

const moveFileToFolder = async (targetFolder: any) => {
  const movingEntry = draggingFile.value
  draggingFile.value = null
  dragOverFolderId.value = null
  dragOverRoot.value = false

  if (!movingEntry) {
    return
  }

  if (targetFolder?.id && movingEntry.id === targetFolder.id) {
    return
  }

  try {
    await request.put(`/files/${movingEntry.id}/move`, {
      targetFolderId: targetFolder?.id ?? null
    })
    ElMessage.success(targetFolder?.id ? `已移动到文件夹：${targetFolder.fileName}` : '已移动到项目根目录')
    fetchFiles()
  } catch (error: any) {
    const msg = error?.response?.data?.message || '移动失败'
    ElMessage.error(msg)
  }
}

const handleBreadcrumbDragOver = (folder: any, event: DragEvent) => {
  if (!draggingFile.value || draggingFile.value.id === folder.id) {
    return
  }

  dragOverRoot.value = false
  dragOverFolderId.value = folder.id
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move'
  }
}

const handleBreadcrumbDragEnter = (folder: any, event: DragEvent) => {
  handleBreadcrumbDragOver(folder, event)
}

const handleBreadcrumbDragLeave = (folder: any) => {
  if (dragOverFolderId.value === folder.id) {
    dragOverFolderId.value = null
  }
}

const handleBreadcrumbRootDragOver = (event: DragEvent) => {
  if (!draggingFile.value) {
    return
  }

  dragOverRoot.value = true
  dragOverFolderId.value = null
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move'
  }
}

const handleBreadcrumbRootDragEnter = (event: DragEvent) => {
  handleBreadcrumbRootDragOver(event)
}

const handleBreadcrumbRootDragLeave = () => {
  dragOverRoot.value = false
}

const handleBreadcrumbRootDrop = async () => {
  if (!draggingFile.value) {
    return
  }

  await moveFileToFolder(null)
}

const handleBreadcrumbDrop = async (folder: any) => {
  if (!draggingFile.value) {
    return
  }

  await moveFileToFolder(folder)
}

const goToParentFolder = () => {
  if (!currentFolderId.value) return
  folderPath.value.pop()
  const parent = folderPath.value[folderPath.value.length - 1]
  currentFolderId.value = parent?.id
  fetchFiles()
}

const goToRootFolder = () => {
  currentFolderId.value = undefined
  folderPath.value = []
  fetchFiles()
}

const handleSearch = () => {
  fetchFiles()
}

const toggleRecycleMode = () => {
  if (!searchForm.projectId) {
    ElMessage.warning('请先选择项目')
    return
  }

  recycleMode.value = !recycleMode.value
  searchForm.keyword = ''
  selectedFileIds.value = []

  if (!recycleMode.value) {
    currentFolderId.value = undefined
    folderPath.value = []
  }

  fetchFiles()
}

const resetSearch = () => {
  searchForm.keyword = ''
  handleSearch()
}

const handleBulkDelete = async () => {
  if (!selectedFileIds.value.length) {
    return
  }

  try {
    await ElMessageBox.confirm(`确定删除选中的 ${selectedFileIds.value.length} 个文件吗？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消'
    })

    const { failedCount } = await deleteFilesByIds(selectedFileIds.value)
    selectedFileIds.value = []
    fetchFiles()

    if (failedCount > 0) {
      ElMessage.warning(`已删除部分文件，失败 ${failedCount} 个`)
      return
    }

    ElMessage.success('已移入回收站，可在 7 天内恢复')
  } catch (error) {
    if (error === 'cancel') {
      return
    }
    console.error('批量删除失败：', error)
    ElMessage.error('批量删除失败')
  }
}

const handleBulkRestore = async () => {
  if (!selectedFileIds.value.length) {
    return
  }

  try {
    await ElMessageBox.confirm(`确定恢复选中的 ${selectedFileIds.value.length} 个项目吗？`, '恢复确认', {
      type: 'warning',
      confirmButtonText: '恢复',
      cancelButtonText: '取消'
    })

    const results = await Promise.allSettled(
      selectedFileIds.value.map(id => request.put(`/files/${id}/restore`))
    )

    const failedCount = results.filter(result => result.status === 'rejected').length
    selectedFileIds.value = []
    fetchFiles()

    if (failedCount > 0) {
      ElMessage.warning(`已恢复部分项目，失败 ${failedCount} 个`)
      return
    }

    ElMessage.success('恢复成功')
  } catch (error) {
    if (error === 'cancel') {
      return
    }
    console.error('批量恢复失败：', error)
    ElMessage.error('批量恢复失败')
  }
}

const handleBulkPermanentDelete = async () => {
  if (!selectedFileIds.value.length) {
    return
  }

  try {
    await ElMessageBox.confirm(`确定彻底删除选中的 ${selectedFileIds.value.length} 个项目吗？彻底删除后不可恢复。`, '彻底删除确认', {
      type: 'warning',
      confirmButtonText: '彻底删除',
      cancelButtonText: '取消'
    })

    const results = await Promise.allSettled(
      selectedFileIds.value.map(id => request.delete(`/files/${id}/permanent`))
    )

    const failedCount = results.filter(result => result.status === 'rejected').length
    selectedFileIds.value = []
    fetchFiles()

    if (failedCount > 0) {
      ElMessage.warning(`已删除部分项目，失败 ${failedCount} 个`)
      return
    }

    ElMessage.success('彻底删除成功')
  } catch (error) {
    if (error === 'cancel') {
      return
    }
    console.error('批量彻底删除失败：', error)
    ElMessage.error('批量彻底删除失败')
  }
}

const openCreateFolderDialog = () => {
  if (!searchForm.projectId) {
    ElMessage.warning('请先选择项目')
    return
  }

  createFolderForm.folderName = ''
  createFolderVisible.value = true
}

const handleCreateFolder = async () => {
  if (creatingFolder.value) {
    return
  }

  const folderName = createFolderForm.folderName.trim()
  if (!folderName) {
    ElMessage.warning('请输入文件夹名称')
    return
  }

  if (!searchForm.projectId) {
    ElMessage.warning('请先选择项目')
    return
  }

  creatingFolder.value = true
  try {
    await request.post(`/projects/${searchForm.projectId}/folders`, {
      parentId: currentFolderId.value,
      folderName
    })
    ElMessage.success('文件夹创建成功')
    createFolderVisible.value = false
    fetchFiles()
  } catch (error: any) {
    const msg = error?.response?.data?.message || '文件夹创建失败'
    ElMessage.error(msg)
  } finally {
    creatingFolder.value = false
  }
}

const beforeUpload = (file: File) => {
  if (!searchForm.projectId) {
    ElMessage.warning('请先选择项目')
    return false
  }

  const isLt200M = file.size / 1024 / 1024 < 200
  if (!isLt200M) {
    ElMessage.error('文件大小不能超过 200MB!')
    return false
  }

  uploadProgress.value = 0
  uploading.value = true
  uploadFileName.value = file.name || ''
  return true
}

const cleanupThumbnailUrls = (keepIds: number[] = []) => {
  const keepSet = new Set(keepIds)
  Object.entries(thumbnailUrlMap.value).forEach(([id, url]) => {
    if (!keepSet.has(Number(id))) {
      URL.revokeObjectURL(url)
      delete thumbnailUrlMap.value[Number(id)]
    }
  })
}

const fetchThumbnail = async (file: any) => {
  if (!isImage(file.fileType) || thumbnailUrlMap.value[file.id]) {
    return
  }

  try {
    const token = localStorage.getItem('token')
    if (!token) return

    const response = await axios.get(`/api/files/${file.id}/download`, {
      responseType: 'blob',
      headers: { Authorization: `Bearer ${token}` }
    })

    thumbnailUrlMap.value[file.id] = URL.createObjectURL(response.data)
  } catch (error) {
    console.error('缩略图加载失败：', error)
  }
}

const refreshImageThumbnails = (list: any[]) => {
  const keepIds = list.map(file => file.id)
  cleanupThumbnailUrls(keepIds)

  const imageFiles = list.filter(file => isImage(file.fileType))
  imageFiles.forEach(file => {
    fetchThumbnail(file)
  })
}

const cleanupPreviewUrl = () => {
  if (previewUrl.value) {
    URL.revokeObjectURL(previewUrl.value)
    previewUrl.value = ''
  }
}

const cleanupPreviewHtml = () => {
  previewHtml.value = ''
}

const escapeHtml = (value: string) => {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;')
}

const buildWorkbookPreviewHtml = (workbook: any, xlsxLib: any) => {
  if (!workbook?.SheetNames?.length) {
    return '<p>工作簿无可预览内容</p>'
  }

  const sections = workbook.SheetNames.map((sheetName: string, index: number) => {
    const sheet = workbook.Sheets[sheetName]
    const tableHtml = xlsxLib.utils.sheet_to_html(sheet, {
      id: `sheet-${index}`,
      editable: false,
      header: '',
      footer: ''
    })
    const parser = new DOMParser()
    const doc = parser.parseFromString(tableHtml, 'text/html')
    const table = doc.querySelector('table')
    const safeTitle = escapeHtml(sheetName)

    return `
      <section class="sheet-section">
        <h4 class="sheet-title">工作表：${safeTitle}</h4>
        <div class="sheet-table-wrap">${table ? table.outerHTML : '<p>该工作表无数据</p>'}</div>
      </section>
    `
  })

  return sections.join('')
}

const isIframePreviewType = (type?: string) => {
  const ext = (type || '').toLowerCase()
  return ['.pdf', '.txt', '.csv', '.md', '.json'].includes(ext)
}

const isHtmlPreviewType = (type?: string) => {
  const ext = (type || '').toLowerCase()
  return ['.doc', '.docx', '.xls', '.xlsx'].includes(ext)
}

const resolvePreviewMode = (type?: string) => {
  if (isImage(type || '')) return 'image' as const
  if (isVideo(type || '')) return 'video' as const
  if (isHtmlPreviewType(type)) return 'html' as const
  if (isIframePreviewType(type)) return 'iframe' as const
  return 'unsupported' as const
}

const openPreview = async (file: any) => {
  if (file?.isFolder) {
    ElMessage.warning('文件夹不支持预览，请点击进入')
    return
  }

  previewTitle.value = file.fileName || '文件预览'
  previewVisible.value = true
  previewLoading.value = true
  previewMode.value = resolvePreviewMode(file.fileType)
  cleanupPreviewUrl()
  cleanupPreviewHtml()

  if (previewMode.value === 'unsupported') {
    previewLoading.value = false
    return
  }

  try {
    const token = localStorage.getItem('token')
    if (!token) {
      ElMessage.error('登录已过期，请重新登录')
      previewVisible.value = false
      return
    }

    if (previewMode.value === 'html') {
      const ext = (file.fileType || '').toLowerCase()
      if (ext === '.doc') {
        const res = await request.get(`/files/${file.id}/doc-preview`)
        previewHtml.value = res.data || '<p>文档内容为空</p>'
      } else {
        const response = await axios.get(`/api/files/${file.id}/download`, {
          responseType: 'arraybuffer',
          headers: { Authorization: `Bearer ${token}` }
        })

        if (ext === '.docx') {
        const mammoth = await import('mammoth/mammoth.browser')
        const result = await mammoth.convertToHtml({ arrayBuffer: response.data })
        previewHtml.value = result.value || '<p>文档内容为空</p>'
        } else if (ext === '.xls' || ext === '.xlsx') {
          const xlsx = await import('xlsx')
          const workbook = xlsx.read(response.data, { type: 'array' })
          previewHtml.value = buildWorkbookPreviewHtml(workbook, xlsx)
        }
      }
    } else {
      const response = await axios.get(`/api/files/${file.id}/download`, {
        responseType: 'blob',
        headers: { Authorization: `Bearer ${token}` }
      })

      previewUrl.value = URL.createObjectURL(response.data)
    }
  } catch (error: any) {
    if (error?.response?.status === 401) {
      ElMessage.error('登录已过期，请重新登录')
    } else {
      ElMessage.error('预览加载失败，请稍后重试')
    }
    previewVisible.value = false
  } finally {
    previewLoading.value = false
  }
}

const closePreview = () => {
  cleanupPreviewUrl()
  cleanupPreviewHtml()
  previewMode.value = 'unsupported'
}

const handleUploadProgress = (event: any, file: any) => {
  if (typeof event?.percent === 'number') {
    uploadProgress.value = Math.round(event.percent)
    uploading.value = true
  }

  if (file?.name) {
    uploadFileName.value = file.name
  }
}

const handleUploadSuccess = () => {
  ElMessage.success('上传成功')
  uploadProgress.value = 0
  uploading.value = false
  uploadFileName.value = ''
  fetchFiles()
}

const resolveUploadErrorMessage = (error: any) => {
  const fallback = '上传失败，请稍后重试'

  const tryResolveMessage = (payload: any) => {
    if (!payload) {
      return ''
    }

    if (typeof payload === 'string') {
      try {
        const parsed = JSON.parse(payload)
        return parsed?.message || parsed?.errors?.parentId?.[0] || parsed?.title || ''
      } catch {
        return payload
      }
    }

    if (typeof payload === 'object') {
      return payload?.message || payload?.errors?.parentId?.[0] || payload?.title || ''
    }

    return ''
  }

  const directMessage = tryResolveMessage(error?.response) || tryResolveMessage(error?.response?.data)
  if (directMessage) {
    return directMessage
  }

  const rawMessage = `${error?.message || ''}`
  if (rawMessage) {
    const jsonStart = rawMessage.indexOf('{')
    const jsonEnd = rawMessage.lastIndexOf('}')
    if (jsonStart >= 0 && jsonEnd > jsonStart) {
      const maybeJson = rawMessage.slice(jsonStart, jsonEnd + 1)
      const parsedMessage = tryResolveMessage(maybeJson)
      if (parsedMessage) {
        return parsedMessage
      }
    }

    return rawMessage
  }

  return fallback
}

const handleUploadError = (error: any) => {
  const message = resolveUploadErrorMessage(error)

  uploadProgress.value = 0
  uploading.value = false
  uploadFileName.value = ''

  const normalized = `${message || ''}`
  if (normalized.includes('parentId') || normalized.includes('父级文件夹不存在') || normalized.includes('undefined')) {
    ElMessage.warning('请先创建并进入目标文件夹后再上传文件')
    return
  }

  ElMessage.error(message)
}

const downloadFile = async (file: any) => {
  if (file?.isFolder) {
    ElMessage.warning('文件夹不支持下载')
    return
  }

  try {
    const token = localStorage.getItem('token')
    if (!token) {
      ElMessage.error('登录已过期，请重新登录')
      return
    }

    const response = await axios.get(`/api/files/${file.id}/download`, {
      responseType: 'blob',
      headers: {
        Authorization: `Bearer ${token}`
      }
    })

    const blob = new Blob([response.data])
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = file.fileName
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch (error: any) {
    if (error?.response?.status === 401) {
      ElMessage.error('登录已过期，请重新登录')
      return
    }
    ElMessage.error('下载失败，请稍后重试')
  }
}

const deleteFile = async (id: number) => {
  try {
    await request.delete(`/files/${id}`)
    ElMessage.success('已移入回收站，可在 7 天内恢复')
    fetchFiles()
  } catch (error) {
    console.error('删除失败：', error)
  }
}

const restoreEntry = async (entry: any) => {
  if (!entry?.id) {
    return
  }

  try {
    await request.put(`/files/${entry.id}/restore`)
    ElMessage.success('恢复成功')
    fetchFiles()
  } catch (error) {
    console.error('恢复失败：', error)
  }
}

const permanentlyDeleteEntry = async (entry: any) => {
  if (!entry?.id) {
    return
  }

  const entryLabel = entry.isFolder ? '文件夹' : '文件'

  try {
    await ElMessageBox.confirm(`确定彻底删除${entryLabel}“${entry.fileName}”吗？彻底删除后不可恢复。`, '彻底删除确认', {
      type: 'warning',
      confirmButtonText: '彻底删除',
      cancelButtonText: '取消'
    })

    await request.delete(`/files/${entry.id}/permanent`)
    ElMessage.success('彻底删除成功')
    fetchFiles()
  } catch (error) {
  }
}

const formatDeletedAt = (deletedAt?: string) => {
  if (!deletedAt) {
    return '-'
  }

  const date = new Date(deletedAt)
  if (Number.isNaN(date.getTime())) {
    return '-'
  }

  return `${date.getFullYear()}-${`${date.getMonth() + 1}`.padStart(2, '0')}-${`${date.getDate()}`.padStart(2, '0')} ${`${date.getHours()}`.padStart(2, '0')}:${`${date.getMinutes()}`.padStart(2, '0')}`
}

const formatUploadedAt = (uploadedAt?: string) => {
  if (!uploadedAt) {
    return '-'
  }

  const date = new Date(uploadedAt)
  if (Number.isNaN(date.getTime())) {
    return '-'
  }

  return `${date.getFullYear()}-${`${date.getMonth() + 1}`.padStart(2, '0')}-${`${date.getDate()}`.padStart(2, '0')} ${`${date.getHours()}`.padStart(2, '0')}:${`${date.getMinutes()}`.padStart(2, '0')}`
}

const getRemainingDays = (deletedAt?: string) => {
  if (!deletedAt) {
    return 0
  }

  const deletedTime = new Date(deletedAt).getTime()
  if (Number.isNaN(deletedTime)) {
    return 0
  }

  const expiredTime = deletedTime + 7 * 24 * 60 * 60 * 1000
  const remainMs = expiredTime - Date.now()
  if (remainMs <= 0) {
    return 0
  }

  return Math.ceil(remainMs / (24 * 60 * 60 * 1000))
}

const isImage = (type: string) => {
  const imageTypes = ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp']
  return imageTypes.some(t => type?.toLowerCase().includes(t))
}

const isVideo = (type: string) => {
  const videoTypes = ['.mp4', '.avi', '.mov', '.wmv', '.flv']
  return videoTypes.some(t => type?.toLowerCase().includes(t))
}

const getFileExt = (type?: string) => {
  if (!type) return 'FILE'
  const ext = type.replace('.', '').trim().toUpperCase()
  return ext || 'FILE'
}

const getFileBadgeClass = (type?: string) => {
  const ext = (type || '').toLowerCase()
  if (['.pdf'].includes(ext)) return 'badge-pdf'
  if (['.doc', '.docx', '.txt'].includes(ext)) return 'badge-doc'
  if (['.xls', '.xlsx', '.csv'].includes(ext)) return 'badge-xls'
  if (['.ppt', '.pptx'].includes(ext)) return 'badge-ppt'
  if (['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp'].includes(ext)) return 'badge-img'
  if (['.mp4', '.avi', '.mov', '.wmv', '.flv'].includes(ext)) return 'badge-video'
  if (['.zip', '.rar', '.7z'].includes(ext)) return 'badge-zip'
  return 'badge-other'
}

const formatFileSize = (bytes: number) => {
  if (!bytes) return '-'
  const units = ['B', 'KB', 'MB', 'GB']
  let unitIndex = 0
  let size = bytes
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024
    unitIndex++
  }
  return `${size.toFixed(2)} ${units[unitIndex]}`
}

onMounted(() => {
  window.addEventListener('click', closeEntryContextMenu)
  fetchProjects()
})

onBeforeUnmount(() => {
  window.removeEventListener('click', closeEntryContextMenu)
  closePreview()
  cleanupThumbnailUrls([])
})
</script>

<style scoped>
.file-page {
  padding: 10px;
}

.explorer-card {
  min-height: calc(100vh - 120px);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.header-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.header-tip {
  font-size: 12px;
  color: var(--el-color-warning-dark-2);
  background: var(--el-color-warning-light-9);
  border: 1px solid var(--el-color-warning-light-5);
  border-radius: 999px;
  padding: 4px 10px;
  white-space: nowrap;
}

.explorer-layout {
  display: flex;
  gap: 12px;
}

.left-tree-panel {
  width: 240px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  padding: 10px;
  background: var(--el-fill-color-extra-light);
}

.panel-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  margin-bottom: 8px;
}

.project-search-input {
  width: 130px;
}

.right-content-panel {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  min-height: calc(100vh - 260px);
}

.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.toolbar-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.selection-count {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.upload-progress {
  margin: 6px 0 12px;
  padding: 8px 10px;
  border-radius: 6px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
}

.upload-progress-meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-bottom: 6px;
  gap: 8px;
}

.upload-progress-name {
  min-width: 0;
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.upload-progress-value {
  font-weight: 600;
  color: var(--el-text-color-regular);
}

.recycle-toggle-btn {
  background: var(--el-color-warning-light-5);
  border-color: var(--el-color-warning);
  color: var(--el-color-warning-dark-2);
  font-weight: 600;
}

.recycle-toggle-btn:hover {
  background: var(--el-color-warning-light-3);
  border-color: var(--el-color-warning-dark-2);
  color: var(--el-color-warning-dark-2);
}

.view-mode-toggle {
  margin-right: 4px;
}

.path-bar {
  color: var(--el-text-color-secondary);
  font-size: 13px;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.recycle-hint {
  display: inline-flex;
  align-items: center;
  padding: 6px 10px;
  border-radius: 6px;
  background: var(--el-color-warning-light-6);
  border: 1px solid var(--el-color-warning);
  color: var(--el-color-warning-dark-2);
  font-weight: 600;
}

.path-bar :deep(.el-breadcrumb) {
  line-height: 1.2;
  flex: 1;
  min-width: 0;
}

.path-back-btn {
  font-weight: 600;
  padding: 6px 10px;
}

.path-node {
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  min-height: 24px;
  padding: 2px 10px;
  border-radius: 4px;
  user-select: none;
  border: 1px solid transparent;
}

.path-node:hover {
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  border-color: var(--el-color-primary-light-5);
}

.path-drop-active {
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-8);
  border-color: var(--el-color-primary);
}

.path-project-node {
  font-weight: 600;
}

.path-project-name {
  color: var(--el-text-color-primary);
}

.search-bar {
  display: flex;
  gap: 10px;
  margin-bottom: 12px;
}

.file-name {
  display: flex;
  align-items: center;
  gap: 8px;
}

.folder-link {
  cursor: pointer;
  color: var(--el-color-primary);
}

.folder-link:hover {
  text-decoration: underline;
}

.file-icon {
  color: #909399;
}

.content-area {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.icon-view {
  min-height: 100%;
}

.icon-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(132px, 1fr));
  gap: 8px;
  min-height: 260px;
}

.empty-content-center {
  min-height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.icon-card {
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  padding: 5px 6px;
  height: 150px;
  background: var(--el-bg-color);
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  gap: 6px;
  position: relative;
}

.icon-card.is-selected {
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 1px var(--el-color-primary-light-5) inset;
}

.icon-select {
  position: absolute;
  top: 6px;
  right: 6px;
  z-index: 2;
  background: var(--el-bg-color);
  border-radius: 6px;
  padding: 0 4px;
}

.icon-card:hover {
  border-color: var(--el-color-primary-light-5);
}

.icon-card.folder-card {
  cursor: pointer;
}

.icon-card.folder-card .icon-card-main {
  width: 100%;
  text-align: center;
}

.icon-card.folder-card .icon-actions:empty {
  display: none;
}

.drop-target-active {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.icon-card-main {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 100%;
  gap: 2px;
}

.thumb-box {
  width: 52px;
  height: 52px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  background: var(--el-fill-color-light);
  overflow: hidden;
}

.folder-thumb {
  width: 44px;
  height: 34px;
  position: relative;
}

.folder-tab {
  position: absolute;
  top: 0;
  left: 4px;
  width: 18px;
  height: 7px;
  border-radius: 4px 4px 0 0;
  background: var(--el-color-warning-light-5);
}

.folder-body {
  position: absolute;
  top: 6px;
  left: 0;
  right: 0;
  bottom: 0;
  border-radius: 4px;
  background: var(--el-color-warning);
}

.thumb-image {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.file-badge {
  width: 40px;
  height: 48px;
  border-radius: 6px;
  display: flex;
  align-items: flex-end;
  justify-content: center;
  padding-bottom: 5px;
  box-sizing: border-box;
}

.file-badge-ext {
  color: var(--el-color-white);
  font-size: 10px;
  font-weight: 700;
  line-height: 1;
}

.badge-pdf {
  background: var(--el-color-danger);
}

.badge-doc {
  background: var(--el-color-primary);
}

.badge-xls {
  background: var(--el-color-success);
}

.badge-ppt {
  background: var(--el-color-warning);
}

.badge-img {
  background: var(--el-color-info);
}

.badge-video {
  background: var(--el-color-primary-light-3);
}

.badge-zip {
  background: var(--el-color-warning-dark-2);
}

.badge-other {
  background: var(--el-text-color-secondary);
}

.icon-name {
  width: 100%;
  text-align: center;
  font-size: 12px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  min-height: 18px;
  line-height: 18px;
}

.icon-meta {
  font-size: 10px;
  color: var(--el-text-color-secondary);
  line-height: 1;
  min-height: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.icon-uploader {
  width: 100%;
  text-align: center;
  font-size: 10px;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  line-height: 1.2;
  min-height: 14px;
}

.icon-actions {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 4px;
  min-height: 20px;
  width: 100%;
}

.action-btn {
  border-radius: 4px;
  padding: 1px 6px;
  border: 1px solid transparent;
}

.action-preview,
.action-download {
  background: var(--el-color-primary-light-9);
  border-color: var(--el-color-primary-light-5);
  color: var(--el-color-primary);
}

.action-restore {
  background: var(--el-color-success-light-9);
  border-color: var(--el-color-success-light-5);
  color: var(--el-color-success);
}

.action-permanent {
  background: var(--el-color-danger-light-9);
  border-color: var(--el-color-danger-light-5);
  color: var(--el-color-danger);
}

.folder-context-menu {
  position: fixed;
  z-index: 2200;
  min-width: 120px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color-overlay);
  box-shadow: var(--el-box-shadow-light);
  padding: 4px 0;
}

.context-item {
  line-height: 32px;
  font-size: 13px;
  padding: 0 12px;
  cursor: pointer;
  color: var(--el-text-color-regular);
}

.context-item:hover {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
}

.context-item.danger:hover {
  background: var(--el-color-danger-light-9);
  color: var(--el-color-danger);
}

.preview-wrap {
  min-height: 420px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.preview-image,
.preview-video {
  max-width: 100%;
  max-height: 70vh;
}

.preview-frame {
  width: 100%;
  height: 70vh;
  border: 0;
  background: var(--el-fill-color-blank);
}

.preview-html {
  width: 100%;
  max-height: 70vh;
  overflow: auto;
  padding: 12px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  box-sizing: border-box;
}

.preview-html :deep(.sheet-section + .sheet-section) {
  margin-top: 14px;
}

.preview-html :deep(.sheet-title) {
  margin: 0 0 8px;
  font-size: 13px;
  color: var(--el-text-color-regular);
}

.preview-html :deep(.sheet-table-wrap) {
  overflow: auto;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
}

.preview-html :deep(table) {
  width: 100%;
  border-collapse: collapse;
  background: var(--el-bg-color);
}

.preview-html :deep(th),
.preview-html :deep(td) {
  border: 1px solid var(--el-border-color-lighter);
  padding: 6px 8px;
  font-size: 12px;
  color: var(--el-text-color-primary);
  white-space: nowrap;
}

.preview-html :deep(th) {
  background: var(--el-fill-color-light);
  font-weight: 600;
}

.preview-html :deep(tr:nth-child(even) td) {
  background: var(--el-fill-color-extra-light);
}

.pagination {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px solid var(--el-border-color-lighter);
  background: var(--el-bg-color);
  display: flex;
  justify-content: flex-end;
}
</style>
