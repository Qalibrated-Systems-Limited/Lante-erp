import { useState } from 'react'
import { Card, DataTable, Btn, Badge, Select, Loading, Alert } from '../../components/ui.jsx'
import { useNegligence } from '../../hooks/operations/useNegligence.js'
import ReportNegligenceModal from '../../components/operations/negligence/ReportNegligenceModal.jsx'
import NegligenceDetailModal from '../../components/operations/negligence/NegligenceDetailModal.jsx'

const STATUS_VARIANT = { Logged: 'amber', UnderReview: 'blue', Responded: 'blue', Closed: 'green' }
const fmt = (d) => (d ? new Date(d).toLocaleDateString() : '—')

// O11.4 — negligence register (report + respond). Standalone page under Operations.
export default function NegligencePage() {
  const { items, loading, error, statusFilter, setStatus, canReport, canRespond, report, respond } = useNegligence()
  const [showReport, setShowReport] = useState(false)
  const [detail, setDetail] = useState(null)

  const headers = ['Number', 'Employee', 'Title', 'Severity', 'Status', 'Flags', 'Reported']
  const rows = items.map(i => [
    <span style={{ fontWeight: 600 }}>{i.incidentNumber}</span>,
    i.employeeName || i.employeeId,
    i.title,
    <Badge variant={i.severity === 'Critical' || i.severity === 'Major' ? 'red' : 'default'}>{i.severity}</Badge>,
    <Badge variant={STATUS_VARIANT[i.status] ?? 'default'}>{i.status}</Badge>,
    <span style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
      {i.loggedLate && <Badge variant="amber">Late</Badge>}
      {i.isRepeatOffense && <Badge variant="red">Repeat</Badge>}
      {i.escalatedToMdAt && <Badge variant="purple">MD</Badge>}
    </span>,
    fmt(i.reportedAt),
  ])

  return (
    <>
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        <div className="flex items-center justify-between mb-6 flex-wrap gap-3">
          <div>
            <h1 className="text-2xl font-extrabold text-zinc-950">Negligence Register</h1>
            <p className="text-sm text-zinc-500 mt-1">Log within 24h · respond within 5 days · repeat within 12 months triggers a final warning.</p>
          </div>
          {canReport && <Btn variant="primary" onClick={() => setShowReport(true)}>+ Report Incident</Btn>}
        </div>

        {error && <Alert type="error">{error}</Alert>}

        <Card style={{ padding: 0 }}>
          <div style={{ padding: '10px 16px', borderBottom: '1px solid #eef1f5', display: 'flex', alignItems: 'center', gap: 12 }}>
            <div style={{ width: 200 }}>
              <Select value={statusFilter} onChange={setStatus}
                options={[{ value: '', label: 'All statuses' }, ...['Logged', 'UnderReview', 'Responded', 'Closed'].map(s => ({ value: s, label: s }))]}
                style={{ marginBottom: 0 }} />
            </div>
            <span style={{ fontSize: 12, color: '#9aa7b4', marginLeft: 'auto' }}>{items.length} incident{items.length === 1 ? '' : 's'}</span>
          </div>
          {loading ? <Loading /> : <DataTable headers={headers} rows={rows} empty="No negligence incidents." onRowClick={(_, i) => setDetail(items[i])} />}
        </Card>
      </main>

      {showReport && <ReportNegligenceModal onClose={() => setShowReport(false)} onSave={report} />}
      {detail && <NegligenceDetailModal incident={detail} canRespond={canRespond} onClose={() => setDetail(null)} onRespond={respond} />}
    </>
  )
}
