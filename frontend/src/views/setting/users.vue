<template>
  <div class="users-page">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>用户管理</span>
          <div class="header-actions">
            <el-button v-permission="'settings.users.import'" @click="downloadImportTemplate">
              下载Excel模板
            </el-button>
            <el-upload
              v-permission="'settings.users.import'"
              :show-file-list="false"
              :http-request="uploadImportExcel"
              accept=".xlsx"
              :disabled="importing"
            >
              <el-button :loading="importing" type="success">
                上传Excel导入
              </el-button>
            </el-upload>
            <el-button type="primary" v-permission="'settings.users.create'" @click="showCreateDialog">
              <el-icon><Plus /></el-icon>
              新建用户
            </el-button>
          </div>
        </div>
      </template>
      
      <div class="search-bar">
        <el-input
          v-model="searchForm.keyword"
          placeholder="搜索用户名或姓名"
          clearable
          style="width: 200px;"
          @keyup.enter="handleSearch"
        />
        <el-select v-model="searchForm.roleId" placeholder="角色" clearable style="width: 150px;">
          <el-option
            v-for="role in roles"
            :key="role.id"
            :label="role.name"
            :value="role.id"
          />
        </el-select>
        <el-select v-model="searchForm.isActive" placeholder="状态" clearable style="width: 150px;">
          <el-option label="启用" :value="true" />
          <el-option label="禁用" :value="false" />
        </el-select>
        <el-button type="primary" @click="handleSearch">搜索</el-button>
        <el-button @click="resetSearch">重置</el-button>
      </div>
      
      <el-table :data="users" style="width: 100%;" v-loading="loading">
        <el-table-column prop="username" label="用户名" width="150" />
        <el-table-column prop="realName" label="姓名" width="140" />
        <el-table-column prop="phone" label="手机号" width="150" />
        <el-table-column prop="roleName" label="角色" width="120">
          <template #default="{ row }">
            <el-tag :type="getRoleType(row.roleName)">
              {{ row.roleName }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="isActive" label="状态" width="80">
          <template #default="{ row }">
            <el-tag :type="row.isActive ? 'success' : 'danger'">
              {{ row.isActive ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" width="160">
          <template #default="{ row }">
            {{ formatDateTime(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column prop="lastLoginAt" label="上次登录时间" width="160">
          <template #default="{ row }">
            {{ row.lastLoginAt ? formatDateTime(row.lastLoginAt) : '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="loginCount" label="累计登录次数" width="120" />
        <el-table-column label="操作" width="200" fixed="right">
          <template #default="{ row }">
            <el-button type="primary" link v-permission="'settings.users.edit'" @click="editUser(row)">
              编辑
            </el-button>
            <el-button
              type="primary"
              link
              v-permission="'settings.users.edit'"
              @click="toggleUserStatus(row)"
            >
              {{ row.isActive ? '禁用' : '启用' }}
            </el-button>
            <el-popconfirm
              v-permission="'settings.users.delete'"
              title="确定要删除这个用户吗？"
              @confirm="deleteUser(row.id)"
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
    
    <!-- 创建/编辑用户对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="isEdit ? '编辑用户' : '新建用户'"
      width="500px"
    >
      <el-form
        ref="formRef"
        :model="form"
        :rules="formRules"
        label-width="100px"
      >
        <el-form-item label="用户名" prop="username">
          <el-input v-model="form.username" placeholder="请输入用户名" :disabled="isEdit" />
        </el-form-item>
        <el-form-item label="姓名" prop="realName">
          <el-input v-model="form.realName" placeholder="请输入姓名" />
        </el-form-item>
        <el-form-item label="手机号" prop="phone">
          <el-input v-model="form.phone" placeholder="请输入手机号" />
        </el-form-item>
        <el-form-item v-if="!isEdit" label="密码" prop="password">
          <el-input v-model="form.password" type="password" placeholder="请输入密码" show-password />
        </el-form-item>
        <el-form-item label="角色" prop="roleId">
          <el-select v-model="form.roleId" placeholder="请选择角色">
            <el-option
              v-for="role in roles"
              :key="role.id"
              :label="role.name"
              :value="role.id"
            />
          </el-select>
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
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { request } from '@/api/request'
import dayjs from 'dayjs'
import axios from 'axios'

const loading = ref(false)
const submitting = ref(false)
const importing = ref(false)
const dialogVisible = ref(false)
const isEdit = ref(false)
const editId = ref<number | null>(null)
const formRef = ref<FormInstance>()

const users = ref<any[]>([])
const roles = ref<any[]>([])

const searchForm = reactive({
  keyword: '',
  roleId: undefined as number | undefined,
  isActive: undefined as boolean | undefined
})

const pagination = reactive({
  page: 1,
  pageSize: 10,
  total: 0
})

const form = reactive({
  username: '',
  realName: '',
  phone: '',
  password: '',
  roleId: undefined as number | undefined
})

const formRules: FormRules = {
  username: [
    { required: true, message: '请输入用户名', trigger: 'blur' },
    { min: 3, max: 50, message: '用户名长度在 3 到 50 个字符', trigger: 'blur' }
  ],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码长度不能少于6位', trigger: 'blur' }
  ],
  roleId: [
    { required: true, message: '请选择角色', trigger: 'change' }
  ]
}

const fetchUsers = async () => {
  loading.value = true
  try {
    const params: any = {
      page: pagination.page,
      pageSize: pagination.pageSize
    }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    if (searchForm.roleId) params.roleId = searchForm.roleId
    if (searchForm.isActive !== undefined) params.isActive = searchForm.isActive
    
    const res = await request.get('/users', { params })
    users.value = res.data.items
    pagination.total = res.data.totalCount
  } catch (error) {
    console.error('获取用户列表失败：', error)
  } finally {
    loading.value = false
  }
}

const fetchRoles = async () => {
  try {
    const res = await request.get('/roles')
    roles.value = res.data
  } catch (error) {
    console.error('获取角色列表失败：', error)
  }
}

const handleSearch = () => {
  pagination.page = 1
  fetchUsers()
}

const resetSearch = () => {
  searchForm.keyword = ''
  searchForm.roleId = undefined
  searchForm.isActive = undefined
  handleSearch()
}

const handleSizeChange = (val: number) => {
  pagination.pageSize = val
  fetchUsers()
}

const handleCurrentChange = (val: number) => {
  pagination.page = val
  fetchUsers()
}

const showCreateDialog = () => {
  isEdit.value = false
  editId.value = null
  resetForm()
  dialogVisible.value = true
}

const editUser = (row: any) => {
  isEdit.value = true
  editId.value = row.id
  Object.assign(form, {
    username: row.username,
    realName: row.realName || '',
    phone: row.phone,
    roleId: row.roleId
  })
  dialogVisible.value = true
}

const resetForm = () => {
  Object.assign(form, {
    username: '',
    realName: '',
    phone: '',
    password: '',
    roleId: undefined
  })
  formRef.value?.resetFields()
}

const getApiErrorMessage = (error: any, fallback: string) => {
  const responseData = error?.response?.data

  if (responseData?.errors && typeof responseData.errors === 'object') {
    const firstFieldErrors = Object.values(responseData.errors)
      .reduce((acc: string[], current: any) => {
        if (Array.isArray(current)) {
          current.forEach((item) => {
            if (typeof item === 'string') {
              acc.push(item)
            }
          })
        }
        return acc
      }, [])

    if (firstFieldErrors.length > 0) {
      return firstFieldErrors[0]
    }
  }

  if (responseData?.message) {
    return responseData.message
  }

  return fallback
}

const handleSubmit = async () => {
  if (!formRef.value) return
  
  await formRef.value.validate(async (valid) => {
    if (valid) {
      submitting.value = true
      try {
        if (isEdit.value && editId.value) {
          await request.put(`/users/${editId.value}`, form)
          ElMessage.success('更新成功')
        } else {
          await request.post('/users', form)
          ElMessage.success('创建成功')
        }
        dialogVisible.value = false
        fetchUsers()
      } catch (error) {
        ElMessage.error(getApiErrorMessage(error, '操作失败，请稍后重试'))
        console.error('操作失败：', error)
      } finally {
        submitting.value = false
      }
    }
  })
}

const toggleUserStatus = async (row: any) => {
  try {
    await request.put(`/users/${row.id}`, { isActive: !row.isActive })
    ElMessage.success(row.isActive ? '已禁用' : '已启用')
    fetchUsers()
  } catch (error) {
    console.error('操作失败：', error)
  }
}

const deleteUser = async (id: number) => {
  try {
    await request.delete(`/users/${id}`)
    ElMessage.success('删除成功')
    fetchUsers()
  } catch (error) {
    console.error('删除失败：', error)
  }
}

const downloadImportTemplate = async () => {
  try {
    const token = localStorage.getItem('token')
    if (!token) {
      ElMessage.error('登录已过期，请重新登录')
      return
    }

    const response = await axios.get('/api/users/import-template/download', {
      responseType: 'blob',
      headers: {
        Authorization: `Bearer ${token}`
      }
    })

    const blob = new Blob([response.data], { type: String(response.headers['content-type'] || 'application/octet-stream') })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = '用户导入模板.xlsx'
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  } catch (error) {
    console.error('下载模板失败：', error)
    ElMessage.error('下载模板失败，请稍后重试')
  }
}

const uploadImportExcel = async (options: any) => {
  const uploadFile = options?.file as File | undefined
  if (!uploadFile) {
    ElMessage.warning('请选择要上传的Excel文件')
    return
  }

  if (!uploadFile.name.toLowerCase().endsWith('.xlsx')) {
    ElMessage.warning('仅支持 .xlsx 格式文件')
    return
  }

  const formData = new FormData()
  formData.append('file', uploadFile)

  importing.value = true
  try {
    const res = await request.post('/users/import-template/upload', formData)
    const data = res.data || {}
    ElMessage.success(`导入完成：成功 ${data.importedCount ?? 0} 条，跳过 ${data.skippedCount ?? 0} 条`)
    await fetchUsers()
  } catch (error) {
    ElMessage.error(getApiErrorMessage(error, '导入失败，请稍后重试'))
    console.error('导入用户失败：', error)
  } finally {
    importing.value = false
  }
}

const getRoleType = (roleName: string) => {
  const types: Record<string, string> = {
    '管理员': 'danger',
    '项目经理': 'warning',
    '普通成员': 'info'
  }
  return types[roleName] || 'info'
}

const formatDateTime = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}

onMounted(() => {
  fetchUsers()
  fetchRoles()
})
</script>

<style scoped>
.users-page {
  --biz-card-bg: rgba(255, 255, 255, 0.94);
  --biz-card-border: #d7e4f8;
  --biz-text-strong: #0f3b8c;
  padding: 12px;
  height: 100%;
  min-height: 0;
  display: flex;
  background: #f5f8fc;
  overflow: hidden;
}

.users-page :deep(.el-card) {
  width: 100%;
  display: flex;
  flex-direction: column;
  border-radius: 16px;
  border: 1px solid var(--biz-card-border);
  background: var(--biz-card-bg);
  box-shadow: 0 18px 40px rgba(36, 66, 135, 0.12);
  overflow: hidden;
}

.users-page :deep(.el-card__header) {
  background: #f4f9ff;
  border-bottom: 1px solid #dce8fb;
}

.users-page :deep(.el-card__body) {
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
}

.header-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.search-bar {
  display: flex;
  gap: 10px;
  margin-bottom: 12px;
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid #dbe8ff;
  background: #f8fbff;
  align-items: center;
  flex-wrap: wrap;
}

.users-page :deep(.el-table) {
  border-radius: 12px;
  border: 1px solid #dce8fb;
  overflow: hidden;
}

.users-page :deep(.el-table th.el-table__cell) {
  background: #f3f8ff;
  color: #2e4566;
  font-weight: 700;
}

.users-page :deep(.el-table tr:hover > td.el-table__cell) {
  background: #f5f9ff;
}

.users-page :deep(.el-button) {
  border-radius: 999px;
}

.users-page :deep(.el-input__wrapper),
.users-page :deep(.el-select__wrapper),
.users-page :deep(.el-textarea__inner) {
  border-radius: 999px;
}

.users-page :deep(.el-textarea__inner) {
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
  .users-page {
    padding: 8px;
  }

  .users-page :deep(.el-card__body) {
    padding: 10px;
  }

  .card-header > span {
    font-size: 17px;
  }
}
</style>
