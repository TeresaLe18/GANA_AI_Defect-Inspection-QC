import axios from 'axios'

export const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5080'
const http = axios.create({ baseURL: `${API_BASE}/api`, timeout: 120000 })

/** Normalises ProblemDetails / network errors into one readable message. */
export function errorMessage(err) {
  if (err.response) {
    const d = err.response.data
    if (d?.errors) return Object.values(d.errors).flat().join(' ')
    return d?.detail || d?.title || `Server error ${err.response.status}`
  }
  return 'Cannot reach the server. Is the API running?'
}

export const imageSrc = (url) => `${API_BASE}${url}`

export const getProducts = () => http.get('/products').then((r) => r.data)
export const getInspectors = () => http.get('/inspectors').then((r) => r.data)
export const getCategories = () => http.get('/defect-categories').then((r) => r.data)
export const getSummary = () => http.get('/dashboard/summary').then((r) => r.data)
export const getInspections = (params) => http.get('/inspections', { params }).then((r) => r.data)
export const discardInspection = (id) => http.delete(`/inspections/${id}`)
export const confirmReport = (body) => http.post('/confirm-report', body).then((r) => r.data)

export function analyze(file, productId, inspectorId) {
  const form = new FormData()
  form.append('image', file)
  form.append('productId', productId)
  form.append('inspectorId', inspectorId)
  return http.post('/inspections/analyze', form).then((r) => r.data)
}
