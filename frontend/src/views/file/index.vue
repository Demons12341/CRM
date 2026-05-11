<template>
  <div class="file-page">
    <el-card class="explorer-card">
      <template #header>
        <div class="card-header">
          <div class="header-main-row">
            <span class="header-title">文件资源管理器</span>
            <div class="header-tip-stack">
              <div class="header-tip-bar header-tip-bar-primary">共享文件夹：全员可查看，可右键设置私密</div>
              <span class="header-tip">其余项目：本人上传的文件可右击共享给项目成员</span>
            </div>
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
          <el-tree ref="fileProjectTreeRef" v-if="filteredProjectTreeData.length" :data="filteredProjectTreeData" node-key="id"
            :current-node-key="searchForm.projectId" :expand-on-click-node="false" highlight-current
            :props="{ children: 'children', label: 'label' }"
            @node-click="handleProjectNodeClick">
            <template #default="{ node, data }">
              <span v-if="data.isBusinessLine" class="project-tree-node business-line-node" @click.stop="toggleFileBusinessLine(node)">
                <span class="project-tree-name" :title="data.label">{{ data.label }}</span>
                <span class="project-tree-meta">
                  <el-tag size="small" type="info">{{ data.children?.length || 0 }}</el-tag>
                </span>
              </span>
              <span v-else class="project-tree-name" :title="data.label">{{ data.label }}</span>
            </template>
          </el-tree>
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
              <el-button class="recycle-toggle-btn" size="small" @click="toggleRecycleMode">
                {{ recycleMode ? '返回文件列表' : '回收站' }}
              </el-button>
              <el-button v-if="!recycleMode" type="success" size="small" v-permission="'files.upload'" :disabled="!searchForm.projectId"
                @click="openCreateFolderDialog">
                <el-icon>
                  <FolderAdd />
                </el-icon>
                新建文件夹
              </el-button>
              <el-button v-if="!recycleMode" type="danger" size="small" v-permission="'files.delete'" :disabled="!selectedCount"
                @click="handleBulkDelete">
                批量删除
              </el-button>
              <el-button v-else type="danger" size="small" v-permission="'files.delete'" :disabled="!selectedCount"
                @click="handleBulkPermanentDelete">
                批量彻底删除
              </el-button>
              <el-upload v-if="!recycleMode" v-permission="'files.upload'" :action="uploadUrl" :headers="uploadHeaders" :data="uploadData"
                :before-upload="beforeUpload" :on-success="handleUploadSuccess" :on-error="handleUploadError"
                :on-progress="handleUploadProgress" :show-file-list="false">
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

          <div v-if="downloading" class="upload-progress">
            <div class="upload-progress-meta">
              <span class="upload-progress-name">正在下载：{{ downloadFileName || '文件' }}</span>
              <span class="upload-progress-value">
                {{ downloadProgressKnown ? `${downloadProgress}%` : `${downloadLoadedText} / 计算总大小中...` }}
              </span>
            </div>
            <el-progress :percentage="downloadProgressKnown ? downloadProgress : 100"
              :indeterminate="!downloadProgressKnown" :stroke-width="8" />
          </div>

          <div class="search-bar">
            <el-input v-model="searchForm.keyword" :placeholder="recycleMode ? '搜索回收站文件名' : '搜索当前项目文件名'" clearable
              class="search-input" @keyup.enter="handleSearch" />
            <el-button type="primary" @click="handleSearch">搜索</el-button>
            <el-button @click="resetSearch">重置</el-button>
          </div>

          <div v-if="recycleMode || searchForm.projectId" class="overview-row">
            <span class="overview-chip">视图：{{ recycleMode ? '回收站' : '文件列表' }}</span>
            <!-- <span class="overview-chip">项目：{{ recycleMode ? '全部项目' : (currentProjectName || '-') }}</span> -->
            <!-- <span class="overview-chip">目录：{{ folderPath.length ? folderPath[folderPath.length - 1].fileName : '项目根目录' -->
            <!-- }}</span> -->
            <span class="overview-chip">条目：{{ files.length }}</span>
            <span v-if="selectedCount" class="overview-chip is-active">已选：{{ selectedCount }}</span>
          </div>

          <div class="content-area">
            <div v-if="recycleMode || searchForm.projectId" v-loading="loading" class="icon-view">
              <div v-if="files.length" class="icon-grid">
                <div v-for="file in files" :key="file.id" class="icon-card"
                  :class="{ 'drop-target-active': dragOverFolderId === file.id && file.isFolder, 'folder-card': file.isFolder, 'is-selected': isSelected(file), 'recycle-card': recycleMode, 'search-card': !recycleMode && !!searchForm.keyword }"
                  :draggable="!recycleMode" @click="handleCardClick(file)" @dragstart="handleDragStart(file, $event)"
                  @dragend="handleDragEnd" @dragover.prevent="handleDragOver(file, $event)"
                  @dragleave="handleDragLeave(file)" @drop.prevent="handleDropToFolder(file)"
                  @contextmenu.prevent="openEntryContextMenu(file, $event)">
                  <el-checkbox v-if="isSelectable(file)" class="icon-select" :model-value="isSelected(file)"
                    @change="handleSelectionChange(file, Boolean($event))" @click.stop />
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
                      {{ recycleMode ? `删除时间：${formatDeletedAt(file.deletedAt)}` : (file.isFolder ? '文件夹' :
                        formatFileSize(file.fileSize)) }}
                    </div>
                    <div v-if="!recycleMode && !file.isFolder" class="icon-flag-row">
                      <el-tag v-if="isSharedFolderProject && !file.isShared" type="warning" size="small">私密</el-tag>
                      <el-tag v-else-if="!isSharedFolderProject && file.isShared" type="success"
                        size="small">已共享</el-tag>
                    </div>
                    <div class="icon-uploader" :title="file.uploaderName || '-'">上传人：{{ file.uploaderName || '-' }}
                    </div>
                    <div class="icon-uploader" :title="formatUploadedAt(file.uploadedAt)">
                      上传时间：{{ formatUploadedAt(file.uploadedAt) }}
                    </div>
                    <div v-if="recycleMode" class="icon-uploader" :title="file.projectName || '-'">
                      所属项目：{{ file.projectName || '-' }}
                    </div>
                    <div v-if="!recycleMode && searchForm.keyword && !file.isFolder" class="icon-uploader"
                      :title="file.locationPath || '项目根目录'">
                      所在目录：
                      <el-link type="primary" @click.stop="goToFileLocation(file)">
                        {{ file.locationPath || '项目根目录' }}
                      </el-link>
                    </div>
                    <div v-if="recycleMode && getRemainingDays(file.deletedAt) > 0" class="icon-uploader">
                      剩余恢复天数：{{ getRemainingDays(file.deletedAt) }}
                    </div>
                  </div>
                  <div class="icon-actions">
                    <template v-if="recycleMode">
                      <el-button v-permission="'files.delete'" class="action-btn action-restore" type="success" link size="small"
                        @click.stop="restoreEntry(file)">恢复</el-button>
                      <el-button v-permission="'files.delete'" class="action-btn action-permanent" type="danger" link size="small"
                        @click.stop="permanentlyDeleteEntry(file)">彻底删除</el-button>
                    </template>
                    <template v-else-if="!file.isFolder">
                      <el-button class="action-btn action-preview" type="primary" link size="small"
                        @click.stop="openPreview(file)">预览</el-button>
                      <el-button v-permission="'files.download'" class="action-btn action-download" type="primary" link size="small"
                        @click.stop="downloadFile(file)">下载</el-button>
                    </template>
                  </div>
                </div>
              </div>
              <div v-else class="empty-content-center">
                <el-empty :description="recycleMode ? '回收站暂无内容' : '当前目录暂无内容'" />
              </div>
            </div>
            <el-empty v-else description="请先在左侧选择项目，或直接进入回收站" />
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
            <div v-if="canShareEntry(entryContextMenu.entry)" class="context-item" @click="handleShareEntry">{{
              getShareActionText() }}</div>
            <div v-if="canMakePrivateEntry(entryContextMenu.entry)" class="context-item"
              @click="handleMakePrivateEntry">{{
                getPrivateActionText() }}
            </div>
            <div v-if="canRestoreManagerVisible(entryContextMenu.entry)" class="context-item"
              @click="handleRestoreManagerVisibleEntry">
              恢复仅管理员/项目负责人可见
            </div>
            <div v-if="canRestoreSharedInSharedFolder(entryContextMenu.entry)" class="context-item"
              @click="handleRestoreSharedEntry">
              恢复共享
            </div>
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
import { useRoute } from 'vue-router'

