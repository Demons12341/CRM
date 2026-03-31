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
  padding: 10px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.footer-actions {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>
