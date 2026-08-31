import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Modal, Input, Select, Loading, Alert } from '../../components/ui.jsx'
import * as hr from '../../services/hr.js'

// ─────────────────────────────────────────────────────────────────────────────
// H9 — KPI scorecards, appraisals, 360 feedback and improvement plans
// (P14–P17). REAL, wired to hr-service.
//
// Two things this screen must get right, because they are the whole point:
//
//  1. THE ORDER. Self-assessment → line manager → MD → HR. The server enforces
//     it and also refuses to let one person take two consecutive steps. The UI
//     only ever offers the step the appraisal is actually on, so nobody is
//     invited to do something that will be refused.
//
//  2. 360 ANONYMITY. Individual ratings are never shown next to a reviewer —
//     the API does not return the pairing at all. What is shown is counts,
//     group averages, and who has yet to respond (a non-response carries no
//     score, so naming it reveals nothing).
//
// Items sourced from attendance or the 360 aggregate are scored by the system
// and render read-only, with a note saying why.
// ─────────────────────────────────────────────────────────────────────────────

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const pct = (n) => (n == null ? '—' : `${Number(n).toFixed(2)}`)

const APPRAISAL_VARIANT = {
  PendingSelf: 'default', PendingLineManager: 'amber', PendingMd: 'blue',
  PendingHr: 'purple', Completed: 'green', Cancelled: 'red',
}
const PIP_VARIANT = { Active: 'amber', Extended: 'blue', Completed: 'green', EscalatedToDisciplinary: 'red' }
const PIP_LABEL = {
  Active: 'Active', Extended: 'Extended', Completed: 'Improved — closed',
  EscalatedToDisciplinary: 'Escalated to disciplinary',
}
const SOURCES = ['Manual', 'Attendance', 'Feedback360', 'Revenue']
const SYSTEM_SOURCES = ['Attendance', 'Feedback360']

function Kpi({ label, value, sub, color }) {
  return (
    <Card style={{ padding: 14 }}>
      <p style={{ fontSize: 20, fontWeight: 800, color: color ?? T.navy, margin: 0 }}>{value}</p>
      <p style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .4, marginTop: 3 }}>{label}</p>
      {sub && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{sub}</p>}
    </Card>
  )
}

function relay(flash, r) {
  const warnings = r?.warnings ?? []
  const message = r?.message ?? 'Done.'
  if (warnings.length) flash(`${message} — ${warnings.join(' ')}`, 'warning')
  else flash(message)
}
const relayError = (flash, e, fallback) => flash(e.response?.data?.message ?? fallback, 'error')

