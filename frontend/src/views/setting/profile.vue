<template>
  <div class="profile-page">
    <el-row :gutter="20">
      <el-col :span="12">
        <el-card>
          <template #header>
            <span>个人信息</span>
          </template>

          <el-form ref="profileFormRef" :model="profileForm" :rules="profileRules" label-width="90px">
            <el-form-item label="用户名">
              <el-input v-model="profileForm.username" disabled />
            </el-form-item>
            <el-form-item label="手机号" prop="phone">
              <el-input v-model="profileForm.phone" placeholder="请输入手机号" />
            </el-form-item>
            <el-form-item label="角色">
              <el-input v-model="profileForm.roleName" disabled />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="savingProfile" @click="saveProfile">保存信息</el-button>
              <el-button @click="loadCurrentUser">重置</el-button>
            </el-form-item>
          </el-form>
        </el-card>
      </el-col>

      <el-col :span="12">
        <el-card>
          <template #header>
            <span>修改密码</span>
          </template>

          <el-form ref="passwordFormRef" :model="passwordForm" :rules="passwordRules" label-width="100px">
            <el-form-item label="原密码" prop="oldPassword">
              <el-input v-model="passwordForm.oldPassword" type="password" show-password placeholder="请输入原密码" />
            </el-form-item>
            <el-form-item label="新密码" prop="newPassword">
              <el-input v-model="passwordForm.newPassword" type="password" show-password placeholder="请输入新密码" />
            </el-form-item>
            <el-form-item label="确认新密码" prop="confirmPassword">
              <el-input v-model="passwordForm.confirmPassword" type="password" show-password placeholder="请再次输入新密码" />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="savingPassword" @click="changePassword">修改密码</el-button>
              <el-button @click="resetPasswordForm">重置</el-button>
            </el-form-item>
          </el-form>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { request } from '@/api/request'

const profileFormRef = ref<FormInstance>()
const passwordFormRef = ref<FormInstance>()

const savingProfile = ref(false)
const savingPassword = ref(false)
const currentUserId = ref<number | null>(null)

const profileForm = reactive({
  username: '',
  phone: '',
  roleName: ''
})

const passwordForm = reactive({
  oldPassword: '',
  newPassword: '',
  confirmPassword: ''
})

const profileRules: FormRules = {}

const passwordRules: FormRules = {
  oldPassword: [{ required: true, message: '请输入原密码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码长度不能少于 6 位', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请确认新密码', trigger: 'blur' },
    {
      validator: (_rule, value, callback) => {
        if (!value) {
          callback(new Error('请确认新密码'))
          return
        }
        if (value !== passwordForm.newPassword) {
          callback(new Error('两次输入的新密码不一致'))
          return
        }
        callback()
      },
      trigger: 'blur'
    }
  ]
}

const loadCurrentUser = async () => {
  try {
    const res = await request.get('/auth/current')
    const user = res.data
    currentUserId.value = user.id
    Object.assign(profileForm, {
      username: user.username || '',
      phone: user.phone || '',
      roleName: user.roleName || ''
    })

    localStorage.setItem('user', JSON.stringify(user))
  } catch (error) {
    console.error('获取当前用户信息失败：', error)
  }
}

const saveProfile = async () => {
  if (!profileFormRef.value || !currentUserId.value) return

  await profileFormRef.value.validate(async (valid) => {
    if (!valid) return

    savingProfile.value = true
    try {
      await request.put(`/users/${currentUserId.value}`, {
        phone: profileForm.phone
      })
      ElMessage.success('个人信息保存成功')
      await loadCurrentUser()
    } catch (error) {
      console.error('保存个人信息失败：', error)
    } finally {
      savingProfile.value = false
    }
  })
}

const resetPasswordForm = () => {
  Object.assign(passwordForm, {
    oldPassword: '',
    newPassword: '',
    confirmPassword: ''
  })
  passwordFormRef.value?.resetFields()
}

const changePassword = async () => {
  if (!passwordFormRef.value) return

  await passwordFormRef.value.validate(async (valid) => {
    if (!valid) return

    savingPassword.value = true
    try {
      await request.post('/auth/change-password', passwordForm)
      ElMessage.success('密码修改成功，请重新登录')
      localStorage.removeItem('token')
      localStorage.removeItem('user')
      window.location.href = '/login'
    } catch (error) {
      console.error('修改密码失败：', error)
    } finally {
      savingPassword.value = false
    }
  })
}

onMounted(() => {
  loadCurrentUser()
})
</script>

<style scoped>
.profile-page {
  padding: 10px;
}
</style>
