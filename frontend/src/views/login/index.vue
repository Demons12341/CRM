<template>
  <div class="login-container">
    <div class="login-box">
      <div class="login-header">
        <div class="brand-mark">
          <el-icon>
            <Folder />
          </el-icon>
        </div>
        <div class="brand-text">
          <h2>项目管理</h2>
          <p>统一任务、文件与进度协作平台</p>
        </div>
      </div>

      <el-form ref="loginFormRef" :model="loginForm" :rules="loginRules" class="login-form" @keyup.enter="handleLogin">
        <div class="form-title">账号登录</div>

        <el-form-item prop="username">
          <el-input v-model="loginForm.username" placeholder="请输入用户名" prefix-icon="User" size="large" />
        </el-form-item>

        <el-form-item prop="password">
          <el-input v-model="loginForm.password" type="password" placeholder="请输入密码" prefix-icon="Lock" size="large"
            show-password />
        </el-form-item>

        <el-form-item>
          <el-button type="primary" size="large" :loading="loading" class="login-button" @click="handleLogin">
            登录
          </el-button>
        </el-form-item>
      </el-form>

      <div v-if="showDefaultAccount" class="login-footer">
        <p>默认账号：admin / TKgy@123</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { request } from '@/api/request'

const router = useRouter()
const loginFormRef = ref<FormInstance>()
const loading = ref(false)
const showDefaultAccount = import.meta.env.DEV

const loginForm = reactive({
  username: '',
  password: ''
})

const loginRules: FormRules = {
  username: [
    { required: true, message: '请输入用户名', trigger: 'blur' }
  ],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码长度不能少于6位', trigger: 'blur' }
  ]
}

const handleLogin = async () => {
  if (!loginFormRef.value) return

  await loginFormRef.value.validate(async (valid) => {
    if (valid) {
      loading.value = true
      try {
        const res = await request.post('/auth/login', loginForm)
        localStorage.setItem('token', res.data.token)
        localStorage.setItem('user', JSON.stringify(res.data.user))
        ElMessage.success('登录成功')
        router.push('/')
      } catch (error) {
        console.error('登录失败：', error)
      } finally {
        loading.value = false
      }
    }
  })
}
</script>

<style scoped>
.login-container {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 100%;
  padding: 24px;
  background: #f0f2f5;
}

.login-box {
  width: 420px;
  padding: 28px 30px 24px;
  background: #fff;
  border-radius: 14px;
  border: 1px solid #e5e7eb;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.08);
}

.login-header {
  display: flex;
  align-items: center;
  gap: 14px;
  margin-bottom: 24px;
  padding-bottom: 18px;
  border-bottom: 1px solid #eef2f7;
}

.brand-mark {
  width: 44px;
  height: 44px;
  border-radius: 12px;
  background: #eff6ff;
  color: #2563eb;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 22px;
  flex-shrink: 0;
}

.brand-text h2 {
  margin: 0;
  font-size: 22px;
  color: #1f2937;
  line-height: 1.2;
}

.brand-text p {
  margin: 6px 0 0;
  font-size: 14px;
  color: #6b7280;
}

.login-form {
  margin-bottom: 12px;
}

.form-title {
  font-size: 15px;
  color: #334155;
  font-weight: 600;
  margin-bottom: 14px;
}

.login-button {
  width: 100%;
  height: 42px;
  font-weight: 600;
}

.login-form :deep(.el-form-item) {
  margin-bottom: 18px;
}

.login-form :deep(.el-input__wrapper) {
  border-radius: 10px;
  box-shadow: 0 0 0 1px #d1d5db inset;
}

.login-form :deep(.el-input__wrapper.is-focus) {
  box-shadow: 0 0 0 1px #2563eb inset;
}

.login-footer {
  text-align: center;
  font-size: 12px;
  color: #94a3b8;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 8px 10px;
}

.login-footer p {
  margin: 0;
}

@media (max-width: 520px) {
  .login-container {
    padding: 12px;
  }

  .login-box {
    width: 100%;
    padding: 20px 16px 16px;
  }
}
</style>
