import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Modal, Input, Select, Loading, Alert } from '../../components/ui.jsx'
import * as hr from '../../services/hr.js'

// ─────────────────────────────────────────────────────────────────────────────
// H5 — pay configuration (P7 + P8). REAL, wired to hr-service
// (/api/v1/hr/payroll/*).
//
// H5 CONFIGURES, H6 COMPUTES. Nothing on these screens works out what anyone is
// actually paid — that is the payroll run. What these tabs produce is the rule
// set H6 runs on, and the Setup tab's blockers list is the honest measure of
// whether that rule set is finished.
//
// Two things the server does that the UI must not second-guess:
//   • Seeded PAYE bands and statutory rates arrive flagged "unconfirmed". Tax
//     law moves every Finance Act, so a seeded number nobody has checked is
//     shown as a warning, not as settled fact, until someone with approval
//     rights confirms it.
//   • A salary proposal cannot be approved by the person who proposed it. The
//     server enforces that; the UI just relays the refusal.
//
// Every payroll endpoint sits behind hr.payroll.read/write/approve, NOT the
// general hr.* tier — pay must not be visible to everyone who can read an
// employee record. A user without that tier gets a 403, which these tabs render
// as an explicit "no access" panel rather than a misleading empty table.
// ─────────────────────────────────────────────────────────────────────────────

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const money = (n, ccy = 'KES') => (n == null ? '—' : `${ccy} ${Number(n).toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`)
const num = (n) => (n == null ? '—' : Number(n).toLocaleString('en-KE'))

const STATUS_VARIANT = { Proposed: 'amber', Approved: 'green', Rejected: 'red', Superseded: 'default' }
const PERIOD_VARIANT = { Open: 'green', Locked: 'amber', Closed: 'default' }
const CATEGORY_VARIANT = { Statutory: 'blue', Loan: 'amber', Voluntary: 'default', CourtOrder: 'red', Advance: 'amber' }

const CALC_TYPES = ['FixedAmount', 'PercentOfBasic', 'PercentOfGross', 'Statutory', 'VariableInput']
const STATUTORY_RULES = ['None', 'Paye', 'Nssf', 'Sha', 'HousingLevy', 'Helb']
const RATE_TYPES = ['PercentOfGross', 'TieredPercent', 'FixedAmount', 'PerEmployeeAmount']
const CATEGORIES = ['Statutory', 'Loan', 'Voluntary', 'CourtOrder', 'Advance']

function Kpi({ label, value, sub, color }) {
  return (
    <Card style={{ padding: 14 }}>
      <p style={{ fontSize: 22, fontWeight: 800, color: color ?? T.navy, margin: 0 }}>{value}</p>
      <p style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .4, marginTop: 3 }}>{label}</p>
      {sub && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{sub}</p>}
    </Card>
  )
}

// Payroll is permission-gated separately from the rest of HR, so a 403 here is a
// normal state for most staff, not an error worth alarming them about.
function NoAccess() {
  return (
    <Card style={{ padding: 28, textAlign: 'center' }}>
      <p style={{ fontSize: 32, margin: 0 }}>🔒</p>
      <p style={{ fontWeight: 700, color: T.navy, marginTop: 10 }}>Payroll data is restricted</p>
      <p style={{ fontSize: 13, color: T.mgrey, marginTop: 6, maxWidth: 460, marginInline: 'auto' }}>
        Salary structures, pay assignments and statutory rates need the payroll permissions
        (<code>hr.payroll.read</code> and above), which are held separately from general HR access.
      </p>
    </Card>
  )
}

const isForbidden = (e) => e?.response?.status === 403

/** Relays an action result: its message plus every warning, because H5 says a
 *  great deal through warnings (out-of-band pay, unconfirmed rates, clamped
 *  one-off deductions) that would be lost if only the message were shown. A
 *  result carrying warnings is shown as a warning, not as a green tick. */
function relay(flash, r) {
  const warnings = r?.warnings ?? []
  const message = r?.message ?? 'Done.'
  if (warnings.length) flash(`${message} — ${warnings.join(' ')}`, 'warning')
  else flash(message)
}
const relayError = (flash, e, fallback) => flash(e.response?.data?.message ?? fallback, 'error')

