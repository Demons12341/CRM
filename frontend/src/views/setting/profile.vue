<template>
  <div class="profile-page">
    <div class="profile-hero">
      <div class="profile-hero-main">
        <div class="profile-avatar">{{ (profileForm.realName || profileForm.username || 'U').slice(0, 1).toUpperCase() }}</div>
        <div class="profile-hero-text">
          <div class="profile-hero-title">个人设置中心</div>
          <div class="profile-hero-subtitle">维护账号资料、联系方式与登录密码，提升账号安全性</div>
        </div>
      </div>
      <div class="profile-meta-chips">
        <span class="meta-chip">账号：{{ profileForm.username || '-' }}</span>
        <span class="meta-chip">角色：{{ profileForm.roleName || '-' }}</span>
        <span class="meta-chip">手机：{{ profileForm.phone || '-' }}</span>
      </div>
    </div>

    <el-row :gutter="14" class="profile-grid">
      <el-col :xs="24" :md="15">
        <el-card class="profile-card">
          <template #header>
            <div class="section-title">账户信息</div>
          </template>

          <el-form ref="profileFormRef" :model="profileForm" :rules="profileRules" label-width="96px">
            <el-form-item label="登录账号">
              <el-input v-model="profileForm.username" disabled />
            </el-form-item>
            <el-form-item label="手机号" prop="phone">
              <el-input v-model="profileForm.phone" placeholder="请输入手机号" />
            </el-form-item>
            <el-form-item label="当前角色">
              <el-input v-model="profileForm.roleName" disabled />
            </el-form-item>
            <el-form-item>
              <div class="form-actions">
                <el-button type="primary" :loading="savingProfile" @click="saveProfile">保存信息</el-button>
                <el-button @click="loadCurrentUser">重置</el-button>
              </div>
            </el-form-item>
          </el-form>
        </el-card>
      </el-col>

      <el-col :xs="24" :md="9">
        <el-card class="password-card">
          <template #header>
            <div class="section-title">密码与安全</div>
          </template>

          <el-form ref="passwordFormRef" :model="passwordForm" :rules="passwordRules" label-width="96px">
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
              <div class="form-actions">
                <el-button type="primary" :loading="savingPassword" @click="changePassword">修改密码</el-button>
                <el-button @click="resetPasswordForm">重置</el-button>
              </div>
            </el-form-item>
          </el-form>

          <div class="security-tips">
            <div class="tips-title">安全建议</div>
            <div class="tips-item">使用至少 8 位密码并包含数字与字母</div>
            <div class="tips-item">避免与其他系统使用相同密码</div>
            <div class="tips-item">定期更新密码，发现异常立即修改</div>
          </div>
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
  realName: '',
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
      realName: user.realName || '',
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
  --biz-card-bg: rgba(255, 255, 255, 0.94);
  --biz-card-border: #d7e4f8;
  --biz-text-strong: #0f3b8c;
  --biz-text-muted: #5f6b7a;
  padding: 12px;
  min-height: 100%;
  box-sizing: border-box;
  background: #f5f8fc;
}

.profile-hero {
  padding: 14px 16px;
  border-radius: 16px;
  border: 1px solid #dce8fb;
  background: #f4f9ff;
  box-shadow: 0 10px 22px rgba(36, 66, 135, 0.08);
  margin-bottom: 14px;
}

.profile-hero-main {
  display: flex;
  align-items: center;
  gap: 12px;
}

.profile-avatar {
  width: 48px;
  height: 48px;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #3e7eff;
  color: #fff;
  font-size: 22px;
  font-weight: 700;
  flex-shrink: 0;
}

.profile-hero-text {
  min-width: 0;
}

.profile-hero-title {
  font-size: 22px;
  line-height: 1.1;
  color: var(--biz-text-strong);
  font-weight: 800;
  letter-spacing: 0.3px;
}

.profile-hero-subtitle {
  margin-top: 8px;
  font-size: 13px;
  color: var(--biz-text-muted);
}

.profile-meta-chips {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-top: 12px;
}

.meta-chip {
  height: 28px;
  display: inline-flex;
  align-items: center;
  border-radius: 999px;
  border: 1px solid #cddfff;
  background: #eaf1ff;
  color: #395171;
  font-size: 12px;
  padding: 0 12px;
}

.profile-grid {
  margin-left: 0 !important;
  margin-right: 0 !important;
}

.section-title {
  font-size: 17px;
  color: #1d3250;
  font-weight: 700;
}

.form-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.profile-page :deep(.el-card) {
  border-radius: 16px;
  border: 1px solid var(--biz-card-border);
  background: var(--biz-card-bg);
  box-shadow: 0 18px 40px rgba(36, 66, 135, 0.12);
  overflow: hidden;
}

.profile-page :deep(.el-card__header) {
  background: #f4f9ff;
  border-bottom: 1px solid #dce8fb;
}

.profile-page :deep(.el-card__body) {
  padding: 16px;
}

.profile-page :deep(.el-form-item) {
  margin-bottom: 16px;
}

.profile-page :deep(.el-input__wrapper) {
  border-radius: 999px;
}

.profile-page :deep(.el-button) {
  border-radius: 999px;
}

.password-card :deep(.el-form-item__label) {
  color: #324a69;
}

.profile-card :deep(.el-form-item__label) {
  color: #324a69;
}

.security-tips {
  margin-top: 8px;
  padding: 12px;
  border: 1px solid #dce8fb;
  border-radius: 12px;
  background: #f8fbff;
}

.tips-title {
  font-size: 13px;
  color: #1d3250;
  font-weight: 700;
  margin-bottom: 6px;
}

.tips-item {
  font-size: 12px;
  color: #5f6b7a;
  line-height: 1.6;
}

@media (max-width: 900px) {
  .profile-page {
    padding: 8px;
  }

  .profile-hero-title {
    font-size: 18px;
  }

  .profile-avatar {
    width: 42px;
    height: 42px;
    font-size: 18px;
  }

  .profile-page :deep(.el-card__body) {
    padding: 12px;
  }
}
</style>
