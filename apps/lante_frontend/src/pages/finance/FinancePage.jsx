import { useState } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, Tabs, SectionHeader, DataTable, Modal, Input, Select } from '../../components/ui.jsx'
import CoaTab from '../../components/finance/CoaTab.jsx'
import JournalsTab from '../../components/finance/JournalsTab.jsx'
import TrialBalanceTab from '../../components/finance/TrialBalanceTab.jsx'
import BalanceSheetTab from '../../components/finance/BalanceSheetTab.jsx'
import InvoicesTab from '../../components/finance/InvoicesTab.jsx'
import PaymentsTab from '../../components/finance/PaymentsTab.jsx'
import BudgetsTab from '../../components/finance/BudgetsTab.jsx'
import MonthEndTab from '../../components/finance/MonthEndTab.jsx'
import CashFlowTab from '../../components/finance/CashFlowTab.jsx'
import ImprestTab from '../../components/finance/ImprestTab.jsx'
import BankRecTab from '../../components/finance/BankRecTab.jsx'
import TreasuryTab from '../../components/finance/TreasuryTab.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// Finance — full port of the deployed QSL Finance module (13 tabs). UI-only shell:
// data-heavy tabs (Payroll, Chart of Accounts, Cash Flow, Treasury, Payment
// Authority) carry MOCK data matching the deployed screenshots; transactional
// tabs start empty and modals add local rows. Wire to /api/finance later.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'
const CLIENTS = [
  { value: '', label: 'Select…' },
  { value: 'coast', label: 'Coast Water Works Dev Agency' },
  { value: 'kplc', label: 'Kenya Power & Lighting Co.' },
  { value: 'bamburi', label: 'Bamburi Cement Ltd' },
]
const DEPTS = ['Executive', 'Finance', 'Projects', 'HR', 'Engineering', 'BD', 'ICT']
const clientLabel = (v) => CLIENTS.find(c => c.value === v)?.label || '—'
const rid = (p) => `${p}-${String(Math.floor((typeof performance !== 'undefined' ? performance.now() : 0) % 9000) + 1000)}`

const TABS = [
  { id: 'imprest', label: 'Imprest Tracker' }, { id: 'documents', label: 'Documents' },
  { id: 'payroll', label: 'Payroll' }, { id: 'coa', label: 'Chart of Accounts' },
  { id: 'journals', label: 'Journals' }, { id: 'monthend', label: 'Month-End & P&L' },
  { id: 'tb', label: 'Trial Balance & Balance Sheet' }, { id: 'bankrec', label: 'Bank Reconciliation' },
  { id: 'cashflow', label: 'Cash Flow' }, { id: 'budgets', label: 'Budgets' },
  { id: 'payments', label: 'Payments (AP)' }, { id: 'treasury', label: 'Treasury & Statutory' },
  { id: 'payauth', label: 'Payment Authority' },
]
const DOC_TABS = [
  { id: 'quotations', label: 'Quotations' }, { id: 'invoices', label: 'Invoices' },
  { id: 'debit', label: 'Debit Notes' }, { id: 'credit', label: 'Credit Notes' }, { id: 'travel', label: 'Travel Claims' },
]

// ── Mock data (matches deployed) ──────────────────────────────────────────────
const PAYROLL_ROWS = [
  ['Henry Adar', 'Executive', 450000, 450000, 127383, 2160, 1700, 312007],
  ['Sarah Kamau', 'Finance', 280000, 280000, 76383, 2160, 1700, 195557],
  ['James Otieno', 'Projects', 260000, 260000, 70383, 2160, 1700, 181857],
  ['Grace Wanjiku', 'HR', 220000, 220000, 58383, 2160, 1700, 154457],
  ['David Mwangi', 'Engineering', 240000, 240000, 64383, 2160, 1700, 168157],
  ['Faith Njeri', 'BD', 200000, 200000, 52383, 2160, 1700, 140757],
  ['Paul Ochieng', 'ICT', 230000, 230000, 61383, 2160, 1700, 161307],
]
const PAY_AUTH = [
  ['Staff', 'Kshs 5,000', 'staff', 'default'], ['Department Head', 'Kshs 20,000', 'dept head', 'blue'],
  ['Finance Manager', 'Kshs 100,000', 'finance manager', 'navy'], ['CFO', 'Kshs 500,000', 'cfo', 'amber'],
  ['Managing Director', 'No limit (top authority)', 'md', 'red'],
]

