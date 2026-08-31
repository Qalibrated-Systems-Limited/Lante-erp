import { Card, DataTable, Btn, Badge, Loading, Alert, EmptyState } from '../../components/ui.jsx'
import { useEquipmentHistory } from '../../hooks/operations/useEquipmentHistory.js'

// O11.6 — search a piece of equipment's calibration/service history by serial number (FSR_EQUIPMENT).
export default function EquipmentHistoryPage() {
  const { serial, setSerial, items, loading, error, search } = useEquipmentHistory()

  const headers = ['Description', 'Manufacturer', 'Model', 'Tag', 'Condition (before → after)', 'Work done']
  const rows = (items ?? []).map(e => [
    e.description || '—',
    e.manufacturer || '—',
    e.model || '—',
    e.tagNumber || '—',
    `${e.conditionBefore || '—'} → ${e.conditionAfter || '—'}`,
    e.workDone || e.notes || '—',
  ])

  return (
    <>
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        <div className="mb-6">
          <h1 className="text-2xl font-extrabold text-zinc-950">Equipment Service History</h1>
          <p className="text-sm text-zinc-500 mt-1">Look up every field service report an instrument has appeared on, by serial number.</p>
        </div>

        <Card style={{ marginBottom: 18 }}>
          <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
            <input
              value={serial}
              onChange={e => setSerial(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && search()}
              placeholder="Serial number…"
              style={{ flex: 1, minWidth: 220, padding: '10px 12px', border: '1.5px solid #e6eaee', borderRadius: 8, fontSize: 14 }}
            />
            <Btn variant="primary" onClick={() => search()} disabled={loading || !serial.trim()}>Search</Btn>
          </div>
        </Card>

        {error && <Alert type="error">{error}</Alert>}

        {loading ? <Loading />
          : items === null ? <EmptyState icon="🔎" title="Search by serial number" sub="Enter an instrument's serial number to see its full service history." />
          : items.length === 0 ? <EmptyState icon="📭" title="No history found" sub={`No equipment records match serial "${serial}".`} />
          : (
            <Card style={{ padding: 0 }}>
              <div style={{ padding: '10px 16px', borderBottom: '1px solid #eef1f5', display: 'flex', alignItems: 'center', gap: 8 }}>
                <Badge variant="navy">{serial}</Badge>
                <span style={{ fontSize: 12, color: '#9aa7b4', marginLeft: 'auto' }}>{items.length} record{items.length === 1 ? '' : 's'}</span>
              </div>
              <DataTable headers={headers} rows={rows} />
            </Card>
          )}
      </main>
    </>
  )
}
