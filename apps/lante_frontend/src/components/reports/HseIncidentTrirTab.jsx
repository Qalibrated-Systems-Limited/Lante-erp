import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, SectionHeader, DataTable, Loading, EmptyState, Input } from '../ui.jsx'
import { getHseIncidentsTrir } from '../../services/reports.js'
import { exportToExcel, exportReportToPdf } from '../../utils/export.js'

// Type/Severity/Status all arrive as raw enum ordinals (HSEService.Core.Enums.IncidentType/
// IncidentSeverity/IncidentStatus, mirrored in ReportingService's HseDtos.cs) — reporting-service
// doesn't convert them to strings, matching how HSE's own module already treats these fields
// (see apps/lante_frontend/src/pages/hse/tabs/IncidentsTab.jsx's SEVERITIES.indexOf pattern).
const INCIDENT_TYPES = ['Near Miss', 'First Aid', 'Medical Treatment', 'Lost Time Injury', 'Positive Observation']
const INCIDENT_STATUSES = ['Open', 'Under Investigation', 'Corrective Action Pending', 'Closed']
const SEVERITY_LABELS = ['None', 'Low', 'Medium', 'High', 'Critical']
const SEVERITY_VARIANT = { low: 'green', medium: 'amber', high: 'red', critical: 'red' }
const severityLabel = (s) => SEVERITY_LABELS[s] ?? 'Unknown'
const severityBadge = (s) => SEVERITY_VARIANT[severityLabel(s).toLowerCase()] || 'default'