export default function FinancePage() {
  const [tab, setTab] = useState('imprest')
  const [docTab, setDocTab] = useState('quotations')
  const [tbTab, setTbTab] = useState('tb')
  const [msg, setMsg] = useState(null)
  const [modal, setModal] = useState(null)

  const [quotes, setQuotes] = useState([])
  const [debit, setDebit] = useState([])
  const [credit, setCredit] = useState([])
  const [travel, setTravel] = useState([])

  const notify = (text, type = 'success') => setMsg({ type, text })

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {msg && <Alert type={msg.type}>{msg.text}</Alert>}
        <Tabs tabs={TABS} active={tab} setActive={setTab} />

        {tab === 'imprest' && <ImprestTab notify={notify} />}

        {tab === 'documents' && (
          <>
            <Tabs tabs={DOC_TABS} active={docTab} setActive={setDocTab} />
            {docTab === 'quotations' && <>
              <SectionHeader title="Quotations" action={<Btn onClick={() => setModal('quote')}>+ New Quote</Btn>} />
              <TableCard headers={['Quote No', 'Client', 'Subtotal', 'VAT', 'Total', 'Status', 'Date']} empty="No records found."
                rows={quotes.map(q => [q.no, q.client, fmt.kes(q.subtotal), fmt.kes(q.vat), <strong>{fmt.kes(q.total)}</strong>, <Badge variant="amber">draft</Badge>, fmt.date(q.date)])} />
            </>}
            {docTab === 'invoices' && <InvoicesTab notify={notify} />}
            {docTab === 'debit' && <>
              <SectionHeader title="Debit Notes" action={<Btn onClick={() => setModal('debit')}>+ New Debit Note</Btn>} />
              <TableCard headers={['Note No', 'Client', 'Amount', 'Reason', 'Date']} empty="No records found."
                rows={debit.map(n => [n.no, n.client, <strong>{fmt.kes(n.amount)}</strong>, n.reason, fmt.date(n.date)])} />
            </>}
            {docTab === 'credit' && <>
              <SectionHeader title="Credit Notes" action={<Btn onClick={() => setModal('credit')}>+ New Credit Note</Btn>} />
              <TableCard headers={['Note No', 'Client', 'Amount', 'Reason', 'Date']} empty="No records found."
                rows={credit.map(n => [n.no, n.client, <strong>{fmt.kes(n.amount)}</strong>, n.reason, fmt.date(n.date)])} />
            </>}
            {docTab === 'travel' && <>
              <SectionHeader title="Travel Claims" action={<Btn onClick={() => setModal('travel')}>+ New Claim</Btn>} />
              <TableCard headers={['Claim No', 'Employee', 'Purpose', 'Total', 'Status', 'Action']} empty="No records found."
                rows={travel.map(c => [c.no, c.employee, c.purpose, <strong>{fmt.kes(c.total)}</strong>, <Badge variant="amber">pending</Badge>, <Btn size="sm" variant="ghost">View</Btn>])} />
            </>}
          </>
        )}

        {tab === 'payroll' && <PayrollTab />}
        {tab === 'coa' && <CoaTab />}

        {tab === 'journals' && <JournalsTab notify={notify} />}

        {tab === 'monthend' && <MonthEndTab notify={notify} />}

        {tab === 'tb' && <>
          <Tabs tabs={[{ id: 'tb', label: 'Trial Balance' }, { id: 'bs', label: 'Balance Sheet' }]} active={tbTab} setActive={setTbTab} />
          {tbTab === 'tb' ? <TrialBalanceTab /> : <BalanceSheetTab />}
        </>}

        {tab === 'bankrec' && <BankRecTab notify={notify} />}

        {tab === 'cashflow' && <CashFlowTab notify={notify} />}

        {tab === 'budgets' && <BudgetsTab notify={notify} />}

        {tab === 'payments' && <PaymentsTab notify={notify} />}

        {tab === 'treasury' && <TreasuryTab notify={notify} />}

        {tab === 'payauth' && <PaymentAuthorityTab />}

        {/* ── Modals ── */}
        {modal === 'quote' && <QuoteModal onClose={() => setModal(null)} onSave={q => { setQuotes(x => [q, ...x]); setModal(null); notify(`Quote ${q.no} created.`) }} />}
        {modal === 'debit' && <NoteModal title="New Debit Note" onClose={() => setModal(null)} onSave={n => { setDebit(x => [n, ...x]); setModal(null); notify('Debit note created.') }} />}
        {modal === 'credit' && <NoteModal title="New Credit Note" onClose={() => setModal(null)} onSave={n => { setCredit(x => [n, ...x]); setModal(null); notify('Credit note created.') }} />}
        {modal === 'travel' && <TravelClaimModal onClose={() => setModal(null)} onSave={c => { setTravel(x => [c, ...x]); setModal(null); notify(`Claim ${c.no} submitted.`) }} />}
      </div>
    </>
  )
}