/** Every payroll tab loads the same way: fetch, tolerate a 403, and stop. */
function usePayrollData(loader, deps = []) {
  const [state, setState] = useState({ loading: true, denied: false, data: null })
  const load = useCallback(() => {
    setState(s => ({ ...s, loading: true }))
    loader()
      .then(data => setState({ loading: false, denied: false, data }))
      .catch(e => setState({ loading: false, denied: isForbidden(e), data: null }))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps)
  useEffect(() => { load() }, [load])
  return { ...state, reload: load }
}

// ═════════════════════════════════════════════════════════════════════════════
// Setup — the readiness picture, the seeders and the payroll calendar
// ═════════════════════════════════════════════════════════════════════════════
export function PayrollSetupTab({ flash }) {
  const [year, setYear] = useState(new Date().getFullYear())
  const [genOpen, setGenOpen] = useState(false)
  const [busy, setBusy] = useState('')

  const summary = usePayrollData(() => hr.payrollSummary(), [])
  const periods = usePayrollData(() => hr.listPayrollPeriods(year), [year])

  const s = summary.data
  const reload = () => { summary.reload(); periods.reload() }

  const seed = async (key, fn, label) => {
    setBusy(key)
    try { relay(flash, await fn()); reload() }
    catch (e) { relayError(flash, e, `${label} failed.`) }
    finally { setBusy('') }
  }

  if (summary.loading) return <Loading />
  if (summary.denied) return <NoAccess />

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Salary Structures" value={s?.salaryStructures ?? 0} color={T.navy} sub={`${s?.activeComponents ?? 0} components`} />
        <Kpi label="On Approved Salary" value={s?.employeesOnApprovedSalary ?? 0} color={T.green} />
        <Kpi label="Awaiting Approval" value={s?.salariesAwaitingApproval ?? 0} color={T.amber} />
        <Kpi label="Monthly Basic" value={money(s?.approvedMonthlyBasicTotal)} color={T.navy} sub="approved assignments" />
        <Kpi label="PAYE Bands" value={s?.payeBandsInForce ?? 0} color={T.blue} sub="in force today" />
        <Kpi label="Unconfirmed Rates" value={s?.ratesNeedingConfirmation ?? 0} color={s?.ratesNeedingConfirmation ? T.red : T.green} sub="never checked" />
        <Kpi label="Active Deductions" value={s?.activeDeductions ?? 0} color={T.navy} sub={money(s?.activeDeductionMonthlyTotal)} />
        <Kpi label="Current Period" value={s?.currentPeriodCode ?? '—'} color={T.navy} sub={`${s?.openPeriods ?? 0} open`} />
      </div>

      {/* The blockers list is the point of this screen: it is the server's own
          answer to "could payroll run today", not a client-side guess. */}
      <Card style={{ padding: 16, marginBottom: 18, borderLeft: `4px solid ${s?.blockers?.length ? T.red : T.green}` }}>
        <p style={{ fontWeight: 700, color: T.navy, margin: 0 }}>
          {s?.blockers?.length ? `${s.blockers.length} thing(s) still block a payroll run` : 'Configuration is complete'}
        </p>
        {s?.blockers?.length
          ? (
            <ul style={{ margin: '10px 0 0', paddingLeft: 18, fontSize: 13, color: T.dgrey }}>
              {s.blockers.map((b, i) => <li key={i} style={{ marginBottom: 4 }}>{b}</li>)}
            </ul>
          )
          : <p style={{ fontSize: 13, color: T.mgrey, marginTop: 6 }}>Structures are GL-mapped, the rate tables are confirmed and every employee on the payroll has an approved salary.</p>}
      </Card>

      <SectionHeader
        title="Install the defaults"
        sub="Each of these is idempotent — running it twice changes nothing. The rate tables arrive flagged as unconfirmed on purpose."
      />
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 12, marginBottom: 22 }}>
        <SeedCard title="Salary structure" busy={busy === 'structure'}
          sub="Basic and house allowance plus the four statutory deduction lines, mapped to finance's GL accounts where they exist."
          onRun={() => seed('structure', hr.seedStructure, 'Seeding the structure')} />
        <SeedCard title="PAYE bands" busy={busy === 'paye'}
          sub="The progressive income-tax scale. Check every band against the current Finance Act before confirming it."
          onRun={() => seed('paye', hr.seedPayeBands, 'Seeding the PAYE bands')} />
        <SeedCard title="Statutory rates" busy={busy === 'rates'}
          sub="NSSF tiers, SHA, the Housing Levy, HELB and personal relief."
          onRun={() => seed('rates', hr.seedStatutoryRates, 'Seeding the statutory rates')} />
        <SeedCard title="Deduction catalogue" busy={busy === 'deductions'}
          sub="HELB, SACCO, staff loan, salary advance and court orders."
          onRun={() => seed('deductions', hr.seedDeductionTypes, 'Seeding the deduction types')} />
      </div>

      <SectionHeader
        title="Payroll calendar"
        sub="HR's own monthly periods, separate from finance's fiscal periods. A salary assignment and a deduction both start from one of these."
        action={<Btn size="sm" onClick={() => setGenOpen(true)}>Generate a year</Btn>}
      />
      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={year} onChange={e => setYear(Number(e.target.value))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          {[year - 1, year, year + 1, year + 2].filter((v, i, a) => a.indexOf(v) === i).map(y => <option key={y} value={y}>{y}</option>)}
        </select>
      </div>

      {periods.loading ? <Loading /> : (
        <DataTable
          headers={['Period', 'Runs', 'Cut-off', 'Payment', 'Status', '']}
          empty={`No payroll periods exist for ${year}.`}
          rows={(periods.data ?? []).map(p => [
            <span style={{ fontFamily: 'monospace', fontWeight: 700 }}>{p.code}</span>,
            `${fmtDate(p.startDate)} – ${fmtDate(p.endDate)}`,
            fmtDate(p.cutOffDate),
            fmtDate(p.paymentDate),
            <Badge variant={PERIOD_VARIANT[p.status] ?? 'default'}>{p.status}</Badge>,
            p.isCurrent ? <Badge variant="blue">current</Badge> : '',
          ])}
        />
      )}

      {genOpen && <GeneratePeriodsModal defaultYear={year} flash={flash}
        onClose={() => setGenOpen(false)} onSaved={() => { setGenOpen(false); reload() }} />}
    </div>
  )
}

function SeedCard({ title, sub, busy, onRun }) {
  return (
    <Card style={{ padding: 14 }}>
      <p style={{ fontWeight: 700, color: T.navy, margin: 0, fontSize: 14 }}>{title}</p>
      <p style={{ fontSize: 12, color: T.mgrey, margin: '6px 0 12px', lineHeight: 1.45 }}>{sub}</p>
      <Btn size="sm" variant="outline" onClick={onRun} disabled={busy}>{busy ? 'Installing…' : 'Install'}</Btn>
    </Card>
  )
}

