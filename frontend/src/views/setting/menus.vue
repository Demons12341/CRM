<template>
  <div class="menus-page">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>菜单权限</span>
        </div>
      </template>

      <el-form inline>
        <el-form-item label="角色">
          <el-select v-model="selectedRoleId" placeholder="请选择角色" style="width: 220px" @change="loadRolePermissions">
            <el-option v-for="role in roles" :key="role.id" :label="role.name" :value="role.id" />
          </el-select>
        </el-form-item>
      </el-form>

      <el-alert
        v-if="currentRole?.name === '管理员'"
        type="info"
        :closable="false"
        title="管理员默认拥有全部菜单权限，无需单独分配"
        style="margin-bottom: 16px"
      />

      <el-tree
        v-loading="loading"
        ref="treeRef"
        :data="menuOptions"
        node-key="code"
        show-checkbox
        :default-expand-all="true"
        :props="{ label: 'name', children: 'children' }"
      />

      <div class="footer-actions">
        <el-button type="primary" :disabled="!selectedRoleId || currentRole?.name === '管理员'" :loading="saving" @click="savePermissions">
          保存权限
        </el-button>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { ElTree } from 'element-plus'
import { request } from '@/api/request'

const loading = ref(false)
const saving = ref(false)
const roles = ref<any[]>([])
const menuOptions = ref<any[]>([])
const selectedRoleId = ref<number | undefined>()
const treeRef = ref<InstanceType<typeof ElTree>>()

const currentRole = computed(() => roles.value.find((item: any) => item.id === selectedRoleId.value))

const fetchRoles = async () => {
  const res = await request.get('/roles')
  roles.value = res.data || []
}

const fetchMenuOptions = async () => {
  const res = await request.get('/roles/menu-options')
  menuOptions.value = res.data || []
}

const loadRolePermissions = async () => {
  if (!selectedRoleId.value) return

  loading.value = true
  try {
    const res = await request.get(`/roles/${selectedRoleId.value}/menu-permissions`)
    const permissions = res.data || []
    treeRef.value?.setCheckedKeys(permissions)
  } finally {
    loading.value = false
  }
}

const savePermissions = async () => {
  if (!selectedRoleId.value) {
    ElMessage.warning('请先选择角色')
    return
  }

  saving.value = true
  try {
    const checkedKeys = treeRef.value?.getCheckedKeys(false) || []
    await request.put(`/roles/${selectedRoleId.value}/menu-permissions`, {
      menuCodes: checkedKeys
    })
    ElMessage.success('菜单权限已保存')
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  await Promise.all([fetchRoles(), fetchMenuOptions()])
  if (roles.value.length > 0) {
    selectedRoleId.value = roles.value[0].id
    await loadRolePermissions()
  }
})
</script>

<style scoped>
.menus-page {
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

.menus-page :deep(.el-card) {
  border-radius: 16px;
  border: 1px solid var(--biz-card-border);
  background: var(--biz-card-bg);
  box-shadow: 0 18px 40px rgba(36, 66, 135, 0.12);
  overflow: hidden;
}

.menus-page :deep(.el-card__header) {
  background: #f4f9ff;
  border-bottom: 1px solid #dce8fb;
}

.menus-page :deep(.el-card__body) {
  padding: 14px;
}

.menus-page :deep(.el-form--inline) {
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid #dbe8ff;
  background: #f8fbff;
  margin-bottom: 12px;
}

.menus-page :deep(.el-tree) {
  border: 1px solid #dce8fb;
  border-radius: 12px;
  padding: 12px;
  background: #fbfdff;
}

.menus-page :deep(.el-tree-node__content) {
  border-radius: 8px;
}

.menus-page :deep(.el-tree-node__content:hover) {
  background: #edf5ff;
}

.menus-page :deep(.el-button) {
  border-radius: 999px;
}

.menus-page :deep(.el-input__wrapper),
.menus-page :deep(.el-select__wrapper) {
  border-radius: 999px;
}

.footer-actions {
  margin-top: 12px;
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
  border-top: 1px solid #e4ecfa;
}

@media (max-width: 900px) {
  .menus-page {
    padding: 8px;
  }

  .card-header > span {
    font-size: 17px;
  }
}
</style>
