<template>
  <div class="business-lines-page">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>业务线管理</span>
          <el-button type="primary" v-permission="'business-lines.create'" @click="showCreateDialog">
            <el-icon><Plus /></el-icon>
            新建业务线
          </el-button>
        </div>
      </template>

      <el-table :data="businessLines" style="width: 100%" v-loading="loading">
        <el-table-column prop="name" label="业务线名称" min-width="200" />
        <el-table-column prop="description" label="描述" min-width="260" show-overflow-tooltip />
        <el-table-column prop="sortOrder" label="排序" width="100" />
        <el-table-column prop="createdAt" label="创建时间" width="180">
          <template #default="{ row }">
            {{ formatDateTime(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="{ row }">
            <el-button type="primary" link v-permission="'business-lines.edit'" @click="editBusinessLine(row)">编辑</el-button>
            <el-popconfirm v-permission="'business-lines.delete'" title="确定要删除该业务线吗？" @confirm="deleteBusinessLine(row.id)">
              <template #reference>
                <el-button type="danger" link>删除</el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑业务线' : '新建业务线'" width="480px">
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="90px">
        <el-form-item label="名称" prop="name">
          <el-input v-model="form.name" placeholder="请输入业务线名称" />
        </el-form-item>
        <el-form-item label="描述" prop="description">
          <el-input v-model="form.description" type="textarea" :rows="3" placeholder="请输入业务线描述" />
        </el-form-item>
        <el-form-item label="排序" prop="sortOrder">
          <el-input-number v-model="form.sortOrder" :min="0" :max="9999" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="handleSubmit">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { request } from '@/api/request'
import dayjs from 'dayjs'

const loading = ref(false)
const submitting = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const editId = ref<number | null>(null)
const formRef = ref<FormInstance>()
const businessLines = ref<any[]>([])

const form = reactive({
  name: '',
  description: '',
  sortOrder: 0
})

const formRules: FormRules = {
  name: [
    { required: true, message: '请输入业务线名称', trigger: 'blur' },
    { max: 50, message: '名称长度不能超过 50 个字符', trigger: 'blur' }
  ]
}

const fetchBusinessLines = async () => {
  loading.value = true
  try {
    const res = await request.get('/business-lines')
    businessLines.value = res.data || []
  } catch (error) {
    console.error('获取业务线列表失败：', error)
  } finally {
    loading.value = false
  }
}

const resetForm = () => {
  Object.assign(form, {
    name: '',
    description: '',
    sortOrder: 0
  })
  formRef.value?.resetFields()
}

const showCreateDialog = () => {
  isEdit.value = false
  editId.value = null
  resetForm()
  dialogVisible.value = true
}

const editBusinessLine = (row: any) => {
  isEdit.value = true
  editId.value = row.id
  Object.assign(form, {
    name: row.name,
    description: row.description || '',
    sortOrder: row.sortOrder ?? 0
  })
  dialogVisible.value = true
}

const handleSubmit = async () => {
  if (!formRef.value) return

  await formRef.value.validate(async (valid) => {
    if (!valid) return

    submitting.value = true
    try {
      if (isEdit.value && editId.value) {
        await request.put(`/business-lines/${editId.value}`, form)
        ElMessage.success('更新成功')
      } else {
        await request.post('/business-lines', form)
        ElMessage.success('创建成功')
      }
      dialogVisible.value = false
      fetchBusinessLines()
    } catch (error) {
      console.error('保存业务线失败：', error)
    } finally {
      submitting.value = false
    }
  })
}

const deleteBusinessLine = async (id: number) => {
  try {
    await request.delete(`/business-lines/${id}`)
    ElMessage.success('删除成功')
    fetchBusinessLines()
  } catch (error) {
    console.error('删除业务线失败：', error)
  }
}

const formatDateTime = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}

onMounted(() => {
  fetchBusinessLines()
})
</script>

<style scoped>
.business-lines-page {
  padding: 12px;
  min-height: 100%;
  box-sizing: border-box;
  background: #f5f8fc;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
}

.card-header > span {
  font-size: 20px;
  color: #0f3b8c;
  font-weight: 800;
}

.business-lines-page :deep(.el-card) {
  border-radius: 16px;
  border: 1px solid #d7e4f8;
  background: rgba(255, 255, 255, 0.94);
  box-shadow: 0 18px 40px rgba(36, 66, 135, 0.12);
  overflow: hidden;
}

.business-lines-page :deep(.el-card__header) {
  background: #f4f9ff;
  border-bottom: 1px solid #dce8fb;
}

.business-lines-page :deep(.el-card__body) {
  padding: 14px;
}

.business-lines-page :deep(.el-table) {
  border-radius: 12px;
  border: 1px solid #dce8fb;
  overflow: hidden;
}

.business-lines-page :deep(.el-table th.el-table__cell) {
  background: #f3f8ff;
  color: #2e4566;
  font-weight: 700;
}

.business-lines-page :deep(.el-button) {
  border-radius: 999px;
}

.business-lines-page :deep(.el-input__wrapper) {
  border-radius: 999px;
}

.business-lines-page :deep(.el-textarea__inner) {
  border-radius: 12px;
}
</style>
