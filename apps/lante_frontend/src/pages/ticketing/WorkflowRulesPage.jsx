import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { listWorkflowRules, createWorkflowRule, updateWorkflowRule, deleteWorkflowRule, toggleWorkflowRule, listTags } from '../../services/ticketing.js'
import { ActionParams } from '../../components/ticketing/workflow/ActionParams.jsx'
import api from '../../api/axios.js'

const TRIGGER_EVENTS = [
  { value: 0, label: 'Ticket Created' },
  { value: 1, label: 'Ticket Updated' },
  { value: 2, label: 'Status Changed' },
  { value: 3, label: 'Priority Changed' },
  { value: 4, label: 'Comment Added' },
  { value: 5, label: 'Agent Replied' },
  { value: 6, label: 'SLA Breached' },
  { value: 7, label: 'Ticket Escalated' },
]

const CONDITION_FIELDS = ['Priority', 'Status', 'CategoryId', 'DepartmentId', 'AssignedToUserId', 'IsEscalated', 'Source']
const CONDITION_OPS = [
  { value: 0, label: 'equals' },
  { value: 1, label: 'not equals' },
  { value: 2, label: 'contains' },
  { value: 3, label: 'is empty' },
  { value: 4, label: 'is not empty' },
]

const ACTION_TYPES = [
  { value: 0, label: 'Assign to User' },
  { value: 2, label: 'Change Status' },
  { value: 3, label: 'Add Tag' },
  { value: 4, label: 'Remove Tag' },
  { value: 5, label: 'Send Notification' },
  { value: 6, label: 'Add Comment' },
  { value: 7, label: 'Escalate To' },
]

const STATUSES = ['New','Assigned','InProgress','Pending','Escalated','Resolved','Closed','Reopened']
const PRIORITIES = ['Low','Medium','High','Critical']

const EMPTY_RULE = {
  name: '', description: '', triggerEvent: 0,
  conditions: [], actions: [], isActive: true, runOrder: 0, stopOnMatch: false,
}