// ── Small shared helpers ──────────────────────────────────────────────────────
function FinBanner({ type, children }) {
  const bg = type === 'warning' ? T.amberL : T.blueL
  const border = type === 'warning' ? '#FCD34D' : '#BFDBFE'
  return <div style={{ background: bg, border: `1px solid ${border}`, borderRadius: 10, padding: '12px 16px', marginBottom: 18, fontSize: 13, color: T.dgrey, display: 'flex', gap: 8 }}><span>{type === 'warning' ? '⚠️' : 'ℹ️'}</span><span>{children}</span></div>
}
function TableCard({ headers, rows, empty }) {
  return <Card style={{ padding: 0, overflow: 'hidden' }}><DataTable headers={headers} rows={rows} empty={empty} /></Card>
}

// ── Payroll ─────────────────────────────────────────────────────────────────
function PayrollTab() {
  const steps = [['1', 'FM Review', '← Click to sign'], ['2', 'CFO Sign', ''], ['3', 'MD Approve', ''], ['4', 'Locked ✅', '']]
  return (
    <>
      <FinBanner type="info"><strong style={{ color: T.blue }}>HR-010:</strong> Payroll requires 3-step digital signature approval: Finance Manager → CFO → MD. All three must sign before payroll can be processed.</FinBanner>
      <Card style={{ marginBottom: 16 }}>
        <div style={{ fontSize: 13, fontWeight: 700, color: T.navy, marginBottom: 22 }}>Approval Workflow — 2026-06</div>
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: 4 }}>
          {steps.map(([n, label, hint], i) => (
            <div key={n} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', position: 'relative' }}>
              {i < 3 && <div style={{ position: 'absolute', top: 15, left: '50%', width: '100%', height: 2, background: T.lgrey }} />}
              <div style={{ width: 32, height: 32, borderRadius: '50%', background: i === 0 ? T.gold : T.lgrey, color: i === 0 ? '#fff' : T.mgrey, display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 700, fontSize: 13, position: 'relative', zIndex: 1 }}>{n}</div>
              <div style={{ fontSize: 12, fontWeight: 600, color: i === 0 ? T.navy : T.mgrey, marginTop: 8, textAlign: 'center' }}>{label}</div>
              {hint && <div style={{ fontSize: 11, color: T.gold, marginTop: 2 }}>{hint}</div>}
            </div>
          ))}
        </div>
      </Card>
      <FinBanner type="info"><strong style={{ color: T.blue }}>HR-008</strong> — Cut-off: <strong>20 Jun 2026</strong> · Pay date (last working day): <strong>30 Jun 2026</strong></FinBanner>
      <div style={{ ...KPI_GRID, marginBottom: 22 }}>
        <Kpi label="Total Gross" value="Kshs 4,095,000" icon="💰" />
        <Kpi label="Total PAYE" value="Kshs 1,060,926" icon="🏛️" variant="red" />
        <Kpi label="Total Deductions" value="Kshs 1,207,271" icon="➖" variant="amber" />
        <Kpi label="Total Net Pay" value="Kshs 2,887,729" icon="✅" variant="green" />
      </div>
      <SectionHeader title="Payroll Register" sub="HR-011 bank payment file export"
        action={<div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>{['KCB', 'EQUITY', 'NCBA', 'CO-OP', 'ALL'].map(b => <Btn key={b} size="sm" variant="ghost">⬇ {b}</Btn>)}</div>} />
      <TableCard headers={['Employee', 'Dept', 'Basic', 'Overtime', 'Gross', 'PAYE', 'NSSF', 'SHA', 'Net Pay', '']}
        rows={PAYROLL_ROWS.map(r => [<strong>{r[0]}</strong>, <Badge variant="navy">{r[1]}</Badge>, fmt.kes(r[2]), '—', fmt.kes(r[3]), <span style={{ color: T.red }}>{fmt.kes(r[4])}</span>, fmt.kes(r[5]), fmt.kes(r[6]), <strong style={{ color: T.green }}>{fmt.kes(r[7])}</strong>, <Btn size="sm" variant="ghost">Payslip</Btn>])} />
    </>
  )
}

