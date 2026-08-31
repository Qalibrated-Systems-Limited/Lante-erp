import { useState } from 'react'
import { Card, DataTable, Btn, Badge, Loading, Alert } from '../../components/ui.jsx'
import { useReferenceStandards, expiryBadge } from '../../hooks/operations/useReferenceStandards.js'
import ReferenceStandardModal from '../../components/operations/calibration/ReferenceStandardModal.jsx'

// O11.5 — reference-standard register (calibration traceability + expiry). Presentational shell
// over useReferenceStandards; matches existing ops pages (Tailwind shell + shared ui.jsx kit).
export default function ReferenceStandardsPage() {
  const { items, loading, error, activeOnly, setActiveOnly, canWrite, canDelete, save, remove } = useReferenceStandards()
  const [modal, setModal] = useState(null)   // null | {} (new) | standard (edit)

  const onDelete = async (std) => {
    if (!window.confirm(`Retire reference standard ${std.assetId}?`)) return
    try { await remove(std.id) } catch { /* surfaced by list refresh */ }
  }

  const headers = ['Asset ID', 'Description', 'Nominal', 'Traceability Cert', 'Next Due', 'Status', 'Actions']
  const rows = items.map(s => {
    const eb = expiryBadge(s)
    return [
      <span style={{ fontWeight: 600 }}>{s.assetId}</span>,
      s.description,
      s.nominalValue || '—',
      s.traceabilityCertNo || '—',
      <Badge variant={eb.variant}>{eb.label}</Badge>,
      <Badge variant={s.status === 'Active' ? 'green' : 'default'}>{s.status}</Badge>,
      <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
        {canWrite && <Btn size="sm" variant="outline" onClick={() => setModal(s)}>Edit</Btn>}
        {canDelete && <Btn size="sm" variant="danger" onClick={() => onDelete(s)}>Retire</Btn>}
      </div>,
    ]
  })

  return (
    <>
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        <div className="flex items-center justify-between mb-6 flex-wrap gap-3">
          <div>
            <h1 className="text-2xl font-extrabold text-zinc-950">Reference Standards</h1>
            <p className="text-sm text-zinc-500 mt-1">Calibration traceability register — the standards that back issued certificates.</p>
          </div>
          {canWrite && <Btn variant="primary" onClick={() => setModal({})}>+ New Standard</Btn>}
        </div>

        {error && <Alert type="error">{error}</Alert>}

        <Card style={{ padding: 0 }}>
          <div style={{ padding: '12px 16px', borderBottom: '1px solid #eef1f5', display: 'flex', alignItems: 'center', gap: 10 }}>
            <label style={{ fontSize: 12, color: '#5b6b7c', display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
              <input type="checkbox" checked={activeOnly} onChange={e => setActiveOnly(e.target.checked)} />
              Active only
            </label>
            <span style={{ fontSize: 12, color: '#9aa7b4', marginLeft: 'auto' }}>{items.length} standard{items.length === 1 ? '' : 's'}</span>
          </div>
          {loading ? <Loading /> : <DataTable headers={headers} rows={rows} empty="No reference standards registered yet." />}
        </Card>
      </main>

      {modal && (
        <ReferenceStandardModal
          standard={modal.id ? modal : null}
          onClose={() => setModal(null)}
          onSave={save}
        />
      )}
    </>
  )
}
