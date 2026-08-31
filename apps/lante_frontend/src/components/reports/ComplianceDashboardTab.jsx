import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, SectionHeader, DataTable, Loading, EmptyState } from '../ui.jsx'
import { getComplianceDashboardReport } from '../../services/reports.js'
import { exportToExcel, exportReportToPdf } from '../../utils/export.js'

const RAG_VARIANT = { green: 'green', amber: 'amber', red: 'red' }

export default function ComplianceDashboardTab({ canExport }) {
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    setError(null)
    getComplianceDashboardReport()
      .then(setData)
      .catch(() => setError('Failed to load compliance dashboard.'))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => { load() }, [load])

  const statutory = data?.statutory ?? {}
  const deadlines = statutory.upcomingDeadlines ?? []
  const policies = data?.policies ?? []

  function exportDeadlines() {
    exportToExcel({
      title: 'Upcoming Statutory Deadlines',
      filename: 'compliance-upcoming-deadlines',
      sheetName: 'Deadlines',
      columns: [
        { header: 'Source Type', accessor: r => r.sourceType, width: 20 },
        { header: 'Name', accessor: r => r.name, width: 30 },
        { header: 'Due Date', accessor: r => fmt.date(r.dueDate), width: 16 },
        { header: 'RAG', accessor: r => r.rag, width: 10 },
      ],
      rows: deadlines,
    })
  }
  function exportPolicies() {
    exportToExcel({
      title: 'Policy Acknowledgement',
      filename: 'compliance-policy-acknowledgement',
      sheetName: 'Policies',
      columns: [
        { header: 'Policy', accessor: r => r.title, width: 34 },
        { header: 'Acknowledged Count', accessor: r => r.acknowledgedCount, width: 18 },
      ],
      rows: policies,
    })
  }
  function exportDeadlinesPdf() {
    exportReportToPdf({
      title: 'Upcoming Statutory Deadlines',
      filename: 'compliance-upcoming-deadlines',
      summary: [
        { label: 'Green', value: fmt.num(statutory.greenCount) },
        { label: 'Amber', value: fmt.num(statutory.amberCount) },
        { label: 'Red', value: fmt.num(statutory.redCount) },
      ],
      sections: [{
        heading: 'Upcoming Statutory Deadlines',
        columns: [
          { header: 'Source Type', accessor: r => r.sourceType },
          { header: 'Name', accessor: r => r.name },
          { header: 'Due Date', accessor: r => fmt.date(r.dueDate) },
          { header: 'RAG', accessor: r => r.rag },
        ],
        rows: deadlines,
      }],
    })
  }
  function exportPoliciesPdf() {
    exportReportToPdf({
      title: 'Policy Acknowledgement',
      filename: 'compliance-policy-acknowledgement',
      sections: [{
        heading: 'Policy Acknowledgement',
        columns: [
          { header: 'Policy', accessor: r => r.title },
          { header: 'Acknowledged Count', accessor: r => fmt.num(r.acknowledgedCount) },
        ],
        rows: policies,
      }],
    })
  }

  return (
    <>
      {error && <Alert type="error">{error}</Alert>}

      {loading ? <Loading /> : !data ? (
        <EmptyState title="No compliance data" />
      ) : (
        <>
          <div style={{ ...KPI_GRID, marginBottom: 22 }}>
            <Kpi label="Gifts Flagged (Pending Review)" value={fmt.num(data.giftsFlaggedPendingReview)} icon="🎁" variant={data.giftsFlaggedPendingReview ? 'amber' : 'green'} />
            <Kpi label="COI Declarations Outstanding" value={fmt.num(data.coiDeclarationsOutstandingThisYear)} icon="📝" variant={data.coiDeclarationsOutstandingThisYear ? 'amber' : 'green'} />
            <Kpi label="Whistleblower Cases Open" value={fmt.num(data.whistleblowerCasesOpen)} icon="📢" variant={data.whistleblowerCasesOpen ? 'red' : 'green'} />
            <Kpi label="DSR Due Soon" value={fmt.num(data.dsrDueSoon)} icon="🔒" variant={data.dsrDueSoon ? 'amber' : 'green'} />
            <Kpi label="DSR Overdue" value={fmt.num(data.dsrOverdue)} icon="⏰" variant={data.dsrOverdue ? 'red' : 'green'} />
            <Kpi label="Data Breaches Pending ODPC Notification" value={fmt.num(data.dataBreachesPendingOdpcNotification)} icon="🚨" variant={data.dataBreachesPendingOdpcNotification ? 'red' : 'green'} />
            <Kpi label="Licences Expiring Soon" value={fmt.num(data.licencesExpiringSoon)} icon="📄" variant={data.licencesExpiringSoon ? 'amber' : 'green'} />
            <Kpi label="Licences Expired" value={fmt.num(data.licencesExpired)} icon="❌" variant={data.licencesExpired ? 'red' : 'green'} />
            <Kpi label="ABC Training Due Soon" value={fmt.num(data.abcTrainingDueSoon)} icon="🎓" variant={data.abcTrainingDueSoon ? 'amber' : 'green'} />
            <Kpi label="Related Party Transactions Unreported" value={fmt.num(data.relatedPartyTransactionsUnreported)} icon="🔗" variant={data.relatedPartyTransactionsUnreported ? 'amber' : 'green'} />
          </div>

          <SectionHeader title="Statutory RAG Summary" />
          <div style={{ display: 'flex', gap: 12, marginBottom: 22, flexWrap: 'wrap' }}>
            <Badge variant="green" size="lg">🟢 Green: {fmt.num(statutory.greenCount)}</Badge>
            <Badge variant="amber" size="lg">🟡 Amber: {fmt.num(statutory.amberCount)}</Badge>
            <Badge variant="red" size="lg">🔴 Red: {fmt.num(statutory.redCount)}</Badge>
          </div>

          <SectionHeader title="Upcoming Statutory Deadlines"
            action={canExport && deadlines.length > 0 && <div style={{ display: 'flex', gap: 8 }}>
              <Btn size="sm" variant="ghost" onClick={exportDeadlines}>⬇ Export</Btn>
              <Btn size="sm" variant="ghost" onClick={exportDeadlinesPdf}>⬇ PDF</Btn>
            </div>} />
          <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 22 }}>
            <DataTable headers={['Source Type', 'Name', 'Due Date', 'RAG']} empty="No upcoming deadlines."
              rows={deadlines.map(d => [
                d.sourceType, d.name, fmt.date(d.dueDate), <Badge variant={RAG_VARIANT[(d.rag ?? '').toLowerCase()] || 'default'}>{d.rag}</Badge>,
              ])} />
          </Card>

          <SectionHeader title="Policy Acknowledgement"
            action={canExport && policies.length > 0 && <div style={{ display: 'flex', gap: 8 }}>
              <Btn size="sm" variant="ghost" onClick={exportPolicies}>⬇ Export</Btn>
              <Btn size="sm" variant="ghost" onClick={exportPoliciesPdf}>⬇ PDF</Btn>
            </div>} />
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable headers={['Policy', 'Acknowledged Count']} empty="No policies found."
              rows={policies.map(p => [<strong>{p.title}</strong>, fmt.num(p.acknowledgedCount)])} />
          </Card>
        </>
      )}
    </>
  )
}
