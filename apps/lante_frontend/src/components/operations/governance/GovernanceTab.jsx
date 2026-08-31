import { useState, useEffect, useCallback } from 'react'
import * as ops from '../../../services/operations.js'

// PR3 — RAID and change control on one tab. Risks, issues and change requests are three views of the
// same question ("what could stop this, what already has, and what have we agreed to change"), so
// they sit together rather than scattered across the project.

const kes = (v) => `KES ${Number(v ?? 0).toLocaleString('en-KE', { maximumFractionDigits: 0 })}`
const fmt = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')

const SCALE = ['Low', 'Medium', 'High']
const ISSUE_SEVERITIES = ['Low', 'Medium', 'High', 'Critical']

// Severity carries a shape as well as a colour — the band is also printed as text, so the register
// stays readable in greyscale and to anyone who cannot separate red from green.
const SEVERITY_STYLE = {
  Critical: 'bg-red-100 text-red-800 border-red-200',
  High:     'bg-orange-100 text-orange-800 border-orange-200',
  Medium:   'bg-amber-100 text-amber-800 border-amber-200',
  Low:      'bg-emerald-100 text-emerald-800 border-emerald-200',
}

const CR_STYLE = {
  Draft:     'bg-gray-100 text-gray-700',
  Submitted: 'bg-amber-100 text-amber-800',
  Approved:  'bg-emerald-100 text-emerald-800',
  Rejected:  'bg-red-100 text-red-700',
  Withdrawn: 'bg-gray-100 text-gray-500',
}

function Pill({ children, className = '' }) {
  return <span className={`inline-block text-[11px] font-semibold px-2 py-0.5 rounded border ${className}`}>{children}</span>
}

function Stat({ label, value, tone }) {
  const toneCls = tone === 'bad' ? 'text-red-700' : tone === 'warn' ? 'text-amber-700' : 'text-gray-900'
  return (
    <div className="border border-gray-200 rounded-lg px-4 py-3">
      <p className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">{label}</p>
      <p className={`text-lg font-bold mt-0.5 tabular-nums ${toneCls}`}>{value}</p>
    </div>
  )
}

function Empty({ icon, title, sub }) {
  return (
    <div className="text-center py-10 border border-dashed border-gray-200 rounded-lg">
      <div className="text-2xl mb-1">{icon}</div>
      <p className="text-sm font-semibold text-gray-700">{title}</p>
      {sub && <p className="text-xs text-gray-400 mt-1">{sub}</p>}
    </div>
  )
}