// ── Chart of Accounts ─────────────────────────────────────────────────────────
// ── Trial Balance & Balance Sheet ─────────────────────────────────────────────

// ── Payment Authority ─────────────────────────────────────────────────────────
function PaymentAuthorityTab() {
  return (
    <>
      <FinBanner type="info"><strong style={{ color: T.blue }}>LT-FIN-007 — Payment Authority Matrix:</strong> the system enforces these limits on every payment voucher. A payment above a role's limit is refused with an escalation message — no overrides. Edit the limits in Administration → System Settings → Finance.</FinBanner>
      <TableCard headers={['Authorisation Level', 'Payment Limit (s)', 'Role']}
        rows={PAY_AUTH.map(([level, limit, role, variant]) => [<strong>{level}</strong>, <span style={{ color: T.gold, fontWeight: 700, fontFamily: T.mono }}>{limit}</span>, <Badge variant={variant}>{role}</Badge>])} />
    </>
  )
}

// ── Modals ──────────────────────────────────────────────────────────────────
function LineItems({ lines, setLines }) {
  return (<>
    {lines.map((l, i) => (
      <div key={i} style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: 8 }}>
        <Input label={i === 0 ? 'Description' : ''} value={l.description} onChange={v => { const n = [...lines]; n[i] = { ...l, description: v }; setLines(n) }} />
        <Input label={i === 0 ? 'Qty' : ''} type="number" value={l.qty} onChange={v => { const n = [...lines]; n[i] = { ...l, qty: v }; setLines(n) }} />
        <Input label={i === 0 ? 'Unit Price' : ''} type="number" value={l.price} onChange={v => { const n = [...lines]; n[i] = { ...l, price: v }; setLines(n) }} />
      </div>
    ))}
    <Btn size="sm" variant="ghost" onClick={() => setLines([...lines, { description: '', qty: 1, price: '' }])}>+ Add Line</Btn>
  </>)
}
const linesTotal = (lines) => lines.reduce((s, l) => s + (+l.qty || 0) * (+l.price || 0), 0)
function ModalActions({ onClose, onSave, disabled, label }) {
  return <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn disabled={disabled} onClick={onSave}>{label}</Btn></div>
}
function QuoteModal({ onClose, onSave }) {
  const [client, setClient] = useState(''); const [valid, setValid] = useState(''); const [lines, setLines] = useState([{ description: '', qty: 1, price: '' }])
  const subtotal = linesTotal(lines), vat = subtotal * 0.16
  return <Modal title="New Quotation" onClose={onClose} width={560}>
    <Select label="Client" value={client} onChange={setClient} options={CLIENTS} required />
    <Input label="Valid Until" type="date" value={valid} onChange={setValid} />
    <LineItems lines={lines} setLines={setLines} />
    <ModalActions onClose={onClose} disabled={!client} onSave={() => onSave({ no: rid('QT'), client: clientLabel(client), subtotal, vat, total: subtotal + vat, date: valid })} label="Create Quote" />
  </Modal>
}
function NoteModal({ title, onClose, onSave }) {
  const [f, setF] = useState({ client: '', amount: '', reason: '' })
  return <Modal title={title} onClose={onClose} width={460}>
    <Select label="Client" value={f.client} onChange={v => setF({ ...f, client: v })} options={CLIENTS} required />
    <Input label="Amount (Kshs)" type="number" value={f.amount} onChange={v => setF({ ...f, amount: v })} required />
    <Input label="Reason" value={f.reason} onChange={v => setF({ ...f, reason: v })} required />
    <ModalActions onClose={onClose} disabled={!f.client || !f.amount || !f.reason} onSave={() => onSave({ no: rid('NOTE'), client: clientLabel(f.client), amount: +f.amount, reason: f.reason, date: '' })} label="Create" />
  </Modal>
}
function TravelClaimModal({ onClose, onSave }) {
  const [f, setF] = useState({ purpose: '', amount: '' })
  return <Modal title="New Travel Claim" onClose={onClose} width={460}>
    <Input label="Purpose" value={f.purpose} onChange={v => setF({ ...f, purpose: v })} required />
    <Input label="Total (Kshs)" type="number" value={f.amount} onChange={v => setF({ ...f, amount: v })} required />
    <ModalActions onClose={onClose} disabled={!f.purpose || !f.amount} onSave={() => onSave({ no: rid('TC'), employee: 'Henry Adar', purpose: f.purpose, total: +f.amount })} label="Submit" />
  </Modal>
}