const route = useRoute()

const loading = ref(false)
const fileProjectTreeRef = ref<any>()
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
const downloading = ref(false)
const downloadFileName = ref('')
const downloadProgress = ref(0)
const downloadProgressKnown = ref(true)
const downloadLoadedText = ref('0 B')
const selectedFileIds = ref<number[]>([])
const currentUser = ref<any>(null)
const locateQuery = ref<{ projectId: number, fileName: string, taskFolder: string } | null>(null)

const withSilentError = (config: any = {}) => ({
  ...config,
  headers: {
    ...(config?.headers || {}),
    'X-Silent-Error': '1'
  }
})

const getErrorMessage = (error: any, fallback: string) => {
  return error?.response?.data?.message || error?.response?.data?.title || fallback
}

const notifyPermissionOrError = (error: any, fallback: string) => {
  const status = Number(error?.response?.status)
  const message = getErrorMessage(error, fallback)

  if (status === 403) {
    ElMessage.warning(message || '暂无访问权限')
    return
  }

  ElMessage.error(message || fallback)
}

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

const BUSINESS_LINES = ref<string[]>([])

const fetchBusinessLines = async () => {
  try {
    const res = await request.get('/business-lines')
    BUSINESS_LINES.value = (res.data || []).map((bl: any) => bl.name)
  } catch (error) {
    BUSINESS_LINES.value = []
  }
}

const projectTreeData = computed(() => {
  const sharedFolder = projects.value.find((project: any) => getProjectName(project) === SHARED_FOLDER_PROJECT_NAME)
  const otherProjects = projects.value.filter((project: any) => getProjectName(project) !== SHARED_FOLDER_PROJECT_NAME)

  const groups: Record<string, any[]> = {}
  for (const project of otherProjects) {
    const bl = project.businessLine || '未分类业务线'
    if (!groups[bl]) groups[bl] = []
    groups[bl].push(project)
  }

  const orderedLines = [...BUSINESS_LINES.value, '未分类业务线'].filter(line => groups[line]?.length)

  const result: any[] = []

  if (sharedFolder) {
    result.push({
      id: normalizeProjectId(sharedFolder),
      label: `${getProjectName(sharedFolder)}（${getProjectManagerName(sharedFolder)}）`
    })
  }

  for (const line of orderedLines) {
    result.push({
      id: `bl_${line}`,
      label: line,
      isBusinessLine: true,
      children: groups[line].map((project: any) => ({
        id: normalizeProjectId(project),
        label: `${getProjectName(project)}（${getProjectManagerName(project)}）`
      }))
    })
  }

  return result
})