function useData(loader, deps = []) {
  const [state, setState] = useState({ loading: true, data: null })
  const load = useCallback(() => {
    setState(s => ({ ...s, loading: true }))
    loader().then(data => setState({ loading: false, data })).catch(() => setState({ loading: false, data: null }))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps)
  useEffect(() => { load() }, [load])
  return { ...state, reload: load }
}

// ═════════════════════════════════════════════════════════════════════════════
// Scorecards and targets (P14)
// ═════════════════════════════════════════════════════════════════════════════
export function ScorecardsTab({ flash }) {
  const [year, setYear] = useState(new Date().getFullYear())
  const [editing, setEditing] = useState(null)
  const [targetsFor, setTargetsFor] = useState(null)

  const summary = useData(() => hr.appraisalSummary(year), [year])
  const cards = useData(() => hr.listKpiScorecards({ year, includeInactive: true }), [year])

  if (cards.loading) return <Loading />
  const s = summary.data
  const reload = () => { summary.reload(); cards.reload() }

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Scorecards" value={s?.scorecards ?? 0} />
        <Kpi label="Invalid weights" value={s?.scorecardsWithInvalidWeights ?? 0}
          color={s?.scorecardsWithInvalidWeights ? T.red : T.green} sub="must total 100%" />
        <Kpi label="Staff with targets" value={s?.employeesWithTargets ?? 0} />
        <Kpi label="No scorecard" value={s?.employeesWithoutScorecard ?? 0}
          color={s?.employeesWithoutScorecard ? T.amber : T.green} sub="role not covered" />
        <Kpi label="Active PIPs" value={s?.activePips ?? 0} color={s?.activePips ? T.amber : T.green} />
      </div>

      <SectionHeader
        title="KPI Scorecards"
        sub="One scorecard per role per year. Item weights must total exactly 100% — a scorecard that does not add up produces a plausible score that is quietly wrong for everyone on it."
        action={<Btn size="sm" onClick={() => setEditing({})}>+ Scorecard</Btn>}
      />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={year} onChange={e => setYear(Number(e.target.value))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          {[year + 1, year, year - 1].filter((v, i, a) => a.indexOf(v) === i).map(y => <option key={y} value={y}>{y}</option>)}
        </select>
      </div>

      {(cards.data ?? []).map(c => (
        <Card key={c.id} style={{ padding: 16, marginBottom: 14 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap' }}>
            <div>
              <p style={{ fontWeight: 700, color: T.navy, margin: 0, fontSize: 15 }}>
                {c.name} {!c.isActive && <Badge variant="default">Inactive</Badge>}
              </p>
              <p style={{ fontSize: 12, color: T.mgrey, margin: '4px 0 0' }}>
                {c.positionTitle ?? 'All staff without a role-specific card'} · PIP threshold {pct(c.pipThreshold)} ·
                {' '}{c.employeesWithTargets} employee(s) with targets
              </p>
            </div>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <Badge variant={c.weightsValid ? 'green' : 'red'}>{pct(c.totalWeight)}%</Badge>
              <Btn size="sm" variant="outline" onClick={() => setEditing(c)}>Edit</Btn>
              <Btn size="sm" variant="outline" onClick={() => setTargetsFor(c)}>Targets</Btn>
              <Btn size="sm" onClick={async () => {
                try { relay(flash, await hr.assignKpiTargets(c.id)); reload() }
                catch (e) { relayError(flash, e, 'Could not assign targets.') }
              }}>Assign targets</Btn>
            </div>
          </div>

          {!c.weightsValid && (
            <Alert type="error">
              The weights total {pct(c.totalWeight)}%, not 100%. Targets cannot be assigned and a cycle will not use it until this is fixed.
            </Alert>
          )}

          <div style={{ marginTop: 12 }}>
            <DataTable
              headers={['Weight', 'Item', 'Measured', 'Source', 'Unit']}
              empty="No items."
              rows={(c.items ?? []).map(i => [
                <strong>{pct(i.weightPercent)}%</strong>,
                <span>
                  {i.itemName}
                  {i.sourceNote && <span style={{ display: 'block', fontSize: 11, color: T.mgrey }}>{i.sourceNote}</span>}
                </span>,
                i.measurementType,
                <Badge variant={SYSTEM_SOURCES.includes(i.targetSource) ? 'blue' : i.targetSource === 'Revenue' ? 'amber' : 'default'}>
                  {i.targetSource}
                </Badge>,
                i.unit ?? '—',
              ])}
            />
          </div>
        </Card>
      ))}

      {(cards.data ?? []).length === 0 && <Alert type="info">No scorecards for {year} yet.</Alert>}

      {editing && <ScorecardModal card={editing.id ? editing : null} year={year} flash={flash}
        onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
      {targetsFor && <TargetsModal card={targetsFor} flash={flash}
        onClose={() => setTargetsFor(null)} onSaved={reload} />}
    </div>
  )
}

function ScorecardModal({ card, year, flash, onClose, onSaved }) {
  const [posns, setPosns] = useState([])
  const [f, setF] = useState({
    name: card?.name ?? '', description: card?.description ?? '',
    positionId: card?.positionId ?? '', year: card?.year ?? year,
    pipThreshold: card?.pipThreshold ?? 50,
    items: card?.items?.length
      ? card.items.map(i => ({ id: i.id, itemName: i.itemName, weightPercent: i.weightPercent, measurementType: i.measurementType, targetSource: i.targetSource, unit: i.unit ?? '', displayOrder: i.displayOrder }))
      : [{ itemName: '', weightPercent: '', measurementType: 'Qualitative', targetSource: 'Manual', unit: '', displayOrder: 1 }],
  })
  const [busy, setBusy] = useState(false)

  useEffect(() => { hr.listPositions().then(p => setPosns(p ?? [])).catch(() => {}) }, [])

  const setItem = (i, key, v) => setF({ ...f, items: f.items.map((o, j) => j === i ? { ...o, [key]: v } : o) })
  const addItem = () => setF({ ...f, items: [...f.items, { itemName: '', weightPercent: '', measurementType: 'Qualitative', targetSource: 'Manual', unit: '', displayOrder: f.items.length + 1 }] })
  const dropItem = (i) => setF({ ...f, items: f.items.filter((_, j) => j !== i) })

  const total = f.items.reduce((n, i) => n + (Number(i.weightPercent) || 0), 0)
  const valid = Math.abs(total - 100) < 0.005

  const save = async () => {
    setBusy(true)
    const dto = {
      name: f.name, description: f.description || null,
      positionId: f.positionId || null, year: Number(f.year),
      pipThreshold: Number(f.pipThreshold),
      items: f.items.filter(i => i.itemName.trim()).map((i, idx) => ({
        id: i.id, itemName: i.itemName, weightPercent: Number(i.weightPercent) || 0,
        measurementType: i.measurementType, targetSource: i.targetSource,
        unit: i.unit || null, displayOrder: i.displayOrder || idx + 1,
      })),
    }
    try {
      relay(flash, card ? await hr.updateKpiScorecard(card.id, dto) : await hr.createKpiScorecard(dto))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the scorecard.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={card ? `Edit ${card.name}` : 'New KPI scorecard'} onClose={onClose} width={760}>
      <Input label="Name" value={f.name} onChange={v => setF({ ...f, name: v })} required />
      <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: 10 }}>
        <Select label="Role" value={f.positionId} onChange={v => setF({ ...f, positionId: v })}
          options={[{ value: '', label: 'All staff (default card)' },
            ...posns.map(p => ({ value: p.id, label: p.title }))]} />
        <Input label="Year" type="number" value={f.year} onChange={v => setF({ ...f, year: v })} readOnly={!!card} />
        <Input label="PIP threshold" type="number" value={f.pipThreshold} onChange={v => setF({ ...f, pipThreshold: v })}
          note="Below this raises a plan" />
      </div>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 14, marginBottom: 6 }}>
        <p style={{ fontWeight: 700, color: T.navy, fontSize: 13, margin: 0 }}>Items</p>
        <Badge variant={valid ? 'green' : 'red'}>{total.toFixed(2)}% of 100%</Badge>
      </div>

      {f.items.map((i, idx) => (
        <Card key={idx} style={{ padding: 10, marginBottom: 8 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '3fr 1fr', gap: 10 }}>
            <Input label={`Item ${idx + 1}`} value={i.itemName} onChange={v => setItem(idx, 'itemName', v)} />
            <Input label="Weight %" type="number" value={i.weightPercent} onChange={v => setItem(idx, 'weightPercent', v)} />
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr auto', gap: 10, alignItems: 'flex-end' }}>
            <Select label="Measured" value={i.measurementType} onChange={v => setItem(idx, 'measurementType', v)}
              options={[{ value: 'Qualitative', label: 'Qualitative' }, { value: 'Quantitative', label: 'Quantitative' }]} />
            <Select label="Source" value={i.targetSource} onChange={v => setItem(idx, 'targetSource', v)}
              options={SOURCES.map(v => ({ value: v, label: v }))} />
            <Input label="Unit" value={i.unit} onChange={v => setItem(idx, 'unit', v)} />
            {f.items.length > 1 &&
              <Btn size="sm" variant="ghost" onClick={() => dropItem(idx)} style={{ marginBottom: 12 }}>Remove</Btn>}
          </div>
          {SYSTEM_SOURCES.includes(i.targetSource) && (
            <p style={{ fontSize: 11, color: T.blue, margin: 0 }}>
              Scored automatically — nobody types a figure for this item.
            </p>
          )}
          {i.targetSource === 'Revenue' && (
            <p style={{ fontSize: 11, color: T.amber, margin: 0 }}>
              Not wired yet: per-employee revenue lives in CRM, not finance. Score this manually for now.
            </p>
          )}
        </Card>
      ))}
      <Btn size="sm" variant="outline" onClick={addItem}>+ Item</Btn>

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !f.name.trim() || !valid}>{busy ? 'Saving…' : 'Save scorecard'}</Btn>
      </div>
      {!valid && <p style={{ fontSize: 12, color: T.red, textAlign: 'right', marginTop: 6 }}>
        The weights must total exactly 100% before this can be saved.
      </p>}
    </Modal>
  )
}

