<template>
  <div class="project-detail-page">
    <el-card v-loading="loading">
      <template #header>
        <div class="card-header">
          <span>项目详情</span>
          <el-button link type="primary" @click="goBack">返回项目列表</el-button>
        </div>
      </template>

      <el-descriptions v-if="project" :column="2" border>
        <el-descriptions-item label="项目ID">{{ project.id }}</el-descriptions-item>
        <el-descriptions-item label="项目名称">{{ project.name }}</el-descriptions-item>
        <el-descriptions-item label="负责人">{{ project.managerName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="优先级">
          <el-tag :type="getPriorityType(project.priority)">{{ project.priorityName || getPriorityName(project.priority) }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="状态">
          <el-tag :type="getStatusType(project.status)">{{ project.statusName }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="开始日期">{{ formatDate(project.startDate) }}</el-descriptions-item>
        <el-descriptions-item label="结束日期">{{ formatDate(project.endDate) }}</el-descriptions-item>
        <el-descriptions-item label="整体进度">
          <el-progress :percentage="Number(project.progress || 0)" :stroke-width="10" />
        </el-descriptions-item>
        <el-descriptions-item label="任务数">{{ project.taskCount }}</el-descriptions-item>
        <el-descriptions-item label="描述" :span="2">{{ project.description || '-' }}</el-descriptions-item>
      </el-descriptions>

      <el-divider content-position="left">项目成员</el-divider>
      <el-table v-if="sortedMembers.length" :data="sortedMembers" style="width: 100%">
        <el-table-column prop="username" label="用户名" width="180" />
        <el-table-column prop="phone" label="电话" width="160">
          <template #default="{ row }">{{ row.phone || '-' }}</template>
        </el-table-column>
        <el-table-column prop="role" label="项目角色" width="120" />
        <el-table-column prop="joinedAt" label="加入时间" width="180">
          <template #default="{ row }">{{ formatDateTime(row.joinedAt) }}</template>
        </el-table-column>
      </el-table>
      <el-empty v-else description="暂无项目成员" />
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { request } from '@/api/request'
import dayjs from 'dayjs'

const route = useRoute()
const router = useRouter()

const projectId = computed(() => Number(route.params.id))
const loading = ref(false)
const project = ref<any>(null)
const members = ref<any[]>([])

const sortedMembers = computed(() => {
  const list = members.value || []
  const managerId = project.value?.managerId

  if (!managerId || !list.length) {
    return list
  }

  return [...list].sort((left, right) => {
    const leftIsManager = left.userId === managerId
    const rightIsManager = right.userId === managerId

    if (leftIsManager && !rightIsManager) return -1
    if (!leftIsManager && rightIsManager) return 1
    return 0
  })
})

const fetchProjectDetail = async () => {
  loading.value = true
  try {
    const res = await request.get(`/projects/${projectId.value}`)
    project.value = res.data
  } catch (error) {
    console.error('获取项目详情失败：', error)
  } finally {
    loading.value = false
  }
}

const fetchMembers = async () => {
  try {
    const res = await request.get(`/projects/${projectId.value}/members`)
    members.value = res.data || []
  } catch (error) {
    console.error('获取项目成员失败：', error)
  }
}

const getStatusType = (status: number) => {
  const types: Record<number, string> = {
    0: 'info',
    1: 'primary',
    2: 'success',
    3: 'warning'
  }
  return types[status] || 'info'
}

const getPriorityType = (priority: number) => {
  const types: Record<number, string> = {
    1: 'info',
    2: 'warning',
    3: 'danger',
    4: 'danger'
  }
  return types[priority] || 'info'
}

const getPriorityName = (priority: number) => {
  const names: Record<number, string> = {
    1: '低',
    2: '中',
    3: '高',
    4: '紧急'
  }
  return names[priority] || '未知'
}

const formatDate = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD')
}

const formatDateTime = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}

const goBack = () => {
  router.push('/projects')
}

onMounted(async () => {
  await Promise.all([fetchProjectDetail(), fetchMembers()])
})
</script>

<style scoped>
.project-detail-page {
  padding: 10px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