const filteredProjectTreeData = computed(() => {
  const keyword = projectSearchKeyword.value.trim().toLowerCase()
  if (!keyword) {
    return projectTreeData.value
  }

  return projectTreeData.value
    .filter((node: any) => {
      if (node.isBusinessLine) {
        return node.children?.some((child: any) =>
          `${child.label || ''}`.toLowerCase().includes(keyword)
        )
      }
      return `${node.label || ''}`.toLowerCase().includes(keyword)
    })
    .map((node: any) => {
      if (node.isBusinessLine) {
        return {
          ...node,
          children: node.children.filter((child: any) =>
            `${child.label || ''}`.toLowerCase().includes(keyword)
          )
        }
      }
      return node
    })
})

const currentProjectName = computed(() => {
  const current = projects.value.find((p: any) => normalizeProjectId(p) === searchForm.projectId)
  return getProjectName(current)
})

const isSharedFolderProject = computed(() => currentProjectName.value === SHARED_FOLDER_PROJECT_NAME)

const getCurrentUserId = () => {
  const id = Number(currentUser.value?.id)
  return Number.isFinite(id) ? id : 0
}

const isEntryOwner = (entry: any) => {
  if (!entry) {
    return false
  }

  const currentUserId = getCurrentUserId()
  return currentUserId > 0 && Number(entry.uploadedBy) === currentUserId
}

const canShareEntry = (entry: any) => {
  if (!entry || entry.isFolder || isSharedFolderProject.value) {
    return false
  }

  if (!isEntryOwner(entry)) {
    return false
  }

  return !entry.isShared
}

const canMakePrivateEntry = (entry: any) => {
  if (!entry || entry.isFolder) {
    return false
  }

  if (!isEntryOwner(entry)) {
    return false
  }

  if (isSharedFolderProject.value) {
    return !!entry.isShared
  }

  return false
}

const canRestoreManagerVisible = (entry: any) => {
  if (!entry || entry.isFolder || isSharedFolderProject.value) {
    return false
  }

  if (!isEntryOwner(entry)) {
    return false
  }

  return !!entry.isShared
}

const canRestoreSharedInSharedFolder = (entry: any) => {
  if (!entry || entry.isFolder || !isSharedFolderProject.value) {
    return false
  }

  if (!isEntryOwner(entry)) {
    return false
  }

  return !entry.isShared
}

const getShareActionText = () => {
  return '共享给项目成员'
}

const getPrivateActionText = () => {
  return '设为私密'
}

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
    ids.map(id => request.delete(`/files/${id}`, withSilentError()))
  )

  const failedCount = results.filter(result => result.status === 'rejected').length
  return { failedCount }
}

const fetchFiles = async () => {
  if (recycleMode.value) {
    return fetchRecycleBinFiles()
  }

  if (!searchForm.projectId) {
    files.value = []
    cleanupThumbnailUrls([])
    selectedFileIds.value = []
    return true
  }

  loading.value = true
  try {
    const params: any = {
      parentId: currentFolderId.value
    }
    if (searchForm.keyword) {
      params.keyword = searchForm.keyword
      params.recursive = true
    }

    const res = await request.get(`/projects/${searchForm.projectId}/files`, withSilentError({ params }))
    files.value = res.data.items
    reconcileSelection(files.value)
    refreshImageThumbnails(files.value)
    return true
  } catch (error) {
    console.error('获取文件列表失败：', error)
    notifyPermissionOrError(error, '获取文件列表失败')
    files.value = []
    cleanupThumbnailUrls([])
    selectedFileIds.value = []
    return false
  } finally {
    loading.value = false
  }
}

const fetchRecycleBinFiles = async () => {
  loading.value = true
  try {
    const params: any = {}
    if (searchForm.keyword) params.keyword = searchForm.keyword

    const token = localStorage.getItem('token')
    const globalResponse = await axios.get('/api/files/recycle-bin', {
      params,
      headers: token ? { Authorization: `Bearer ${token}` } : undefined
    })

    if (globalResponse?.data?.success) {
      const items = Array.isArray(globalResponse?.data?.data?.items) ? globalResponse.data.data.items : []
      files.value = items
      selectedFileIds.value = []
      cleanupThumbnailUrls([])
      return true
    }

    throw new Error(globalResponse?.data?.message || '获取回收站列表失败')
  } catch (globalError) {
    console.warn('全局回收站接口不可用，已切换到项目聚合模式：', globalError)

    try {
      const token = localStorage.getItem('token')
      selectedFileIds.value = []
      const projectIdNameMap = new Map<number, string>()
      const projectIds = projects.value
        .map((project: any) => {
          const id = normalizeProjectId(project)
          const name = getProjectName(project)
          if (id > 0) {
            projectIdNameMap.set(id, name || '-')
          }
          return id
        })
        .filter((id: number) => id > 0)

      if (!projectIds.length) {
        throw globalError
      }

      const params: any = {}
      if (searchForm.keyword) params.keyword = searchForm.keyword

      const results = await Promise.allSettled(
        projectIds.map(projectId =>
          axios.get(`/api/projects/${projectId}/files/recycle-bin`, {
            params,
            headers: token ? { Authorization: `Bearer ${token}` } : undefined
          })
        )
      )

      const mergedItems = results
        .flatMap((result: any) => {
          if (result?.status !== 'fulfilled') {
            return []
          }

          const payload = result.value?.data
          const items = payload?.success && Array.isArray(payload?.data?.items) ? payload.data.items : []
          return items.map((item: any) => {
            const projectId = Number(item?.projectId)
            return {
              ...item,
              projectName: item?.projectName || projectIdNameMap.get(projectId) || '-'
            }
          })
        })
        .sort((left: any, right: any) => {
          const leftDeleted = new Date(left?.deletedAt || 0).getTime()
          const rightDeleted = new Date(right?.deletedAt || 0).getTime()
          if (rightDeleted !== leftDeleted) {
            return rightDeleted - leftDeleted
          }

          const leftUploaded = new Date(left?.uploadedAt || 0).getTime()
          const rightUploaded = new Date(right?.uploadedAt || 0).getTime()
          return rightUploaded - leftUploaded
        })

      files.value = mergedItems
      selectedFileIds.value = []
      cleanupThumbnailUrls([])

      if (!mergedItems.length) {
        const hasSuccessResult = results.some((result: any) => result?.status === 'fulfilled' && result?.value?.data?.success)
        if (!hasSuccessResult) {
          throw globalError
        }
      }

      return true
    } catch (fallbackError) {
      console.error('获取回收站列表失败：', fallbackError)
      notifyPermissionOrError(fallbackError, '获取回收站列表失败')
      return false
    }
  } finally {
    loading.value = false
  }
}

