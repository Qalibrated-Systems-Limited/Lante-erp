import { useState, useEffect, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, Tabs, SectionHeader, DataTable, Loading } from '../../components/ui.jsx'
import { listInvoices, getFiscalYears, computeVat, fileVat } from '../../services/finance.js'

// ─────────────────────────────────────────────────────────────────────────────
// Tax & KRA. Dashboard / Tax Invoices (eTIMS) / VAT Returns are LIVE from the
// finance service (invoices + /vat). PAYE bands & the statutory calendar remain
// static reference data.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'
const ETIMS_VARIANT = { Accepted: 'green', Pending: 'amber', Rejected: 'red', NotSubmitted: 'default' }
const OBLIGATIONS = [
  ['PAYE Monthly Return & Payment', 'KRA', '9th of month', 'monthly', '2026-08-09'],
  ['NHIF Monthly Contribution', 'NHIF/SHIF', '9th of month', 'monthly', '2026-08-09'],
  ['NSSF Monthly Contribution', 'NSSF', '15th of month', 'monthly', '2026-07-15'],
  ['VAT Monthly Return & Payment', 'KRA', '20th of month', 'monthly', '2026-07-20'],
  ['Housing Levy Monthly Remittance', 'NHFC', '9th of month', 'monthly', '2026-08-09'],
  ['Corporation Tax Instalment (Q1)', 'KRA', '20th of month', 'quarterly', 'See schedule'],
  ['Withholding Tax Return', 'KRA', '20th of month', 'monthly', '2026-07-20'],
  ['Annual Company Return (Registrar)', 'BRS', 'Annual', 'annual', 'See schedule'],
  ['Audited Financial Statements', 'ICPAK', 'Annual', 'annual', 'See schedule'],
  ['Tax Compliance Certificate Renewal', 'KRA', 'Annual', 'annual', 'See schedule'],
]
const PAYE_BANDS = [
  ['0 — Kshs 24,000', '10%', 'First band'],
  ['Kshs 24,001 — Kshs 32,333', '25%', 'Second band'],
  ['Kshs 32,334 — Kshs 500,000', '30%', 'Third band'],
  ['Kshs 500,001 — Kshs 800,000', '32.5%', 'Fourth band'],
  ['Above Kshs 800,000', '35%', 'Top band'],
]
const PAYE_RATES = [
  ['Personal Relief', 'Kshs 2,400/month'], ['NHIF (SHIF)', '2.75% of gross'], ['NSSF', '6% up to Kshs 36,000'],
  ['Housing Levy', '1.5% of gross'], ['WHT — Professional', '5%'], ['WHT — Construction', '3%'],
]