function TargetsModal({ card, flash, onClose, onSaved }) {
  const rows = useData(() => hr.listKpiTargets({ year: card.year }), [card.id])
  const [saving, setSaving] = useState(null)

  const save = async (t, field, value) => {
    setSaving(t.id)
    try {
      relay(flash, await hr.setKpiTarget({ kpiTargetId: t.id, [field]: Number(value) }))
      rows.reload(); onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the target.') }
    finally { setSaving(null) }
  }

  return (
    <Modal title={`${card.name} — targets`} onClose={onClose} width={760}>
      <p style={{ fontSize: 12, color: T.mgrey, marginTop: 0 }}>
        A quantitative item scores actual against target, so a target of zero cannot be scored. Attendance and
        360 items fill themselves in — leave those alone.
      </p>
      {rows.loading ? <Loading /> : (
        <DataTable
          headers={['Employee', 'Item', 'Target', 'Actual', 'Source']}
          empty="No targets — use “Assign targets” first."
          rows={(rows.data ?? []).map(t => [
            <span>
              <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{t.employeeNumber}</span>
              <span style={{ marginLeft: 6 }}>{t.employeeName}</span>
            </span>,
            <span>{t.itemName}{t.unit && <span style={{ color: T.mgrey }}> ({t.unit})</span>}</span>,
            <input type="number" defaultValue={t.targetValue} disabled={saving === t.id}
              onBlur={e => Number(e.target.value) !== t.targetValue && save(t, 'targetValue', e.target.value)}
              style={{ width: 110, height: 30, padding: '0 8px', border: `1px solid ${T.lgrey}`, borderRadius: 5 }} />,
            <input type="number" defaultValue={t.actualValue} disabled={saving === t.id}
              onBlur={e => Number(e.target.value) !== t.actualValue && save(t, 'actualValue', e.target.value)}
              style={{ width: 110, height: 30, padding: '0 8px', border: `1px solid ${T.lgrey}`, borderRadius: 5 }} />,
            <Badge variant={t.actualSource === 'Manual' ? 'default' : 'blue'}>{t.actualSource}</Badge>,
          ])}
        />
      )}
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Close</Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Appraisals (P15 + P16)
// ═════════════════════════════════════════════════════════════════════════════
export function AppraisalsTab({ flash }) {
  const [year, setYear] = useState(new Date().getFullYear())
  const [openId, setOpenId] = useState(null)
  const [opening, setOpening] = useState(false)

  const summary = useData(() => hr.appraisalSummary(year), [year])
  const cycles = useData(() => hr.listAppraisalCycles(year), [year])
  const list = useData(() => hr.listAppraisals({}), [year])

  if (list.loading) return <Loading />
  const s = summary.data
  const reload = () => { summary.reload(); cycles.reload(); list.reload() }

  if (openId) return <AppraisalDetail id={openId} flash={flash} onBack={() => { setOpenId(null); reload() }} />

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Appraisals" value={s?.appraisals ?? 0} sub={s?.openCycleName ?? 'no open cycle'} />
        <Kpi label="With employee" value={s?.awaitingSelf ?? 0} />
        <Kpi label="With manager" value={s?.awaitingLineManager ?? 0} color={T.amber} />
        <Kpi label="With MD" value={s?.awaitingMd ?? 0} color={T.blue} />
        <Kpi label="With HR" value={s?.awaitingHr ?? 0} color={T.purple} />
        <Kpi label="Completed" value={s?.completed ?? 0} color={T.green} />
        <Kpi label="Average score" value={pct(s?.averageFinalScore)} />
      </div>

      <SectionHeader
        title="Appraisal Cycles"
        sub="Two windows a year. Opening one raises an appraisal per active employee, each snapshotting the scorecard and targets in force at that moment."
        action={<Btn size="sm" onClick={() => setOpening(true)}>Open a cycle</Btn>}
      />
      <DataTable
        headers={['Cycle', 'Type', 'Runs', 'Status', 'Appraisals', 'Completed', '']}
        empty={`No cycles for ${year}.`}
        rows={(cycles.data ?? []).map(c => [
          c.name, c.cycleType, `${fmtDate(c.startDate)} – ${fmtDate(c.endDate)}`,
          <Badge variant={c.status === 'Open' ? 'green' : c.status === 'Closed' ? 'default' : 'amber'}>{c.status}</Badge>,
          c.appraisalCount, c.completed,
          c.status === 'Open'
            ? <Btn size="sm" variant="ghost" onClick={async () => {
                try { relay(flash, await hr.closeAppraisalCycle(c.id)); reload() }
                catch (e) { relayError(flash, e, 'Could not close the cycle.') }
              }}>Close</Btn>
            : '',
        ])}
      />

      <div style={{ marginTop: 26 }}>
        <SectionHeader title="Appraisals" sub="Self-assessment → line manager → MD → HR. Each step waits on the one before it, and no one person may take two in a row." />
      </div>
      <DataTable
        headers={['Employee', 'Cycle', 'Scorecard', 'Self', 'Manager', 'Final', 'Status', '']}
        empty="No appraisals yet."
        rows={(list.data ?? []).map(a => [
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{a.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{a.employeeName}</span>
            {a.pipTriggered && <Badge variant="red">PIP</Badge>}
          </span>,
          a.cycleName, a.scorecardName ?? '—',
          pct(a.selfScore), pct(a.lineManagerScore),
          <strong style={{ color: a.finalScore != null && a.finalScore < a.pipThreshold ? T.red : T.dgrey }}>
            {pct(a.finalScore)}
          </strong>,
          <span>
            <Badge variant={APPRAISAL_VARIANT[a.status] ?? 'default'}>{a.status}</Badge>
            <span style={{ display: 'block', fontSize: 11, color: T.mgrey, marginTop: 2 }}>{a.awaitingLabel}</span>
          </span>,
          <Btn size="sm" variant="outline" onClick={() => setOpenId(a.id)}>Open</Btn>,
        ])}
      />

      {opening && <OpenCycleModal year={year} flash={flash}
        onClose={() => setOpening(false)} onSaved={() => { setOpening(false); reload() }} />}
    </div>
  )
}

function OpenCycleModal({ year, flash, onClose, onSaved }) {
  const [f, setF] = useState({ cycleType: 'MidYear', year, name: '', startDate: '', endDate: '' })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.openAppraisalCycle({
        cycleType: f.cycleType, year: Number(f.year), name: f.name || null,
        startDate: f.startDate ? `${f.startDate}T00:00:00Z` : null,
        endDate: f.endDate ? `${f.endDate}T00:00:00Z` : null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not open the cycle.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Open an appraisal cycle" onClose={onClose}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Select label="Cycle" value={f.cycleType} onChange={v => setF({ ...f, cycleType: v })}
          options={[{ value: 'MidYear', label: 'Mid-year' }, { value: 'EndOfYear', label: 'End of year' }]} />
        <Input label="Year" type="number" value={f.year} onChange={v => setF({ ...f, year: v })} />
      </div>
      <Input label="Name" value={f.name} onChange={v => setF({ ...f, name: v })} note="Defaults to the cycle and year" />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Opens" type="date" value={f.startDate} onChange={v => setF({ ...f, startDate: v })} />
        <Input label="Closes" type="date" value={f.endDate} onChange={v => setF({ ...f, endDate: v })} />
      </div>
      <p style={{ fontSize: 12, color: T.mgrey }}>
        One appraisal is raised per active employee, on the scorecard for their role. Anyone whose role has no
        scorecard is skipped and named in the result.
      </p>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy}>{busy ? 'Opening…' : 'Open cycle'}</Btn>
      </div>
    </Modal>
  )
}

function AppraisalDetail({ id, flash, onBack }) {
  const [scores, setScores] = useState({})
  const [comments, setComments] = useState('')
  const [trainingNeeds, setTrainingNeeds] = useState('')
  const [busy, setBusy] = useState(false)
  const [requesting, setRequesting] = useState(false)

  const appraisal = useData(() => hr.getAppraisal(id), [id])
  const fb = useData(() => hr.getFeedback360(id), [id])

  if (appraisal.loading) return <Loading />
  const a = appraisal.data
  if (!a) return <Alert type="error">That appraisal no longer exists.</Alert>

  // Only ever offer the step the appraisal is actually on. The server would refuse anything else, so
  // presenting it would just be inviting a failure.
  const step = {
    PendingSelf: { label: 'Submit self-assessment', fn: hr.submitSelfAssessment, editable: 'self' },
    PendingLineManager: { label: 'Submit review', fn: hr.reviewAppraisal, editable: 'manager' },
    PendingMd: { label: 'Sign off', fn: hr.signOffAppraisal, editable: null },
    PendingHr: { label: 'Record', fn: hr.recordAppraisal, editable: null },
  }[a.status]

  const submit = async () => {
    setBusy(true)
    try {
      relay(flash, await step.fn(id, {
        itemScores: Object.entries(scores).map(([itemScoreId, v]) => ({
          itemScoreId, score: v.score === '' || v.score == null ? null : Number(v.score),
          actualValue: v.actual === '' || v.actual == null ? null : Number(v.actual),
          comments: v.comments || null,
        })),
        comments: comments || null,
        trainingNeeds: trainingNeeds || null,
      }))
      setScores({}); setComments(''); setTrainingNeeds('')
      appraisal.reload()
    } catch (e) { relayError(flash, e, 'Could not submit.') }
    finally { setBusy(false) }
  }

  const setScore = (itemId, key, v) => setScores(s => ({ ...s, [itemId]: { ...s[itemId], [key]: v } }))

  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 14, flexWrap: 'wrap' }}>
        <Btn size="sm" variant="ghost" onClick={onBack}>← All appraisals</Btn>
        <span style={{ fontWeight: 800, fontSize: 17, color: T.navy }}>{a.employeeName}</span>
        <Badge variant={APPRAISAL_VARIANT[a.status] ?? 'default'}>{a.status}</Badge>
        {a.pipTriggered && <Badge variant="red">improvement plan raised</Badge>}
      </div>

      <Alert type="info">
        <strong>{a.awaitingLabel}.</strong> {a.cycleName} · {a.scorecardName} · PIP threshold {pct(a.pipThreshold)}.
        Whoever took the previous step cannot take this one.
      </Alert>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Self" value={pct(a.selfScore)} sub={fmtDate(a.selfAssessmentSubmittedAt)} />
        <Kpi label="Line manager" value={pct(a.lineManagerScore)} sub={fmtDate(a.lineManagerReviewedAt)} />
        <Kpi label="360 aggregate" value={pct(a.feedback360Score)} sub={`${fb.data?.submitted ?? 0} of ${fb.data?.requested ?? 0} in`} />
        <Kpi label="Final" value={pct(a.finalScore)}
          color={a.finalScore != null && a.finalScore < a.pipThreshold ? T.red : T.green}
          sub={`threshold ${pct(a.pipThreshold)}`} />
      </div>

      <SectionHeader title="Scores" sub="Quantitative items score themselves from actual against target. Attendance and 360 items are filled in by the system." />
      <DataTable
        headers={['Weight', 'Item', 'Target', 'Actual', 'Self', 'Manager', 'Final', 'Comment']}
        empty="No items."
        rows={(a.itemScores ?? []).map(s => {
          const system = SYSTEM_SOURCES.includes(s.source)
          const editable = step?.editable && !system
          const quantitative = s.measurementType === 'Quantitative'
          return [
            `${pct(s.weightPercent)}%`,
            <span>
              {s.itemName}
              {s.scoringNote && <span style={{ display: 'block', fontSize: 11, color: system ? T.blue : T.mgrey }}>{s.scoringNote}</span>}
            </span>,
            s.targetValue ? s.targetValue.toLocaleString('en-KE') : '—',
            editable && quantitative
              ? <input type="number" placeholder={String(s.actualValue)}
                  onChange={e => setScore(s.id, 'actual', e.target.value)}
                  style={{ width: 92, height: 30, padding: '0 8px', border: `1px solid ${T.lgrey}`, borderRadius: 5 }} />
              : s.actualValue.toLocaleString('en-KE'),
            pct(s.selfScore),
            pct(s.managerScore),
            <strong>{pct(s.finalScore)}</strong>,
            editable && !quantitative
              ? <input type="number" placeholder="0–100"
                  onChange={e => setScore(s.id, 'score', e.target.value)}
                  style={{ width: 80, height: 30, padding: '0 8px', border: `1px solid ${T.lgrey}`, borderRadius: 5 }} />
              : <span style={{ fontSize: 12, color: T.mgrey }}>{s.comments ?? ''}</span>,
          ]
        })}
      />

      {step && (
        <Card style={{ padding: 14, marginTop: 16 }}>
          <p style={{ fontWeight: 700, color: T.navy, margin: 0, fontSize: 14 }}>{step.label}</p>
          <Input label="Comments" value={comments} onChange={setComments} />
          {(a.status === 'PendingSelf' || a.status === 'PendingLineManager' || a.status === 'PendingHr') && (
            <Input label="Training needs" value={trainingNeeds} onChange={setTrainingNeeds}
              note="HR-020 — these feed the employee's learning plan" />
          )}
          {a.status === 'PendingMd' && a.finalScore != null && a.finalScore < a.pipThreshold && (
            <Alert type="warning">
              This score is below the {pct(a.pipThreshold)} threshold — signing off raises an improvement plan
              automatically (HR-019).
            </Alert>
          )}
          <Btn onClick={submit} disabled={busy} style={{ marginTop: 8 }}>{busy ? 'Submitting…' : step.label}</Btn>
        </Card>
      )}

      <div style={{ marginTop: 26 }}>
        <SectionHeader
          title="360-Degree Feedback"
          sub="Individual ratings are anonymous — only counts, group averages and outstanding reviewers are shown."
          action={<Btn size="sm" variant="outline" onClick={() => setRequesting(true)}>Request feedback</Btn>}
        />
      </div>
      {fb.loading ? <Loading /> : (
        <Card style={{ padding: 14 }}>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 12 }}>
            <Kpi label="Responses" value={`${fb.data?.submitted ?? 0} / ${fb.data?.requested ?? 0}`} />
            <Kpi label="Peers" value={pct(fb.data?.peerAverage)} />
            <Kpi label="Subordinates" value={pct(fb.data?.subordinateAverage)} />
            <Kpi label="Line manager" value={pct(fb.data?.lineManagerScore)} />
            <Kpi label="Aggregate" value={pct(fb.data?.aggregate)} color={T.navy} />
          </div>
          {(fb.data?.awaitingResponse ?? []).length > 0 && (
            <p style={{ fontSize: 12, color: T.amber, marginTop: 10 }}>
              Awaiting: {fb.data.awaitingResponse.join(', ')}
            </p>
          )}
          {(fb.data?.comments ?? []).length > 0 && (
            <div style={{ marginTop: 10 }}>
              <p style={{ fontSize: 12, fontWeight: 700, color: T.navy, margin: 0 }}>Comments (unattributed)</p>
              {fb.data.comments.map((c, i) => (
                <p key={i} style={{ fontSize: 12, color: T.dgrey, margin: '4px 0 0', fontStyle: 'italic' }}>“{c}”</p>
              ))}
            </div>
          )}
        </Card>
      )}

      {requesting && <RequestFeedbackModal appraisalId={id} employeeId={a.employeeId} flash={flash}
        onClose={() => setRequesting(false)} onSaved={() => { setRequesting(false); fb.reload() }} />}
    </div>
  )
}