const fetchProjects = async () => {
  try {
    const res = await request.get('/projects', withSilentError({ params: { pageSize: 200 } }))
    const items = Array.isArray(res.data.items) ? [...res.data.items] : []
    items.sort((left: any, right: any) => {
      const leftIsShared = getProjectName(left) === SHARED_FOLDER_PROJECT_NAME
      const rightIsShared = getProjectName(right) === SHARED_FOLDER_PROJECT_NAME

      if (leftIsShared && !rightIsShared) return -1
      if (!leftIsShared && rightIsShared) return 1
      return 0
    })

    projects.value = items
    if (!projects.value.length) {
      return
    }

    const queryProjectId = locateQuery.value?.projectId
    const hasQueryProject = Number.isFinite(queryProjectId)
      && Number(queryProjectId) > 0
      && projects.value.some((project: any) => normalizeProjectId(project) === Number(queryProjectId))

    searchForm.projectId = hasQueryProject
      ? Number(queryProjectId)
      : normalizeProjectId(projects.value[0])

    await fetchFiles()

    if (locateQuery.value) {
      await locateFileFromQuery()
    }
  } catch (error) {
    console.error('获取项目列表失败：', error)
    notifyPermissionOrError(error, '获取项目列表失败')
  }
}

const locateFileFromQuery = async () => {
  const target = locateQuery.value
  if (!target) {
    return
  }

  if (!target.projectId || !target.fileName) {
    locateQuery.value = null
    return
  }

  recycleMode.value = false
  searchForm.projectId = target.projectId
  searchForm.keyword = ''
  currentFolderId.value = undefined
  folderPath.value = []
  selectedFileIds.value = []

  try {
    const params: any = {
      keyword: target.fileName,
      recursive: true
    }

    const res = await request.get(`/projects/${target.projectId}/files`, withSilentError({ params }))
    const items = Array.isArray(res?.data?.items) ? res.data.items : []

    const normalizedFileName = `${target.fileName}`.trim()
    const exactFileMatches = items.filter((item: any) => {
      if (item?.isFolder) {
        return false
      }

      return `${item?.fileName || ''}`.trim() === normalizedFileName
    })

    const matchedInTaskFolder = exactFileMatches.find((item: any) => {
      const path = `${item?.locationPath || ''}`
      return !!target.taskFolder && path.split('/').map((name: string) => name.trim()).includes(target.taskFolder)
    })

    const targetFile = matchedInTaskFolder || exactFileMatches[0]
    if (!targetFile) {
      ElMessage.warning(`未找到交付物文件：${target.fileName}`)
      await fetchFiles()
      locateQuery.value = null
      return
    }

    await goToFileLocation(targetFile)
  } catch (error) {
    console.error('定位交付物文件失败：', error)
    ElMessage.warning('定位交付物文件失败，请稍后重试')
  } finally {
    locateQuery.value = null
  }
}

const handleProjectNodeClick = (node: any) => {
  if (node?.isBusinessLine) {
    return
  }
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

const toggleFileBusinessLine = (treeNode: any) => {
  treeNode.expanded = !treeNode.expanded
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
  const targetId = Number(target.id)
  currentFolderId.value = Number.isFinite(targetId) && targetId > 0 ? targetId : undefined
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

    await request.put(`/files/${entry.id}/rename`, { name }, withSilentError())

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
    notifyPermissionOrError(error, '重命名失败')
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

    await deleteFile(entry)
  } catch (error) {
  }
}

const handleShareEntry = async () => {
  const entry = entryContextMenu.entry
  if (!canShareEntry(entry)) {
    closeEntryContextMenu()
    return
  }

  closeEntryContextMenu()

  try {
    await request.put(`/files/${entry.id}/share`, undefined, withSilentError())
    ElMessage.success('共享成功')
    fetchFiles()
  } catch (error: any) {
    notifyPermissionOrError(error, '共享失败')
  }
}

const handleMakePrivateEntry = async () => {
  const entry = entryContextMenu.entry
  if (!canMakePrivateEntry(entry)) {
    closeEntryContextMenu()
    return
  }

  closeEntryContextMenu()

  try {
    await request.put(`/files/${entry.id}/private`, undefined, withSilentError())
    ElMessage.success(isSharedFolderProject.value ? '已设为私密' : '已取消共享')
    fetchFiles()
  } catch (error: any) {
    notifyPermissionOrError(error, '设置私密失败')
  }
}

const handleRestoreManagerVisibleEntry = async () => {
  const entry = entryContextMenu.entry
  if (!canRestoreManagerVisible(entry)) {
    closeEntryContextMenu()
    return
  }

  closeEntryContextMenu()

  try {
    await request.put(`/files/${entry.id}/private`, undefined, withSilentError())
    ElMessage.success('已恢复为仅管理员/项目负责人可见')
    fetchFiles()
  } catch (error: any) {
    notifyPermissionOrError(error, '恢复失败')
  }
}