function GeneratePeriodsModal({ defaultYear, flash, onClose, onSaved }) {
  const [f, setF] = useState({ year: String(defaultYear), cutOffDay: '20', paymentDay: '28' })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.generatePeriods({
        year: Number(f.year),
        cutOffDay: f.cutOffDay === '' ? null : Number(f.cutOffDay),
        paymentDay: f.paymentDay === '' ? null : Number(f.paymentDay),
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not generate the periods.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Generate payroll periods" onClose={onClose}>
      <p style={{ fontSize: 12, color: T.mgrey, marginTop: 0 }}>
        Creates the twelve monthly periods for a year. Existing periods are left alone, so this is safe to re-run.
        Both days are clamped to the length of each month — a 31st cut-off still lands on the 28th in February.
      </p>
      <Input label="Year" type="number" value={f.year} onChange={v => setF({ ...f, year: v })} required />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Cut-off day" type="number" value={f.cutOffDay} onChange={v => setF({ ...f, cutOffDay: v })}
          note="Changes after this belong to the next period" />
        <Input label="Payment day" type="number" value={f.paymentDay} onChange={v => setF({ ...f, paymentDay: v })} />
      </div>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.year}>{busy ? 'Generating…' : 'Generate'}</Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Salary structures — grades, structures, components and their GL mapping
// ═════════════════════════════════════════════════════════════════════════════
export function SalaryStructuresTab({ flash }) {
  const [gradeModal, setGradeModal] = useState(null)
  const [structModal, setStructModal] = useState(null)
  const [compModal, setCompModal] = useState(null)
  const [mapping, setMapping] = useState(null)

  const grades = usePayrollData(() => hr.listJobGrades(true), [])
  const structures = usePayrollData(() => hr.listStructures(true), [])

  if (structures.loading) return <Loading />
  if (structures.denied) return <NoAccess />

  const reload = () => { grades.reload(); structures.reload() }

  return (
    <div>
      <SectionHeader
        title="Job Grades"
        sub="A grade says what a band pays; a position says what someone does. Pay outside a grade's band is warned about, never blocked — it is a real decision HR and the MD make deliberately."
        action={<Btn size="sm" onClick={() => setGradeModal({})}>+ Grade</Btn>}
      />
      <DataTable
        headers={['Code', 'Grade', 'Band', 'Structures', 'Status', 'Actions']}
        empty="No job grades configured."
        rows={(grades.data ?? []).map(g => [
          <span style={{ fontFamily: 'monospace', fontWeight: 700 }}>{g.code}</span>,
          g.name,
          g.minSalary == null && g.maxSalary == null
            ? <span style={{ color: T.mgrey }}>no band</span>
            : `${num(g.minSalary)} – ${g.maxSalary == null ? 'no ceiling' : num(g.maxSalary)}`,
          g.structureCount,
          <Badge variant={g.isActive ? 'green' : 'default'}>{g.isActive ? 'Active' : 'Inactive'}</Badge>,
          <Btn size="sm" variant="outline" onClick={() => setGradeModal(g)}>Edit</Btn>,
        ])}
      />

      <div style={{ marginTop: 26 }}>
        <SectionHeader
          title="Salary Structures"
          sub="Each component carries the GL account its payroll line posts to. A component with no account is what stops H6 building a journal, so it is called out here."
          action={<Btn size="sm" onClick={() => setStructModal({})}>+ Structure</Btn>}
        />
      </div>

      {(structures.data ?? []).length === 0 && (
        <Alert type="info">No salary structures yet. The Setup tab can install a standard one with its statutory lines already GL-mapped.</Alert>
      )}

      {(structures.data ?? []).map(st => (
        <Card key={st.id} style={{ padding: 16, marginBottom: 16 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap' }}>
            <div>
              <p style={{ fontWeight: 700, color: T.navy, margin: 0, fontSize: 15 }}>
                {st.name} <span style={{ fontWeight: 500, color: T.mgrey, fontSize: 12 }}>{st.currencyCode}</span>
                {!st.isActive && <Badge variant="default">Inactive</Badge>}
              </p>
              <p style={{ fontSize: 12, color: T.mgrey, margin: '4px 0 0' }}>
                {st.description ?? 'No description.'}
                {st.jobGradeCode && ` · grade ${st.jobGradeCode}`}
                {` · ${st.assignedEmployees} employee(s) paid on it`}
              </p>
              {st.componentsWithoutGlAccount > 0 && (
                <p style={{ fontSize: 12, color: T.red, fontWeight: 600, margin: '6px 0 0' }}>
                  {st.componentsWithoutGlAccount} component(s) have no GL account — payroll cannot post them.
                </p>
              )}
            </div>
            <div style={{ display: 'flex', gap: 8 }}>
              <Btn size="sm" variant="outline" onClick={() => setStructModal(st)}>Edit</Btn>
              <Btn size="sm" onClick={() => setCompModal({ structureId: st.id })}>+ Component</Btn>
            </div>
          </div>

          <div style={{ marginTop: 14 }}>
            <DataTable
              headers={['Order', 'Code', 'Component', 'Type', 'Basis', 'Taxable', 'GL Account', 'Actions']}
              empty="No components on this structure yet."
              rows={(st.components ?? []).map(c => [
                c.componentOrder,
                <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{c.code}</span>,
                <span style={{ opacity: c.isActive ? 1 : .5 }}>{c.name}{!c.isActive && ' (inactive)'}</span>,
                <Badge variant={c.componentType === 'Earning' ? 'green' : 'amber'}>{c.componentType}</Badge>,
                c.basis,
                c.isTaxable ? 'Yes' : 'No',
                c.glAccountCode
                  ? <span style={{ fontSize: 12 }}><strong>{c.glAccountCode}</strong> {c.glAccountName}</span>
                  : <Badge variant="red">unmapped</Badge>,
                <div style={{ display: 'flex', gap: 6 }}>
                  <Btn size="sm" variant="outline" onClick={() => setMapping({ kind: 'component', id: c.id, label: c.name, current: c.glAccountId })}>GL</Btn>
                  <Btn size="sm" variant="outline" onClick={() => setCompModal({ structureId: st.id, component: c })}>Edit</Btn>
                  {c.isActive && <Btn size="sm" variant="ghost" onClick={async () => {
                    try { relay(flash, await hr.deactivateComponent(c.id)); reload() }
                    catch (e) { relayError(flash, e, 'Could not deactivate the component.') }
                  }}>Stop</Btn>}
                </div>,
              ])}
            />
          </div>
        </Card>
      ))}

      {gradeModal && <GradeModal grade={gradeModal.id ? gradeModal : null} flash={flash}
        onClose={() => setGradeModal(null)} onSaved={() => { setGradeModal(null); reload() }} />}
      {structModal && <StructureModal structure={structModal.id ? structModal : null} grades={grades.data ?? []} flash={flash}
        onClose={() => setStructModal(null)} onSaved={() => { setStructModal(null); reload() }} />}
      {compModal && <ComponentModal ctx={compModal} flash={flash}
        onClose={() => setCompModal(null)} onSaved={() => { setCompModal(null); reload() }} />}
      {mapping && <GlAccountModal target={mapping} flash={flash}
        onClose={() => setMapping(null)} onSaved={() => { setMapping(null); reload() }} />}
    </div>
  )
}

function GradeModal({ grade, flash, onClose, onSaved }) {
  const [f, setF] = useState({
    code: grade?.code ?? '', name: grade?.name ?? '', description: grade?.description ?? '',
    minSalary: grade?.minSalary ?? '', maxSalary: grade?.maxSalary ?? '',
    displayOrder: grade?.displayOrder ?? 0, isActive: grade?.isActive ?? true,
  })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    const dto = {
      code: f.code, name: f.name, description: f.description || null,
      minSalary: f.minSalary === '' ? null : Number(f.minSalary),
      maxSalary: f.maxSalary === '' ? null : Number(f.maxSalary),
      displayOrder: Number(f.displayOrder) || 0, isActive: f.isActive,
    }
    try {
      relay(flash, grade ? await hr.updateJobGrade(grade.id, dto) : await hr.createJobGrade(dto))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the grade.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={grade ? `Edit ${grade.code}` : 'New job grade'} onClose={onClose}>
      <Input label="Code" value={f.code} onChange={v => setF({ ...f, code: v })} required readOnly={!!grade}
        note={grade ? 'The code is the stable key and cannot be changed.' : 'Short and stable, e.g. G3.'} />
      <Input label="Name" value={f.name} onChange={v => setF({ ...f, name: v })} required />
      <Input label="Description" value={f.description} onChange={v => setF({ ...f, description: v })} />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Band minimum" type="number" value={f.minSalary} onChange={v => setF({ ...f, minSalary: v })} />
        <Input label="Band maximum" type="number" value={f.maxSalary} onChange={v => setF({ ...f, maxSalary: v })} />
      </div>
      <p style={{ fontSize: 12, color: T.mgrey }}>
        The band is advisory. A salary outside it is accepted with a warning, because out-of-band pay is
        sometimes deliberate — and refusing it would only push people into editing the grade instead.
      </p>
      <Input label="Display order" type="number" value={f.displayOrder} onChange={v => setF({ ...f, displayOrder: v })} />
      <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, marginTop: 6 }}>
        <input type="checkbox" checked={f.isActive} onChange={e => setF({ ...f, isActive: e.target.checked })} /> Active
      </label>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.code.trim() || !f.name.trim()}>{busy ? 'Saving…' : 'Save'}</Btn>
      </div>
    </Modal>
  )
}

function StructureModal({ structure, grades, flash, onClose, onSaved }) {
  const [f, setF] = useState({
    name: structure?.name ?? '', description: structure?.description ?? '',
    jobGradeId: structure?.jobGradeId ?? '', currencyCode: structure?.currencyCode ?? 'KES',
    isActive: structure?.isActive ?? true,
  })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    const dto = {
      name: f.name, description: f.description || null,
      jobGradeId: f.jobGradeId || null, currencyCode: f.currencyCode, isActive: f.isActive,
    }
    try {
      relay(flash, structure ? await hr.updateStructure(structure.id, dto) : await hr.createStructure(dto))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the structure.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={structure ? `Edit ${structure.name}` : 'New salary structure'} onClose={onClose}>
      <Input label="Name" value={f.name} onChange={v => setF({ ...f, name: v })} required />
      <Input label="Description" value={f.description} onChange={v => setF({ ...f, description: v })} />
      <Select label="Job grade" value={f.jobGradeId} onChange={v => setF({ ...f, jobGradeId: v })}
        options={[{ value: '', label: 'No grade' }, ...grades.map(g => ({ value: g.id, label: `${g.code} — ${g.name}` }))]} />
      <Input label="Currency" value={f.currencyCode} onChange={v => setF({ ...f, currencyCode: v })} note="ISO code; KES unless the tenant pays in something else." />
      <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, marginTop: 6 }}>
        <input type="checkbox" checked={f.isActive} onChange={e => setF({ ...f, isActive: e.target.checked })} /> Active
      </label>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.name.trim()}>{busy ? 'Saving…' : 'Save'}</Btn>
      </div>
    </Modal>
  )
}

