import { ref } from 'vue'
import { request } from '@/api/request'

const unreadCount = ref(0)
let loadingPromise: Promise<void> | null = null

const fetchUnreadCount = async () => {
  if (loadingPromise) {
    await loadingPromise
    return
  }

  loadingPromise = (async () => {
    try {
      const res = await request.get('/alerts/unread-count')
      unreadCount.value = res.data
    } catch (error) {
      console.error('获取未读告警数量失败：', error)
    } finally {
      loadingPromise = null
    }
  })()

  await loadingPromise
}

const clearUnreadCount = () => {
  unreadCount.value = 0
}

export const useAlertUnread = () => {
  return {
    unreadCount,
    fetchUnreadCount,
    clearUnreadCount
  }
}
