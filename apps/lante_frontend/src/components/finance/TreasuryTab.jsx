import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, Input, Select, SectionHeader, DataTable, Loading } from '../ui.jsx'
import {
  getCurrencies, setCurrencyRate, refreshRates,
  getObligations, listRemittances, remitStatutory,
} from '../../services/finance.js'

const OB_VARIANT = { Remitted: 'green', Pending: 'blue', Overdue: 'red' }
const thisMonth = () => new Date().toISOString().slice(0, 7)

export default function TreasuryTab({ notify }) {
  const [ccy, setCcy] = useState([])
  const [period, setPeriod] = useState(thisMonth())
  const [obligations, setObligations] = useState([])
  const [remittances, setRemittances] = useState([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(null)

  const loadCcy = useCallback(() => getCurrencies().then(c => setCcy(c ?? [])).catch(() => {}), [])
  const loadStatutory = useCallback((p) => Promise.all([getObligations(p), listRemittances()])
    .then(([o, r]) => { setObligations(o ?? []); setRemittances(r ?? []) })
    .catch(() => notify?.('Failed to load statutory data.', 'error')), [notify])

  useEffect(() => {
    Promise.all([loadCcy(), loadStatutory(period)]).finally(() => setLoading(false))
  }, [loadCcy, loadStatutory, period])

  async function remit(o) {
    setBusy(o.code)
    try { await remitStatutory({ obligationCode: o.code, period }); notify?.(`${o.name} for ${period} remitted.`); await loadStatutory(period) }
    catch (e) { notify?.(e.response?.data?.message ?? 'Remittance failed.', 'error') }
    finally { setBusy(null) }
  }
  async function refresh() {
    setBusy('rates')
    try { await refreshRates(); notify?.('Rates refreshed (stub provider).'); await loadCcy() }
    catch { notify?.('Refresh failed.', 'error') } finally { setBusy(null) }
  }

  const totalDue = obligations.filter(o => o.status !== 'Remitted').reduce((s, o) => s + o.outstandingBalance, 0)

  if (loading) return <Loading />

  return (
    <>
      <Alert type="info"><strong>Treasury & Statutory:</strong> maintain exchange rates and remit statutory deductions (PAYE / NSSF / SHA / Housing / WHT / VAT). Balances are read live from the GL liability accounts; remitting posts Dr liability / Cr bank. FX revaluation and the P9 card depend on the integrations & HR-payroll modules and will light up once those post.</Alert>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(360px, 1fr))', gap: 20, marginBottom: 22 }}>
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <SectionHeader title="Exchange Rates (to KES)" style={{ padding: '14px 16px 0' }}
            action={<Btn size="sm" variant="ghost" onClick={refresh} disabled={busy === 'rates'}>{busy === 'rates' ? 'Refreshing…' : 'Refresh (stub)'}</Btn>} />
          <DataTable headers={['Currency', 'Rate', 'Source', 'As Of', '']}
            empty="No currencies."
            rows={ccy.map(c => [
              <span><strong>{c.code}</strong> <span style={{ color: T.mgrey, fontSize: 12 }}>{c.name}</span></span>,
              <strong>{c.isBaseCurrency ? '1.000000' : Number(c.exchangeRate).toFixed(4)}</strong>,
              <Badge variant={c.source === 1 ? 'blue' : 'default'}>{c.source === 1 ? 'API' : 'Manual'}</Badge>,
              <span style={{ fontSize: 12, color: T.mgrey }}>{c.lastFetchedAt ? fmt.date(c.lastFetchedAt) : '—'}</span>,
              c.isBaseCurrency ? <span style={{ color: T.mgrey, fontSize: 12 }}>base</span> : <RateEditor c={c} notify={notify} onSaved={loadCcy} />,
            ])} />
        </Card>

        <Card>
          <SectionHeader title="Forex Revaluation" sub="Month-end unrealised FX on open foreign balances" />
          <div style={{ padding: '28px 12px', textAlign: 'center', color: T.mgrey, fontSize: 13 }}>
            Pending the integrations module — revaluation posts to <strong>5900 Forex Gain/Loss</strong> once live FX feeds and foreign-currency balances are wired.
          </div>
        </Card>
      </div>

      <SectionHeader title="Statutory Remittances" sub={`Outstanding for ${period}: ${fmt.kes(totalDue)}`}
        action={<input type="month" value={period} max={thisMonth()} onChange={e => setPeriod(e.target.value)}
          style={{ padding: '6px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }} />} />
      <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 22 }}>
        <DataTable headers={['Code', 'Obligation', 'Outstanding', 'Due Date', 'Status', 'Action']}
          empty="No obligations."
          rows={obligations.map(o => [
            <span style={{ fontFamily: T.mono, fontSize: 12 }}>{o.code}</span>,
            o.name,
            <strong>{fmt.kes(o.outstandingBalance)}</strong>,
            <span style={{ color: o.status === 'Overdue' ? T.red : T.dgrey, fontWeight: o.status === 'Overdue' ? 700 : 400 }}>
              {fmt.date(o.dueDate)}{o.status !== 'Remitted' ? ` (${o.daysToDue < 0 ? `${-o.daysToDue}d late` : `${o.daysToDue}d`})` : ''}
            </span>,
            <Badge variant={OB_VARIANT[o.status] || 'default'}>{o.status}</Badge>,
            busy === o.code ? <span style={{ color: T.mgrey, fontSize: 12 }}>…</span>
              : o.status === 'Remitted' ? <span style={{ color: T.green, fontSize: 12 }}>✓ {fmt.kes(o.remittedAmount)}</span>
              : o.outstandingBalance > 0 ? <Btn size="sm" onClick={() => remit(o)}>Remit</Btn>
              : <span style={{ color: T.mgrey, fontSize: 12 }}>nil</span>,
          ])} />
      </Card>

      <SectionHeader title="Remittance History" />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable headers={['Ref', 'Obligation', 'Period', 'Amount', 'Due', 'Remitted', 'Payment Ref']}
          empty="No remittances yet."
          rows={remittances.map(r => [
            <span style={{ fontFamily: T.mono, fontSize: 12 }}>{r.refNo}</span>,
            r.obligationName,
            r.period,
            <strong>{fmt.kes(r.amount)}</strong>,
            <span style={{ fontSize: 12, color: T.mgrey }}>{fmt.date(r.dueDate)}</span>,
            fmt.date(r.remittedAt),
            <span style={{ fontFamily: T.mono, fontSize: 11, color: T.mgrey }}>{r.paymentReference || '—'}</span>,
          ])} />
      </Card>
    </>
  )
}

function RateEditor({ c, notify, onSaved }) {
  const [editing, setEditing] = useState(false)
  const [rate, setRate] = useState(String(c.exchangeRate))
  const [saving, setSaving] = useState(false)

  async function save() {
    if (!(+rate > 0)) return
    setSaving(true)
    try { await setCurrencyRate(c.id, +rate); notify?.(`${c.code} rate set to ${rate}.`); setEditing(false); await onSaved() }
    catch (e) { notify?.(e.response?.data?.message ?? 'Failed to set rate.', 'error') }
    finally { setSaving(false) }
  }

  if (!editing) return <Btn size="sm" variant="ghost" onClick={() => setEditing(true)}>Set rate</Btn>
  return (
    <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
      <input type="number" value={rate} onChange={e => setRate(e.target.value)} autoFocus
        style={{ width: 90, padding: '5px 8px', border: `1.5px solid ${T.lgrey}`, borderRadius: 6, fontSize: 12 }} />
      <Btn size="sm" onClick={save} disabled={saving}>{saving ? '…' : 'Save'}</Btn>
      <button onClick={() => setEditing(false)} style={{ background: 'none', border: 'none', color: T.mgrey, cursor: 'pointer' }}>×</button>
    </div>
  )
}
