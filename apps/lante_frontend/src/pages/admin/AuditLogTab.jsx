import { useState, useEffect, useCallback } from 'react'
import api from '../../api/axios.js'
import { Card, DataTable, Badge, Btn, Alert, Loading } from '../../components/ui.jsx'

// Captured centrally at the gateway for every state-changing request it proxies (see
// Program.cs's audit middleware + user-service's InternalAuditLogController) — method/path/
// status/actor only, since the gateway never sees request/response bodies.
export default function AuditLogTab() {
  const [items, setItems] = useState([])
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const pageSize = 50

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/audit-log', { params: { page, pageSize } })
      const data = res.data?.data
      setItems(data?.items ?? [])
      setTotalCount(data?.totalCount ?? 0)
    } catch {
      setError('Failed to load audit log.')
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  const statusVariant = (code) => code >= 500 ? 'red' : code >= 400 ? 'amber' : 'green'

  return (
    <>
      <Alert type="info">
        Every create/update/delete request proxied through the gateway, with who made it and the result.
        This records which endpoint was called, not the field-level change — the gateway never sees request bodies.
      </Alert>
      {error && <Alert type="error">{error}</Alert>}
      {loading ? <Loading /> : (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <DataTable
            headers={['Time', 'Actor', 'Method', 'Path', 'Status']}
            empty="No audit entries yet."
            rows={items.map(a => [
              new Date(a.createdAt).toLocaleString(),
              a.actorEmail ?? '—',
              a.method,
              <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{a.path}</span>,
              <Badge variant={statusVariant(a.statusCode)}>{a.statusCode}</Badge>,
            ])}
          />
        </Card>
      )}
      {totalPages > 1 && (
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 14 }}>
          <span style={{ fontSize: 12, color: '#6b7280' }}>Page {page} of {totalPages}</span>
          <div style={{ display: 'flex', gap: 8 }}>
            <Btn size="sm" variant="ghost" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</Btn>
            <Btn size="sm" variant="ghost" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Next</Btn>
          </div>
        </div>
      )}
    </>
  )
}
