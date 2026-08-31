import { useParams, useNavigate } from 'react-router-dom'
import { Btn, Badge, Loading, Alert } from '../../components/ui.jsx'
import { useServiceReport } from '../../hooks/operations/useServiceReport.js'
import FsrReportView from '../../components/operations/fsr/FsrReportView.jsx'

const STATUS_VARIANT = { Draft: 'default', Submitted: 'amber', UnderReview: 'blue', Approved: 'green', Rejected: 'red', Revised: 'purple' }

// O11.6 — Field Service Report review page (8-section view + TM approve/reject; approval triggers the
// backend timesheet auto-import).
export default function ServiceReportReviewPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { report, loading, error, busy, reviewable, review } = useServiceReport(id)

  if (loading) return <main className="flex-1 max-w-4xl mx-auto w-full px-4 py-8"><Loading /></main>
  if (!report) return <main className="flex-1 max-w-4xl mx-auto w-full px-4 py-8"><Alert type="error">Service report not found.</Alert></main>

  const onReject = () => { const c = window.prompt('Reason for rejection:'); if (c != null) review(false, c).catch(() => {}) }

  return (
    <main className="flex-1 max-w-4xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
      {report.assignmentId && (
        <button onClick={() => navigate(`/modules/operations/assignments/${report.assignmentId}`)} className="text-sm text-gray-500 hover:text-gray-800 mb-5">← Back to Assignment</button>
      )}

      <div className="flex items-start justify-between gap-4 flex-wrap mb-6">
        <div>
          <div className="flex items-center gap-3 flex-wrap">
            <h1 className="text-2xl font-extrabold text-zinc-950">Field Service Report</h1>
            <Badge variant={STATUS_VARIANT[report.status] ?? 'default'}>{report.status}</Badge>
          </div>
          <p className="text-sm text-gray-500 mt-1">{report.technicianName} · {report.customerName}</p>
        </div>
        {reviewable && (
          <div className="flex items-center gap-2">
            <Btn variant="green" disabled={busy} onClick={() => review(true).catch(() => {})}>Approve</Btn>
            <Btn variant="danger" disabled={busy} onClick={onReject}>Reject</Btn>
          </div>
        )}
      </div>

      {error && <Alert type="error">{error}</Alert>}
      {report.status === 'Approved' && <Alert type="success">Approved — the technician's time has been imported onto their weekly timesheet.</Alert>}

      <FsrReportView report={report} />
    </main>
  )
}
