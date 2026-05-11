<template>
  <div class="dashboard-card milestone-timeline-card">
    <div class="card-header">
      <h3 class="card-title">
        <el-icon><Flag /></el-icon>
        近期里程碑
        <span v-if="items.length > 0" class="item-count">{{ items.length }}</span>
      </h3>
    </div>
    <div class="card-body">
      <div v-if="items.length > 0" class="timeline-wrapper">
        <el-timeline>
          <el-timeline-item
            v-for="item in items"
            :key="item.id"
            :timestamp="formatDueDate(item)"
            :type="getTimelineType(item)"
            :hollow="item.status === 2"
            placement="top"
          >
            <div class="milestone-content" @click="router.push(`/projects/${item.projectId}`)">
              <div class="milestone-name">{{ item.milestoneName }}</div>
              <div class="milestone-project">{{ item.projectName }}</div>
              <el-tag :type="getTagType(item)" size="small" class="milestone-tag">
                {{ item.statusName }}
              </el-tag>
            </div>
          </el-timeline-item>
        </el-timeline>
      </div>
      <el-empty v-else description="暂无近期里程碑" :image-size="80" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import dayjs from 'dayjs'

defineProps<{
  items: Array<{
    id: number
    projectId: number
    projectName: string
    milestoneName: string
    dueDate: string | null
    status: number
    statusName: string
  }>
}>()

const router = useRouter()

const isOverdue = (item: { dueDate: string | null; status: number }) => {
  if (!item.dueDate || item.status === 2) return false
  return dayjs(item.dueDate).isBefore(dayjs(), 'day')
}

const formatDueDate = (item: { dueDate: string | null }) => {
  if (!item.dueDate) return '无截止日期'
  return dayjs(item.dueDate).format('MM-DD')
}

const getTimelineType = (item: { dueDate: string | null; status: number }) => {
  if (item.status === 2) return 'success'
  if (isOverdue(item)) return 'danger'
  if (item.status === 1) return 'primary'
  return 'info'
}

const getTagType = (item: { dueDate: string | null; status: number; statusName: string }) => {
  if (item.status === 2) return 'success'
  if (isOverdue(item)) return 'danger'
  if (item.status === 1) return ''
  return 'info'
}
</script>

<style scoped>
.dashboard-card {
  background: #ffffff;
  border-radius: 14px;
  border: 1px solid #dce8fb;
  display: flex;
  flex-direction: column;
  box-shadow: 0 10px 20px rgba(31, 59, 115, 0.08);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 16px;
  border-bottom: 1px solid #e4ecfa;
}

.card-title {
  font-size: 14px;
  font-weight: 700;
  color: #1d3250;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.card-title .el-icon {
  color: #f59e0b;
}

.item-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 20px;
  height: 20px;
  padding: 0 6px;
  background: #fffbeb;
  color: #f59e0b;
  font-size: 12px;
  font-weight: 500;
  border-radius: 10px;
  margin-left: 4px;
}

.card-body {
  flex: 1;
  padding: 12px 16px;
  min-height: 286px;
  max-height: 340px;
  overflow-y: auto;
}

.timeline-wrapper {
  padding-top: 4px;
}

.milestone-content {
  cursor: pointer;
  padding: 2px 0;
}

.milestone-name {
  font-size: 14px;
  color: #1d3250;
  font-weight: 600;
  margin-bottom: 4px;
}

.milestone-project {
  font-size: 12px;
  color: #71839b;
  margin-bottom: 6px;
}

.milestone-tag {
  font-size: 11px;
}

:deep(.el-timeline-item__timestamp) {
  font-size: 11px;
}

:deep(.el-timeline) {
  padding-left: 2px;
}
</style>