export default function WorkflowRulesPage() {
  const navigate = useNavigate()
  const [rules, setRules] = useState([])
  const [tags, setTags] = useState([])
  const [loading, setLoading] = useState(true)
  const [modal, setModal] = useState(null) // null | 'create' | rule object
  const [form, setForm] = useState(EMPTY_RULE)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  async function load() {
    setLoading(true)
    try {
      const [rules, tags] = await Promise.all([listWorkflowRules(), listTags()])
      setRules(rules ?? [])
      setTags(tags ?? [])
    } catch { setError('Failed to load.') }
    finally { setLoading(false) }
  }

  useEffect(() => { load() }, [])

  function openCreate() { setForm(EMPTY_RULE); setModal('create'); setError('') }
  function openEdit(rule) {
    setForm({
      name: rule.name, description: rule.description ?? '',
      triggerEvent: rule.triggerEvent, conditions: rule.conditions ?? [],
      actions: rule.actions ?? [], isActive: rule.isActive,
      runOrder: rule.runOrder, stopOnMatch: rule.stopOnMatch,
    })
    setModal(rule); setError('')
  }

  function addCondition() {
    setForm(f => ({ ...f, conditions: [...f.conditions, { field: 'Priority', operator: 0, value: '' }] }))
  }
  function removeCondition(i) {
    setForm(f => ({ ...f, conditions: f.conditions.filter((_, idx) => idx !== i) }))
  }
  function setCondition(i, key, val) {
    setForm(f => {
      const conditions = [...f.conditions]
      conditions[i] = { ...conditions[i], [key]: val }
      return { ...f, conditions }
    })
  }

  function addAction() {
    setForm(f => ({ ...f, actions: [...f.actions, { type: 0, parameters: {} }] }))
  }
  function removeAction(i) {
    setForm(f => ({ ...f, actions: f.actions.filter((_, idx) => idx !== i) }))
  }
  function setActionType(i, type) {
    setForm(f => {
      const actions = [...f.actions]
      actions[i] = { type: Number(type), parameters: {} }
      return { ...f, actions }
    })
  }
  function setActionParam(i, key, val) {
    setForm(f => {
      const actions = [...f.actions]
      actions[i] = { ...actions[i], parameters: { ...actions[i].parameters, [key]: val } }
      return { ...f, actions }
    })
  }

  async function handleSave(e) {
    e.preventDefault()
    if (!form.name.trim()) { setError('Name is required.'); return }
    setSaving(true); setError('')
    try {
      const payload = { ...form, triggerEvent: Number(form.triggerEvent), runOrder: Number(form.runOrder) }
      if (modal === 'create') {
        await createWorkflowRule(payload)
      } else {
        await updateWorkflowRule(modal.id, payload)
      }
      setModal(null); load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to save.')
    } finally { setSaving(false) }
  }

  async function toggleActive(rule) {
    try {
      await toggleWorkflowRule(rule.id, !rule.isActive)
      setRules(rs => rs.map(r => r.id === rule.id ? { ...r, isActive: !r.isActive } : r))
    } catch { setError('Failed to update.') }
  }

  async function handleDelete(id) {
    if (!window.confirm('Delete this workflow rule?')) return
    try {
      await deleteWorkflowRule(id)
      setRules(rs => rs.filter(r => r.id !== id))
    } catch { setError('Failed to delete.') }
  }

  const triggerLabel = v => TRIGGER_EVENTS.find(t => t.value === v)?.label ?? v

  return (
    <>
      <main className="flex-1 max-w-5xl mx-auto w-full px-4 sm:px-6 py-8">
        <div className="flex items-center gap-3 mb-6">
          <button onClick={() => navigate('/modules/ticketing')}
            className="p-2 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors">
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
          </button>
          <div className="flex-1">
            <h1 className="text-xl font-extrabold text-zinc-950">Workflow Rules</h1>
            <p className="text-sm text-gray-500">Automate ticket actions with conditions and triggers</p>
          </div>
          <button onClick={openCreate}
            className="inline-flex items-center gap-2 px-4 py-2.5 bg-amber-500 hover:bg-amber-600 text-white text-sm font-semibold rounded-lg transition-colors">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            New Rule
          </button>
        </div>

        {error && !modal && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-4 py-3 text-sm mb-4">{error}</div>
        )}

        {loading ? (
          <div className="space-y-3">{[1,2,3].map(i => <div key={i} className="h-20 bg-white rounded-xl border border-gray-100 animate-pulse" />)}</div>
        ) : rules.length === 0 ? (
          <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-16">
            <div className="text-3xl mb-2">⚙️</div>
            <p className="font-medium text-gray-600">No workflow rules yet</p>
            <p className="text-sm text-gray-400 mt-1">Create rules to automate ticket workflows</p>
          </div>
        ) : (
          <div className="space-y-3">
            {rules.sort((a, b) => a.runOrder - b.runOrder).map(rule => (
              <div key={rule.id} className={`bg-white rounded-xl border p-4 ${rule.isActive ? 'border-gray-200' : 'border-gray-100 opacity-60'}`}>
                <div className="flex items-start gap-4">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                      <span className="font-semibold text-gray-800">{rule.name}</span>
                      <span className={`px-2 py-0.5 text-xs font-medium rounded-full ${rule.isActive ? 'bg-green-50 text-green-700' : 'bg-gray-100 text-gray-400'}`}>
                        {rule.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </div>
                    {rule.description && <p className="text-xs text-gray-400 mb-2">{rule.description}</p>}
                    <div className="flex flex-wrap items-center gap-2 text-xs">
                      <span className="px-2 py-0.5 bg-amber-50 text-amber-700 rounded font-medium">
                        When: {triggerLabel(rule.triggerEvent)}
                      </span>
                      {(rule.conditions ?? []).length > 0 && (
                        <span className="px-2 py-0.5 bg-blue-50 text-blue-700 rounded font-medium">
                          {rule.conditions.length} condition{rule.conditions.length !== 1 ? 's' : ''}
                        </span>
                      )}
                      {(rule.actions ?? []).length > 0 && (
                        <span className="px-2 py-0.5 bg-purple-50 text-purple-700 rounded font-medium">
                          {rule.actions.length} action{rule.actions.length !== 1 ? 's' : ''}
                        </span>
                      )}
                      {rule.stopOnMatch && (
                        <span className="px-2 py-0.5 bg-red-50 text-red-600 rounded font-medium">Stop on match</span>
                      )}
                    </div>
                  </div>
                  <div className="flex items-center gap-1 shrink-0">
                    <button onClick={() => toggleActive(rule)}
                      className={`px-3 py-1.5 text-xs font-semibold rounded-lg transition-colors ${rule.isActive ? 'bg-gray-100 text-gray-600 hover:bg-gray-200' : 'bg-green-50 text-green-700 hover:bg-green-100'}`}>
                      {rule.isActive ? 'Disable' : 'Enable'}
                    </button>
                    <button onClick={() => openEdit(rule)}
                      className="p-1.5 text-gray-300 hover:text-amber-500 rounded transition-colors">
                      <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                      </svg>
                    </button>
                    <button onClick={() => handleDelete(rule.id)}
                      className="p-1.5 text-gray-300 hover:text-red-500 rounded transition-colors">
                      <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                      </svg>
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </main>

      {modal && (
        <div className="fixed inset-0 z-50 flex items-start justify-center bg-black/40 px-4 py-8 overflow-y-auto">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-2xl p-6 my-auto">
            <h2 className="text-lg font-bold text-zinc-950 mb-5">
              {modal === 'create' ? 'New Workflow Rule' : 'Edit Workflow Rule'}
            </h2>
            {error && (
              <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3 mb-4">{error}</div>
            )}
            <form onSubmit={handleSave} className="space-y-5">
              {/* Basic info */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Rule Name *</label>
                  <input type="text" value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
                    placeholder="e.g. Auto-assign critical tickets"
                    className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Trigger Event</label>
                  <select value={form.triggerEvent} onChange={e => setForm(f => ({ ...f, triggerEvent: Number(e.target.value) }))}
                    className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400">
                    {TRIGGER_EVENTS.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
                  </select>
                </div>
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Description</label>
                <input type="text" value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
                  placeholder="What does this rule do?"
                  className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
              </div>

              {/* Conditions */}
              <div>
                <div className="flex items-center justify-between mb-2">
                  <label className="text-xs font-semibold text-gray-500 uppercase tracking-wider">
                    Conditions <span className="text-gray-400 font-normal normal-case">(all must match)</span>
                  </label>
                  <button type="button" onClick={addCondition}
                    className="text-xs text-amber-600 hover:text-amber-700 font-semibold">
                    + Add Condition
                  </button>
                </div>
                {form.conditions.length === 0 ? (
                  <p className="text-xs text-gray-400 py-2">No conditions — rule will run for all matching events.</p>
                ) : (
                  <div className="space-y-2">
                    {form.conditions.map((cond, i) => (
                      <div key={i} className="flex gap-2 items-center bg-gray-50 rounded-lg p-2">
                        <select value={cond.field}
                          onChange={e => setCondition(i, 'field', e.target.value)}
                          className="px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400">
                          {CONDITION_FIELDS.map(f => <option key={f} value={f}>{f}</option>)}
                        </select>
                        <select value={cond.operator}
                          onChange={e => setCondition(i, 'operator', Number(e.target.value))}
                          className="px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400">
                          {CONDITION_OPS.map(op => <option key={op.value} value={op.value}>{op.label}</option>)}
                        </select>
                        {cond.operator !== 3 && cond.operator !== 4 && (
                          cond.field === 'Priority' ? (
                            <select value={cond.value} onChange={e => setCondition(i, 'value', e.target.value)}
                              className="flex-1 px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400">
                              <option value="">Select…</option>
                              {PRIORITIES.map(p => <option key={p} value={p}>{p}</option>)}
                            </select>
                          ) : cond.field === 'Status' ? (
                            <select value={cond.value} onChange={e => setCondition(i, 'value', e.target.value)}
                              className="flex-1 px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400">
                              <option value="">Select…</option>
                              {STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
                            </select>
                          ) : (
                            <input type="text" value={cond.value} onChange={e => setCondition(i, 'value', e.target.value)}
                              placeholder="Value…"
                              className="flex-1 px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400" />
                          )
                        )}
                        <button type="button" onClick={() => removeCondition(i)}
                          className="p-1 text-gray-300 hover:text-red-400 transition-colors shrink-0">
                          <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                          </svg>
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Actions */}
              <div>
                <div className="flex items-center justify-between mb-2">
                  <label className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Actions</label>
                  <button type="button" onClick={addAction}
                    className="text-xs text-amber-600 hover:text-amber-700 font-semibold">
                    + Add Action
                  </button>
                </div>
                {form.actions.length === 0 ? (
                  <p className="text-xs text-gray-400 py-2">No actions defined.</p>
                ) : (
                  <div className="space-y-2">
                    {form.actions.map((action, i) => (
                      <div key={i} className="bg-purple-50 border border-purple-100 rounded-lg p-3 space-y-2">
                        <div className="flex items-center gap-2">
                          <select value={action.type} onChange={e => setActionType(i, e.target.value)}
                            className="flex-1 px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400 bg-white">
                            {ACTION_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
                          </select>
                          <button type="button" onClick={() => removeAction(i)}
                            className="p-1 text-gray-300 hover:text-red-400 transition-colors">
                            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                            </svg>
                          </button>
                        </div>
                        <ActionParams action={action} index={i} setParam={setActionParam} tags={tags} />
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Options */}
              <div className="flex flex-wrap gap-4">
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Run Order</label>
                  <input type="number" value={form.runOrder} onChange={e => setForm(f => ({ ...f, runOrder: Number(e.target.value) }))}
                    min={0} className="w-24 px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                </div>
                <label className="flex items-center gap-2 cursor-pointer mt-5">
                  <input type="checkbox" checked={form.stopOnMatch} onChange={e => setForm(f => ({ ...f, stopOnMatch: e.target.checked }))}
                    className="rounded border-gray-300 text-amber-500 focus:ring-amber-400" />
                  <span className="text-sm text-gray-600">Stop on match (don&apos;t run later rules)</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer mt-5">
                  <input type="checkbox" checked={form.isActive} onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))}
                    className="rounded border-gray-300 text-amber-500 focus:ring-amber-400" />
                  <span className="text-sm text-gray-600">Active</span>
                </label>
              </div>

              <div className="flex items-center justify-end gap-3 pt-2 border-t border-gray-100">
                <button type="button" onClick={() => setModal(null)}
                  className="px-4 py-2 text-sm border border-gray-200 rounded-lg text-gray-600 hover:bg-gray-50">
                  Cancel
                </button>
                <button type="submit" disabled={saving}
                  className="px-5 py-2 bg-amber-500 hover:bg-amber-600 disabled:opacity-50 text-white text-sm font-semibold rounded-lg">
                  {saving ? 'Saving…' : 'Save Rule'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  )
}