function ComponentModal({ ctx, flash, onClose, onSaved }) {
  const c = ctx.component
  const [f, setF] = useState({
    code: c?.code ?? '', name: c?.name ?? '',
    componentType: c?.componentType ?? 'Earning',
    calculationType: c?.calculationType ?? 'FixedAmount',
    amount: c?.amount ?? '', percentage: c?.percentage ?? '',
    statutory: c?.statutory ?? 'None', isTaxable: c?.isTaxable ?? true,
    componentOrder: c?.componentOrder ?? 0, isActive: c?.isActive ?? true,
  })
  const [busy, setBusy] = useState(false)

  const needsAmount = f.calculationType === 'FixedAmount'
  const needsPercent = f.calculationType === 'PercentOfBasic' || f.calculationType === 'PercentOfGross'
  const needsRule = f.calculationType === 'Statutory'

  const save = async () => {
    setBusy(true)
    const dto = {
      code: f.code, name: f.name,
      componentType: f.componentType, calculationType: f.calculationType,
      amount: needsAmount && f.amount !== '' ? Number(f.amount) : null,
      percentage: needsPercent && f.percentage !== '' ? Number(f.percentage) : null,
      statutory: f.statutory, isTaxable: f.isTaxable,
      componentOrder: Number(f.componentOrder) || 0, isActive: f.isActive,
    }
    try {
      relay(flash, c ? await hr.updateComponent(c.id, dto) : await hr.addComponent(ctx.structureId, dto))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the component.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={c ? `Edit ${c.code}` : 'New salary component'} onClose={onClose}>
      <Input label="Code" value={f.code} onChange={v => setF({ ...f, code: v })} required readOnly={!!c}
        note={c ? 'The code is what the payroll engine matches on and cannot change.' : 'e.g. TRANSPORT. Unique within the structure.'} />
      <Input label="Name" value={f.name} onChange={v => setF({ ...f, name: v })} required />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Select label="Type" value={f.componentType} onChange={v => setF({ ...f, componentType: v })}
          options={[{ value: 'Earning', label: 'Earning' }, { value: 'Deduction', label: 'Deduction' }]} />
        <Select label="Calculation" value={f.calculationType} onChange={v => setF({ ...f, calculationType: v })}
          options={CALC_TYPES.map(v => ({ value: v, label: v }))} />
      </div>
      {needsAmount && <Input label="Amount" type="number" value={f.amount} onChange={v => setF({ ...f, amount: v })} required />}
      {needsPercent && <Input label="Percentage" type="number" value={f.percentage} onChange={v => setF({ ...f, percentage: v })} required
        note="Of basic pay, or of gross, depending on the calculation above." />}
      {needsRule && (
        <Select label="Statutory rule" value={f.statutory} onChange={v => setF({ ...f, statutory: v })}
          options={STATUTORY_RULES.map(v => ({ value: v, label: v }))} />
      )}
      {needsRule && (
        <p style={{ fontSize: 12, color: T.mgrey }}>
          A statutory component carries no figure of its own — the payroll run computes it from the rate tables
          on the Statutory Rates tab.
        </p>
      )}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Payslip order" type="number" value={f.componentOrder} onChange={v => setF({ ...f, componentOrder: v })} />
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, justifyContent: 'center' }}>
          <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13 }}>
            <input type="checkbox" checked={f.isTaxable} onChange={e => setF({ ...f, isTaxable: e.target.checked })} /> Taxable
          </label>
          <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13 }}>
            <input type="checkbox" checked={f.isActive} onChange={e => setF({ ...f, isActive: e.target.checked })} /> Active
          </label>
        </div>
      </div>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.code.trim() || !f.name.trim()}>{busy ? 'Saving…' : 'Save'}</Btn>
      </div>
    </Modal>
  )
}

/** Shared by components, statutory rates and deduction types — all three post to
 *  the same chart of accounts, read live from finance. */
