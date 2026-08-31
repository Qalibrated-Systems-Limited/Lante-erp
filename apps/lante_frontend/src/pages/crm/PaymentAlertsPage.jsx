import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Kpi, KPI_GRID } from '../../components/ui.jsx'
import * as crm from '../../services/crm.js'
import {
  usePaymentAlerts, SEVERITY_VARIANT, ALERT_STATUS_VARIANT, alertTypeLabel,
} from '../../hooks/crm/usePaymentAlerts.js'
import { useState } from 'react'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const fmtKes = (n) => new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')

export default function PaymentAlertsPage() {
  const p = usePaymentAlerts()
  const [toast, setToast] = useState('')
  const flash = (x) => { setToast(x); setTimeout(() => setToast(''), 3000) }
  const s = p.summary
  const d = s?.debtors

  const ack = async (id) => {
    try { await crm.acknowledgePaymentAlert(id); flash('Alert acknowledged.'); p.reload() }
    catch (e) { flash(e.response?.data?.message ?? 'Failed.') }
  }

  return (
    <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
      {toast && <div className="fixed bottom-6 right-6 z-[1100] bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg">{toast}</div>}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 800, color: T.navy, margin: 0 }}>Payment &amp; Debtor Alerts</h1>
          <p style={{ fontSize: 13, color: T.mgrey, margin: '2px 0 0' }}>Receivables, payables &amp; debtor escalations — live from Finance</p>
        </div>
        <Btn size="sm" variant="ghost" onClick={p.reload} disabled={p.loading}>{p.loading ? 'Refreshing…' : '↻ Refresh'}</Btn>
      </div>

      {p.error && <Card style={{ marginBottom: 16, borderColor: T.red }}><span style={{ color: T.red, fontSize: 13 }}>{p.error}</span></Card>}
      {s && !s.financeConnected && (
        <Card style={{ marginBottom: 16, borderColor: T.gold }}>
          <span style={{ color: T.gold, fontSize: 13 }}>⚠ Finance service not connected — figures below are unavailable. The alert log still reflects the last successful sweep.</span>
        </Card>
      )}

      <div style={{ ...KPI_GRID, marginBottom: 20 }}>
        <Kpi label="Overdue Receivables" value={p.loading ? '…' : fmtKes(s?.overdueReceivables)} sub={`${s?.overdueInvoiceCount ?? 0} invoice(s)`} icon="📥" variant={s?.overdueReceivables > 0 ? 'amber' : 'green'} />
        <Kpi label="Payables Due ≤7d" value={p.loading ? '…' : fmtKes(s?.payablesDueSoon)} sub={`${s?.payablesDueSoonCount ?? 0} invoice(s)`} icon="📤" variant="blue" />
        <Kpi label="Debtors > 60 days" value={p.loading ? '…' : fmtKes(d?.days61Plus)} sub={`of ${fmtKes(d?.total)} total`} icon="⏳" variant={d?.days61Plus > 0 ? 'red' : 'green'} />
        <Kpi label="Open Alerts" value={p.loading ? '…' : (s?.openAlerts ?? 0)} sub={`${s?.criticalAlerts ?? 0} critical`} icon="🔔" variant={s?.criticalAlerts > 0 ? 'red' : 'default'} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 20, marginBottom: 20 }}>
        <Card>
          <SectionHeader title="Debtor Aging" sub="Outstanding receivables by age" />
          {!d ? <p style={{ color: T.mgrey, fontSize: 13 }}>—</p> : (
            <div style={{ display: 'grid', gap: 8 }}>
              <AgeRow label="Current (not due)" value={d.current} color={T.green} />
              <AgeRow label="1–30 days" value={d.days1To30} color={T.navy} />
              <AgeRow label="31–60 days" value={d.days31To60} color={T.gold} />
              <AgeRow label="61+ days" value={d.days61Plus} color={T.red} />
              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 8, display: 'flex', justifyContent: 'space-between' }}>
                <strong style={{ fontSize: 13, color: T.navy }}>Total</strong><strong style={{ fontSize: 13, color: T.navy }}>{fmtKes(d.total)}</strong>
              </div>
            </div>
          )}
        </Card>
        <Card>
          <SectionHeader title="Top Debtors" />
          <DataTable headers={['Client', 'Outstanding', '>60d']} empty={p.loading ? 'Loading…' : 'No debtors.'}
            rows={(s?.topDebtors ?? []).map(t => [
              t.customerName ?? t.customerId,
              <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(t.outstanding)}</span>,
              <span style={{ whiteSpace: 'nowrap', color: t.over60 > 0 ? T.red : undefined }}>{fmtKes(t.over60)}</span>,
            ])} />
        </Card>
      </div>

      <Card>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, marginBottom: 12, flexWrap: 'wrap' }}>
          <SectionHeader title="Alert Feed" sub="Raised by the Finance-backed sweep" />
          <div style={{ display: 'flex', gap: 8 }}>
            <select value={p.severityF} onChange={e => p.setSeverityF(e.target.value)} className="input" style={{ maxWidth: 150 }}>
              <option value="">All severities</option><option>Critical</option><option>Warning</option><option>Info</option>
            </select>
            <select value={p.statusF} onChange={e => p.setStatusF(e.target.value)} className="input" style={{ maxWidth: 150 }}>
              <option value="">All statuses</option><option>Open</option><option>Acknowledged</option>
            </select>
          </div>
        </div>
        <DataTable headers={['Severity', 'Type', 'For', 'Detail', 'Amount', 'Raised', 'Status', p.canWrite ? '' : null].filter(x => x !== null)}
          empty={p.loading ? 'Loading…' : 'No alerts.'}
          rows={p.alerts.map(a => [
            <Badge variant={SEVERITY_VARIANT[a.severity] ?? 'default'}>{a.severity}</Badge>,
            alertTypeLabel(a.alertType),
            <span style={{ fontSize: 12, color: T.mgrey }}>{a.targetRole}</span>,
            <span style={{ fontSize: 12 }}>{a.message}</span>,
            a.amount != null ? <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(a.amount)}</span> : '—',
            fmtDate(a.createdAt),
            <Badge variant={ALERT_STATUS_VARIANT[a.status] ?? 'default'}>{a.status}</Badge>,
            ...(p.canWrite ? [a.status === 'Open'
              ? <Btn size="sm" variant="ghost" onClick={() => ack(a.id)}>Acknowledge</Btn>
              : <span style={{ color: T.mgrey, fontSize: 12 }}>✓</span>] : []),
          ])} />
      </Card>
    </div>
  )
}

function AgeRow({ label, value, color }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
      <span style={{ fontSize: 13, color: T.mgrey }}>{label}</span>
      <strong style={{ fontSize: 14, color }}>{fmtKes(value)}</strong>
    </div>
  )
}
