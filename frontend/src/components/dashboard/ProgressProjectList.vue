<template>
  <div class="dashboard-card progress-project-card">
    <div class="card-header">
      <h3 class="card-title">
        <el-icon><TrendCharts /></el-icon>
        项目重大进展
        <span v-if="items.length > 0" class="item-count">{{ items.length }}</span>
      </h3>
    </div>
    <div class="card-body">
      <div v-if="items.length > 0" class="project-list">
        <div
          v-for="item in items"
          :key="item.id"
          class="project-item"
          @click="$emit('select', item)"
        >
          <div class="project-info">
            <div class="project-name">{{ item.name }}</div>
            <div class="project-meta">
              <span class="manager">{{ item.managerName }}</span>
              <span class="change-time">{{ formatRelativeTime(item.statusChangedAt) }}</span>
            </div>
          </div>
          <el-tag :type="getStatusTagType(item.status)" size="small">
            {{ item.statusName }}
          </el-tag>
        </div>
      </div>
      <el-empty v-else description="暂无近期项目进展" :image-size="80" />
    </div>
  </div>
</template>

<script setup lang="ts">
import dayjs from 'dayjs'

defineEmits<{
  select: [item: any]
}>()

defineProps<{
  items: Array<{
    id: number
    name: string
    businessLine?: string
    status: number
    statusName: string
    statusChangedAt: string | null
    managerName: string
  }>
}>()

const getStatusTagType = (status: number) => {
  if (status >= 8) return 'success'
  if (status >= 5) return 'warning'
  if (status >= 3) return ''
  return 'info'
}

const formatRelativeTime = (date: string | null) => {
  if (!date) return '-'
  const now = dayjs()
  const target = dayjs(date)
  const diffMinutes = now.diff(target, 'minute')
  const diffHours = now.diff(target, 'hour')
  const diffDays = now.diff(target, 'day')

  if (diffMinutes < 1) return '刚刚'
  if (diffMinutes < 60) return `${diffMinutes}分钟前`
  if (diffHours < 24) return `${diffHours}小时前`
  if (diffDays < 7) return `${diffDays}天前`
  return dayjs(date).format('MM-DD')
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
  color: #22c55e;
}

.item-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 20px;
  height: 20px;
  padding: 0 6px;
  background: #f0fdf4;
  color: #22c55e;
  font-size: 12px;
  font-weight: 500;
  border-radius: 10px;
  margin-left: 4px;
}

.card-body {
  flex: 1;
  padding: 12px;
  min-height: 286px;
  max-height: 340px;
  overflow-y: auto;
}

.project-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.project-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px;
  border-radius: 10px;
  cursor: pointer;
  transition: background 0.15s ease, border-color 0.15s ease;
  border: 1px solid transparent;
  gap: 12px;
  overflow: hidden;
}

.project-item :deep(.el-tag) {
  flex-shrink: 0;
}

.project-item:hover {
  background: #f3f8ff;
  border-color: #dce8fb;
}

.project-info {
  flex: 1;
  width: 0;
  min-width: 0;
  overflow: hidden;
}

.project-name {
  font-size: 14px;
  color: #1d3250;
  font-weight: 600;
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  display: block;
  max-width: 100%;
}

.project-meta {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: #71839b;
}

.change-time {
  color: #94a3b8;
}
</style>