function RequestFeedbackModal({ appraisalId, employeeId, flash, onClose, onSaved }) {
  const [employees, setEmployees] = useState([])
  const [picked, setPicked] = useState({})
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    hr.listEmployees({ pageSize: 200 })
      .then(r => setEmployees((r.data ?? []).filter(e => e.id !== employeeId))).catch(() => {})
  }, [employeeId])

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.requestFeedback360(appraisalId, {
        reviewers: Object.entries(picked).filter(([, t]) => t)
          .map(([reviewerEmployeeId, reviewerType]) => ({ reviewerEmployeeId, reviewerType })),
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not request feedback.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Request 360 feedback" onClose={onClose} width={620}>
      <p style={{ fontSize: 12, color: T.mgrey, marginTop: 0 }}>
        Peers and subordinates are averaged separately, so a large peer group cannot drown out a single
        subordinate. What each person says stays anonymous to the employee.
      </p>
      <div style={{ maxHeight: 320, overflowY: 'auto', border: `1px solid ${T.lgrey}`, borderRadius: 7, padding: 8 }}>
        {employees.map(e => (
          <div key={e.id} style={{ display: 'flex', gap: 10, alignItems: 'center', padding: '4px 0' }}>
            <span style={{ flex: 1, fontSize: 13 }}>
              <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{e.employeeNumber}</span> {e.fullName}
            </span>
            <select value={picked[e.id] ?? ''} onChange={ev => setPicked({ ...picked, [e.id]: ev.target.value })}
              style={{ height: 30, padding: '0 8px', border: `1px solid ${T.lgrey}`, borderRadius: 5, fontSize: 12 }}>
              <option value="">not a reviewer</option>
              <option value="Peer">Peer</option>
              <option value="Subordinate">Subordinate</option>
              <option value="LineManager">Line manager</option>
            </select>
          </div>
        ))}
      </div>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy || !Object.values(picked).some(Boolean)}>
          {busy ? 'Requesting…' : 'Request'}
        </Btn>
      </div>
    </Modal>
  )
}

