import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Card, DataTable, Btn, Badge, Loading, Alert } from '../../components/ui.jsx'
import { useTimesheetDetail } from '../../hooks/operations/useTimesheetDetail.js'
import TimesheetEntryModal from '../../components/operations/timesheets/TimesheetEntryModal.jsx'

const STATUS_VARIANT = { Draft: 'default', Submitted: 'amber', Approved: 'green', Rejected: 'red' }
const fmtDate = (d) => (d ? new Date(d).toLocaleDateString() : '—')

export default function TimesheetDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const {
    ts, loading, error, busy, editable, canReview, hasUnapprovedOt,
    addEntry, updateEntry, deleteEntry, requestOvertime, submit, review,
  } = useTimesheetDetail(id)
  const [entryModal, setEntryModal] = useState(null)   // null | {} (new) | entry (edit)

  if (loading) return <main className="flex-1 max-w-5xl mx-auto w-full px-4 py-8"><Loading /></main>
  if (!ts) return <main className="flex-1 max-w-5xl mx-auto w-full px-4 py-8"><Alert type="error">Timesheet not found.</Alert></main>

  const onDeleteEntry = (e) => { if (window.confirm('Delete this entry?')) deleteEntry(e.id).catch(() => {}) }
  // PR2 — this no longer raises a request in HR; it checks that the manager already approved the day.
  // HR only grants overtime before it is worked, so nothing here can create an approval after the fact.
  const onCheckOt = (e) => {
    if (window.confirm(`Check HR for an approved overtime record covering ${e.overtimeHours}h on ${fmtDate(e.workDate)}?`))
      requestOvertime(e.id, { reason: null }).catch(() => {})
  }
  const onReject = () => { const c = window.prompt('Reason for rejection:'); if (c != null) review(false, c).catch(() => {}) }

  const headers = ['Date', 'Project', 'Description', 'Hours', 'Overtime', ...(editable ? ['Actions'] : [])]
  const rows = (ts.entries ?? []).map(e => {
    const cells = [
      fmtDate(e.workDate),
      e.projectId ? <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{e.projectId.slice(0, 8)}</span> : '—',
      <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
        {e.description}
        {e.source === 'FsrImport' && <Badge variant="blue">FSR</Badge>}
        {e.source === 'CheckIn' && <Badge variant="blue">Check-in</Badge>}
        {e.isBillable === false && <Badge variant="default">non-billable</Badge>}
      </span>,
      (e.hours ?? 0).toFixed(2),
      e.overtimeHours > 0
        ? <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
            {e.overtimeHours.toFixed(2)}
            {e.isOvertimeApproved
              ? <Badge variant="green">approved</Badge>
              : <Badge variant="red">unapproved</Badge>}
            {editable && !e.isOvertimeApproved && <Btn size="sm" variant="outline" onClick={() => onCheckOt(e)}>Check HR</Btn>}
          </span>
        : '—',
    ]
    if (editable) cells.push(
      <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
        <Btn size="sm" variant="outline" onClick={() => setEntryModal(e)}>Edit</Btn>
        <Btn size="sm" variant="danger" onClick={() => onDeleteEntry(e)}>Del</Btn>
      </div>,
    )
    return cells
  })

  return (
    <>
      <main className="flex-1 max-w-5xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        <button onClick={() => navigate('/modules/operations/timesheets')} className="text-sm text-gray-500 hover:text-gray-800 mb-5">← Back to Timesheets</button>

        <div className="flex items-start justify-between gap-4 flex-wrap mb-6">
          <div>
            <div className="flex items-center gap-3 flex-wrap">
              <h1 className="text-2xl font-extrabold text-zinc-950">Week of {fmtDate(ts.weekStartDate)}</h1>
              <Badge variant={STATUS_VARIANT[ts.status] ?? 'default'}>{ts.status}</Badge>
            </div>
            <p className="text-sm text-gray-500 mt-1">{ts.employeeName} · {fmtDate(ts.weekStartDate)} – {fmtDate(ts.weekEndDate)}</p>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            {editable && <Btn variant="gold" onClick={() => setEntryModal({})}>+ Add Entry</Btn>}
            {editable && <Btn variant="primary" disabled={busy || hasUnapprovedOt} onClick={() => submit().catch(() => {})}>Submit</Btn>}
            {canReview && <Btn variant="green" disabled={busy} onClick={() => review(true).catch(() => {})}>Approve</Btn>}
            {canReview && <Btn variant="danger" disabled={busy} onClick={onReject}>Reject</Btn>}
          </div>
        </div>

        {error && <Alert type="error">{error}</Alert>}
        {ts.status === 'Rejected' && ts.rejectionReason && <Alert type="warning">Rejected: {ts.rejectionReason}</Alert>}
        {editable && hasUnapprovedOt && (
          <Alert type="warning">
            Submit is blocked until all overtime is pre-approved. Overtime must be approved by your
            manager in HR <strong>before</strong> it is worked — use “Check HR” to pick up an approval
            that already exists.
          </Alert>
        )}

        <div style={{ display: 'flex', gap: 16, marginBottom: 18, flexWrap: 'wrap' }}>
          <Card style={{ flex: 1, minWidth: 180 }}>
            <div style={{ fontSize: 26, fontWeight: 800, color: '#1b3a5c' }}>{(ts.totalHours ?? 0).toFixed(2)}</div>
            <div style={{ fontSize: 12, color: '#9aa7b4' }}>Total hours</div>
          </Card>
          <Card style={{ flex: 1, minWidth: 180 }}>
            <div style={{ fontSize: 26, fontWeight: 800, color: (ts.overtimeHours ?? 0) > 0 ? '#c9a24b' : '#1b3a5c' }}>{(ts.overtimeHours ?? 0).toFixed(2)}</div>
            <div style={{ fontSize: 12, color: '#9aa7b4' }}>Overtime hours</div>
          </Card>
        </div>

        <Card style={{ padding: 0 }}>
          <DataTable headers={headers} rows={rows} empty="No entries yet." />
        </Card>
      </main>

      {entryModal && (
        <TimesheetEntryModal
          entry={entryModal.id ? entryModal : null}
          weekStart={ts.weekStartDate}
          onClose={() => setEntryModal(null)}
          onSave={(dto, entryId) => (entryId ? updateEntry(entryId, dto) : addEntry(dto))}
        />
      )}
    </>
  )
}