export default function TaxPage() {
  const [tab, setTab] = useState('dashboard')
  const [msg, setMsg] = useState(null)
  const [invoices, setInvoices] = useState([])
  const [periods, setPeriods] = useState([])
  const [loading, setLoading] = useState(true)

  const [periodId, setPeriodId] = useState('')
  const [vat, setVat] = useState(null)
  const [computing, setComputing] = useState(false)

  const notify = (text, type = 'success') => setMsg({ type, text })

  useEffect(() => {
    Promise.all([listInvoices(), getFiscalYears()])
      .then(([inv, years]) => {
        setInvoices(inv ?? [])
        const ps = (years ?? []).flatMap(y => y.periods ?? [])
        setPeriods(ps)
        setPeriodId(ps.find(p => p.name === '2026-06')?.id ?? ps[0]?.id ?? '')
      })
      .catch(() => notify('Failed to load tax data.', 'error'))
      .finally(() => setLoading(false))
  }, [])

  const issued = invoices.filter(i => i.status !== 'Draft' && i.status !== 'Cancelled')
  const etimsSubmitted = invoices.filter(i => i.etimsReference).length
  const totalValue = issued.reduce((s, i) => s + i.total, 0)

  const compute = useCallback(async () => {
    if (!periodId) return
    setComputing(true)
    try { setVat(await computeVat(periodId)) }
    catch (e) { notify(e.response?.data?.message ?? 'Compute failed.', 'error') }
    finally { setComputing(false) }
  }, [periodId])

  async function file() {
    try { const r = await fileVat(periodId); setVat(r); notify(`VAT return filed for ${r.periodName}.`) }
    catch (e) { notify(e.response?.data?.message ?? 'File failed.', 'error') }
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {msg && <Alert type={msg.type}>{msg.text}</Alert>}
        <Tabs tabs={[
          { id: 'dashboard', label: 'Tax Dashboard' }, { id: 'etims', label: 'Tax Invoices (eTIMS)' },
          { id: 'vat', label: 'VAT Returns' }, { id: 'paye', label: 'PAYE Returns' }, { id: 'calendar', label: 'Statutory Calendar' },
        ]} active={tab} setActive={setTab} />

        {tab === 'dashboard' && (
          <>
            <div style={{ ...KPI_GRID, marginBottom: 18 }}>
              <Kpi label="Invoices Issued" value={issued.length} icon="📄" />
              <Kpi label="eTIMS Submitted" value={etimsSubmitted} icon="✅" variant="green" />
              <Kpi label="Total Invoice Value" value={loading ? '—' : fmt.kes(totalValue)} icon="💰" />
              <Kpi label="Periods Open" value={periods.filter(p => p.status === 'Open').length} icon="🗓️" />
            </div>
            <Alert type="info"><strong>Kenya Tax Obligations:</strong> PAYE due 9th · NHIF due 9th · NSSF due 15th · VAT due 20th · Housing Levy due 9th. KRA eTIMS invoice submission required for all VAT-registered sales.</Alert>
            <SectionHeader title="Statutory Obligations Calendar" />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable headers={['Obligation', 'Agency', 'Due Day', 'Frequency', 'Next Due']}
                rows={OBLIGATIONS.map(([o, ag, day, freq, next]) => [
                  <strong>{o}</strong>, <Badge variant="blue">{ag}</Badge>, day, <span style={{ color: T.mgrey }}>{freq}</span>,
                  <strong style={{ color: next.includes('-') ? T.navy : T.mgrey }}>{next}</strong>,
                ])} />
            </Card>
          </>
        )}

        {tab === 'etims' && (
          <>
            <Alert type="info"><strong>KRA eTIMS:</strong> invoices submit to eTIMS on issue. Create invoices in Finance → Documents → Invoices; this is the same register.</Alert>
            <SectionHeader title="Tax Invoice Register" />
            {loading ? <Loading /> : (
              <Card style={{ padding: 0, overflow: 'hidden' }}>
                <DataTable headers={['Invoice No', 'Client', 'Date', 'Subtotal', 'VAT', 'Total', 'eTIMS']}
                  empty="No invoices yet."
                  rows={invoices.map(i => [
                    <span style={{ fontFamily: T.mono, fontSize: 12 }}>{i.invoiceNo}</span>, i.customerName, fmt.date(i.invoiceDate),
                    fmt.kes(i.subtotal), fmt.kes(i.vatAmount), <strong>{fmt.kes(i.total)}</strong>,
                    i.etimsReference ? <Badge variant={ETIMS_VARIANT[i.etimsStatus] || 'default'}>{i.etimsStatus}</Badge> : <Badge variant="default">Not submitted</Badge>,
                  ])} />
              </Card>
            )}
          </>
        )}

        {tab === 'vat' && (
          <>
            <Alert type="info"><strong>VAT:</strong> 16% standard rate, due 20th of the following month. Net VAT = Output VAT (sales) − Input VAT (purchases). Output is auto-computed from issued invoices; input comes from AP (coming soon).</Alert>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 20 }}>
              <Card>
                <SectionHeader title="Compute VAT Return" action={
                  <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                    <select value={periodId} onChange={e => { setPeriodId(e.target.value); setVat(null) }}
                      style={{ padding: '7px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
                      {periods.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                    </select>
                    <Btn onClick={compute} disabled={computing || !periodId}>{computing ? 'Computing…' : 'Compute'}</Btn>
                  </div>
                } />
                {vat && (
                  <>
                    <VatLine label="Output VAT (Sales)" value={<span style={{ color: T.red }}>{fmt.kes(vat.outputVat)}</span>} />
                    <VatLine label="Input VAT (Purchases)" value={<span style={{ color: T.green }}>{fmt.kes(vat.inputVat)}</span>} />
                    <VatLine label="Net VAT Payable" value={<span style={{ color: T.navy }}>{fmt.kes(vat.netVatPayable)}</span>} bold />
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginTop: 12 }}>
                      <Btn onClick={file} disabled={vat.status === 'Filed'}>{vat.status === 'Filed' ? 'Filed ✓' : 'File on KRA iTax'}</Btn>
                      <Badge variant={vat.status === 'Filed' ? 'green' : 'amber'}>{vat.status}</Badge>
                    </div>
                  </>
                )}
              </Card>
              <Card>
                <SectionHeader title="Kenya VAT Categories" />
                {[['A', 'Standard Rate', '16%', 'red'], ['B', 'Zero Rated — Exports', '0%', 'green'], ['C', 'Zero Rated — Other', '0%', 'green'], ['E', 'Exempt', 'N/A', 'default']].map(([code, name, rate, v]) => (
                  <div key={code} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '11px 0', borderBottom: `1px solid ${T.lgrey}` }}>
                    <span style={{ fontSize: 13 }}><strong style={{ color: T.navy }}>{code}</strong>&nbsp;&nbsp;{name}</span>
                    <Badge variant={v}>{rate}</Badge>
                  </div>
                ))}
              </Card>
            </div>
          </>
        )}

        {tab === 'paye' && (
          <Card>
            <SectionHeader title="PAYE Bands — Kenya 2024/2025" sub="Source: Kenya Revenue Authority" />
            <div style={{ overflowX: 'auto', marginBottom: 18 }}>
              <DataTable headers={['Income Band (Monthly)', 'Tax Rate', 'Notes']}
                rows={PAYE_BANDS.map(([band, rate, note]) => [band, <strong style={{ color: T.red }}>{rate}</strong>, <span style={{ color: T.mgrey }}>{note}</span>])} />
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 12 }}>
              {PAYE_RATES.map(([l, v]) => (
                <div key={l} style={{ background: T.offwt, borderRadius: 8, padding: '12px 14px' }}>
                  <div style={{ fontSize: 11, color: T.mgrey, marginBottom: 3 }}>{l}</div>
                  <div style={{ fontSize: 14, fontWeight: 700, color: T.navy }}>{v}</div>
                </div>
              ))}
            </div>
          </Card>
        )}

        {tab === 'calendar' && (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable headers={['Obligation', 'Agency', 'Due', 'Frequency', 'Penalty for Late Filing']}
              rows={OBLIGATIONS.map(([o, ag, day, freq]) => [
                <strong>{o}</strong>, ag, freq === 'annual' ? 'Annually' : day, <span style={{ color: T.mgrey }}>{freq}</span>,
                <span style={{ color: T.amber }}>5% of tax due + 2% p.m. interest</span>,
              ])} />
          </Card>
        )}
      </div>
    </>
  )
}

function VatLine({ label, value, bold }) {
  return <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '12px 0', borderBottom: `1px solid ${T.lgrey}`, fontSize: bold ? 15 : 14, fontWeight: 700 }}><span style={{ color: T.navy }}>{label}</span><strong>{value}</strong></div>
}
