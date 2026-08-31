import { useState, useEffect, useCallback } from 'react'
import api from '../../api/axios.js'
import { Card, DataTable, Btn, Alert, Loading } from '../../components/ui.jsx'

// #216's field-level trail: each service's SaveChangesInterceptor records before/after values
// per changed property, in the same transaction as the change. Unlike AuditLogTab (the gateway's
// method/path/status log), this answers "what actually changed, from what, to what."
const SERVICES = [
  { id: 'finance', label: 'Finance', path: '/api/v1/finance/audit-log' },
  { id: 'crm', label: 'CRM', path: '/api/v1/crm-audit-log' },
  { id: 'stores', label: 'Stores', path: '/api/v1/stores-audit-log' },
  { id: 'fleet', label: 'Fleet', path: '/api/v1/fleet-audit-log' },
  { id: 'compliance', label: 'Compliance', path: '/api/v1/compliance-audit-log' },
  { id: 'hse', label: 'HSE', path: '/api/v1/hse-audit-log' },
  { id: 'licensing', label: 'Licensing', path: '/api/v1/licensing-audit-log' },
  { id: 'subcontracts', label: 'Subcontracts', path: '/api/v1/subcontracts-audit-log' },
  { id: 'ticketing', label: 'Ticketing', path: '/api/v1/ticketing-audit-log' },
]

export default function FieldAuditLogTab() {
  const [serviceId, setServiceId] = useState(SERVICES[0].id)
  const [items, setItems] = useState([])
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [entityId, setEntityId] = useState('')
  const [actor, setActor] = useState('')
  const pageSize = 50

  const service = SERVICES.find(s => s.id === serviceId) ?? SERVICES[0]

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get(service.path, {
        params: {
          page, pageSize,
          entityId: entityId || undefined,
          actor: actor || undefined,
        },
      })
      const data = res.data?.data
      setItems(data?.items ?? [])
      setTotalCount(data?.totalCount ?? 0)
    } catch {
      setError(`Failed to load ${service.label}'s audit trail.`)
      setItems([])
      setTotalCount(0)
    } finally {
      setLoading(false)
    }
  }, [service.path, service.label, page, entityId, actor])

  useEffect(() => { load() }, [load])
  useEffect(() => { setPage(1) }, [serviceId])

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  return (
    <>
      <Alert type="info">
        Field-level changes captured at the database boundary — the entity, the fields that
        actually moved, who changed them, and when. Pick a service to see its trail.
      </Alert>

      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', margin: '12px 0' }}>
        {SERVICES.map(s => (
          <Btn
            key={s.id}
            size="sm"
            variant={s.id === serviceId ? 'primary' : 'ghost'}
            onClick={() => setServiceId(s.id)}
          >
            {s.label}
          </Btn>
        ))}
      </div>

      <div style={{ display: 'flex', gap: 8, marginBottom: 12, flexWrap: 'wrap' }}>
        <input
          placeholder="Filter by entity id…"
          value={entityId}
          onChange={e => { setEntityId(e.target.value); setPage(1) }}
          style={{ padding: '6px 10px', fontSize: 12.5, border: '1px solid #d1d5db', borderRadius: 6, minWidth: 200 }}
        />
        <input
          placeholder="Filter by actor…"
          value={actor}
          onChange={e => { setActor(e.target.value); setPage(1) }}
          style={{ padding: '6px 10px', fontSize: 12.5, border: '1px solid #d1d5db', borderRadius: 6, minWidth: 200 }}
        />
      </div>

      {error && <Alert type="error">{error}</Alert>}
      {loading ? <Loading /> : (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <DataTable
            headers={['Time', 'Entity', 'Entity Id', 'Action', 'Actor', 'Change']}
            empty={`No field-level audit entries yet for ${service.label}.`}
            rows={items.map(a => [
              new Date(a.at).toLocaleString(),
              a.entity,
              <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{a.entityId}</span>,
              a.action,
              a.actor ?? 'system',
              <span style={{ fontSize: 11.5 }}>{a.details ?? '—'}</span>,
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
