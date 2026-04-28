<template>
  <div class="process-page">
    <el-card>
      <template #header>
        <div class="card-header">
          <div class="header-title-wrap">
            <span class="header-title">项目任务模板</span>
            <span class="header-subtitle">维护项目标准工序模板，支持拖拽调整顺序与默认模板设置</span>
          </div>
          <el-button type="primary" @click="openCreateDialog">新建模板</el-button>
        </div>
      </template>

      <el-table :data="templates" v-loading="loading" style="width: 100%" class="template-table">
        <el-table-column prop="name" label="模板名称" width="220" />
        <el-table-column prop="description" label="描述" min-width="260" show-overflow-tooltip />
        <el-table-column prop="isDefault" label="默认" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isDefault ? 'success' : 'info'">{{ row.isDefault ? '是' : '否' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="步骤数" width="100">
          <template #default="{ row }">{{ row.steps?.length || 0 }}</template>
        </el-table-column>
        <el-table-column label="操作" width="280" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="editTemplate(row)">编辑</el-button>
            <el-button link type="primary" @click="setDefault(row.id)" :disabled="row.isDefault">设为默认</el-button>
            <el-popconfirm title="确认删除该模板？" @confirm="deleteTemplate(row.id)">
              <template #reference>
                <el-button link type="danger">删除</el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑项目任务模板' : '新建项目任务模板'" width="980px">
      <el-form :model="form" label-width="90px">
        <el-form-item label="模板名称">
          <el-input v-model="form.name" placeholder="请输入模板名称" />
        </el-form-item>
        <el-form-item label="模板描述">
          <el-input v-model="form.description" placeholder="请输入模板描述" />
        </el-form-item>
        <el-form-item label="默认模板">
          <el-switch v-model="form.isDefault" />
        </el-form-item>
      </el-form>

      <div class="step-header">
        <span>工序步骤</span>
        <el-button type="primary" link @click="addStep">新增步骤</el-button>
      </div>

      <el-table :data="form.steps" style="width: 100%" max-height="360" :row-class-name="getStepRowClass">
        <el-table-column label="拖动" width="70">
          <template #default="{ $index }">
            <div
              class="drag-handle"
              draggable="true"
              title="按住拖动调整顺序"
              @dragstart="onStepDragStart($event, $index)"
              @dragover.prevent="onStepDragOver($index)"
              @drop.prevent
              @dragend="onStepDragEnd"
            >
              ☰
            </div>
          </template>
        </el-table-column>
        <el-table-column label="序号" width="70">
          <template #default="{ $index }">{{ $index + 1 }}</template>
        </el-table-column>
        <el-table-column label="阶段" width="140">
          <template #default="{ row }">
            <el-input v-model="row.stage" placeholder="如：施工" />
          </template>
        </el-table-column>
        <el-table-column label="工序名称" min-width="220">
          <template #default="{ row }">
            <el-input v-model="row.name" placeholder="请输入工序名称" />
          </template>
        </el-table-column>
        <el-table-column label="优先级" width="120">
          <template #default="{ row }">
            <el-select v-model="row.priority">
              <el-option label="低" :value="1" />
              <el-option label="中" :value="2" />
              <el-option label="高" :value="3" />
              <el-option label="紧急" :value="4" />
            </el-select>
          </template>
        </el-table-column>
        <el-table-column label="预计工期(天)" width="170">
          <template #default="{ row }">
            <el-input-number
              v-model="row.estimatedDays"
              :min="1"
              :max="365"
              class="estimated-days-input"
            />
          </template>
        </el-table-column>
        <el-table-column label="操作" width="90" fixed="right">
          <template #default="{ $index }">
            <el-button link type="danger" @click="removeStep($index)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="saveTemplate">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { request } from '@/api/request'

const loading = ref(false)
const submitting = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const editId = ref<number | null>(null)
const draggingStepIndex = ref<number | null>(null)

const templates = ref<any[]>([])

const form = ref<any>({
  name: '',
  description: '',
  isDefault: false,
  steps: []
})

const fetchTemplates = async () => {
  loading.value = true
  try {
    const res = await request.get('/process-templates')
    templates.value = res.data
  } catch (error) {
    console.error('获取项目任务模板失败：', error)
  } finally {
    loading.value = false
  }
}

const openCreateDialog = () => {
  isEdit.value = false
  editId.value = null
  form.value = {
    name: '',
    description: '',
    isDefault: false,
    steps: []
  }
  dialogVisible.value = true
}

const editTemplate = (row: any) => {
  isEdit.value = true
  editId.value = row.id
  form.value = {
    name: row.name,
    description: row.description,
    isDefault: row.isDefault,
    steps: (row.steps || []).map((s: any) => ({
      sortOrder: s.sortOrder,
      stage: s.stage,
      name: s.name,
      description: s.description,
      priority: s.priority,
      estimatedDays: s.estimatedDays
    }))
  }
  dialogVisible.value = true
}

const addStep = () => {
  form.value.steps.push({
    sortOrder: form.value.steps.length + 1,
    stage: '',
    name: '',
    description: '',
    priority: 2,
    estimatedDays: 3
  })
}

const removeStep = (index: number) => {
  form.value.steps.splice(index, 1)
}

