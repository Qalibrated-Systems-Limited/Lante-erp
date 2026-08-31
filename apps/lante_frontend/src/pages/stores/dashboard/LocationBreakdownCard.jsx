import { useState, useEffect } from 'react'
import api from '../../../api/axios.js'
import { T } from '../../../theme/tokens.js'
import { Card, Loading, EmptyState } from '../../../components/ui.jsx'
import { MapPin } from 'lucide-react'

// Matches the backend's PaginationParameters.MaxPageSize — a location at or
// above this many in-stock lots gets flagged approximate rather than exact,
// since there's no cross-item aggregate-balance endpoint to ask instead.
const LOT_CAP = 100

export default function LocationBreakdownCard() {
  const [rows, setRows] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      try {
        const locRes = await api.get('/api/v1/locations', { params: { pageSize: LOT_CAP, isActive: true } })
        const locations = locRes.data?.data?.items ?? []

        const results = await Promise.allSettled(
          locations.map(loc => api.get('/api/v1/stock-units', { params: { locationId: loc.id, status: 'InStock', pageSize: LOT_CAP } }))
        )

        const computed = locations.map((loc, i) => {
          const res = results[i]
          if (res.status !== 'fulfilled') return { id: loc.id, name: loc.name, itemCount: 0, qty: 0, approx: false }
          const data = res.value.data?.data
          const units = data?.items ?? []
          const totalCount = data?.totalCount ?? units.length
          return {
            id: loc.id,
            name: loc.name,
            itemCount: new Set(units.map(u => u.itemId)).size,
            qty: units.reduce((sum, u) => sum + (u.qty || 0), 0),
            approx: totalCount > units.length,
          }
        }).filter(r => r.itemCount > 0 || r.qty > 0)

        if (!cancelled) setRows(computed)
      } catch {
        if (!cancelled) setRows([])
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    load()
    return () => { cancelled = true }
  }, [])

  return (
    <Card style={{ padding: 0, overflow: 'hidden' }}>
      <div style={{ padding: '14px 18px', borderBottom: `1px solid ${T.lgrey}` }}>
        <h2 style={{ fontSize: 15, fontWeight: 700, color: T.navy, margin: 0 }}>Stock by Location</h2>
        <p style={{ fontSize: 11, color: T.mgrey, margin: '3px 0 0' }}>Based on in-stock units; locations with 100+ lots show as approximate.</p>
      </div>
      {loading ? <Loading /> : rows.length === 0 ? (
        <EmptyState icon={<MapPin size={40} />} title="No stock at any location yet" />
      ) : (
        <div>
          {rows.map(r => (
            <div key={r.id} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 18px', borderBottom: `1px solid ${T.lgrey}` }}>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: 0 }}>{r.name}</p>
              <span style={{ fontSize: 12, color: T.mgrey }}>
                {r.approx ? '~' : ''}{r.itemCount} item{r.itemCount !== 1 ? 's' : ''} · {r.approx ? '~' : ''}{r.qty} units
              </span>
            </div>
          ))}
        </div>
      )}
    </Card>
  )
}
