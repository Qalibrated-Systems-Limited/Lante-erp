import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext.jsx'
import * as ops from '../../services/operations.js'

// PR4b — the template library and the standing schedules that raise projects from it. Both are
// "how work gets started", so they live on one page rather than being hunted for separately.

const kes = (v) => `KES ${Number(v ?? 0).toLocaleString('en-KE', { maximumFractionDigits: 0 })}`
const fmt = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')
const CATEGORIES = ['Labour', 'Materials', 'Equipment', 'Fleet', 'Subcontractor', 'Other']
const FREQUENCIES = ['Monthly', 'Quarterly', 'SemiAnnually', 'Annually']

function Empty({ icon, title, sub }) {
  return (
    <div className="text-center py-10 border border-dashed border-gray-200 rounded-lg">
      <div className="text-2xl mb-1">{icon}</div>
      <p className="text-sm font-semibold text-gray-700">{title}</p>
      {sub && <p className="text-xs text-gray-400 mt-1">{sub}</p>}
    </div>
  )
}

export default function ProjectTemplatesPage() {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const canAuthor = hasPermission('projects.approve')
  const canUse    = hasPermission('projects.write')

  const [view, setView] = useState('templates')
  const [templates, setTemplates] = useState([])
  const [schedules, setSchedules] = useState([])
  const [editing, setEditing] = useState(null)     // template being authored
  const [using, setUsing] = useState(null)         // template being instantiated
  const [schedForm, setSchedForm] = useState(null)
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      const [t, s] = await Promise.all([
        ops.getProjectTemplates(canAuthor),
        ops.getRecurringSchedules(canAuthor),
      ])
      setTemplates(t ?? []); setSchedules(s ?? [])
    } catch { setErr('Could not load templates.') }
  }, [canAuthor])

  useEffect(() => { load() }, [load])

  const run = async (fn, after) => {
    setBusy(true); setErr('')
    try { const r = await fn(); await load(); after?.(r) }
    catch (e) { setErr(e?.response?.data?.message || 'Action failed.') }
    finally { setBusy(false) }
  }

  const blankTemplate = {
    name: '', description: '', type: 'Calibration', isActive: true,
    milestones: [{ title: '', offsetDays: 0, durationDays: 7, valuePct: 100, tasks: [] }],
    budgetLines: [],
  }

  const openEditor = async (t) => {
    if (!t) return setEditing(blankTemplate)
    const full = await ops.getProjectTemplate(t.id)
    setEditing({ ...full, isActive: full.isActive })
  }

  const totalPct = (editing?.milestones ?? []).reduce((s, m) => s + (Number(m.valuePct) || 0), 0)

  return (
    <main className="flex-1 max-w-6xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex items-start justify-between gap-4 flex-wrap mb-6">
        <div>
          <h1 className="text-2xl font-extrabold text-zinc-950">Templates &amp; recurring work</h1>
          <p className="text-sm text-gray-500 mt-1">
            A standard job's milestones, tasks and priced budget — so a recalibration starts filled in
            rather than blank.
          </p>
        </div>
        {canAuthor && view === 'templates' && (
          <button onClick={() => openEditor(null)} className="text-sm font-semibold bg-amber-500 text-white px-3 py-2 rounded">
            + New template
          </button>
        )}
        {canAuthor && view === 'recurring' && (
          <button onClick={() => setSchedForm({ templateId: templates[0]?.id ?? '', name: '', frequency: 'Annually', interval: 1, leadTimeDays: 14, contractValue: 0, isActive: true })}
                  className="text-sm font-semibold bg-amber-500 text-white px-3 py-2 rounded">
            + New schedule
          </button>
        )}
      </div>

      {err && <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2.5 mb-4">{err}</div>}

      <div className="flex gap-1 border-b border-gray-200 mb-5">
        {[['templates', `Templates (${templates.length})`], ['recurring', `Recurring (${schedules.length})`]].map(([k, l]) => (
          <button key={k} onClick={() => setView(k)}
                  className={`px-3 py-2 text-sm font-semibold border-b-2 -mb-px ${
                    view === k ? 'border-amber-500 text-amber-700' : 'border-transparent text-gray-500 hover:text-gray-800'}`}>
            {l}
          </button>
        ))}
      </div>

      {/* ── Template library ─────────────────────────────────────────────── */}
      {view === 'templates' && !editing && (
        templates.length === 0
          ? <Empty icon="📋" title="No templates yet" sub="Capture the shape of a job once and reuse it." />
          : (
            <div className="grid gap-3 sm:grid-cols-2">
              {templates.map(t => (
                <div key={t.id} className="border border-gray-200 rounded-lg px-4 py-3">
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="font-semibold text-sm text-gray-900">{t.name}</p>
                      {t.description && <p className="text-xs text-gray-500 mt-0.5">{t.description}</p>}
                      <p className="text-[11px] text-gray-400 mt-1">
                        {t.type} · {t.milestoneCount} milestone(s) · {t.budgetLineCount} budget line(s)
                        {t.useCount > 0 && ` · used ${t.useCount}×`}
                      </p>
                    </div>
                    {!t.isActive && <span className="text-[11px] bg-gray-100 text-gray-500 rounded px-1.5 py-0.5">inactive</span>}
                  </div>
                  <div className="flex gap-3 mt-2">
                    {canUse && t.isActive && (
                      <button onClick={() => setUsing({ template: t, form: { name: '', clientName: '', contractValue: 0, startDate: new Date().toISOString().slice(0, 10) } })}
                              className="text-xs font-semibold text-amber-600 hover:text-amber-800">Use this</button>
                    )}
                    {canAuthor && <button onClick={() => openEditor(t)} className="text-xs text-gray-500 hover:text-gray-800">Edit</button>}
                    {canAuthor && t.isActive && (
                      <button onClick={() => window.confirm('Deactivate this template?') && run(() => ops.deactivateTemplate(t.id))}
                              className="text-xs text-red-500 hover:text-red-700">Deactivate</button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )
      )}

      {/* ── Template editor ──────────────────────────────────────────────── */}
      {view === 'templates' && editing && (
        <form
            onSubmit={(e) => {
              e.preventDefault()
              run(() => ops.saveProjectTemplate(editing.id, editing), () => setEditing(null))
            }}
            className="space-y-4 border border-gray-200 rounded-lg p-4">
          <div className="grid sm:grid-cols-2 gap-3">
            <input className="input" placeholder="Template name" value={editing.name}
                   onChange={e => setEditing({ ...editing, name: e.target.value })} required />
            <input className="input" placeholder="Description" value={editing.description ?? ''}
                   onChange={e => setEditing({ ...editing, description: e.target.value })} />
          </div>

          <div>
            <div className="flex items-center justify-between mb-1">
              <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Milestones</h3>
              {/* The split has to add up: it is how the contract value is apportioned when used. */}
              <span className={`text-[11px] font-semibold ${
                Math.abs(totalPct - 100) < 0.01 ? 'text-emerald-700' : totalPct > 100 ? 'text-red-700' : 'text-amber-700'}`}>
                {totalPct.toFixed(0)}% of contract allocated
              </span>
            </div>
            <div className="space-y-2">
              {editing.milestones.map((m, i) => (
                <div key={i} className="border border-gray-100 rounded p-3 space-y-2 bg-gray-50">
                  <div className="flex gap-2">
                    <input className="input flex-1" placeholder="Milestone title" value={m.title}
                           onChange={e => {
                             const ms = [...editing.milestones]; ms[i] = { ...m, title: e.target.value }
                             setEditing({ ...editing, milestones: ms })
                           }} required />
                    <button type="button" onClick={() => setEditing({ ...editing, milestones: editing.milestones.filter((_, j) => j !== i) })}
                            className="text-xs text-red-500 px-2">Remove</button>
                  </div>
                  <div className="grid grid-cols-3 gap-2">
                    {[['offsetDays', 'Starts day'], ['durationDays', 'Runs days'], ['valuePct', '% of value']].map(([k, label]) => (
                      <label key={k} className="text-[11px] font-semibold text-gray-500">{label}
                        <input type="number" className="input mt-0.5" value={m[k] ?? 0}
                               onChange={e => {
                                 const ms = [...editing.milestones]; ms[i] = { ...m, [k]: Number(e.target.value) || 0 }
                                 setEditing({ ...editing, milestones: ms })
                               }} />
                      </label>
                    ))}
                  </div>
                </div>
              ))}
            </div>
            <button type="button"
                    onClick={() => setEditing({ ...editing, milestones: [...editing.milestones, { title: '', offsetDays: 0, durationDays: 7, valuePct: 0, tasks: [] }] })}
                    className="text-xs font-semibold text-amber-600 hover:text-amber-800 mt-2">+ Add milestone</button>
          </div>

          <div>
            <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-1">Budget lines</h3>
            <div className="space-y-2">
              {(editing.budgetLines ?? []).map((l, i) => (
                <div key={i} className="grid grid-cols-2 sm:grid-cols-5 gap-2 items-end border border-gray-100 rounded p-2 bg-gray-50">
                  <select className="input" value={l.category}
                          onChange={e => {
                            const ls = [...editing.budgetLines]; ls[i] = { ...l, category: e.target.value }
                            setEditing({ ...editing, budgetLines: ls })
                          }}>
                    {CATEGORIES.map(c => <option key={c}>{c}</option>)}
                  </select>
                  <input className="input" placeholder="Description" value={l.description ?? ''}
                         onChange={e => {
                           const ls = [...editing.budgetLines]; ls[i] = { ...l, description: e.target.value }
                           setEditing({ ...editing, budgetLines: ls })
                         }} />
                  {[['quantity', 'Qty'], ['unitCostRate', 'Cost/unit'], ['unitClientRate', 'Client/unit']].map(([k, ph]) => (
                    <input key={k} type="number" className="input" placeholder={ph} value={l[k] ?? 0}
                           onChange={e => {
                             const ls = [...editing.budgetLines]; ls[i] = { ...l, [k]: Number(e.target.value) || 0 }
                             setEditing({ ...editing, budgetLines: ls })
                           }} />
                  ))}
                </div>
              ))}
            </div>
            <button type="button"
                    onClick={() => setEditing({ ...editing, budgetLines: [...(editing.budgetLines ?? []), { category: 'Labour', description: '', quantity: 1, unitCostRate: 0, unitClientRate: 0 }] })}
                    className="text-xs font-semibold text-amber-600 hover:text-amber-800 mt-2">+ Add budget line</button>
          </div>

          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setEditing(null)} className="text-xs text-gray-500">Cancel</button>
            <button type="submit" disabled={busy} className="text-xs font-semibold bg-amber-500 text-white px-3 py-1.5 rounded">
              Save template
            </button>
          </div>
        </form>
      )}

      {/* ── Recurring schedules ──────────────────────────────────────────── */}
      {view === 'recurring' && (
        <div className="space-y-3">
          <p className="text-xs text-gray-500">
            Each occurrence is raised as a <strong>draft</strong> project, a lead time before it is due —
            so the visit does not get forgotten, but nobody's calendar fills with work that was never
            confirmed with the client.
          </p>

          {schedForm && (
            <form
                onSubmit={(e) => { e.preventDefault(); run(() => ops.saveRecurringSchedule(schedForm.id, schedForm), () => setSchedForm(null)) }}
                className="border border-gray-200 rounded-lg p-4 space-y-3 bg-gray-50">
              <div className="grid sm:grid-cols-2 gap-3">
                <select className="input" value={schedForm.templateId}
                        onChange={e => setSchedForm({ ...schedForm, templateId: e.target.value })} required>
                  <option value="">— pick a template —</option>
                  {templates.filter(t => t.isActive).map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
                </select>
                <input className="input" placeholder="Schedule name" value={schedForm.name}
                       onChange={e => setSchedForm({ ...schedForm, name: e.target.value })} required />
                <input className="input" placeholder="Client name" value={schedForm.clientName ?? ''}
                       onChange={e => setSchedForm({ ...schedForm, clientName: e.target.value })} />
                <input type="number" className="input" placeholder="Contract value" value={schedForm.contractValue ?? 0}
                       onChange={e => setSchedForm({ ...schedForm, contractValue: Number(e.target.value) || 0 })} />
                <label className="text-[11px] font-semibold text-gray-500">Frequency
                  <select className="input mt-0.5" value={schedForm.frequency}
                          onChange={e => setSchedForm({ ...schedForm, frequency: e.target.value })}>
                    {FREQUENCIES.map(f => <option key={f}>{f}</option>)}
                  </select>
                </label>
                <label className="text-[11px] font-semibold text-gray-500">Next due
                  <input type="date" className="input mt-0.5" value={schedForm.nextDueDate?.slice(0, 10) ?? ''}
                         onChange={e => setSchedForm({ ...schedForm, nextDueDate: e.target.value })} required />
                </label>
                <label className="text-[11px] font-semibold text-gray-500">Raise this many days early
                  <input type="number" className="input mt-0.5" value={schedForm.leadTimeDays ?? 14}
                         onChange={e => setSchedForm({ ...schedForm, leadTimeDays: Number(e.target.value) || 0 })} />
                </label>
              </div>
              <div className="flex justify-end gap-2">
                <button type="button" onClick={() => setSchedForm(null)} className="text-xs text-gray-500">Cancel</button>
                <button type="submit" disabled={busy} className="text-xs font-semibold bg-amber-500 text-white px-3 py-1.5 rounded">Save schedule</button>
              </div>
            </form>
          )}

          {schedules.length === 0
            ? <Empty icon="🔁" title="No recurring work" sub="Standing recalibration visits appear here." />
            : schedules.map(s => (
                <div key={s.id} className="border border-gray-200 rounded-lg px-4 py-3">
                  <div className="flex items-start justify-between gap-3 flex-wrap">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="font-semibold text-sm text-gray-900">{s.name}</span>
                        <span className="text-[11px] bg-gray-100 text-gray-600 rounded px-1.5 py-0.5">{s.frequency}</span>
                        {s.dueNow && <span className="text-[11px] bg-amber-100 text-amber-800 rounded px-1.5 py-0.5">due now</span>}
                        {!s.isActive && <span className="text-[11px] bg-gray-100 text-gray-500 rounded px-1.5 py-0.5">paused</span>}
                      </div>
                      <p className="text-[11px] text-gray-400 mt-1">
                        {s.templateName ?? 'template'} · {s.clientName ?? 'no client'} · {kes(s.contractValue)}
                        {' · next '}{fmt(s.nextDueDate)} (raised {s.leadTimeDays}d early)
                        {s.generatedCount > 0 && ` · ${s.generatedCount} raised so far`}
                      </p>
                    </div>
                    <div className="flex gap-2 shrink-0">
                      {canUse && (
                        <button onClick={() => run(() => ops.runRecurringNow(s.id), (id) => id && navigate(`/modules/operations/projects/${id}`))}
                                disabled={busy} className="text-xs font-semibold text-amber-600 hover:text-amber-800">Raise now</button>
                      )}
                      {canAuthor && (
                        <>
                          <button onClick={() => setSchedForm({ ...s, nextDueDate: s.nextDueDate?.slice(0, 10) })}
                                  className="text-xs text-gray-500 hover:text-gray-800">Edit</button>
                          <button onClick={() => run(() => ops.setRecurringActive(s.id, !s.isActive))}
                                  className="text-xs text-gray-500 hover:text-gray-800">{s.isActive ? 'Pause' : 'Resume'}</button>
                        </>
                      )}
                    </div>
                  </div>
                </div>
              ))}
        </div>
      )}

      {/* ── Use a template ───────────────────────────────────────────────── */}
      {using && (
        <div className="fixed inset-0 bg-black/30 grid place-items-center p-4 z-50" onClick={() => setUsing(null)}>
          <form
              onClick={e => e.stopPropagation()}
              onSubmit={(e) => {
                e.preventDefault()
                run(() => ops.instantiateTemplate(using.template.id, using.form),
                    (id) => { setUsing(null); if (id) navigate(`/modules/operations/projects/${id}`) })
              }}
              className="bg-white rounded-xl p-5 w-full max-w-md space-y-3">
            <h2 className="font-bold text-gray-900">New project from “{using.template.name}”</h2>
            <input className="input" placeholder="Project name" value={using.form.name}
                   onChange={e => setUsing({ ...using, form: { ...using.form, name: e.target.value } })} required />
            <input className="input" placeholder="Client name" value={using.form.clientName}
                   onChange={e => setUsing({ ...using, form: { ...using.form, clientName: e.target.value } })} />
            <label className="text-[11px] font-semibold text-gray-500 block">Contract value
              <input type="number" className="input mt-0.5" value={using.form.contractValue}
                     onChange={e => setUsing({ ...using, form: { ...using.form, contractValue: Number(e.target.value) || 0 } })} />
              <span className="block text-[11px] font-normal text-gray-400 mt-0.5">
                Milestone amounts are worked out from this using the template's split.
              </span>
            </label>
            <label className="text-[11px] font-semibold text-gray-500 block">Start date
              <input type="date" className="input mt-0.5" value={using.form.startDate}
                     onChange={e => setUsing({ ...using, form: { ...using.form, startDate: e.target.value } })} required />
            </label>
            <div className="flex justify-end gap-2 pt-1">
              <button type="button" onClick={() => setUsing(null)} className="text-xs text-gray-500">Cancel</button>
              <button type="submit" disabled={busy} className="text-xs font-semibold bg-amber-500 text-white px-3 py-1.5 rounded">
                Create draft project
              </button>
            </div>
          </form>
        </div>
      )}
    </main>
  )
}