export default function GovernanceTab({ projectId, canWrite, canApprove, onToast }) {
  const [view, setView] = useState('risks')
  const [summary, setSummary] = useState(null)
  const [risks, setRisks] = useState([])
  const [issues, setIssues] = useState([])
  const [crs, setCrs] = useState([])
  const [versions, setVersions] = useState([])
  const [includeClosed, setIncludeClosed] = useState(false)
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)

  const [riskForm, setRiskForm] = useState(null)
  const [issueForm, setIssueForm] = useState(null)
  const [crForm, setCrForm] = useState(null)

  const load = useCallback(async () => {
    try {
      const [s, r, i, c, v] = await Promise.all([
        ops.getGovernanceSummary(projectId),
        ops.getProjectRisks(projectId, includeClosed),
        ops.getProjectIssues(projectId, includeClosed),
        ops.getChangeRequests(projectId),
        ops.getBudgetVersions(projectId).catch(() => []),
      ])
      setSummary(s); setRisks(r ?? []); setIssues(i ?? []); setCrs(c ?? []); setVersions(v ?? [])
    } catch {
      setErr('Could not load the governance register.')
    }
  }, [projectId, includeClosed])

  useEffect(() => { load() }, [load])

  // Every action funnels through here so the API's message — which is the useful part, e.g. why a
  // change request cannot be approved — reaches the screen instead of a generic failure.
  const run = async (fn, okMsg) => {
    setBusy(true); setErr('')
    try {
      await fn()
      await load()
      if (okMsg) onToast?.(okMsg)
    } catch (e) {
      setErr(e?.response?.data?.message || 'Action failed.')
    } finally {
      setBusy(false)
    }
  }

  const tabs = [
    ['risks', `Risks${summary?.openRisks ? ` (${summary.openRisks})` : ''}`],
    ['issues', `Issues${summary?.openIssues ? ` (${summary.openIssues})` : ''}`],
    ['changes', `Change Requests${summary?.pendingChangeRequests ? ` (${summary.pendingChangeRequests})` : ''}`],
  ]

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
        <Stat label="Open risks" value={summary?.openRisks ?? 0} />
        <Stat label="High / critical" value={summary?.highRisks ?? 0} tone={summary?.highRisks ? 'bad' : undefined} />
        <Stat label="Reviews overdue" value={summary?.risksOverdueReview ?? 0} tone={summary?.risksOverdueReview ? 'warn' : undefined} />
        <Stat label="Open issues" value={summary?.openIssues ?? 0} />
        <Stat label="Issues overdue" value={summary?.overdueIssues ?? 0} tone={summary?.overdueIssues ? 'bad' : undefined} />
        <Stat label="CRs awaiting" value={summary?.pendingChangeRequests ?? 0} tone={summary?.pendingChangeRequests ? 'warn' : undefined} />
      </div>

      {err && <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2.5">{err}</div>}

      <div className="flex items-center justify-between gap-3 flex-wrap border-b border-gray-200">
        <div className="flex gap-1">
          {tabs.map(([k, label]) => (
            <button
                key={k}
                onClick={() => setView(k)}
                className={`px-3 py-2 text-sm font-semibold border-b-2 -mb-px ${
                  view === k ? 'border-amber-500 text-amber-700' : 'border-transparent text-gray-500 hover:text-gray-800'}`}>
              {label}
            </button>
          ))}
        </div>
        {view !== 'changes' && (
          <label className="flex items-center gap-2 text-xs text-gray-500 pb-2">
            <input type="checkbox" checked={includeClosed} onChange={e => setIncludeClosed(e.target.checked)} />
            Show closed
          </label>
        )}
      </div>

      {view === 'risks' && (
        <RiskRegister
            risks={risks} canWrite={canWrite} busy={busy}
            form={riskForm} setForm={setRiskForm}
            onSave={(dto, id) => run(
              () => (id ? ops.updateProjectRisk(projectId, id, dto) : ops.addProjectRisk(projectId, dto)),
              id ? 'Risk updated.' : 'Risk added.').then(() => setRiskForm(null))}
            onDelete={(id) => run(() => ops.deleteProjectRisk(projectId, id), 'Risk removed.')}
            onRealise={(id, dto) => run(() => ops.realiseProjectRisk(projectId, id, dto), 'Risk realised as an issue.')}
        />
      )}

      {view === 'issues' && (
        <IssueLog
            issues={issues} canWrite={canWrite} busy={busy}
            form={issueForm} setForm={setIssueForm}
            onSave={(dto, id) => run(
              () => (id ? ops.updateProjectIssue(projectId, id, dto) : ops.addProjectIssue(projectId, dto)),
              id ? 'Issue updated.' : 'Issue raised.').then(() => setIssueForm(null))}
            onResolve={(id, dto) => run(() => ops.resolveProjectIssue(projectId, id, dto), 'Issue resolved.')}
        />
      )}

      {view === 'changes' && (
        <ChangeRequests
            crs={crs} versions={versions} canWrite={canWrite} canApprove={canApprove} busy={busy}
            form={crForm} setForm={setCrForm}
            onSave={(dto, id) => run(
              () => (id ? ops.updateChangeRequest(projectId, id, dto) : ops.createChangeRequest(projectId, dto)),
              id ? 'Change request updated.' : 'Change request raised.').then(() => setCrForm(null))}
            onSubmit={(id) => run(() => ops.submitChangeRequest(projectId, id), 'Sent for approval.')}
            onWithdraw={(id) => run(() => ops.withdrawChangeRequest(projectId, id), 'Change request withdrawn.')}
            onDecide={(id, dto) => run(
              () => ops.decideChangeRequest(projectId, id, dto),
              dto.approved ? 'Approved — the baseline has moved.' : 'Change request rejected.')}
        />
      )}
    </div>
  )
}

