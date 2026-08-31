// Quality dashboard API service — aggregated customer-satisfaction survey results
// (compliance-service's QualityDashboardController). Authenticated; gated by compliance.read.
import api from '../api/axios.js'

const unwrap = (r) => r.data?.data

export const getQualityDashboard = () => api.get('/api/v1/quality-dashboard').then(unwrap)