function GlAccountModal({ target, flash, onClose, onSaved }) {
  const [accounts, setAccounts] = useState(null)
  const [selected, setSelected] = useState(target.current ?? '')
  const [busy, setBusy] = useState(false)

  useEffect(() => { hr.listGlAccounts().then(a => setAccounts(a ?? [])).catch(() => setAccounts([])) }, [])

  const save = async () => {
    setBusy(true)
    const map = { component: hr.mapComponentAccount, statutory: hr.mapStatutoryAccount, deduction: hr.mapDeductionAccount }
    try { relay(flash, await map[target.kind](target.id, selected)); onSaved() }
    catch (e) { relayError(flash, e, 'Could not map the account.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`GL account — ${target.label}`} onClose={onClose}>
      {accounts === null ? <Loading /> : accounts.length === 0 ? (
        <Alert type="warning">
          Finance's chart of accounts could not be read. That means finance is unreachable or not configured —
          not that there are no accounts. Anything already mapped keeps working in the meantime.
        </Alert>
      ) : (
        <>
          <Select label="Posting account" value={selected} onChange={setSelected}
            options={[{ value: '', label: 'Select an account…' },
              ...accounts.map(a => ({ value: a.id, label: `${a.code} — ${a.name}` }))]} />
          <p style={{ fontSize: 12, color: T.mgrey }}>
            Only accounts finance will accept a posting on are listed — header accounts are left out.
          </p>
        </>
      )}
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !selected}>{busy ? 'Mapping…' : 'Map account'}</Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Employee salaries — propose, approve, and the pay history that survives both
// ═════════════════════════════════════════════════════════════════════════════
export function EmployeeSalariesTab({ flash }) {
  const [status, setStatus] = useState('')
  const [proposing, setProposing] = useState(false)
  const [deciding, setDeciding] = useState(null)

  const salaries = usePayrollData(() => hr.listSalaries({ status: status || undefined }), [status])

  if (salaries.loading) return <Loading />
  if (salaries.denied) return <NoAccess />

  return (
    <div>
      <Alert type="info">
        <strong>A raise never edits the old record.</strong> Approving a new assignment supersedes the previous one
        and keeps it, so "what were they on in March" survives every later change. The person who proposes a salary
        cannot approve it — that needs a second officer.
      </Alert>

      <SectionHeader
        title="Salary Assignments"
        sub="HR proposes and the MD approves. An unapproved assignment does not pay."
        action={<Btn size="sm" onClick={() => setProposing(true)}>+ Propose salary</Btn>}
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All statuses</option>
          {['Proposed', 'Approved', 'Rejected', 'Superseded'].map(s => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      <DataTable
        headers={['Employee', 'Structure', 'Basic', 'Effective from', 'Status', 'Proposed', 'Decided', 'Actions']}
        empty={status ? 'No assignments match this filter.' : 'No salary assignments yet.'}
        rows={(salaries.data ?? []).map(s => [
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{s.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{s.employeeName}</span>
          </span>,
          s.salaryStructureName ?? '—',
          <strong>{money(s.basicSalary, s.currencyCode)}</strong>,
          <span style={{ fontFamily: 'monospace' }}>{s.effectiveFromPeriodCode}</span>,
          <span>
            <Badge variant={STATUS_VARIANT[s.status] ?? 'default'}>{s.status}</Badge>
            {s.rejectionReason && <span style={{ display: 'block', fontSize: 11, color: T.red, marginTop: 3 }}>{s.rejectionReason}</span>}
          </span>,
          fmtDate(s.proposedAt),
          fmtDate(s.approvedAt),
          s.status === 'Proposed'
            ? <Btn size="sm" onClick={() => setDeciding(s)}>Decide</Btn>
            : <span style={{ fontSize: 11, color: T.mgrey }}>closed</span>,
        ])}
      />

      {proposing && <ProposeSalaryModal flash={flash}
        onClose={() => setProposing(false)} onSaved={() => { setProposing(false); salaries.reload() }} />}
      {deciding && <DecideSalaryModal salary={deciding} flash={flash}
        onClose={() => setDeciding(null)} onSaved={() => { setDeciding(null); salaries.reload() }} />}
    </div>
  )
}

function ProposeSalaryModal({ flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [structures, setStructures] = useState([])
  const [periods, setPeriods] = useState([])
  const [f, setF] = useState({ employeeId: '', salaryStructureId: '', basicSalary: '', effectiveFromPeriodId: '', notes: '' })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {})
    hr.listStructures().then(s => setStructures(s ?? [])).catch(() => {})
    hr.listPayrollPeriods(new Date().getFullYear()).then(p => setPeriods(p ?? [])).catch(() => {})
  }, [])

  // Only open periods can take a new assignment; the server refuses the rest anyway.
  const openPeriods = periods.filter(p => p.status === 'Open')

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.proposeSalary({
        employeeId: f.employeeId, salaryStructureId: f.salaryStructureId,
        basicSalary: Number(f.basicSalary),
        effectiveFromPeriodId: f.effectiveFromPeriodId || null,
        notes: f.notes || null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not propose the salary.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Propose a salary" onClose={onClose}>
      <Select label="Employee" value={f.employeeId} onChange={v => setF({ ...f, employeeId: v })}
        options={[{ value: '', label: 'Select…' },
          ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} required />
      <Select label="Salary structure" value={f.salaryStructureId} onChange={v => setF({ ...f, salaryStructureId: v })}
        options={[{ value: '', label: 'Select…' }, ...structures.map(s => ({ value: s.id, label: s.name }))]} required />
      <Input label="Basic salary" type="number" value={f.basicSalary} onChange={v => setF({ ...f, basicSalary: v })} required
        note="The structure supplies the shape of pay; this is the figure the percentages work from." />
      <Select label="Effective from" value={f.effectiveFromPeriodId} onChange={v => setF({ ...f, effectiveFromPeriodId: v })}
        options={[{ value: '', label: 'Current period' }, ...openPeriods.map(p => ({ value: p.id, label: p.code }))]} />
      <Input label="Notes" value={f.notes} onChange={v => setF({ ...f, notes: v })} />
      <p style={{ fontSize: 12, color: T.mgrey }}>
        A raise must start in a later period than the employee's current approved assignment, and the assignment
        does not pay until it is approved.
      </p>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.employeeId || !f.salaryStructureId || !f.basicSalary}>
          {busy ? 'Proposing…' : 'Propose'}
        </Btn>
      </div>
    </Modal>
  )
}

function DecideSalaryModal({ salary, flash, onClose, onSaved }) {
  const [decision, setDecision] = useState('Approve')
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.decideSalary(salary.id, { decision, reason: reason || null }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not record the decision.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`Decide — ${salary.employeeName}`} onClose={onClose}>
      <Card style={{ padding: 12, marginBottom: 12 }}>
        <p style={{ margin: 0, fontSize: 13 }}>
          <strong>{money(salary.basicSalary, salary.currencyCode)}</strong> on {salary.salaryStructureName},
          effective from <span style={{ fontFamily: 'monospace' }}>{salary.effectiveFromPeriodCode}</span>.
        </p>
        {salary.notes && <p style={{ margin: '6px 0 0', fontSize: 12, color: T.mgrey }}>{salary.notes}</p>}
      </Card>
      <Select label="Decision" value={decision} onChange={setDecision}
        options={[{ value: 'Approve', label: 'Approve' }, { value: 'Reject', label: 'Reject' }]} />
      <Input label={decision === 'Reject' ? 'Reason (required)' : 'Reason'} value={reason} onChange={setReason}
        required={decision === 'Reject'} />
      <p style={{ fontSize: 12, color: T.mgrey }}>
        {decision === 'Approve'
          ? 'Approving supersedes this employee\'s previous assignment, which is kept as history. You cannot approve a proposal you made yourself.'
          : 'A rejection is recorded against the proposal and leaves the period free to re-propose.'}
      </p>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || (decision === 'Reject' && !reason.trim())}>
          {busy ? 'Saving…' : decision}
        </Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Statutory rates — the PAYE scale and the non-PAYE rules
// ═════════════════════════════════════════════════════════════════════════════
export function StatutoryRatesTab({ flash }) {
  const [bandModal, setBandModal] = useState(null)
  const [rateModal, setRateModal] = useState(null)
  const [mapping, setMapping] = useState(null)
  const [busy, setBusy] = useState(false)

  const bands = usePayrollData(() => hr.listPayeBands({}), [])
  const rates = usePayrollData(() => hr.listStatutoryRates({}), [])

  if (bands.loading) return <Loading />
  if (bands.denied) return <NoAccess />

  const reload = () => { bands.reload(); rates.reload() }
  const unconfirmed = [...(bands.data ?? []), ...(rates.data ?? [])].filter(x => x.needsConfirmation).length

  const confirm = async () => {
    setBusy(true)
    try { relay(flash, await hr.confirmRates({ source: '' })); reload() }
    catch (e) { relayError(flash, e, 'Could not confirm the rates.') }
    finally { setBusy(false) }
  }

  return (
    <div>
      {unconfirmed > 0 && (
        <Alert type="warning">
          <strong>{unconfirmed} figure(s) have never been checked.</strong> Everything the seeders install is
          flagged unconfirmed on purpose — the rates were right when they were written, but PAYE bands, NSSF
          tiers and the SHA rate all move with legislation. Check them against the current Finance Act, then
          confirm. Until then payroll treats them as a blocker.
        </Alert>
      )}

      <SectionHeader
        title="PAYE Scale"
        sub="Bands are half-open: each one taxes income above its lower bound up to and including its upper bound, so no shilling falls between two bands. Gaps and overlaps are reported rather than silently mis-taxing."
        action={
          <div style={{ display: 'flex', gap: 8 }}>
            {unconfirmed > 0 && <Btn size="sm" variant="outline" onClick={confirm} disabled={busy}>{busy ? 'Confirming…' : 'Confirm all'}</Btn>}
            <Btn size="sm" onClick={() => setBandModal({})}>+ Band</Btn>
          </div>
        }
      />
      <DataTable
        headers={['#', 'Band', 'Rate', 'Effective from', 'Checked?', 'Source', 'Actions']}
        empty="No PAYE bands installed. The Setup tab can install the scale."
        rows={(bands.data ?? []).map(b => [
          b.bandOrder,
          b.band,
          `${b.rate}%`,
          fmtDate(b.effectiveFrom),
          b.needsConfirmation ? <Badge variant="red">unconfirmed</Badge> : <Badge variant="green">confirmed</Badge>,
          <span style={{ fontSize: 11, color: T.mgrey }}>{b.source ?? '—'}</span>,
          <Btn size="sm" variant="outline" onClick={() => setBandModal(b)}>Edit</Btn>,
        ])}
      />

      <div style={{ marginTop: 26 }}>
        <SectionHeader
          title="Statutory Rates"
          sub="NSSF's tiers, SHA, the Housing Levy, HELB and personal relief. Each carries the GL account its liability posts to."
          action={<Btn size="sm" onClick={() => setRateModal({})}>+ Rate</Btn>}
        />
      </div>
      <DataTable
        headers={['Code', 'Rule', 'Applies as', 'Effective from', 'GL Account', 'Checked?', 'Actions']}
        empty="No statutory rates installed."
        rows={(rates.data ?? []).map(r => [
          <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{r.code}</span>,
          <span>{r.name}<span style={{ display: 'block', fontSize: 11, color: T.mgrey }}>{r.component}</span></span>,
          r.basis,
          fmtDate(r.effectiveFrom),
          r.glAccountCode
            ? <span style={{ fontSize: 12 }}><strong>{r.glAccountCode}</strong> {r.glAccountName}</span>
            : r.rateType === 'FixedAmount'
              ? <span style={{ fontSize: 11, color: T.mgrey }}>not posted separately</span>
              : <Badge variant="red">unmapped</Badge>,
          r.needsConfirmation ? <Badge variant="red">unconfirmed</Badge> : <Badge variant="green">confirmed</Badge>,
          <div style={{ display: 'flex', gap: 6 }}>
            <Btn size="sm" variant="outline" onClick={() => setMapping({ kind: 'statutory', id: r.id, label: r.name, current: r.glAccountId })}>GL</Btn>
            <Btn size="sm" variant="outline" onClick={() => setRateModal(r)}>Edit</Btn>
          </div>,
        ])}
      />

      {bandModal && <PayeBandModal band={bandModal.id ? bandModal : null} flash={flash}
        onClose={() => setBandModal(null)} onSaved={() => { setBandModal(null); reload() }} />}
      {rateModal && <StatutoryRateModal rate={rateModal.id ? rateModal : null} flash={flash}
        onClose={() => setRateModal(null)} onSaved={() => { setRateModal(null); reload() }} />}
      {mapping && <GlAccountModal target={mapping} flash={flash}
        onClose={() => setMapping(null)} onSaved={() => { setMapping(null); reload() }} />}
    </div>
  )
}

function PayeBandModal({ band, flash, onClose, onSaved }) {
  const [f, setF] = useState({
    bandOrder: band?.bandOrder ?? 1,
    lowerBound: band?.lowerBound ?? 0,
    upperBound: band?.upperBound ?? '',
    rate: band?.rate ?? '',
    effectiveFrom: (band?.effectiveFrom ?? new Date().toISOString()).slice(0, 10),
    source: band?.source ?? '',
  })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    const dto = {
      bandOrder: Number(f.bandOrder), lowerBound: Number(f.lowerBound),
      upperBound: f.upperBound === '' ? null : Number(f.upperBound),
      rate: Number(f.rate), effectiveFrom: `${f.effectiveFrom}T00:00:00Z`,
      source: f.source || null, isActive: true,
    }
    try {
      relay(flash, band ? await hr.updatePayeBand(band.id, dto) : await hr.createPayeBand(dto))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the band.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={band ? `Edit band ${band.bandOrder}` : 'New PAYE band'} onClose={onClose}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Position in the scale" type="number" value={f.bandOrder} onChange={v => setF({ ...f, bandOrder: v })} required />
        <Input label="Rate %" type="number" value={f.rate} onChange={v => setF({ ...f, rate: v })} required />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Lower bound" type="number" value={f.lowerBound} onChange={v => setF({ ...f, lowerBound: v })} required
          note="Exclusive — income at exactly this figure belongs to the band below." />
        <Input label="Upper bound" type="number" value={f.upperBound} onChange={v => setF({ ...f, upperBound: v })}
          note="Inclusive. Leave blank for the top band." />
      </div>
      <Input label="Effective from" type="date" value={f.effectiveFrom} onChange={v => setF({ ...f, effectiveFrom: v })} required
        note="A rate change adds a new dated set rather than editing this one, so an old month can still be re-run." />
      <Input label="Source" value={f.source} onChange={v => setF({ ...f, source: v })} note="The instrument this figure comes from." />
      <p style={{ fontSize: 12, color: T.mgrey }}>
        Saving marks this band as checked, since a figure a person typed in is one a person has verified.
        The scale as a whole is re-validated afterwards and any gap or overlap is reported.
      </p>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || f.rate === ''}>{busy ? 'Saving…' : 'Save'}</Btn>
      </div>
    </Modal>
  )
}

function StatutoryRateModal({ rate, flash, onClose, onSaved }) {
  const [f, setF] = useState({
    code: rate?.code ?? '', name: rate?.name ?? '',
    component: rate?.component ?? 'Nssf', rateType: rate?.rateType ?? 'PercentOfGross',
    rate: rate?.rate ?? '', fixedAmount: rate?.fixedAmount ?? '',
    tierLowerBound: rate?.tierLowerBound ?? '', tierUpperBound: rate?.tierUpperBound ?? '',
    minAmount: rate?.minAmount ?? '', maxAmount: rate?.maxAmount ?? '',
    employerRate: rate?.employerRate ?? '',
    effectiveFrom: (rate?.effectiveFrom ?? new Date().toISOString()).slice(0, 10),
    source: rate?.source ?? '', notes: rate?.notes ?? '',
  })
  const [busy, setBusy] = useState(false)

  const n = (v) => (v === '' || v == null ? null : Number(v))
  const isTiered = f.rateType === 'TieredPercent'
  const isPercent = f.rateType === 'PercentOfGross' || isTiered
  const isFixed = f.rateType === 'FixedAmount'

  const save = async () => {
    setBusy(true)
    const dto = {
      code: f.code, name: f.name, component: f.component, rateType: f.rateType,
      rate: isPercent ? n(f.rate) : null,
      fixedAmount: isFixed ? n(f.fixedAmount) : null,
      tierLowerBound: isTiered ? n(f.tierLowerBound) : null,
      tierUpperBound: isTiered ? n(f.tierUpperBound) : null,
      minAmount: n(f.minAmount), maxAmount: n(f.maxAmount), employerRate: n(f.employerRate),
      effectiveFrom: `${f.effectiveFrom}T00:00:00Z`,
      source: f.source || null, notes: f.notes || null, isActive: true,
    }
    try {
      relay(flash, rate ? await hr.updateStatutoryRate(rate.id, dto) : await hr.createStatutoryRate(dto))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the rate.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={rate ? `Edit ${rate.code}` : 'New statutory rate'} onClose={onClose} width={600}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Code" value={f.code} onChange={v => setF({ ...f, code: v })} required />
        <Input label="Name" value={f.name} onChange={v => setF({ ...f, name: v })} required />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Select label="Component" value={f.component} onChange={v => setF({ ...f, component: v })}
          options={STATUTORY_RULES.filter(r => r !== 'None').map(v => ({ value: v, label: v }))} />
        <Select label="Applies as" value={f.rateType} onChange={v => setF({ ...f, rateType: v })}
          options={RATE_TYPES.map(v => ({ value: v, label: v }))} />
      </div>
      {isPercent && <Input label="Rate %" type="number" value={f.rate} onChange={v => setF({ ...f, rate: v })} required />}
      {isFixed && <Input label="Fixed amount" type="number" value={f.fixedAmount} onChange={v => setF({ ...f, fixedAmount: v })} required />}
      {isTiered && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
          <Input label="Tier from" type="number" value={f.tierLowerBound} onChange={v => setF({ ...f, tierLowerBound: v })}
            note="Half-open, like the PAYE bands." />
          <Input label="Tier to" type="number" value={f.tierUpperBound} onChange={v => setF({ ...f, tierUpperBound: v })} />
        </div>
      )}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
        <Input label="Minimum" type="number" value={f.minAmount} onChange={v => setF({ ...f, minAmount: v })} />
        <Input label="Maximum" type="number" value={f.maxAmount} onChange={v => setF({ ...f, maxAmount: v })} />
        <Input label="Employer %" type="number" value={f.employerRate} onChange={v => setF({ ...f, employerRate: v })}
          note="Where the employer matches" />
      </div>
      <Input label="Effective from" type="date" value={f.effectiveFrom} onChange={v => setF({ ...f, effectiveFrom: v })} required
        note="A new effective date supersedes the old rate rather than overwriting it." />
      <Input label="Source" value={f.source} onChange={v => setF({ ...f, source: v })} />
      <Input label="Notes" value={f.notes} onChange={v => setF({ ...f, notes: v })} />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.code.trim() || !f.name.trim()}>{busy ? 'Saving…' : 'Save'}</Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Deductions — the catalogue and what each employee actually carries
// ═════════════════════════════════════════════════════════════════════════════
export function PayrollDeductionsTab({ flash }) {
  const [typeModal, setTypeModal] = useState(null)
  const [addOpen, setAddOpen] = useState(false)
  const [mapping, setMapping] = useState(null)
  const [showStopped, setShowStopped] = useState(false)

  const types = usePayrollData(() => hr.listDeductionTypes(true), [])
  const rows = usePayrollData(() => hr.listDeductions({ includeInactive: showStopped }), [showStopped])

  if (types.loading) return <Loading />
  if (types.denied) return <NoAccess />

  const reload = () => { types.reload(); rows.reload() }

  const stop = async (d) => {
    const reason = window.prompt(`Stop ${d.deductionTypeName} for ${d.employeeName}? Give a reason:`)
    if (reason === null) return
    try { relay(flash, await hr.stopDeduction(d.id, { reason })); reload() }
    catch (e) { relayError(flash, e, 'Could not stop the deduction.') }
  }

  return (
    <div>
      <SectionHeader
        title="Deduction Catalogue"
        sub="Configured once, applied to many. Whether a deduction comes off before tax matters — a pension contribution does, a court order does not."
        action={<Btn size="sm" onClick={() => setTypeModal({})}>+ Type</Btn>}
      />
      <DataTable
        headers={['Code', 'Name', 'Category', 'Pre-tax', 'Frequency', 'GL Account', 'In use', 'Actions']}
        empty="No deduction types configured. The Setup tab can install the common catalogue."
        rows={(types.data ?? []).map(t => [
          <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{t.code}</span>,
          <span style={{ opacity: t.isActive ? 1 : .5 }}>{t.name}{!t.isActive && ' (inactive)'}</span>,
          <Badge variant={CATEGORY_VARIANT[t.category] ?? 'default'}>{t.category}</Badge>,
          t.reducesTaxableIncome ? 'Yes' : 'No',
          t.isRecurring ? 'Recurring' : 'One-off',
          t.glAccountCode
            ? <span style={{ fontSize: 12 }}><strong>{t.glAccountCode}</strong> {t.glAccountName}</span>
            : <Badge variant="red">unmapped</Badge>,
          t.activeDeductions,
          <div style={{ display: 'flex', gap: 6 }}>
            <Btn size="sm" variant="outline" onClick={() => setMapping({ kind: 'deduction', id: t.id, label: t.name, current: t.glAccountId })}>GL</Btn>
            <Btn size="sm" variant="outline" onClick={() => setTypeModal(t)}>Edit</Btn>
          </div>,
        ])}
      />

      <div style={{ marginTop: 26 }}>
        <SectionHeader
          title="Employee Deductions"
          sub="Stopping a deduction never deletes it — the row is closed with who stopped it and when, so the trail stays auditable."
          action={<Btn size="sm" onClick={() => setAddOpen(true)}>+ Apply deduction</Btn>}
        />
      </div>

      <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, marginBottom: 12 }}>
        <input type="checkbox" checked={showStopped} onChange={e => setShowStopped(e.target.checked)} /> Include stopped deductions
      </label>

      {rows.loading ? <Loading /> : (
        <DataTable
          headers={['Employee', 'Deduction', 'Category', 'Schedule', 'Status', 'Actions']}
          empty={showStopped ? 'No deductions on file.' : 'No live deductions.'}
          rows={(rows.data ?? []).map(d => [
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{d.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{d.employeeName}</span>
            </span>,
            d.deductionTypeName,
            <Badge variant={CATEGORY_VARIANT[d.category] ?? 'default'}>{d.category}</Badge>,
            d.schedule,
            d.isActive
              ? <Badge variant="green">Active</Badge>
              : <span>
                  <Badge variant="default">Stopped</Badge>
                  <span style={{ display: 'block', fontSize: 11, color: T.mgrey, marginTop: 3 }}>{fmtDate(d.removedAt)}</span>
                </span>,
            d.isActive
              ? <Btn size="sm" variant="ghost" onClick={() => stop(d)}>Stop</Btn>
              : <span style={{ fontSize: 11, color: T.mgrey }}>closed</span>,
          ])}
        />
      )}

      {typeModal && <DeductionTypeModal type={typeModal.id ? typeModal : null} flash={flash}
        onClose={() => setTypeModal(null)} onSaved={() => { setTypeModal(null); reload() }} />}
      {addOpen && <AddDeductionModal types={(types.data ?? []).filter(t => t.isActive)} flash={flash}
        onClose={() => setAddOpen(false)} onSaved={() => { setAddOpen(false); reload() }} />}
      {mapping && <GlAccountModal target={mapping} flash={flash}
        onClose={() => setMapping(null)} onSaved={() => { setMapping(null); reload() }} />}
    </div>
  )
}

function DeductionTypeModal({ type, flash, onClose, onSaved }) {
  const [f, setF] = useState({
    code: type?.code ?? '', name: type?.name ?? '', description: type?.description ?? '',
    category: type?.category ?? 'Voluntary', reducesTaxableIncome: type?.reducesTaxableIncome ?? false,
    isRecurring: type?.isRecurring ?? true, isActive: type?.isActive ?? true,
    displayOrder: type?.displayOrder ?? 0,
  })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    const dto = {
      code: f.code, name: f.name, description: f.description || null,
      category: f.category, reducesTaxableIncome: f.reducesTaxableIncome, isRecurring: f.isRecurring,
      isActive: f.isActive, displayOrder: Number(f.displayOrder) || 0,
    }
    try {
      relay(flash, type ? await hr.updateDeductionType(type.id, dto) : await hr.createDeductionType(dto))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the deduction type.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={type ? `Edit ${type.code}` : 'New deduction type'} onClose={onClose}>
      <Input label="Code" value={f.code} onChange={v => setF({ ...f, code: v })} required readOnly={!!type} />
      <Input label="Name" value={f.name} onChange={v => setF({ ...f, name: v })} required />
      <Input label="Description" value={f.description} onChange={v => setF({ ...f, description: v })} />
      <Select label="Category" value={f.category} onChange={v => setF({ ...f, category: v })}
        options={CATEGORIES.map(v => ({ value: v, label: v }))} />
      <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, marginTop: 8 }}>
        <input type="checkbox" checked={f.reducesTaxableIncome} onChange={e => setF({ ...f, reducesTaxableIncome: e.target.checked })} />
        Taken before tax is computed
      </label>
      <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, marginTop: 6 }}>
        <input type="checkbox" checked={f.isRecurring} onChange={e => setF({ ...f, isRecurring: e.target.checked })} />
        Recurring — a one-off type is taken once, in the period it starts
      </label>
      <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, marginTop: 6 }}>
        <input type="checkbox" checked={f.isActive} onChange={e => setF({ ...f, isActive: e.target.checked })} /> Active
      </label>
      <Input label="Display order" type="number" value={f.displayOrder} onChange={v => setF({ ...f, displayOrder: v })} />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.code.trim() || !f.name.trim()}>{busy ? 'Saving…' : 'Save'}</Btn>
      </div>
    </Modal>
  )
}