const handleRestoreSharedEntry = async () => {
  const entry = entryContextMenu.entry
  if (!canRestoreSharedInSharedFolder(entry)) {
    closeEntryContextMenu()
    return
  }

  closeEntryContextMenu()

  try {
    await request.put(`/files/${entry.id}/share`, undefined, withSilentError())
    ElMessage.success('已恢复共享')
    fetchFiles()
  } catch (error: any) {
    notifyPermissionOrError(error, '恢复共享失败')
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
    }, withSilentError())
    ElMessage.success(targetFolder?.id ? `已移动到文件夹：${targetFolder.fileName}` : '已移动到项目根目录')
    fetchFiles()
  } catch (error: any) {
    notifyPermissionOrError(error, '移动失败')
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
  const parent = [...folderPath.value].reverse().find(folder => Number(folder?.id) > 0)
  currentFolderId.value = parent ? Number(parent.id) : undefined
  fetchFiles()
}

const buildFolderPathFromLocation = (locationPath: string | undefined, targetFolderId: number) => {
  const normalized = `${locationPath || ''}`.trim()
  if (!normalized || normalized === '项目根目录') {
    return [{ id: targetFolderId, fileName: '当前文件夹' }]
  }

  const names = normalized
    .split('/')
    .map(name => name.trim())
    .filter(Boolean)

  if (!names.length) {
    return [{ id: targetFolderId, fileName: '当前文件夹' }]
  }

  return names.map((fileName, index) => ({
    id: index === names.length - 1 ? targetFolderId : -1,
    fileName
  }))
}

const resolveFolderPathFromLocation = async (projectId: number, locationPath: string | undefined, targetFolderId: number) => {
  const normalized = `${locationPath || ''}`.trim()
  if (!normalized || normalized === '项目根目录') {
    return [{ id: targetFolderId, fileName: '当前文件夹' }]
  }

  const names = normalized
    .split('/')
    .map(name => name.trim())
    .filter(Boolean)

  if (!names.length) {
    return [{ id: targetFolderId, fileName: '当前文件夹' }]
  }

  const resolved: Array<{ id: number, fileName: string }> = []
  let parentId: number | undefined = undefined

  for (let index = 0; index < names.length; index++) {
    const currentName = names[index]
    const params: any = { parentId }
    const res = await request.get(`/projects/${projectId}/files`, withSilentError({ params }))
    const items = Array.isArray(res?.data?.items) ? res.data.items : []
    const folder = items.find((item: any) => item?.isFolder && item?.fileName === currentName)

    if (!folder?.id) {
      return buildFolderPathFromLocation(locationPath, targetFolderId)
    }

    const folderId = Number(folder.id)
    resolved.push({ id: folderId, fileName: folder.fileName || currentName })
    parentId = folderId
  }

  if (resolved.length) {
    resolved[resolved.length - 1].id = targetFolderId
  }

  return resolved
}

const goToFileLocation = async (file: any) => {
  if (!searchForm.projectId || recycleMode.value) {
    return
  }

  try {
    searchForm.keyword = ''

    const targetFolderId = file?.parentId as number | undefined
    const targetFileId = Number(file?.id)
    if (!targetFolderId) {
      currentFolderId.value = undefined
      folderPath.value = []
      await fetchFiles()
      if (targetFileId && files.value.some(item => item.id === targetFileId)) {
        selectedFileIds.value = [targetFileId]
      }
      return
    }

    folderPath.value = await resolveFolderPathFromLocation(searchForm.projectId, file?.locationPath, targetFolderId)
    currentFolderId.value = targetFolderId
    await fetchFiles()
    if (targetFileId && files.value.some(item => item.id === targetFileId)) {
      selectedFileIds.value = [targetFileId]
    }
  } catch (error) {
    console.error('定位文件所在目录失败：', error)
    ElMessage.error('定位目录失败，请稍后重试')
  }
}

const goToRootFolder = () => {
  if (!searchForm.projectId) return
  currentFolderId.value = undefined
  folderPath.value = []
  fetchFiles()
}

const handleSearch = () => {
  fetchFiles()
}