// ═════════════════════════════════════════════════════════════════════════════
// Improvement plans (P17)
// ═════════════════════════════════════════════════════════════════════════════
export function PipsTab({ flash }) {
  const [status, setStatus] = useState('')
  const [editing, setEditing] = useState(null)
  const rows = useData(() => hr.listPips({ status: status || undefined }), [status])

  if (rows.loading) return <Loading />

  const close = async (p) => {
    const outcome = window.prompt(`Close ${p.employeeName}'s plan. Outcome — type Completed, Extended or EscalatedToDisciplinary:`)
    if (!outcome) return
    const notes = window.prompt('Note on the outcome:')
    if (notes === null) return
    let newReviewDate = null
    if (outcome === 'Extended') {
      const d = window.prompt('New review date (YYYY-MM-DD):')
      if (!d) return
      newReviewDate = `${d}T00:00:00Z`
    }
    try { relay(flash, await hr.closePip(p.id, { outcome, notes, newReviewDate })); rows.reload() }
    catch (e) { relayError(flash, e, 'Could not close the plan.') }
  }

  return (
    <div>
      <Alert type="info">
        <strong>Raised automatically</strong> when the MD signs off an appraisal below the scorecard's threshold
        (HR-019). Three ways out: improved and closed, extended with new dates, or escalated to the disciplinary
        process — which is H10 and not yet built, so an escalation is recorded and alerted here rather than
        opening a case.
      </Alert>

      <SectionHeader title="Performance Improvement Plans"
        sub="The trigger score and threshold are stored on the plan, so it stays explicable even after the scorecard changes." />

      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        <select value={status} onChange={e => setStatus(e.target.value)}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All</option>
          {['Active', 'Extended', 'Completed', 'EscalatedToDisciplinary'].map(v =>
            <option key={v} value={v}>{PIP_LABEL[v]}</option>)}
        </select>
      </div>

      <DataTable
        headers={['Employee', 'Trigger', 'Started', 'Review', 'Status', 'Objectives', 'Actions']}
        empty="No improvement plans."
        rows={(rows.data ?? []).map(p => [
          <span>
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{p.employeeNumber}</span>
            <span style={{ marginLeft: 6 }}>{p.employeeName}</span>
          </span>,
          <span style={{ color: T.red, fontWeight: 600 }}>{pct(p.triggerScore)}</span>,
          fmtDate(p.startDate),
          <span style={{ color: p.isReviewDue ? T.red : T.dgrey, fontWeight: p.isReviewDue ? 600 : 400 }}>
            {fmtDate(p.secondReviewDate ?? p.reviewDate)}{p.isReviewDue && ' — due'}
          </span>,
          <span>
            <Badge variant={PIP_VARIANT[p.status] ?? 'default'}>{PIP_LABEL[p.status] ?? p.status}</Badge>
            {p.outcome && <span style={{ display: 'block', fontSize: 11, color: T.mgrey, marginTop: 2 }}>{p.outcome}</span>}
          </span>,
          <span style={{ fontSize: 12, color: p.objectives ? T.dgrey : T.red }}>
            {p.objectives ?? 'none set'}
          </span>,
          p.status === 'Active' || p.status === 'Extended'
            ? <div style={{ display: 'flex', gap: 6 }}>
                <Btn size="sm" variant="outline" onClick={() => setEditing(p)}>Edit</Btn>
                <Btn size="sm" onClick={() => close(p)}>Close</Btn>
              </div>
            : <span style={{ fontSize: 11, color: T.mgrey }}>closed</span>,
        ])}
      />

      {editing && <PipModal pip={editing} flash={flash}
        onClose={() => setEditing(null)} onSaved={() => { setEditing(null); rows.reload() }} />}
    </div>
  )
}

