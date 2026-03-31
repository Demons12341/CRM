<template>
  <div class="users-page">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>用户管理</span>
          <el-button type="primary" @click="showCreateDialog">
            <el-icon><Plus /></el-icon>
            新建用户
          </el-button>
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
        <el-table-column label="操作" width="200" fixed="right">
          <template #default="{ row }">
            <el-button type="primary" link @click="editUser(row)">
              编辑
            </el-button>
            <el-button
              type="primary"
              link
              @click="toggleUserStatus(row)"
            >
              {{ row.isActive ? '禁用' : '启用' }}
            </el-button>
            <el-popconfirm
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

const loading = ref(false)
const submitting = ref(false)
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
      .flat()
      .filter((item: any) => typeof item === 'string') as string[]

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
  padding: 10px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.search-bar {
  display: flex;
  gap: 10px;
  margin-bottom: 20px;
}

.pagination {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
}
</style>
