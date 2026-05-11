<template>
  <div class="dashboard-card upcoming-overdue-card">
    <div class="card-header">
      <h3 class="card-title">
        <el-icon><AlarmClock /></el-icon>
        即将超期告警
        <span v-if="items.length > 0" class="item-count">{{ items.length }}</span>
      </h3>
    </div>
    <div class="card-body">
      <div v-if="items.length > 0" class="overdue-list">
        <div
          v-for="item in items"
          :key="`${item.itemType}-${item.id}`"
          class="overdue-item"
          :class="{ 'is-overdue': item.isOverdue }"
          @click="handleClick(item)"
        >
          <div class="overdue-info">
            <div class="overdue-title">{{ item.title }}</div>
            <div class="overdue-meta">
              <span class="overdue-project">{{ item.projectName }}</span>
              <span class="overdue-assignee" v-if="item.assigneeName">{{ item.assigneeName }}</span>
            </div>
          </div>
          <div class="overdue-right">
            <el-tag :type="item.itemType === '项目' ? '' : 'info'" size="small">{{ item.itemType }}</el-tag>
            <div class="overdue-days" :class="{ 'days-overdue': item.isOverdue, 'days-warning': !item.isOverdue && item.daysLeft <= 1 }">
              {{ item.isOverdue ? `已超期${item.daysLeft}天` : `剩余${item.daysLeft}天` }}
            </div>
          </div>
        </div>
      </div>
      <el-empty v-else description="暂无即将超期的项目或任务" :image-size="80" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'

defineProps<{
  items: Array<{
    id: number
    title: string
    projectName: string
    projectId: number
    itemType: string
    dueDate: string | null
    daysLeft: number
    isOverdue: boolean
    assigneeName: string
  }>
}>()

const router = useRouter()

const handleClick = (item: any) => {
  if (item.itemType === '任务') {
    router.push(`/tasks/${item.id}`)
  } else {
    router.push(`/projects/${item.projectId}`)
  }
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
  background: #fef3c7;
  color: #d97706;
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

.overdue-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.overdue-item {
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

.overdue-item :deep(.el-tag) {
  flex-shrink: 0;
}

.overdue-item:hover {
  background: #f3f8ff;
  border-color: #dce8fb;
}

.overdue-item.is-overdue {
  background: #fef2f2;
  border-color: #fecaca;
}

.overdue-item.is-overdue:hover {
  background: #fee2e2;
  border-color: #fca5a5;
}

.overdue-info {
  flex: 1;
  width: 0;
  min-width: 0;
  overflow: hidden;
}

.overdue-title {
  font-size: 14px;
  color: #1d3250;
  font-weight: 600;
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.overdue-meta {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: #71839b;
}

.overdue-project {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 120px;
}

.overdue-right {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 4px;
  flex-shrink: 0;
}

.overdue-days {
  font-size: 12px;
  color: #71839b;
  white-space: nowrap;
}

.overdue-days.days-overdue {
  color: #ef4444;
  font-weight: 600;
}

.overdue-days.days-warning {
  color: #f59e0b;
  font-weight: 600;
}
</style>
