import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, SectionHeader, DataTable, Loading, Progress, Modal, Input } from '../ui.jsx'
import { getFiscalYears, listBudgets, createBudget, listRevenueTargets, createRevenueTarget } from '../../services/finance.js'

const BUDGET_VARIANT = { OnTrack: 'green', Warning: 'amber', Over: 'red' }
const TARGET_VARIANT = { Achieved: 'green', OnTrack: 'blue', Behind: 'amber' }

export default function BudgetsTab({ notify }) {
  const [years, setYears] = useState([])
  const [fyId, setFyId] = useState('')
  const [budgets, setBudgets] = useState([])
  const [targets, setTargets] = useState([])
  const [loading, setLoading] = useState(true)
  const [modal, setModal] = useState(null)   // 'budget' | 'target'

  useEffect(() => {
    getFiscalYears().then(ys => { setYears(ys ?? []); setFyId((ys ?? [])[0]?.id ?? '') })
      .catch(() => notify?.('Failed to load fiscal years.', 'error'))
  }, [notify])

  const load = useCallback(() => {
    if (!fyId) return
    setLoading(true)
    return Promise.all([listBudgets(fyId), listRevenueTargets(fyId)])
      .then(([b, t]) => { setBudgets(b ?? []); setTargets(t ?? []) })
      .catch(() => notify?.('Failed to load budgets.', 'error'))
      .finally(() => setLoading(false))
  }, [fyId, notify])
  useEffect(() => { load() }, [load])

  return (
    <>
      <Alert type="info"><strong style={{ color: T.blue }}>FIN-019/020/021:</strong> annual department budgets &amp; revenue targets vs actuals from posted journals. Budgets flag <strong>amber at 80%</strong> and <strong>red at 100%</strong> consumed.</Alert>
      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'center', gap: 10 }}>
        <span style={{ fontSize: 13, fontWeight: 600, color: T.dgrey }}>Fiscal Year:</span>
        <select value={fyId} onChange={e => setFyId(e.target.value)} style={{ padding: '7px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          {years.map(y => <option key={y.id} value={y.id}>{y.name}</option>)}
        </select>
      </div>

      {loading ? <Loading /> : (
        <>
          <SectionHeader title="Department Budgets vs Actual" sub="Actual expense from posted journals" action={<Btn onClick={() => setModal('budget')}>+ Budget</Btn>} />
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable headers={['Department', 'Cost Centre', 'Annual Budget', 'Actual', 'Consumed', 'Variance', 'Status']}
              empty="No budgets for this year — add one above."
              rows={budgets.map(b => [
                <strong>{b.departmentName}</strong>, b.costCentreLabel || '—',
                fmt.kes(b.annualAmount), fmt.kes(b.actual),
                <div style={{ minWidth: 120 }}>
                  <Progress value={b.consumedPct / 100} />
                  <div style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{b.consumedPct}%</div>
                </div>,
                <span style={{ color: b.variance < 0 ? T.red : T.dgrey }}>{fmt.kes(b.variance)}</span>,
                <Badge variant={BUDGET_VARIANT[b.status] || 'default'}>{b.status}</Badge>,
              ])} />
          </Card>

          <div style={{ height: 22 }} />
          <SectionHeader title="Revenue vs Target" sub="Actual income from posted journals" action={<Btn onClick={() => setModal('target')}>+ Target</Btn>} />
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable headers={['Scope', 'Annual Target', 'Actual', 'Achieved', 'Variance', 'Status']}
              empty="No revenue targets for this year."
              rows={targets.map(t => [
                <strong>{t.scope}</strong>, fmt.kes(t.annualAmount), fmt.kes(t.actual),
                <span style={{ fontWeight: 700, color: t.achievedPct >= 100 ? T.green : T.navy }}>{t.achievedPct}%</span>,
                <span style={{ color: t.variance < 0 ? T.amber : T.green }}>{fmt.kes(t.variance)}</span>,
                <Badge variant={TARGET_VARIANT[t.status] || 'default'}>{t.status}</Badge>,
              ])} />
          </Card>
        </>
      )}

      {modal === 'budget' && <BudgetModal fyId={fyId} onClose={() => setModal(null)} onSaved={load} notify={notify} />}
      {modal === 'target' && <TargetModal fyId={fyId} onClose={() => setModal(null)} onSaved={load} notify={notify} />}
    </>
  )
}

function BudgetModal({ fyId, onClose, onSaved, notify }) {
  const [f, setF] = useState({ department: '', costCentre: '', amount: '' })
  const [saving, setSaving] = useState(false)
  async function save() {
    setSaving(true)
    try {
      await createBudget({ fiscalYearId: fyId, departmentName: f.department, costCentreLabel: f.costCentre || null, annualAmount: +f.amount || 0 })
      notify?.('Budget saved.'); onSaved(); onClose()
    } catch (e) { notify?.(e.response?.data?.message ?? 'Failed to save budget.', 'error'); setSaving(false) }
  }
  return (
    <Modal title="Department Budget" onClose={onClose} width={460}>
      <Input label="Department" value={f.department} onChange={v => setF({ ...f, department: v })} required />
      <Input label="Cost Centre (optional)" value={f.costCentre} onChange={v => setF({ ...f, costCentre: v })} />
      <Input label="Annual Budget (Kshs)" type="number" value={f.amount} onChange={v => setF({ ...f, amount: v })} required />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={saving || !f.department || !(+f.amount > 0)}>Save Budget</Btn>
      </div>
    </Modal>
  )
}

function TargetModal({ fyId, onClose, onSaved, notify }) {
  const [f, setF] = useState({ scope: '', amount: '' })
  const [saving, setSaving] = useState(false)
  async function save() {
    setSaving(true)
    try {
      await createRevenueTarget({ fiscalYearId: fyId, scope: f.scope, annualAmount: +f.amount || 0 })
      notify?.('Revenue target saved.'); onSaved(); onClose()
    } catch (e) { notify?.(e.response?.data?.message ?? 'Failed to save target.', 'error'); setSaving(false) }
  }
  return (
    <Modal title="Revenue Target" onClose={onClose} width={460}>
      <Input label="Scope" value={f.scope} onChange={v => setF({ ...f, scope: v })} required placeholder="e.g. Company-wide / Calibration" />
      <Input label="Annual Target (Kshs)" type="number" value={f.amount} onChange={v => setF({ ...f, amount: v })} required />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={saving || !f.scope || !(+f.amount > 0)}>Save Target</Btn>
      </div>
    </Modal>
  )
}
