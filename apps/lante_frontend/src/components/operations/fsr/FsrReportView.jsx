import { Card, Badge, DataTable } from '../../ui.jsx'

const fmt = (d) => (d ? new Date(d).toLocaleString() : '—')

function Section({ title, children }) {
  return (
    <Card style={{ marginBottom: 14 }}>
      <h3 style={{ fontSize: 13, fontWeight: 700, color: '#1b3a5c', margin: '0 0 10px', textTransform: 'uppercase', letterSpacing: .4 }}>{title}</h3>
      {children}
    </Card>
  )
}
function Row({ label, value }) {
  return (
    <div style={{ display: 'flex', gap: 10, fontSize: 13, padding: '3px 0' }}>
      <span style={{ color: '#9aa7b4', minWidth: 150 }}>{label}</span>
      <span style={{ color: '#334', flex: 1 }}>{value ?? '—'}</span>
    </div>
  )
}

// O11.6 — read-only 8-section Field Service Report (customer / visit / equipment / work / materials /
// recommendations / time / sign-off + rating).
export default function FsrReportView({ report }) {
  if (!report) return null
  const eq = report.equipment ?? []

  return (
    <div>
      {/* §1 customer + §2 visit */}
      <Section title="1 · Customer & Site">
        <Row label="Customer" value={report.customerName} />
        <Row label="Location" value={report.locationName} />
        <Row label="Contact" value={[report.contactPerson, report.contactPhone].filter(Boolean).join(' · ')} />
      </Section>
      <Section title="2 · Visit">
        <Row label="Nature of visit" value={report.natureOfVisit} />
        <Row label="Department" value={report.departmentType} />
        <Row label="Start" value={fmt(report.startDay)} />
        <Row label="End" value={fmt(report.endDay)} />
        <Row label="Total minutes" value={report.totalMinutes} />
      </Section>

      {/* §3 equipment */}
      <Section title={`3 · Equipment Serviced (${eq.length})`}>
        {eq.length === 0
          ? <p style={{ fontSize: 12, color: '#9aa7b4' }}>No equipment recorded.</p>
          : <DataTable
              headers={['Description', 'Serial', 'Manufacturer', 'Model', 'Work done']}
              rows={eq.map(e => [e.description || '—', e.serialNumber || '—', e.manufacturer || '—', e.model || '—', e.workDone || '—'])} />}
      </Section>

      {/* §4–6 work / materials / recommendations */}
      <Section title="4 · Work Performed">
        <p style={{ fontSize: 13, color: '#334', whiteSpace: 'pre-wrap' }}>{report.workSummary || '—'}</p>
      </Section>
      <Section title="5 · Materials Used">
        <p style={{ fontSize: 13, color: '#334', whiteSpace: 'pre-wrap' }}>{report.materialsUsed || '—'}</p>
      </Section>
      <Section title="6 · Recommendations & Follow-up">
        <p style={{ fontSize: 13, color: '#334', whiteSpace: 'pre-wrap' }}>{report.recommendations || '—'}</p>
        {report.followUpRequired && <Badge variant="amber">Follow-up required</Badge>}
        {report.followUpNotes && <p style={{ fontSize: 12, color: '#5b6b7c', marginTop: 6 }}>{report.followUpNotes}</p>}
      </Section>

      {/* §7 time + §8 sign-off */}
      <Section title="7 · Customer Comments">
        <p style={{ fontSize: 13, color: '#334' }}>{report.customerComments || '—'}</p>
      </Section>
      <Section title="8 · Sign-off">
        <Row label="Submitted" value={fmt(report.submittedAt)} />
        <Row label="Customer signatory" value={report.customerSignatureName} />
        <Row label="Technician signatory" value={report.technicianSignatureName} />
        <Row label="Client rating" value={report.clientRating ? '★'.repeat(report.clientRating) + `  (${report.clientRating}/5)` : '—'} />
        {report.approvedBy && <Row label="Approved by" value={`${report.approvedBy} · ${fmt(report.approvedAt)}`} />}
        {report.rejectionReason && <Row label="Rejected" value={report.rejectionReason} />}
      </Section>
    </div>
  )
}