function AddDeductionModal({ types, flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [periods, setPeriods] = useState([])
  const [f, setF] = useState({ employeeId: '', deductionTypeId: '', amount: '', startPeriodId: '', endPeriodId: '', notes: '' })
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    hr.listEmployees({ pageSize: 200 }).then(r => setEmployees(r.data ?? [])).catch(() => {})
    hr.listPayrollPeriods(new Date().getFullYear()).then(p => setPeriods(p ?? [])).catch(() => {})
  }, [])

  const chosen = types.find(t => t.id === f.deductionTypeId)
  const openPeriods = periods.filter(p => p.status === 'Open')

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.addDeduction({
        employeeId: f.employeeId, deductionTypeId: f.deductionTypeId,
        amount: Number(f.amount),
        startPeriodId: f.startPeriodId || null,
        endPeriodId: f.endPeriodId || null,
        notes: f.notes || null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not apply the deduction.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Apply a deduction" onClose={onClose}>
      <Select label="Employee" value={f.employeeId} onChange={v => setF({ ...f, employeeId: v })}
        options={[{ value: '', label: 'Select…' },
          ...employees.map(e => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` }))]} required />
      <Select label="Deduction type" value={f.deductionTypeId} onChange={v => setF({ ...f, deductionTypeId: v })}
        options={[{ value: '', label: 'Select…' }, ...types.map(t => ({ value: t.id, label: `${t.name} (${t.category})` }))]} required />
      <Input label="Amount per period" type="number" value={f.amount} onChange={v => setF({ ...f, amount: v })} required />
      <Select label="Starts in" value={f.startPeriodId} onChange={v => setF({ ...f, startPeriodId: v })}
        options={[{ value: '', label: 'Current period' }, ...openPeriods.map(p => ({ value: p.id, label: p.code }))]} />
      {chosen?.isRecurring !== false && (
        <Select label="Ends after" value={f.endPeriodId} onChange={v => setF({ ...f, endPeriodId: v })}
          options={[{ value: '', label: 'Until stopped' }, ...periods.map(p => ({ value: p.id, label: p.code }))]} />
      )}
      {chosen && chosen.isRecurring === false && (
        <p style={{ fontSize: 12, color: T.amber }}>
          {chosen.name} is a one-off, so it is taken once in the period it starts.
        </p>
      )}
      <Input label="Notes" value={f.notes} onChange={v => setF({ ...f, notes: v })} />
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.employeeId || !f.deductionTypeId || !f.amount}>
          {busy ? 'Applying…' : 'Apply'}
        </Btn>
      </div>
    </Modal>
  )
}