function PipModal({ pip, flash, onClose, onSaved }) {
  const [f, setF] = useState({
    objectives: pip.objectives ?? '', supportProvided: pip.supportProvided ?? '',
    reviewDate: pip.reviewDate?.slice(0, 10) ?? '',
    secondReviewDate: pip.secondReviewDate?.slice(0, 10) ?? '',
  })
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    try {
      relay(flash, await hr.updatePip(pip.id, {
        objectives: f.objectives || null, supportProvided: f.supportProvided || null,
        reviewDate: f.reviewDate ? `${f.reviewDate}T00:00:00Z` : null,
        secondReviewDate: f.secondReviewDate ? `${f.secondReviewDate}T00:00:00Z` : null,
      }))
      onSaved()
    } catch (e) { relayError(flash, e, 'Could not save the plan.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title={`${pip.employeeName} — improvement plan`} onClose={onClose} width={620}>
      <Card style={{ padding: 12, marginBottom: 12 }}>
        <p style={{ margin: 0, fontSize: 13 }}>
          Raised at <strong style={{ color: T.red }}>{pct(pip.triggerScore)}</strong> against a threshold of {pct(pip.threshold)}.
        </p>
      </Card>
      <Input label="Objectives" value={f.objectives} onChange={v => setF({ ...f, objectives: v })}
        note="What has to change, in measurable terms" />
      <Input label="Support provided" value={f.supportProvided} onChange={v => setF({ ...f, supportProvided: v })}
        note="A plan with no support is not a plan" />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Review date" type="date" value={f.reviewDate} onChange={v => setF({ ...f, reviewDate: v })} />
        <Input label="Second review" type="date" value={f.secondReviewDate} onChange={v => setF({ ...f, secondReviewDate: v })} />
      </div>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Save'}</Btn>
      </div>
    </Modal>
  )
}
