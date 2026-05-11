<template>
  <div class="dashboard-card my-projects-card">
    <div class="card-header">
      <h3 class="card-title">
        <el-icon><Folder /></el-icon>
        我的项目
        <span v-if="items.length > 0" class="item-count">{{ items.length }}</span>
      </h3>
    </div>
    <div class="card-body">
      <div v-if="items.length > 0" class="project-list">
        <div
          v-for="project in items"
          :key="project.id"
          class="project-item"
          @click="$emit('select', project)"
        >
          <div class="project-info">
            <div class="project-name">{{ project.name }}</div>
            <div class="project-meta">
              <span v-if="project.businessLine" class="bl-tag">{{ project.businessLine }}</span>
              <span class="manager">{{ project.managerName }}</span>
            </div>
          </div>
          <div class="project-right">
            <el-tag :type="getStatusTagType(project.status)" size="small">{{ project.statusName }}</el-tag>
            <div class="progress-wrap">
              <el-progress :percentage="project.progress" :stroke-width="5" :show-text="false" style="width: 50px;" />
              <span class="progress-text">{{ project.progress }}%</span>
            </div>
          </div>
        </div>
      </div>
      <el-empty v-else description="暂无参与的项目" :image-size="80" />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  items: Array<{
    id: number
    name: string
    businessLine: string | null
    status: number
    statusName: string
    progress: number
    managerName: string
    isManager: boolean
  }>
}>()

defineEmits<{
  select: [project: any]
}>()

const getStatusTagType = (status: number) => {
  if (status >= 8) return 'success'
  if (status >= 5) return 'warning'
  if (status >= 3) return ''
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
  color: #3b82f6;
}

.item-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 20px;
  height: 20px;
  padding: 0 6px;
  background: #eff6ff;
  color: #3b82f6;
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
}

.project-meta {
  display: flex;
  gap: 8px;
  font-size: 12px;
  color: #71839b;
  align-items: center;
}

.bl-tag {
  color: #3b82f6;
  font-weight: 500;
}

.project-right {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 4px;
  flex-shrink: 0;
}

.progress-wrap {
  display: flex;
  align-items: center;
  gap: 6px;
}

.progress-text {
  font-size: 12px;
  color: #60718a;
  font-weight: 500;
}
</style>