export default function HseIncidentTrirTab({ canExport }) {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [hoursWorkedYtd, setHoursWorkedYtd] = useState('')
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    setError(null)
    const params = {}
    if (from) params.from = from
    if (to) params.to = to
    if (hoursWorkedYtd) params.hoursWorkedYtd = hoursWorkedYtd
    getHseIncidentsTrir(Object.keys(params).length ? params : undefined)
      .then(setData)
      .catch(() => setError('Failed to load HSE incidents & TRIR.'))
      .finally(() => setLoading(false))
  }, [from, to, hoursWorkedYtd])

  useEffect(() => { load() }, [load])

  const incidents = data?.incidents ?? []

  function doExport() {
    exportToExcel({
      title: 'HSE Incidents',
      filename: 'hse-incidents-trir',
      sheetName: 'Incidents',
      columns: [
        { header: 'Type', accessor: r => INCIDENT_TYPES[r.type] ?? r.type, width: 18 },
        { header: 'Severity', accessor: r => severityLabel(r.severity), width: 14 },
        { header: 'Site', accessor: r => r.siteName ?? r.siteId, width: 20 },
        { header: 'Occurred At', accessor: r => fmt.date(r.occurredAt), width: 16 },
        { header: 'Status', accessor: r => INCIDENT_STATUSES[r.status] ?? r.status, width: 14 },
      ],
      rows: incidents,
    })
  }
  function doExportPdf() {
    exportReportToPdf({
      title: 'HSE Incidents & TRIR Report',
      filename: 'hse-incidents-trir',
      summary: [
        { label: 'TRIR', value: data?.trir ?? '—' },
        { label: 'LTIF', value: data?.ltif ?? '—' },
        { label: 'Near Misses', value: fmt.num(data?.nearMissCount) },
        { label: 'Total Incidents YTD', value: fmt.num(data?.totalIncidentsYtd) },
        { label: 'Open Corrective Actions', value: fmt.num(data?.openCorrectiveActions) },
        { label: 'Overdue Corrective Actions', value: fmt.num(data?.overdueCorrectiveActions) },
        { label: 'RAMS Pending Approval', value: fmt.num(data?.ramsPendingApproval) },
        { label: 'Training Certs Expiring Soon', value: fmt.num(data?.trainingCertificatesExpiringSoon) },
      ],
      sections: [{
        heading: 'Incident Register',
        columns: [
          { header: 'Type', accessor: r => INCIDENT_TYPES[r.type] ?? r.type },
          { header: 'Severity', accessor: r => severityLabel(r.severity) },
          { header: 'Site', accessor: r => r.siteName ?? r.siteId },
          { header: 'Occurred At', accessor: r => fmt.date(r.occurredAt) },
          { header: 'Status', accessor: r => INCIDENT_STATUSES[r.status] ?? r.status },
        ],
        rows: incidents,
      }],
    })
  }

  return (
    <>
      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'flex-end', gap: 10, flexWrap: 'wrap' }}>
        <div style={{ minWidth: 180 }}><Input label="From" type="date" value={from} onChange={setFrom} /></div>
        <div style={{ minWidth: 180 }}><Input label="To" type="date" value={to} onChange={setTo} /></div>
        <div style={{ minWidth: 200 }}>
          <Input label="Hours Worked YTD" type="number" value={hoursWorkedYtd} onChange={setHoursWorkedYtd} placeholder="Required for TRIR/LTIF" note="Total labour hours worked year-to-date, used to compute TRIR/LTIF." />
        </div>
        <Btn size="sm" onClick={load} style={{ marginBottom: 14 }}>Refresh</Btn>
        {canExport && incidents.length > 0 && <Btn size="sm" variant="ghost" onClick={doExport} style={{ marginBottom: 14 }}>⬇ Export</Btn>}
        {canExport && incidents.length > 0 && <Btn size="sm" variant="ghost" onClick={doExportPdf} style={{ marginBottom: 14 }}>⬇ PDF</Btn>}
      </div>

      {error && <Alert type="error">{error}</Alert>}

      {loading ? <Loading /> : !data ? (
        <EmptyState title="No HSE data" />
      ) : (
        <>
          <div style={{ ...KPI_GRID, marginBottom: 22 }}>
            <Kpi label="TRIR" value={data.trir ?? '—'} icon="📊" />
            <Kpi label="LTIF" value={data.ltif ?? '—'} icon="⚠️" />
            <Kpi label="Near Misses" value={fmt.num(data.nearMissCount)} icon="👁️" variant="amber" />
            <Kpi label="Total Incidents YTD" value={fmt.num(data.totalIncidentsYtd)} icon="🦺" />
            <Kpi label="Open Corrective Actions" value={fmt.num(data.openCorrectiveActions)} icon="🔧" variant={data.openCorrectiveActions ? 'amber' : 'green'} />
            <Kpi label="Overdue Corrective Actions" value={fmt.num(data.overdueCorrectiveActions)} icon="⏰" variant={data.overdueCorrectiveActions ? 'red' : 'green'} />
            <Kpi label="RAMS Pending Approval" value={fmt.num(data.ramsPendingApproval)} icon="📄" variant={data.ramsPendingApproval ? 'amber' : 'green'} />
            <Kpi label="Training Certs Expiring Soon" value={fmt.num(data.trainingCertificatesExpiringSoon)} icon="🎓" variant={data.trainingCertificatesExpiringSoon ? 'amber' : 'green'} />
            <Kpi label="Statutory Inspections Due Soon" value={fmt.num(data.statutoryInspectionsDueSoon)} icon="🛡️" variant={data.statutoryInspectionsDueSoon ? 'amber' : 'green'} />
          </div>

          <SectionHeader title="Incident Register" />
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable headers={['Type', 'Severity', 'Site', 'Occurred At', 'Status']} empty="No incidents in this range."
              rows={incidents.map(i => [
                INCIDENT_TYPES[i.type] ?? i.type,
                <Badge variant={severityBadge(i.severity)}>{severityLabel(i.severity)}</Badge>,
                i.siteName ?? i.siteId,
                fmt.date(i.occurredAt),
                INCIDENT_STATUSES[i.status] ?? i.status,
              ])} />
          </Card>
        </>
      )}
    </>
  )
}