// ── Risks ─────────────────────────────────────────────────────────────────────

function RiskRegister({ risks, canWrite, busy, form, setForm, onSave, onDelete, onRealise }) {
  const blank = { title: '', description: '', likelihood: 'Low', impact: 'Low', mitigation: '', ownerUserId: '', reviewDate: '', status: 'Open' }

  return (
    <div className="space-y-4">
      {canWrite && (
        <div className="flex justify-end">
          <button onClick={() => setForm(form ? null : blank)} className="text-xs font-semibold text-amber-600 hover:text-amber-800">
            {form ? 'Cancel' : '+ Add risk'}
          </button>
        </div>
      )}

      {form && (
        <form
            onSubmit={(e) => { e.preventDefault(); onSave(form, form.id) }}
            className="border border-gray-200 rounded-lg p-4 space-y-3 bg-gray-50">
          <input className="input" placeholder="What could go wrong?" value={form.title}
                 onChange={e => setForm({ ...form, title: e.target.value })} required />
          <textarea className="input" rows={2} placeholder="Detail (optional)" value={form.description}
                    onChange={e => setForm({ ...form, description: e.target.value })} />
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <label className="text-xs font-semibold text-gray-600">Likelihood
              <select className="input mt-1" value={form.likelihood} onChange={e => setForm({ ...form, likelihood: e.target.value })}>
                {SCALE.map(s => <option key={s}>{s}</option>)}
              </select>
            </label>
            <label className="text-xs font-semibold text-gray-600">Impact
              <select className="input mt-1" value={form.impact} onChange={e => setForm({ ...form, impact: e.target.value })}>
                {SCALE.map(s => <option key={s}>{s}</option>)}
              </select>
            </label>
            <label className="text-xs font-semibold text-gray-600">Review by
              <input type="date" className="input mt-1" value={form.reviewDate?.slice(0, 10) ?? ''}
                     onChange={e => setForm({ ...form, reviewDate: e.target.value })} />
            </label>
            {form.id && (
              <label className="text-xs font-semibold text-gray-600">Status
                <select className="input mt-1" value={form.status} onChange={e => setForm({ ...form, status: e.target.value })}>
                  {['Open', 'Mitigating', 'Mitigated', 'Closed'].map(s => <option key={s}>{s}</option>)}
                </select>
              </label>
            )}
          </div>
          <input className="input" placeholder="Mitigation — what are we doing about it?" value={form.mitigation ?? ''}
                 onChange={e => setForm({ ...form, mitigation: e.target.value })} />
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setForm(null)} className="text-xs text-gray-500">Cancel</button>
            <button type="submit" disabled={busy} className="text-xs font-semibold bg-amber-500 text-white px-3 py-1.5 rounded">
              {form.id ? 'Save risk' : 'Add risk'}
            </button>
          </div>
        </form>
      )}

      {risks.length === 0
        ? <Empty icon="🛡️" title="No risks logged" sub="A register nobody fills in is the same as no register." />
        : (
          <div className="space-y-2">
            {risks.map(r => (
              <div key={r.id} className="border border-gray-200 rounded-lg px-4 py-3">
                <div className="flex items-start justify-between gap-3 flex-wrap">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="font-semibold text-sm text-gray-900">{r.title}</span>
                      <Pill className={SEVERITY_STYLE[r.severity]}>{r.severity} · {r.score}/9</Pill>
                      <Pill className="bg-gray-100 text-gray-600 border-gray-200">{r.status}</Pill>
                      {r.reviewOverdue && <Pill className="bg-red-100 text-red-800 border-red-200">review overdue</Pill>}
                    </div>
                    {r.description && <p className="text-xs text-gray-500 mt-1">{r.description}</p>}
                    <p className="text-[11px] text-gray-400 mt-1">
                      {r.likelihood} likelihood × {r.impact} impact
                      {r.reviewDate && ` · review by ${fmt(r.reviewDate)}`}
                    </p>
                    {r.mitigation && <p className="text-xs text-gray-600 mt-1"><span className="font-semibold">Mitigation:</span> {r.mitigation}</p>}
                  </div>
                  {canWrite && r.status !== 'Realised' && (
                    <div className="flex gap-2 shrink-0">
                      <button onClick={() => setForm({ ...r, reviewDate: r.reviewDate?.slice(0, 10) ?? '' })}
                              className="text-xs text-gray-500 hover:text-gray-800">Edit</button>
                      <button
                          onClick={() => window.confirm('This risk has happened. Close it and open an issue?') && onRealise(r.id, {})}
                          className="text-xs font-semibold text-orange-600 hover:text-orange-800">It happened</button>
                      <button onClick={() => window.confirm('Remove this risk?') && onDelete(r.id)}
                              className="text-xs text-red-500 hover:text-red-700">Del</button>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
    </div>
  )
}

// ── Issues ────────────────────────────────────────────────────────────────────

function IssueLog({ issues, canWrite, busy, form, setForm, onSave, onResolve }) {
  const blank = { title: '', description: '', severity: 'Medium', ownerUserId: '', targetResolutionDate: '', status: 'Open' }

  return (
    <div className="space-y-4">
      {canWrite && (
        <div className="flex justify-end">
          <button onClick={() => setForm(form ? null : blank)} className="text-xs font-semibold text-amber-600 hover:text-amber-800">
            {form ? 'Cancel' : '+ Raise issue'}
          </button>
        </div>
      )}

      {form && (
        <form
            onSubmit={(e) => { e.preventDefault(); onSave(form, form.id) }}
            className="border border-gray-200 rounded-lg p-4 space-y-3 bg-gray-50">
          <input className="input" placeholder="What has gone wrong?" value={form.title}
                 onChange={e => setForm({ ...form, title: e.target.value })} required />
          <textarea className="input" rows={2} placeholder="Detail" value={form.description}
                    onChange={e => setForm({ ...form, description: e.target.value })} />
          <div className="grid grid-cols-2 gap-3">
            <label className="text-xs font-semibold text-gray-600">Severity
              <select className="input mt-1" value={form.severity} onChange={e => setForm({ ...form, severity: e.target.value })}>
                {ISSUE_SEVERITIES.map(s => <option key={s}>{s}</option>)}
              </select>
            </label>
            <label className="text-xs font-semibold text-gray-600">Resolve by
              <input type="date" className="input mt-1" value={form.targetResolutionDate?.slice(0, 10) ?? ''}
                     onChange={e => setForm({ ...form, targetResolutionDate: e.target.value })} />
            </label>
          </div>
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setForm(null)} className="text-xs text-gray-500">Cancel</button>
            <button type="submit" disabled={busy} className="text-xs font-semibold bg-amber-500 text-white px-3 py-1.5 rounded">
              {form.id ? 'Save issue' : 'Raise issue'}
            </button>
          </div>
        </form>
      )}

      {issues.length === 0
        ? <Empty icon="⚠️" title="No issues raised" sub="A risk is a maybe; an issue has already happened." />
        : (
          <div className="space-y-2">
            {issues.map(i => (
              <div key={i.id} className="border border-gray-200 rounded-lg px-4 py-3">
                <div className="flex items-start justify-between gap-3 flex-wrap">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="font-mono text-[11px] text-gray-400">{i.number}</span>
                      <span className="font-semibold text-sm text-gray-900">{i.title}</span>
                      <Pill className={SEVERITY_STYLE[i.severity]}>{i.severity}</Pill>
                      <Pill className="bg-gray-100 text-gray-600 border-gray-200">{i.status}</Pill>
                      {i.overdue && <Pill className="bg-red-100 text-red-800 border-red-200">overdue</Pill>}
                      {i.raisedFromRiskId && <Pill className="bg-orange-50 text-orange-700 border-orange-200">from a risk</Pill>}
                    </div>
                    {i.description && <p className="text-xs text-gray-500 mt-1">{i.description}</p>}
                    <p className="text-[11px] text-gray-400 mt-1">
                      Raised {fmt(i.raisedAt)}
                      {i.targetResolutionDate && ` · due ${fmt(i.targetResolutionDate)}`}
                    </p>
                    {i.resolution && <p className="text-xs text-emerald-700 mt-1"><span className="font-semibold">Resolved:</span> {i.resolution}</p>}
                  </div>
                  {canWrite && i.status !== 'Closed' && (
                    <div className="flex gap-2 shrink-0">
                      {i.status !== 'Resolved' && (
                        <button onClick={() => setForm({ ...i, targetResolutionDate: i.targetResolutionDate?.slice(0, 10) ?? '' })}
                                className="text-xs text-gray-500 hover:text-gray-800">Edit</button>
                      )}
                      <button
                          onClick={() => {
                            const r = window.prompt('How was it resolved?')
                            if (r) onResolve(i.id, { resolution: r, close: true })
                          }}
                          className="text-xs font-semibold text-emerald-600 hover:text-emerald-800">Resolve</button>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
    </div>
  )
}

// ── Change requests ───────────────────────────────────────────────────────────

function ChangeRequests({ crs, versions, canWrite, canApprove, busy, form, setForm, onSave, onSubmit, onWithdraw, onDecide }) {
  const blank = { title: '', description: '', justification: '', scheduleImpactDays: 0, budgetVersionId: '' }
  // Only a version that can still be approved is attachable — approving the change request is what
  // approves the budget, so an already-decided one would mean the money moved without this authority.
  const attachable = (versions ?? []).filter(v => v.status === 'Draft' || v.status === 'PendingApproval')

  return (
    <div className="space-y-4">
      <p className="text-xs text-gray-500">
        A change request is the only way to move an approved baseline. Cost impact travels as a budget
        version, so the request and the budget can never disagree about the figure.
      </p>

      {canWrite && (
        <div className="flex justify-end">
          <button onClick={() => setForm(form ? null : blank)} className="text-xs font-semibold text-amber-600 hover:text-amber-800">
            {form ? 'Cancel' : '+ Raise change request'}
          </button>
        </div>
      )}

      {form && (
        <form
            onSubmit={(e) => { e.preventDefault(); onSave(form, form.id) }}
            className="border border-gray-200 rounded-lg p-4 space-y-3 bg-gray-50">
          <input className="input" placeholder="What is changing?" value={form.title}
                 onChange={e => setForm({ ...form, title: e.target.value })} required />
          <textarea className="input" rows={2} placeholder="Description" value={form.description}
                    onChange={e => setForm({ ...form, description: e.target.value })} />
          <textarea className="input" rows={2} placeholder="Justification — why this is unavoidable" value={form.justification}
                    onChange={e => setForm({ ...form, justification: e.target.value })} required />
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <label className="text-xs font-semibold text-gray-600">Schedule impact (days)
              <input type="number" className="input mt-1" value={form.scheduleImpactDays}
                     onChange={e => setForm({ ...form, scheduleImpactDays: Number(e.target.value) || 0 })} />
              <span className="block text-[11px] font-normal text-gray-400 mt-0.5">
                Shifts every milestone baseline. Negative pulls the plan in.
              </span>
            </label>
            <label className="text-xs font-semibold text-gray-600">Cost impact (budget version)
              <select className="input mt-1" value={form.budgetVersionId ?? ''}
                      onChange={e => setForm({ ...form, budgetVersionId: e.target.value || null })}>
                <option value="">— none, schedule only —</option>
                {attachable.map(v => (
                  <option key={v.id} value={v.id}>v{v.versionNo} · {kes(v.totalPlanned)} · {v.status}</option>
                ))}
              </select>
              <span className="block text-[11px] font-normal text-gray-400 mt-0.5">
                Must be submitted for approval before this request can be sent.
              </span>
            </label>
          </div>
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setForm(null)} className="text-xs text-gray-500">Cancel</button>
            <button type="submit" disabled={busy} className="text-xs font-semibold bg-amber-500 text-white px-3 py-1.5 rounded">
              {form.id ? 'Save' : 'Raise'}
            </button>
          </div>
        </form>
      )}

      {crs.length === 0
        ? <Empty icon="📋" title="No change requests" sub="The baseline has not been asked to move." />
        : (
          <div className="space-y-2">
            {crs.map(c => (
              <div key={c.id} className="border border-gray-200 rounded-lg px-4 py-3">
                <div className="flex items-start justify-between gap-3 flex-wrap">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="font-mono text-[11px] text-gray-400">{c.number}</span>
                      <span className="font-semibold text-sm text-gray-900">{c.title}</span>
                      <Pill className={`${CR_STYLE[c.status]} border-transparent`}>{c.status}</Pill>
                    </div>
                    {c.description && <p className="text-xs text-gray-500 mt-1">{c.description}</p>}
                    <p className="text-xs text-gray-600 mt-1"><span className="font-semibold">Why:</span> {c.justification}</p>
                    <p className="text-[11px] text-gray-400 mt-1">
                      Schedule {c.scheduleImpactDays >= 0 ? '+' : ''}{c.scheduleImpactDays}d
                      {c.budgetVersionNo != null && ` · budget v${c.budgetVersionNo} ${kes(c.budgetVersionTotal)}`}
                    </p>
                    {c.status === 'Approved' && (
                      <p className="text-[11px] text-emerald-700 mt-1">
                        Baseline moved {kes(c.previousBaselineBudget)} → {kes(c.newBaselineBudget)}
                        {c.milestonesShifted > 0 && `, ${c.milestonesShifted} milestone baseline(s) shifted`}
                        {c.previousBaselineSetAt && ` · previous baseline of ${fmt(c.previousBaselineSetAt)} kept on this request`}
                      </p>
                    )}
                    {c.status === 'Rejected' && c.decisionReason && (
                      <p className="text-[11px] text-red-600 mt-1">Rejected: {c.decisionReason}</p>
                    )}
                  </div>
                  <div className="flex gap-2 shrink-0">
                    {canWrite && c.status === 'Draft' && (
                      <>
                        <button onClick={() => setForm({ ...c, budgetVersionId: c.budgetVersionId ?? '' })}
                                className="text-xs text-gray-500 hover:text-gray-800">Edit</button>
                        <button onClick={() => onSubmit(c.id)} disabled={busy}
                                className="text-xs font-semibold text-amber-600 hover:text-amber-800">Send for approval</button>
                      </>
                    )}
                    {canWrite && (c.status === 'Draft' || c.status === 'Submitted') && (
                      <button onClick={() => window.confirm('Withdraw this change request?') && onWithdraw(c.id)}
                              className="text-xs text-gray-400 hover:text-gray-700">Withdraw</button>
                    )}
                    {canApprove && c.status === 'Submitted' && (
                      <>
                        <button
                            onClick={() => window.confirm('Approve? This moves the project baseline.') && onDecide(c.id, { approved: true })}
                            disabled={busy}
                            className="text-xs font-semibold text-emerald-600 hover:text-emerald-800">Approve</button>
                        <button
                            onClick={() => {
                              const r = window.prompt('Reason for rejection:')
                              if (r) onDecide(c.id, { approved: false, reason: r })
                            }}
                            className="text-xs font-semibold text-red-600 hover:text-red-800">Reject</button>
                      </>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
    </div>
  )
}
