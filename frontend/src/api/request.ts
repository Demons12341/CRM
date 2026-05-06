import axios from 'axios'
import type { AxiosInstance, AxiosRequestConfig, AxiosResponse } from 'axios'
import { ElMessage } from 'element-plus'
import router from '@/router'

const service: AxiosInstance = axios.create({
  baseURL: '/api',
  timeout: 15000
})

// 请求拦截器
service.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => {
    console.error('请求错误：', error)
    return Promise.reject(error)
  }
)

// 响应拦截器
service.interceptors.response.use(
  (response: AxiosResponse) => {
    const res = response.data
    
    if (!res.success) {
      ElMessage.error(res.message || '请求失败')
      return Promise.reject(new Error(res.message || '请求失败'))
    }
    
    return res
  },
  (error) => {
    console.error('响应错误：', error)
    const headers = error.config?.headers || {}
    const headerValue = typeof headers.get === 'function'
      ? (headers.get('X-Silent-Error') || headers.get('x-silent-error'))
      : (headers['X-Silent-Error'] || headers['x-silent-error'] || headers?.common?.['X-Silent-Error'] || headers?.common?.['x-silent-error'])
    const silentError = headerValue === '1'
    
    if (error.response) {
      const { status, data } = error.response
      const requestUrl = error.config?.url || ''
      const serverMessage = data?.message || '请求失败，请稍后重试'
      
      switch (status) {
        case 401:
          if (requestUrl.includes('/auth/login')) {
            if (!silentError) ElMessage.error(serverMessage || '用户名或密码错误')
          } else {
            if (!silentError) ElMessage.error('登录已过期，请重新登录')
            localStorage.removeItem('token')
            localStorage.removeItem('user')
            if (router.currentRoute.value.path !== '/login') {
              router.replace('/login')
            }
          }
          break
        case 403:
          if (!silentError) ElMessage.error(serverMessage || '没有权限访问')
          break
        case 404:
          if (!silentError) ElMessage.error(serverMessage || '请求的资源不存在')
          break
        case 500:
          if (!silentError) ElMessage.error(serverMessage || '服务器错误')
          break
        default:
          if (!silentError) ElMessage.error(serverMessage)
      }
    } else {
      if (error.code === 'ECONNABORTED') {
        if (!silentError) ElMessage.error('请求超时，请稍后重试')
      } else {
        if (!silentError) ElMessage.error('网络异常，请检查网络连接后重试')
      }
    }
    
    return Promise.reject(error)
  }
)

// 封装请求方法
export const request = {
  get<T = any>(url: string, config?: AxiosRequestConfig): Promise<T> {
    return service.get(url, config)
  },
  
  post<T = any>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {
    return service.post(url, data, config)
  },
  
  put<T = any>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {
    return service.put(url, data, config)
  },
  
  delete<T = any>(url: string, config?: AxiosRequestConfig): Promise<T> {
    return service.delete(url, config)
  }
}

export default service
