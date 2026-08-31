import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Card, DataTable, Btn, Badge, Loading, Alert } from '../../components/ui.jsx'
import { useTimesheets } from '../../hooks/operations/useTimesheets.js'
import CreateTimesheetModal from '../../components/operations/timesheets/CreateTimesheetModal.jsx'

const STATUS_VARIANT = { Draft: 'default', Submitted: 'amber', Approved: 'green', Rejected: 'red' }
const fmtDate = (d) => (d ? new Date(d).toLocaleDateString() : '—')

// O11.2 — timesheets list: the caller's own sheets + (for approvers) the line-manager approval queue.
export default function TimesheetsPage() {
  const navigate = useNavigate()
  const { items, loading, error, view, setView, canApprove, createForWeek } = useTimesheets()
  const [showCreate, setShowCreate] = useState(false)

  const open = (ts) => navigate(`/modules/operations/timesheets/${ts.id}`)

  const headers = ['Week', 'Status', 'Total Hours', 'Overtime', 'Actions']
  const rows = items.map(t => [
    <span style={{ fontWeight: 600 }}>{fmtDate(t.weekStartDate)} – {fmtDate(t.weekEndDate)}</span>,
    <Badge variant={STATUS_VARIANT[t.status] ?? 'default'}>{t.status}</Badge>,
    (t.totalHours ?? 0).toFixed(2),
    (t.overtimeHours ?? 0) > 0 ? <Badge variant="amber">{t.overtimeHours.toFixed(2)}</Badge> : '—',
    <Btn size="sm" variant="outline" onClick={() => open(t)}>Open</Btn>,
  ])

  const TabBtn = ({ id, label }) => (
    <button onClick={() => setView(id)} style={{
      padding: '7px 16px', background: 'none', border: 'none', cursor: 'pointer', fontSize: 13,
      fontWeight: view === id ? 700 : 500, color: view === id ? '#1b3a5c' : '#9aa7b4',
      borderBottom: view === id ? '2px solid #c9a24b' : '2px solid transparent',
    }}>{label}</button>
  )

  return (
    <>
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        <div className="flex items-center justify-between mb-6 flex-wrap gap-3">
          <div>
            <h1 className="text-2xl font-extrabold text-zinc-950">Timesheets</h1>
            <p className="text-sm text-zinc-500 mt-1">Weekly timesheets with overtime pre-approval and line-manager sign-off.</p>
          </div>
          <Btn variant="primary" onClick={() => setShowCreate(true)}>+ New Week</Btn>
        </div>

        {error && <Alert type="error">{error}</Alert>}

        <Card style={{ padding: 0 }}>
          <div style={{ display: 'flex', borderBottom: '1px solid #eef1f5', padding: '0 8px' }}>
            <TabBtn id="mine" label="My Timesheets" />
            {canApprove && <TabBtn id="queue" label="Approval Queue" />}
          </div>
          {loading ? <Loading /> : (
            <DataTable headers={headers} rows={rows}
              empty={view === 'queue' ? 'No timesheets awaiting approval.' : 'No timesheets yet — start a new week.'} />
          )}
        </Card>
      </main>

      {showCreate && (
        <CreateTimesheetModal
          onClose={() => setShowCreate(false)}
          onCreate={async (date) => { const ts = await createForWeek(date); if (ts?.id) open(ts) }}
        />
      )}
    </>
  )
}