const onStepDragStart = (event: DragEvent, index: number) => {
  draggingStepIndex.value = index

  if (!event.dataTransfer) {
    return
  }

  event.dataTransfer.effectAllowed = 'move'
  event.dataTransfer.setData('text/plain', String(index))

  const target = event.target as HTMLElement | null
  const row = target?.closest('tr') as HTMLElement | null
  if (!row) {
    return
  }

  const rowRect = row.getBoundingClientRect()
  const dragImage = row.cloneNode(true) as HTMLElement
  dragImage.style.position = 'fixed'
  dragImage.style.top = '-9999px'
  dragImage.style.left = '-9999px'
  dragImage.style.width = `${rowRect.width}px`
  dragImage.style.background = 'var(--el-bg-color)'
  dragImage.style.border = '1px solid var(--el-border-color)'
  dragImage.style.boxSizing = 'border-box'
  dragImage.style.opacity = '0.95'
  dragImage.style.pointerEvents = 'none'
  dragImage.style.zIndex = '9999'

  document.body.appendChild(dragImage)
  event.dataTransfer.setDragImage(dragImage, 20, 20)

  setTimeout(() => {
    document.body.removeChild(dragImage)
  }, 0)
}

const onStepDragOver = (overIndex: number) => {
  if (draggingStepIndex.value === null || draggingStepIndex.value === overIndex) {
    return
  }

  const fromIndex = draggingStepIndex.value
  const movedStep = form.value.steps[fromIndex]
  form.value.steps.splice(fromIndex, 1)
  form.value.steps.splice(overIndex, 0, movedStep)
  draggingStepIndex.value = overIndex
}

const onStepDragEnd = () => {
  draggingStepIndex.value = null
}

const getStepRowClass = ({ rowIndex }: { rowIndex: number }) => {
  return draggingStepIndex.value === rowIndex ? 'dragging-step-row' : ''
}

const saveTemplate = async () => {
  if (!form.value.name?.trim()) {
    ElMessage.warning('请填写模板名称')
    return
  }

  const payload = {
    name: form.value.name,
    description: form.value.description,
    isDefault: form.value.isDefault,
    steps: form.value.steps.map((s: any, idx: number) => ({
      sortOrder: idx + 1,
      stage: s.stage,
      name: s.name,
      description: s.description,
      priority: s.priority,
      estimatedDays: s.estimatedDays
    }))
  }

  submitting.value = true
  try {
    if (isEdit.value && editId.value) {
      await request.put(`/process-templates/${editId.value}`, payload)
      ElMessage.success('更新成功')
    } else {
      await request.post('/process-templates', payload)
      ElMessage.success('创建成功')
    }
    dialogVisible.value = false
    fetchTemplates()
  } catch (error) {
    console.error('保存模板失败：', error)
  } finally {
    submitting.value = false
  }
}

const deleteTemplate = async (id: number) => {
  try {
    await request.delete(`/process-templates/${id}`)
    ElMessage.success('删除成功')
    fetchTemplates()
  } catch (error) {
    console.error('删除模板失败：', error)
  }
}

const setDefault = async (id: number) => {
  try {
    await request.post(`/process-templates/${id}/set-default`)
    ElMessage.success('已设置为默认模板')
    fetchTemplates()
  } catch (error) {
    console.error('设置默认模板失败：', error)
  }
}

onMounted(() => {
  fetchTemplates()
})
</script>

<style scoped>
.process-page {
  --biz-card-bg: rgba(255, 255, 255, 0.94);
  --biz-card-border: #d7e4f8;
  --biz-text-strong: #0f3b8c;
  --biz-text-muted: #5f6b7a;
  padding: 12px;
  min-height: 100%;
  box-sizing: border-box;
  background: #f5f8fc;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.header-title-wrap {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.header-title {
  font-size: 20px;
  color: var(--biz-text-strong);
  font-weight: 800;
  line-height: 1.1;
}

.header-subtitle {
  font-size: 12px;
  color: var(--biz-text-muted);
  line-height: 1.35;
}

.step-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin: 12px 0 10px;
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid #dbe8ff;
  background: #f8fbff;
}

.step-header > span {
  font-size: 14px;
  color: #1d3250;
  font-weight: 700;
}

.drag-handle {
  width: 28px;
  height: 28px;
  line-height: 28px;
  text-align: center;
  border: 1px dashed var(--el-border-color);
  border-radius: 4px;
  cursor: move;
  user-select: none;
  color: var(--el-text-color-secondary);
  background: #fff;
}

:deep(.dragging-step-row) {
  opacity: 0.45;
}

:deep(.estimated-days-input) {
  width: 100%;
}

.process-page :deep(.el-card) {
  border-radius: 16px;
  border: 1px solid var(--biz-card-border);
  background: var(--biz-card-bg);
  box-shadow: 0 18px 40px rgba(36, 66, 135, 0.12);
  overflow: hidden;
}

.process-page :deep(.el-card__header) {
  background: linear-gradient(140deg, #f4f9ff 0%, #f5fcf8 100%);
  border-bottom: 1px solid #dce8fb;
}

.process-page :deep(.el-card__body) {
  padding: 14px;
}

.process-page :deep(.el-table) {
  border-radius: 12px;
  border: 1px solid #dce8fb;
  overflow: hidden;
}

.process-page :deep(.el-table th.el-table__cell) {
  background: #f3f8ff;
  color: #2e4566;
  font-weight: 700;
}

.process-page :deep(.el-table tr:hover > td.el-table__cell) {
  background: #f5f9ff;
}

.process-page :deep(.el-button) {
  border-radius: 999px;
}

.process-page :deep(.el-input__wrapper),
.process-page :deep(.el-select__wrapper),
.process-page :deep(.el-textarea__inner) {
  border-radius: 999px;
}

@media (max-width: 900px) {
  .process-page {
    padding: 8px;
  }

  .header-title {
    font-size: 17px;
  }
}
</style>
