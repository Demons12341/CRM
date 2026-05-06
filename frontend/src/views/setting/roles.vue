<template>
  <div class="roles-page">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>角色管理</span>
          <el-button type="primary" @click="showCreateDialog">
            <el-icon><Plus /></el-icon>
            新建角色
          </el-button>
        </div>
      </template>

      <el-table :data="roles" style="width: 100%" v-loading="loading">
        <el-table-column prop="name" label="角色名称" width="180" />
        <el-table-column prop="description" label="描述" min-width="240" show-overflow-tooltip />
        <el-table-column prop="permissions" label="权限" min-width="260" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.permissions || '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" width="180">
          <template #default="{ row }">
            {{ formatDateTime(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="{ row }">
            <el-button type="primary" link @click="editRole(row)">编辑</el-button>
            <el-popconfirm title="确定要删除该角色吗？" @confirm="deleteRole(row.id)">
              <template #reference>
                <el-button type="danger" link>删除</el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="dialogVisible" :title="isEdit ? '编辑角色' : '新建角色'" width="520px">
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="90px">
        <el-form-item label="角色名称" prop="name">
          <el-input v-model="form.name" placeholder="请输入角色名称" />
        </el-form-item>
        <el-form-item label="角色描述" prop="description">
          <el-input v-model="form.description" type="textarea" :rows="3" placeholder="请输入角色描述" />
        </el-form-item>
        <el-form-item label="权限标识" prop="permissions">
          <el-input v-model="form.permissions" type="textarea" :rows="4" placeholder="可输入逗号分隔权限，如：project.read,project.write" />
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
const roles = ref<any[]>([])

const form = reactive({
  name: '',
  description: '',
  permissions: ''
})

const formRules: FormRules = {
  name: [
    { required: true, message: '请输入角色名称', trigger: 'blur' },
    { min: 2, max: 50, message: '角色名称长度在 2 到 50 个字符', trigger: 'blur' }
  ]
}

const fetchRoles = async () => {
  loading.value = true
  try {
    const res = await request.get('/roles')
    roles.value = res.data
  } catch (error) {
    console.error('获取角色列表失败：', error)
  } finally {
    loading.value = false
  }
}

const resetForm = () => {
  Object.assign(form, {
    name: '',
    description: '',
    permissions: ''
  })
  formRef.value?.resetFields()
}

const showCreateDialog = () => {
  isEdit.value = false
  editId.value = null
  resetForm()
  dialogVisible.value = true
}

const editRole = (row: any) => {
  isEdit.value = true
  editId.value = row.id
  Object.assign(form, {
    name: row.name,
    description: row.description || '',
    permissions: row.permissions || ''
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
        await request.put(`/roles/${editId.value}`, form)
        ElMessage.success('更新成功')
      } else {
        await request.post('/roles', form)
        ElMessage.success('创建成功')
      }
      dialogVisible.value = false
      fetchRoles()
    } catch (error) {
      console.error('保存角色失败：', error)
    } finally {
      submitting.value = false
    }
  })
}

const deleteRole = async (id: number) => {
  try {
    await request.delete(`/roles/${id}`)
    ElMessage.success('删除成功')
    fetchRoles()
  } catch (error) {
    console.error('删除角色失败：', error)
  }
}

const formatDateTime = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}

onMounted(() => {
  fetchRoles()
})
</script>

<style scoped>
.roles-page {
  --biz-card-bg: rgba(255, 255, 255, 0.94);
  --biz-card-border: #d7e4f8;
  --biz-text-strong: #0f3b8c;
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
  color: var(--biz-text-strong);
  font-weight: 800;
}

.roles-page :deep(.el-card) {
  border-radius: 16px;
  border: 1px solid var(--biz-card-border);
  background: var(--biz-card-bg);
  box-shadow: 0 18px 40px rgba(36, 66, 135, 0.12);
  overflow: hidden;
}

.roles-page :deep(.el-card__header) {
  background: #f4f9ff;
  border-bottom: 1px solid #dce8fb;
}

.roles-page :deep(.el-card__body) {
  padding: 14px;
}

.roles-page :deep(.el-table) {
  border-radius: 12px;
  border: 1px solid #dce8fb;
  overflow: hidden;
}

.roles-page :deep(.el-table th.el-table__cell) {
  background: #f3f8ff;
  color: #2e4566;
  font-weight: 700;
}

.roles-page :deep(.el-table tr:hover > td.el-table__cell) {
  background: #f5f9ff;
}

.roles-page :deep(.el-button) {
  border-radius: 999px;
}

.roles-page :deep(.el-input__wrapper),
.roles-page :deep(.el-textarea__inner) {
  border-radius: 999px;
}

.roles-page :deep(.el-textarea__inner) {
  border-radius: 12px;
}

@media (max-width: 900px) {
  .roles-page {
    padding: 8px;
  }

  .card-header > span {
    font-size: 17px;
  }
}
</style>