const toggleRecycleMode = () => {
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
  selectedFileIds.value = []

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
      selectedFileIds.value.map(id => request.delete(`/files/${id}/permanent`, withSilentError()))
    )

    const failedCount = results.filter(result => result.status === 'rejected').length
    selectedFileIds.value = []
    fetchFiles()

    if (failedCount > 0) {
      ElMessage.warning(`已彻底删除部分项目，失败 ${failedCount} 个`)
      return
    }

    ElMessage.success('批量彻底删除成功')
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
    }, withSilentError())
    ElMessage.success('文件夹创建成功')
    createFolderVisible.value = false
    fetchFiles()
  } catch (error: any) {
    notifyPermissionOrError(error, '文件夹创建失败')
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

  if (downloading.value) {
    ElMessage.warning('当前有文件正在下载，请稍后再试')
    return
  }

  try {
    const token = localStorage.getItem('token')
    if (!token) {
      ElMessage.error('登录已过期，请重新登录')
      return
    }

    downloading.value = true
    downloadFileName.value = file.fileName || '文件'
    downloadProgress.value = 0
    downloadProgressKnown.value = true
    downloadLoadedText.value = '0 B'

    const response = await axios.get(`/api/files/${file.id}/download`, {
      responseType: 'blob',
      headers: {
        Authorization: `Bearer ${token}`
      },
      onDownloadProgress: (event: any) => {
        const loaded = Number(event?.loaded || 0)
        const total = Number(event?.total || 0)

        downloadLoadedText.value = formatTransferSize(loaded)

        if (total > 0) {
          downloadProgressKnown.value = true
          downloadProgress.value = Math.min(100, Math.round((loaded / total) * 100))
          return
        }

        downloadProgressKnown.value = false
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
  } finally {
    downloading.value = false
    downloadFileName.value = ''
    downloadProgress.value = 0
    downloadProgressKnown.value = true
    downloadLoadedText.value = '0 B'
  }
}

const deleteFile = async (entry: any) => {
  const id = Number(entry?.id)
  if (!Number.isFinite(id) || id <= 0) {
    return
  }

  try {
    await request.delete(`/files/${id}`, withSilentError())
    ElMessage.success(entry?.isFolder ? '文件夹已彻底删除' : '已移入回收站，可在 7 天内恢复')
    fetchFiles()
  } catch (error) {
    console.error('删除失败：', error)
    notifyPermissionOrError(error, '删除失败')
  }
}

const restoreEntry = async (entry: any) => {
  if (!entry?.id) {
    return
  }

  try {
    const res = await request.put(`/files/${entry.id}/restore`, undefined, withSilentError())
    ElMessage.success('恢复成功')

    if (recycleMode.value && res?.data?.id) {
      const restored = res.data
      recycleMode.value = false
      searchForm.projectId = Number(restored.projectId) || searchForm.projectId
      searchForm.keyword = ''

      const targetFolderId = Number(restored.parentId)
      if (Number.isFinite(targetFolderId) && targetFolderId > 0) {
        currentFolderId.value = targetFolderId
        const projectId = Number(searchForm.projectId)
        if (Number.isFinite(projectId) && projectId > 0) {
          folderPath.value = await resolveFolderPathFromLocation(projectId, restored.locationPath, targetFolderId)
        } else {
          folderPath.value = buildFolderPathFromLocation(restored.locationPath, targetFolderId)
        }
      } else {
        currentFolderId.value = undefined
        folderPath.value = []
      }

      await fetchFiles()
      if (files.value.some(item => item.id === restored.id)) {
        selectedFileIds.value = [restored.id]
      }
      return
    }

    fetchFiles()
  } catch (error) {
    console.error('恢复失败：', error)
    notifyPermissionOrError(error, '恢复失败')
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

    await request.delete(`/files/${entry.id}/permanent`, withSilentError())
    ElMessage.success('彻底删除成功')
    fetchFiles()
  } catch (error) {
    if (error !== 'cancel') {
      notifyPermissionOrError(error, '彻底删除失败')
    }
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

const formatTransferSize = (bytes: number) => {
  if (bytes <= 0) return '0 B'
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
  const userStr = localStorage.getItem('user')
  if (userStr) {
    currentUser.value = JSON.parse(userStr)
  }

  const queryProjectId = Number(route.query.projectId)
  const queryFileName = `${route.query.fileName || ''}`.trim()
  const queryTaskFolder = `${route.query.taskFolder || ''}`.trim()
  if (queryProjectId > 0 && queryFileName) {
    locateQuery.value = {
      projectId: queryProjectId,
      fileName: queryFileName,
      taskFolder: queryTaskFolder
    }
  }

  window.addEventListener('click', closeEntryContextMenu)
  fetchBusinessLines()
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
  --fm-bg-top: #eff5ff;
  --fm-bg-bottom: #f6fbf8;
  --fm-card-bg: rgba(255, 255, 255, 0.94);
  --fm-card-border: #d7e4f8;
  --fm-strong: #0f3b8c;
  --fm-muted: #5f6b7a;
  --fm-accent: #2b6ff7;
  --fm-accent-soft: #eaf1ff;
  --fm-success: #0d8f68;
  --fm-warn: #946200;
  --fm-danger: #c23a3a;
  padding: 12px;
  height: 100%;
  min-height: 0;
  display: flex;
  overflow: hidden;
  background: #f5f8fc;
}

.explorer-card {
  width: 100%;
  display: flex;
  flex-direction: column;
  border-radius: 16px;
  border: 1px solid var(--fm-card-border);
  background: var(--fm-card-bg);
  box-shadow: 0 18px 40px rgba(36, 66, 135, 0.12);
  overflow: hidden;
}

.file-page :deep(.el-card__header) {
  background: #f4f9ff;
  border-bottom: 1px solid #dce8fb;
}

.file-page :deep(.el-card__body) {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
  padding: 14px;
}

.card-header {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 8px;
  padding: 2px 0;
}

.header-main-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  gap: 8px;
  flex-wrap: wrap;
}

.header-title {
  font-size: 20px;
  font-weight: 800;
  color: var(--fm-strong);
  letter-spacing: 0.4px;
}

.header-subtitle {
  font-size: 13px;
  color: var(--fm-muted);
  line-height: 1.35;
}

.header-tip {
  display: inline-flex;
  align-items: center;
  font-size: 12px;
  color: var(--fm-warn);
  background: #fff8e5;
  border: 1px solid #f2dfa2;
  border-radius: 999px;
  padding: 5px 12px;
  line-height: 1;
  white-space: nowrap;
}

.header-tip-stack {
  display: flex;
  flex-direction: row;
  align-items: center;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 6px;
}

.header-tip-bar {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 999px;
  padding: 5px 12px;
  font-size: 12px;
  line-height: 1;
  white-space: nowrap;
  border: 1px solid transparent;
  box-shadow: 0 1px 0 rgba(255, 255, 255, 0.7) inset, 0 4px 10px rgba(0, 0, 0, 0.04);
}

.header-tip-bar-primary {
  color: #144eaf;
  background: #ebf3ff;
  border-color: #c7dcff;
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
  color: var(--fm-muted);
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
  border-radius: 8px;
  padding: 4px 6px;
}

.left-tree-panel :deep(.el-tree-node__content:hover) {
  background: #edf5ff;
}

.project-tree-node {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;
}

.project-tree-name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
}

.project-tree-meta {
  margin-left: auto;
  flex-shrink: 0;
}

.business-line-node {
  cursor: pointer;
}

.business-line-node .project-tree-name {
  font-weight: 600;
  color: #0f3b8c;
}

.business-line-expand-icon {
  font-size: 10px;
  color: #909399;
  width: 14px;
  flex-shrink: 0;
}

.left-tree-panel :deep(.el-tree--highlight-current .el-tree-node.is-current > .el-tree-node__content) {
  background: #dceaff;
  color: #1248a6;
  font-weight: 700;
}

.right-content-panel {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
  gap: 10px;
  flex-wrap: wrap;
}

.toolbar-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.upload-progress {
  margin: 2px 0 10px;
  padding: 10px 12px;
  border-radius: 10px;
  background: #f6faff;
  border: 1px solid #dbe8ff;
}

.upload-progress-meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 12px;
  color: #4f6179;
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
  color: #1b2f4d;
}

.recycle-toggle-btn {
  background: #fff4da;
  border-color: #efcf85;
  color: #7f5800;
  font-weight: 600;
}

.recycle-toggle-btn:hover {
  background: #ffeab8;
  border-color: #d5a64a;
  color: #6f4b00;
}

.path-bar {
  color: var(--fm-muted);
  font-size: 13px;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.recycle-hint {
  display: inline-flex;
  align-items: center;
  padding: 7px 12px;
  border-radius: 999px;
  background: #fff4da;
  border: 1px solid #efcf85;
  color: #7f5800;
  font-weight: 600;
}

.path-bar :deep(.el-breadcrumb) {
  line-height: 1.2;
  flex: 1;
  min-width: 0;
}

.path-back-btn {
  font-weight: 600;
  padding: 6px 12px;
  border-radius: 999px;
}

.path-node {
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  min-height: 24px;
  padding: 3px 10px;
  border-radius: 999px;
  user-select: none;
  border: 1px solid transparent;
}

.path-node:hover {
  color: #1f57c8;
  background: #eaf1ff;
  border-color: #cddfff;
}

.path-drop-active {
  color: #1f57c8;
  background: #e2ecff;
  border-color: #7ca9f7;
}

.path-project-node {
  font-weight: 600;
}

.path-project-name {
  color: var(--el-text-color-primary);
  max-width: 260px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  display: inline-block;
  vertical-align: bottom;
}

.search-bar {
  display: flex;
  gap: 10px;
  margin-bottom: 10px;
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid #dbe8ff;
  background: #f8fbff;
}

.search-input {
  width: 280px;
}

.overview-row {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 10px;
}

.overview-chip {
  display: inline-flex;
  align-items: center;
  height: 28px;
  padding: 0 12px;
  border-radius: 999px;
  font-size: 12px;
  color: #395171;
  border: 1px solid #d2e2fc;
  background: #fafdff;
}

.overview-chip.is-active {
  color: #fff;
  border-color: #2b6ff7;
  background: #3e7eff;
}

.content-area {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding-right: 2px;
}

.icon-view {
  min-height: 100%;
}

.icon-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  gap: 12px;
  min-height: 150px;
}

.empty-content-center {
  min-height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.icon-card {
  border: 1px solid #dbe6f8;
  border-radius: 14px;
  padding: 9px 9px 7px;
  min-height: 150px;
  background: #ffffff;
  display: flex;
  flex-direction: column;
  justify-content: flex-start;
  align-items: center;
  gap: 8px;
  position: relative;
  overflow: hidden;
  transition: transform 0.18s ease, box-shadow 0.18s ease, border-color 0.18s ease;
}

.icon-card.recycle-card,
.icon-card.search-card {
  height: auto;
  min-height: 186px;
  justify-content: flex-start;
  padding-top: 12px;
}

.icon-card.is-selected {
  border-color: #3c78ec;
  box-shadow: 0 0 0 2px #d7e6ff inset;
}

.icon-select {
  position: absolute;
  top: 8px;
  right: 8px;
  z-index: 2;
  background: #fff;
  border: 1px solid #d6e5ff;
  border-radius: 999px;
  padding: 0 4px;
  line-height: 1;
}

.icon-select :deep(.el-checkbox__label) {
  display: none;
  padding-left: 0;
}

.icon-select :deep(.el-checkbox__input),
.icon-select :deep(.el-checkbox__inner) {
  display: block;
}

.icon-card:hover {
  border-color: #8fb4ff;
  box-shadow: 0 10px 16px rgba(34, 70, 136, 0.14);
}

.icon-card.folder-card {
  cursor: pointer;
  min-height: 114px;
  padding: 7px 9px 5px;
  gap: 4px;
}

.icon-card.folder-card .icon-card-main {
  width: 100%;
  text-align: center;
  gap: 0px;
}

.icon-card.folder-card .thumb-box {
  width: 44px;
  height: 44px;
}

.icon-card.folder-card .folder-thumb {
  width: 44px;
  height: 34px;
}

.icon-card.folder-card .folder-tab {
  width: 18px;
  height: 7px;
  left: 4px;
}

.icon-card.folder-card .icon-actions:empty {
  display: none;
}

.drop-target-active {
  border-color: #2b6ff7;
  background: #ebf2ff;
}

.icon-card-main {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 100%;
  gap: 1px;
}

.icon-card.recycle-card .icon-card-main,
.icon-card.search-card .icon-card-main {
  gap: 3px;
}

.thumb-box {
  width: 52px;
  height: 52px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 14px;
  background: #f4f8ff;
  border: 1px solid #dce8ff;
  overflow: hidden;
}

.folder-thumb {
  width: 52px;
  height: 40px;
  position: relative;
}

.folder-tab {
  position: absolute;
  top: 0;
  left: 5px;
  width: 20px;
  height: 8px;
  border-radius: 6px 6px 0 0;
  background: #ffd573;
}

.folder-body {
  position: absolute;
  top: 7px;
  left: 0;
  right: 0;
  bottom: 0;
  border-radius: 8px;
  background: #ffc748;
}

.thumb-image {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.file-badge {
  width: 50px;
  height: 56px;
  border-radius: 10px;
  display: flex;
  align-items: flex-end;
  justify-content: center;
  padding-bottom: 8px;
  box-sizing: border-box;
  box-shadow: inset 0 -8px 16px rgba(0, 0, 0, 0.12);
}

.file-badge-ext {
  color: #fff;
  font-size: 11px;
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
  background: #65758d;
}

.icon-name {
  width: 100%;
  text-align: center;
  font-size: 13px;
  color: #1e2f47;
  font-weight: 700;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  min-height: 26px;
  line-height: 1.3;
}

.icon-meta {
  font-size: 11px;
  color: #60718a;
  line-height: 1.25;
  min-height: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  text-align: center;
}

.icon-flag-row {
  min-height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.icon-uploader {
  width: 100%;
  text-align: center;
  font-size: 11px;
  color: #5e6e84;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  line-height: 1.3;
  min-height: 13px;
}

.icon-card.recycle-card .icon-uploader,
.icon-card.search-card .icon-uploader {
  white-space: normal;
  overflow: visible;
  text-overflow: clip;
  line-height: 1.35;
}

.icon-actions {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 6px;
  min-height: 26px;
  width: 100%;
}

.action-btn {
  border-radius: 999px;
  padding: 2px 10px;
  border: 1px solid transparent;
  font-weight: 600;
}

.action-preview,
.action-download {
  background: #ebf2ff;
  border-color: #cddfff;
  color: #2759bd;
}

.action-restore {
  background: #e8f8f2;
  border-color: #bde9d9;
  color: var(--fm-success);
}

.action-permanent {
  background: #fdeeee;
  border-color: #f6cece;
  color: var(--fm-danger);
}

.folder-context-menu {
  position: fixed;
  z-index: 2200;
  min-width: 140px;
  border: 1px solid #dbe6f8;
  border-radius: 10px;
  background: #fff;
  box-shadow: 0 16px 24px rgba(14, 40, 94, 0.16);
  padding: 6px 0;
}

.context-item {
  line-height: 34px;
  font-size: 13px;
  padding: 0 14px;
  cursor: pointer;
  color: #2d3f5a;
}

.context-item:hover {
  background: #edf4ff;
  color: #2557bc;
}

.context-item.danger:hover {
  background: #fdeeee;
  color: var(--fm-danger);
}

.preview-wrap {
  min-height: 420px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 12px;
  background: #f7faff;
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
  background: #fff;
  border-radius: 10px;
}

.preview-html {
  width: 100%;
  max-height: 70vh;
  overflow: auto;
  padding: 14px;
  background: #fff;
  border: 1px solid #dbe6f8;
  border-radius: 10px;
  box-sizing: border-box;
}

.preview-html :deep(.sheet-section + .sheet-section) {
  margin-top: 14px;
}

.preview-html :deep(.sheet-title) {
  margin: 0 0 8px;
  font-size: 13px;
  color: #28405f;
}

.preview-html :deep(.sheet-table-wrap) {
  overflow: auto;
  border: 1px solid #dbe6f8;
  border-radius: 8px;
}

.preview-html :deep(table) {
  width: 100%;
  border-collapse: collapse;
  background: #fff;
}

.preview-html :deep(th),
.preview-html :deep(td) {
  border: 1px solid #e8effb;
  padding: 6px 8px;
  font-size: 12px;
  color: #23354f;
  white-space: nowrap;
}

.preview-html :deep(th) {
  background: #f4f8ff;
  font-weight: 600;
}

.preview-html :deep(tr:nth-child(even) td) {
  background: #f9fbff;
}

.pagination {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px solid #e4ecfa;
  background: #fff;
  display: flex;
  justify-content: flex-end;
}

.right-content-panel :deep(.el-button) {
  border-radius: 999px;
}

.search-bar :deep(.el-input__wrapper),
.left-tree-panel :deep(.el-input__wrapper) {
  border-radius: 999px;
}

.right-content-panel :deep(.el-link__inner) {
  font-weight: 600;
}

@media (max-width: 1200px) {
  .left-tree-panel {
    width: 236px;
  }

  .icon-grid {
    grid-template-columns: repeat(auto-fill, minmax(146px, 1fr));
  }
}

@media (max-width: 900px) {
  .file-page {
    padding: 8px;
  }

  .explorer-layout {
    flex-direction: column;
  }

  .left-tree-panel {
    width: 100%;
    max-height: 240px;
  }

  .search-input {
    width: 100%;
  }

  .search-bar {
    flex-wrap: wrap;
  }

  .toolbar {
    align-items: flex-start;
  }

  .path-bar {
    width: 100%;
    flex-wrap: wrap;
  }

  .icon-grid {
    grid-template-columns: repeat(auto-fill, minmax(138px, 1fr));
    gap: 10px;
  }
}

@media (max-width: 600px) {
  .file-page :deep(.el-card__body) {
    padding: 10px;
  }

  .header-title {
    font-size: 17px;
  }

  .header-subtitle {
    font-size: 12px;
  }

  .icon-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .icon-card {
    min-height: 120px;
  }

  .overview-chip {
    height: 26px;
    font-size: 11px;
    padding: 0 10px;
  }
}
</style>
