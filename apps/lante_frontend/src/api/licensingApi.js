import api from './axios'

const BASE = '/api/v1/licenses'

export const licensingApi = {
  // Available app IDs + feature flags (drives the issue form)
  getMetadata: () => api.get(`${BASE}/metadata`),

  // Issue a new license
  issue: (data) => api.post(BASE, data),

  // List all licenses with optional filters
  getAll: (params = {}) => api.get(BASE, { params }),

  // Get one license by ID
  getById: (id) => api.get(`${BASE}/${id}`),

  // Get licenses expiring soon
  getExpiring: (withinDays = 30) => api.get(`${BASE}/expiring`, { params: { withinDays } }),

  // Revoke a license
  revoke: (id, reason) => api.delete(`${BASE}/${id}`, { data: { reason } }),

  // Renew a license — extends ExpiresAt, token is unchanged
  renew: (id, newExpiresAt) => api.post(`${BASE}/${id}/renew`, { newExpiresAt }),
}
