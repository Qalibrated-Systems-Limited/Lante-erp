import { useState, useEffect } from 'react'
import { useAuth } from '../../context/AuthContext.jsx'
import api from '../../api/axios.js'

// ─── NAV ─────────────────────────────────────────────────────────────────────

// Helpdesk config (Categories / SLA / Escalation) now lives under the Helpdesk cluster
// (HelpdeskSettingsPage); this page keeps account & security settings.
const NAV = [
  { id: 'password-policy', label: 'Password Policy', icon: ShieldIcon },
  { id: 'account',    label: 'My Account',         icon: UserIcon },
]

// ─── PAGE ─────────────────────────────────────────────────────────────────────

export default function SettingsPage() {
  const { isCompanyAdmin } = useAuth()
  const [active, setActive] = useState('password-policy')

  const nav = [
    ...NAV,
    ...(isCompanyAdmin ? [{ id: 'branches', label: 'Branches', icon: BranchIcon }] : []),
  ]

  return (
    <>

      <main className="flex-1 max-w-6xl mx-auto w-full px-4 sm:px-6 py-8">
        <div className="mb-6">
          <h1 className="text-2xl font-extrabold text-zinc-950">Settings</h1>
          <p className="text-sm text-gray-500 mt-0.5">Manage system configuration and your account</p>
        </div>

        <div className="flex flex-col lg:flex-row gap-6">
          {/* Sidebar */}
          <aside className="lg:w-56 shrink-0">
            <nav className="bg-white rounded-xl border border-gray-200 overflow-hidden">
              {nav.map((item, i) => {
                const Icon = item.icon
                return (
                  <button
                    key={item.id}
                    onClick={() => setActive(item.id)}
                    className={`w-full flex items-center gap-3 px-4 py-3 text-sm font-medium transition-colors text-left
                      ${i > 0 ? 'border-t border-gray-100' : ''}
                      ${active === item.id
                        ? 'bg-amber-50 text-amber-700 border-l-2 border-l-amber-500'
                        : 'text-gray-600 hover:bg-gray-50 hover:text-gray-900'
                      }`}
                  >
                    <Icon className="w-4 h-4 shrink-0" />
                    {item.label}
                  </button>
                )
              })}
            </nav>
          </aside>

          {/* Content */}
          <div className="flex-1 min-w-0">
            {active === 'password-policy' && <PasswordPolicySection />}
            {active === 'account'         && <AccountSection />}
            {active === 'branches'        && <BranchSwitcherSection />}
          </div>
        </div>
      </main>
    </>
  )
}

// ─── TICKET CATEGORIES ────────────────────────────────────────────────────────

const PRIORITIES = ['Low', 'Medium', 'High', 'Critical']

export function CategoriesSection() {
  const [categories, setCategories] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [modal, setModal] = useState(null) // null | 'create' | 'edit' | 'delete'
  const [selected, setSelected] = useState(null)
  const [form, setForm] = useState(emptyCategory())
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState('')

  useEffect(() => { loadCategories() }, [])

  function emptyCategory() {
    return { name: '', description: '', departmentId: '', defaultPriority: 'Medium', requiresEvidence: false, autoCreateANCR: false }
  }

  async function loadCategories() {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/ticket-categories')
      setCategories(res.data?.data ?? [])
    } catch {
      setError('Failed to load ticket categories.')
    } finally {
      setLoading(false)
    }
  }

  function openCreate() {
    setForm(emptyCategory())
    setFormError('')
    setModal('create')
  }

  function openEdit(cat) {
    setSelected(cat)
    setForm({
      name: cat.name,
      description: cat.description ?? '',
      departmentId: cat.departmentId ?? '',
      defaultPriority: cat.defaultPriority ?? 'Medium',
      requiresEvidence: cat.requiresEvidence ?? false,
      autoCreateANCR: cat.autoCreateANCR ?? false,
    })
    setFormError('')
    setModal('edit')
  }

  async function handleSave() {
    if (!form.name.trim()) { setFormError('Category name is required.'); return }
    setSaving(true)
    setFormError('')
    try {
      if (modal === 'create') {
        await api.post('/api/v1/ticket-categories', { ...form, name: form.name.trim() })
      } else {
        await api.put(`/api/v1/ticket-categories/${selected.id}`, {
          name: form.name.trim(),
          description: form.description.trim() || null,
          departmentId: form.departmentId.trim() || null,
          defaultPriority: PRIORITIES.indexOf(form.defaultPriority),
          requiresEvidence: form.requiresEvidence,
          autoCreateANCR: form.autoCreateANCR,
          isActive: selected?.isActive ?? true,
        })
      }
      setModal(null)
      loadCategories()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to save.')
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete() {
    setSaving(true)
    try {
      await api.delete(`/api/v1/ticket-categories/${selected.id}`)
      setModal(null)
      loadCategories()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to delete.')
    } finally {
      setSaving(false)
    }
  }

  async function toggleActive(cat) {
    try {
      await api.put(`/api/v1/ticket-categories/${cat.id}`, { isActive: !cat.isActive })
      loadCategories()
    } catch { /* silent */ }
  }

  return (
    <Section
      title="Ticket Categories"
      description="Categories group tickets by type. Each category can have its own SLA policies and escalation rules."
      action={<button onClick={openCreate} className="btn-amber text-sm">+ New Category</button>}
    >
      {error && <ErrorBanner>{error}</ErrorBanner>}

      {loading ? (
        <div className="space-y-3">
          {[1,2,3].map(i => <div key={i} className="h-20 bg-gray-100 rounded-xl animate-pulse" />)}
        </div>
      ) : categories.length === 0 ? (
        <EmptyState icon="🏷️" title="No categories yet" sub="Create your first ticket category to get started." />
      ) : (
        <div className="space-y-3">
          {categories.map(cat => (
            <div key={cat.id} className="bg-white rounded-xl border border-gray-200 px-5 py-4 flex items-start justify-between gap-4">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <p className="font-semibold text-gray-900 text-sm">{cat.name}</p>
                  <PriorityBadge priority={cat.defaultPriority} />
                  <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${cat.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
                    {cat.isActive ? 'Active' : 'Inactive'}
                  </span>
                  {cat.requiresEvidence && (
                    <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-blue-50 text-blue-600">Evidence Required</span>
                  )}
                </div>
                {cat.description && <p className="text-xs text-gray-500 mt-1 leading-relaxed">{cat.description}</p>}
                {cat.departmentId && <p className="text-xs text-gray-400 mt-0.5">Dept: {cat.departmentId}</p>}
              </div>
              <div className="flex items-center gap-2 shrink-0">
                <button onClick={() => toggleActive(cat)} className="text-xs text-gray-400 hover:text-gray-600 px-2 py-1 rounded-lg hover:bg-gray-100 transition-colors">
                  {cat.isActive ? 'Disable' : 'Enable'}
                </button>
                <button onClick={() => openEdit(cat)} className="text-xs text-amber-600 hover:text-amber-800 px-2 py-1 rounded-lg hover:bg-amber-50 transition-colors">Edit</button>
                <button onClick={() => { setSelected(cat); setFormError(''); setModal('delete') }} className="text-xs text-red-500 hover:text-red-700 px-2 py-1 rounded-lg hover:bg-red-50 transition-colors">Delete</button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Create / Edit modal */}
      {(modal === 'create' || modal === 'edit') && (
        <Modal title={modal === 'create' ? 'New Category' : 'Edit Category'} onClose={() => setModal(null)}>
          {formError && <ErrorBanner>{formError}</ErrorBanner>}
          <div className="space-y-4">
            <Field label="Name *">
              <input value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} className="input" placeholder="e.g. IT Helpdesk" />
            </Field>
            <Field label="Description">
              <textarea value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))} rows={2} className="input resize-none" placeholder="Optional…" />
            </Field>
            <Field label="Department">
              <input value={form.departmentId} onChange={e => setForm(f => ({ ...f, departmentId: e.target.value }))} className="input" placeholder="e.g. Engineering" />
            </Field>
            <Field label="Default Priority">
              <select value={form.defaultPriority} onChange={e => setForm(f => ({ ...f, defaultPriority: e.target.value }))} className="input">
                {PRIORITIES.map(p => <option key={p}>{p}</option>)}
              </select>
            </Field>
            <div className="flex flex-col gap-2">
              <Toggle
                checked={form.requiresEvidence}
                onChange={v => setForm(f => ({ ...f, requiresEvidence: v }))}
                label="Requires Evidence"
                sub="Tickets in this category must include supporting documentation."
              />
              <Toggle
                checked={form.autoCreateANCR}
                onChange={v => setForm(f => ({ ...f, autoCreateANCR: v }))}
                label="Auto-create ANCR"
                sub="Automatically generate an Action of Non-Conformance Report."
              />
            </div>
          </div>
          <ModalActions onCancel={() => setModal(null)} onConfirm={handleSave} loading={saving} confirmLabel="Save" />
        </Modal>
      )}

      {modal === 'delete' && (
        <Modal title="Delete Category" onClose={() => setModal(null)}>
          {formError && <ErrorBanner>{formError}</ErrorBanner>}
          <p className="text-sm text-gray-600 mb-1">Are you sure you want to delete <strong>{selected?.name}</strong>?</p>
          <p className="text-xs text-gray-400">Tickets already in this category will not be affected, but new tickets cannot be assigned to it.</p>
          <ModalActions onCancel={() => setModal(null)} onConfirm={handleDelete} loading={saving} confirmLabel="Delete" danger />
        </Modal>
      )}
    </Section>
  )
}

// ─── SLA POLICIES ─────────────────────────────────────────────────────────────

export function SLASection() {
  const [categories, setCategories] = useState([])
  const [loading, setLoading] = useState(true)
  const [expanded, setExpanded] = useState(null)
  const [slaPolicies, setSlaPolicies] = useState({}) // categoryId → policies[]
  const [slaLoading, setSlaLoading] = useState({})
  const [addModal, setAddModal] = useState(null) // categoryId or null
  const [form, setForm] = useState({ priority: 'Medium', responseTimeHours: 4, resolutionTimeHours: 24 })
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState('')

  useEffect(() => {
    api.get('/api/v1/ticket-categories')
      .then(r => setCategories(r.data?.data ?? []))
      .finally(() => setLoading(false))
  }, [])

  async function loadPolicies(catId) {
    setSlaLoading(s => ({ ...s, [catId]: true }))
    try {
      const res = await api.get(`/api/v1/ticket-categories/${catId}/sla-policies`)
      setSlaPolicies(s => ({ ...s, [catId]: res.data?.data ?? [] }))
    } catch {
      setSlaPolicies(s => ({ ...s, [catId]: [] }))
    } finally {
      setSlaLoading(s => ({ ...s, [catId]: false }))
    }
  }

  function toggle(catId) {
    if (expanded === catId) { setExpanded(null); return }
    setExpanded(catId)
    if (!slaPolicies[catId]) loadPolicies(catId)
  }

  async function handleAddPolicy() {
    setSaving(true)
    setFormError('')
    try {
      await api.post(`/api/v1/ticket-categories/${addModal}/sla-policies`, {
        priority: PRIORITIES.indexOf(form.priority),
        responseTimeHours: Number(form.responseTimeHours),
        resolutionTimeHours: Number(form.resolutionTimeHours),
      })
      setAddModal(null)
      loadPolicies(addModal)
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to save policy.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Section
      title="SLA Policies"
      description="Set response and resolution time targets per category and priority level."
    >
      {loading ? (
        <div className="space-y-3">{[1,2,3].map(i => <div key={i} className="h-14 bg-gray-100 rounded-xl animate-pulse" />)}</div>
      ) : categories.length === 0 ? (
        <EmptyState icon="⏱️" title="No categories" sub="Create ticket categories first to manage SLA policies." />
      ) : (
        <div className="space-y-3">
          {categories.map(cat => (
            <div key={cat.id} className="bg-white rounded-xl border border-gray-200 overflow-hidden">
              <button
                onClick={() => toggle(cat.id)}
                className="w-full flex items-center justify-between px-5 py-4 text-left hover:bg-gray-50 transition-colors"
              >
                <div className="flex items-center gap-3">
                  <span className="font-semibold text-sm text-gray-900">{cat.name}</span>
                  <PriorityBadge priority={cat.defaultPriority} />
                </div>
                <svg className={`w-4 h-4 text-gray-400 transition-transform ${expanded === cat.id ? 'rotate-180' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                </svg>
              </button>

              {expanded === cat.id && (
                <div className="border-t border-gray-100 px-5 py-4">
                  {slaLoading[cat.id] ? (
                    <p className="text-sm text-gray-400 animate-pulse">Loading…</p>
                  ) : (slaPolicies[cat.id] ?? []).length === 0 ? (
                    <p className="text-sm text-gray-400">No SLA policies configured for this category.</p>
                  ) : (
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="text-xs text-gray-400 uppercase tracking-wide">
                          <th className="text-left pb-2">Priority</th>
                          <th className="text-left pb-2">Response</th>
                          <th className="text-left pb-2">Resolution</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-50">
                        {(slaPolicies[cat.id] ?? []).map(p => (
                          <tr key={p.id}>
                            <td className="py-2"><PriorityBadge priority={p.priority} /></td>
                            <td className="py-2 text-gray-700">{p.responseTimeHours}h</td>
                            <td className="py-2 text-gray-700">{p.resolutionTimeHours}h</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                  <button
                    onClick={() => { setForm({ priority: 'Medium', responseTimeHours: 4, resolutionTimeHours: 24 }); setFormError(''); setAddModal(cat.id) }}
                    className="mt-3 text-xs font-semibold text-amber-600 hover:text-amber-800 transition-colors"
                  >
                    + Add SLA Policy
                  </button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {addModal && (
        <Modal title="Add SLA Policy" onClose={() => setAddModal(null)}>
          {formError && <ErrorBanner>{formError}</ErrorBanner>}
          <div className="space-y-4">
            <Field label="Priority">
              <select value={form.priority} onChange={e => setForm(f => ({ ...f, priority: e.target.value }))} className="input">
                {PRIORITIES.map(p => <option key={p}>{p}</option>)}
              </select>
            </Field>
            <Field label="Response Time (hours)">
              <input type="number" min={1} value={form.responseTimeHours} onChange={e => setForm(f => ({ ...f, responseTimeHours: e.target.value }))} className="input" />
            </Field>
            <Field label="Resolution Time (hours)">
              <input type="number" min={1} value={form.resolutionTimeHours} onChange={e => setForm(f => ({ ...f, resolutionTimeHours: e.target.value }))} className="input" />
            </Field>
          </div>
          <ModalActions onCancel={() => setAddModal(null)} onConfirm={handleAddPolicy} loading={saving} confirmLabel="Add Policy" />
        </Modal>
      )}
    </Section>
  )
}

// ─── PASSWORD POLICY ──────────────────────────────────────────────────────────

// ─── ESCALATION RULES ─────────────────────────────────────────────────────────

const ESCALATION_LEVELS = ['Supervisor', 'Department Head', 'MD']

export function EscalationSection() {
  const [categories, setCategories] = useState([])
  const [loading, setLoading] = useState(true)
  const [expanded, setExpanded] = useState(null)
  const [rules, setRules] = useState({})          // categoryId → rules[]
  const [ruleLoading, setRuleLoading] = useState({})
  const [addModal, setAddModal] = useState(null)  // categoryId or null
  const [form, setForm] = useState({ priority: 'Medium', escalationLevel: 'Supervisor', triggerAfterHours: 24, escalateToUserId: '' })
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState('')

  useEffect(() => {
    api.get('/api/v1/ticket-categories')
      .then(r => setCategories(r.data?.data ?? []))
      .finally(() => setLoading(false))
  }, [])

  async function loadRules(catId) {
    setRuleLoading(s => ({ ...s, [catId]: true }))
    try {
      const res = await api.get(`/api/v1/ticket-categories/${catId}/escalation-rules`)
      setRules(s => ({ ...s, [catId]: res.data?.data ?? [] }))
    } catch {
      setRules(s => ({ ...s, [catId]: [] }))
    } finally {
      setRuleLoading(s => ({ ...s, [catId]: false }))
    }
  }

  function toggle(catId) {
    if (expanded === catId) { setExpanded(null); return }
    setExpanded(catId)
    if (!rules[catId]) loadRules(catId)
  }

  async function handleAddRule() {
    setSaving(true); setFormError('')
    try {
      await api.post(`/api/v1/ticket-categories/${addModal}/escalation-rules`, {
        priority: PRIORITIES.indexOf(form.priority),
        escalationLevel: ESCALATION_LEVELS.indexOf(form.escalationLevel),
        triggerAfterHours: Number(form.triggerAfterHours),
        escalateToUserId: form.escalateToUserId.trim() || null,
      })
      setAddModal(null)
      loadRules(addModal)
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to save escalation rule.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Section
      title="Escalation Rules"
      description="Auto-escalate a ticket to a senior role if it stays open past a threshold (measured on active time — paused while waiting on the customer)."
    >
      {loading ? (
        <div className="space-y-3">{[1,2,3].map(i => <div key={i} className="h-14 bg-gray-100 rounded-xl animate-pulse" />)}</div>
      ) : categories.length === 0 ? (
        <EmptyState icon="⚠️" title="No categories" sub="Create ticket categories first to manage escalation rules." />
      ) : (
        <div className="space-y-3">
          {categories.map(cat => (
            <div key={cat.id} className="bg-white rounded-xl border border-gray-200 overflow-hidden">
              <button
                onClick={() => toggle(cat.id)}
                className="w-full flex items-center justify-between px-5 py-4 text-left hover:bg-gray-50 transition-colors"
              >
                <div className="flex items-center gap-3">
                  <span className="font-semibold text-sm text-gray-900">{cat.name}</span>
                  <PriorityBadge priority={cat.defaultPriority} />
                </div>
                <svg className={`w-4 h-4 text-gray-400 transition-transform ${expanded === cat.id ? 'rotate-180' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                </svg>
              </button>

              {expanded === cat.id && (
                <div className="border-t border-gray-100 px-5 py-4">
                  {ruleLoading[cat.id] ? (
                    <p className="text-sm text-gray-400 animate-pulse">Loading…</p>
                  ) : (rules[cat.id] ?? []).length === 0 ? (
                    <p className="text-sm text-gray-400">No escalation rules configured for this category.</p>
                  ) : (
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="text-xs text-gray-400 uppercase tracking-wide">
                          <th className="text-left pb-2">Priority</th>
                          <th className="text-left pb-2">Escalates to</th>
                          <th className="text-left pb-2">After</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-50">
                        {(rules[cat.id] ?? []).map(r => (
                          <tr key={r.id}>
                            <td className="py-2"><PriorityBadge priority={r.priority} /></td>
                            <td className="py-2 text-gray-700">{ESCALATION_LEVELS[r.escalationLevel] ?? r.escalationLevel}</td>
                            <td className="py-2 text-gray-700">{r.triggerAfterHours}h</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                  <button
                    onClick={() => { setForm({ priority: 'Medium', escalationLevel: 'Supervisor', triggerAfterHours: 24, escalateToUserId: '' }); setFormError(''); setAddModal(cat.id) }}
                    className="mt-3 text-xs font-semibold text-amber-600 hover:text-amber-800 transition-colors"
                  >
                    + Add Escalation Rule
                  </button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {addModal && (
        <Modal title="Add Escalation Rule" onClose={() => setAddModal(null)}>
          {formError && <ErrorBanner>{formError}</ErrorBanner>}
          <Field label="Priority">
            <select value={form.priority} onChange={e => setForm(f => ({ ...f, priority: e.target.value }))}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm">
              {PRIORITIES.map(p => <option key={p} value={p}>{p}</option>)}
            </select>
          </Field>
          <Field label="Escalate to">
            <select value={form.escalationLevel} onChange={e => setForm(f => ({ ...f, escalationLevel: e.target.value }))}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm">
              {ESCALATION_LEVELS.map(l => <option key={l} value={l}>{l}</option>)}
            </select>
          </Field>
          <Field label="Trigger after (hours open)">
            <input type="number" min="1" value={form.triggerAfterHours}
              onChange={e => setForm(f => ({ ...f, triggerAfterHours: e.target.value }))}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm" />
          </Field>
          <Field label="Assign to user (optional)">
            <input type="text" value={form.escalateToUserId} placeholder="User ID to notify — leave blank for the role"
              onChange={e => setForm(f => ({ ...f, escalateToUserId: e.target.value }))}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm" />
          </Field>
          <ModalActions onCancel={() => setAddModal(null)} onConfirm={handleAddRule} loading={saving} confirmLabel="Add Rule" />
        </Modal>
      )}
    </Section>
  )
}

function PasswordPolicySection() {
  const [policy, setPolicy] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    api.get('/api/v1/password-policy')
      .then(r => setPolicy(r.data?.data))
      .catch(() => setError('Failed to load password policy.'))
      .finally(() => setLoading(false))
  }, [])

  return (
    <Section
      title="Password Policy"
      description="These rules apply to all user accounts when setting or changing passwords."
    >
      {error && <ErrorBanner>{error}</ErrorBanner>}

      {loading ? (
        <div className="space-y-3">{[1,2,3,4,5].map(i => <div key={i} className="h-10 bg-gray-100 rounded-lg animate-pulse" />)}</div>
      ) : !policy ? (
        <EmptyState icon="🔒" title="No policy configured" sub="Contact a system administrator to configure the password policy." />
      ) : (
        <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
          <div className="px-5 py-3 bg-gray-50 border-b border-gray-100">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Current Policy (read-only)</p>
          </div>
          <div className="divide-y divide-gray-100">
            <PolicyRow label="Minimum Length" value={`${policy.minimumLength ?? policy.MinimumLength} characters`} />
            <PolicyRow label="Require Uppercase" value={yesNo(policy.requireUppercase ?? policy.RequireUppercase)} positive={policy.requireUppercase ?? policy.RequireUppercase} />
            <PolicyRow label="Require Lowercase" value={yesNo(policy.requireLowercase ?? policy.RequireLowercase)} positive={policy.requireLowercase ?? policy.RequireLowercase} />
            <PolicyRow label="Require Number" value={yesNo(policy.requireDigit ?? policy.RequireDigit)} positive={policy.requireDigit ?? policy.RequireDigit} />
            <PolicyRow label="Require Special Character" value={yesNo(policy.requireSpecialCharacter ?? policy.RequireSpecialCharacter)} positive={policy.requireSpecialCharacter ?? policy.RequireSpecialCharacter} />
            <PolicyRow label="Password Expires After" value={`${policy.maxAgeDays ?? policy.MaxAgeDays} days`} />
          </div>
        </div>
      )}
    </Section>
  )
}

function PolicyRow({ label, value, positive }) {
  return (
    <div className="flex items-center justify-between px-5 py-3.5">
      <span className="text-sm text-gray-600">{label}</span>
      <span className={`text-sm font-semibold ${positive === true ? 'text-green-600' : positive === false ? 'text-gray-400' : 'text-gray-800'}`}>
        {value}
      </span>
    </div>
  )
}

function yesNo(v) { return v ? 'Yes' : 'No' }

// ─── MY ACCOUNT ───────────────────────────────────────────────────────────────

function AccountSection() {
  const { user } = useAuth()
  const [form, setForm] = useState({ currentPassword: '', newPassword: '', confirmNewPassword: '' })
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  async function handleChangePassword(e) {
    e.preventDefault()
    setError('')
    setSuccess('')
    if (!form.currentPassword) { setError('Current password is required.'); return }
    if (!form.newPassword || form.newPassword.length < 8) { setError('New password must be at least 8 characters.'); return }
    if (form.newPassword !== form.confirmNewPassword) { setError('Passwords do not match.'); return }

    setSaving(true)
    try {
      await api.put(`/api/v1/auth/update-password/${user.id}`, {
        currentPassword: form.currentPassword,
        newPassword: form.newPassword,
        confirmNewPassword: form.confirmNewPassword,
      })
      setSuccess('Password changed successfully.')
      setForm({ currentPassword: '', newPassword: '', confirmNewPassword: '' })
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to change password.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Section title="My Account" description="Update your account credentials.">
      {/* Profile info card */}
      <div className="bg-white rounded-xl border border-gray-200 p-5 flex items-center gap-4 mb-6">
        <div className="w-12 h-12 rounded-full bg-zinc-950 flex items-center justify-center text-white font-extrabold text-lg shrink-0">
          {user?.firstName?.[0]?.toUpperCase() ?? '?'}
        </div>
        <div>
          <p className="font-bold text-gray-900">{user?.firstName} {user?.lastName}</p>
          <p className="text-sm text-gray-500">{user?.email}</p>
          {user?.userRoles?.length > 0 && (
            <div className="flex flex-wrap gap-1 mt-1">
              {user.userRoles.map(r => (
                <span key={r} className="text-xs font-medium px-2 py-0.5 bg-zinc-50 text-zinc-950 rounded-full">{r}</span>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Change password */}
      <div className="bg-white rounded-xl border border-gray-200 p-5">
        <h3 className="text-sm font-bold text-gray-800 mb-4">Change Password</h3>

        {error && <ErrorBanner>{error}</ErrorBanner>}
        {success && (
          <div className="flex items-center gap-2 bg-green-50 border border-green-200 text-green-700 rounded-lg px-4 py-3 text-sm mb-4">
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
            {success}
          </div>
        )}

        <form onSubmit={handleChangePassword} className="space-y-4">
          <Field label="Current Password">
            <input
              type="password"
              value={form.currentPassword}
              onChange={e => setForm(f => ({ ...f, currentPassword: e.target.value }))}
              className="input"
              placeholder="Enter your current password"
              autoComplete="current-password"
            />
          </Field>
          <Field label="New Password">
            <input
              type="password"
              value={form.newPassword}
              onChange={e => setForm(f => ({ ...f, newPassword: e.target.value }))}
              className="input"
              placeholder="At least 8 characters"
              autoComplete="new-password"
            />
          </Field>
          <Field label="Confirm New Password">
            <input
              type="password"
              value={form.confirmNewPassword}
              onChange={e => setForm(f => ({ ...f, confirmNewPassword: e.target.value }))}
              className="input"
              placeholder="Repeat new password"
              autoComplete="new-password"
            />
          </Field>
          <div className="flex justify-end pt-1">
            <button
              type="submit"
              disabled={saving}
              className="btn-amber"
            >
              {saving ? 'Saving…' : 'Change Password'}
            </button>
          </div>
        </form>
      </div>
    </Section>
  )
}

// ─── SHARED COMPONENTS ────────────────────────────────────────────────────────

function Section({ title, description, action, children }) {
  return (
    <div>
      <div className="flex items-start justify-between gap-4 mb-5">
        <div>
          <h2 className="text-lg font-bold text-gray-900">{title}</h2>
          {description && <p className="text-sm text-gray-500 mt-0.5">{description}</p>}
        </div>
        {action}
      </div>
      {children}
    </div>
  )
}

function Modal({ title, onClose, children }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6">
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-lg font-bold text-zinc-950">{title}</h2>
          <button onClick={onClose} className="p-1.5 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors">
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}

function ModalActions({ onCancel, onConfirm, loading, confirmLabel, danger }) {
  return (
    <div className="flex items-center justify-end gap-3 mt-6">
      <button onClick={onCancel} className="px-4 py-2 text-sm font-medium border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">Cancel</button>
      <button
        onClick={onConfirm}
        disabled={loading}
        className={`px-5 py-2 text-sm font-semibold rounded-lg disabled:opacity-50 transition-colors text-white ${danger ? 'bg-red-500 hover:bg-red-600' : 'bg-amber-500 hover:bg-amber-600'}`}
      >
        {loading ? 'Saving…' : confirmLabel}
      </button>
    </div>
  )
}

function Field({ label, children }) {
  return (
    <div>
      <label className="block text-sm font-semibold text-gray-700 mb-1.5">{label}</label>
      {children}
    </div>
  )
}

function Toggle({ checked, onChange, label, sub }) {
  return (
    <label className="flex items-start gap-3 cursor-pointer bg-gray-50 rounded-lg px-4 py-3 hover:bg-gray-100 transition-colors">
      <div className="relative mt-0.5 shrink-0">
        <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} className="sr-only peer" />
        <div className="w-9 h-5 bg-gray-200 peer-checked:bg-amber-500 rounded-full transition-colors" />
        <div className="absolute top-0.5 left-0.5 w-4 h-4 bg-white rounded-full shadow transition-transform peer-checked:translate-x-4" />
      </div>
      <div>
        <p className="text-sm font-medium text-gray-800">{label}</p>
        {sub && <p className="text-xs text-gray-500 mt-0.5">{sub}</p>}
      </div>
    </label>
  )
}

function ErrorBanner({ children }) {
  return (
    <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm mb-4">{children}</div>
  )
}

function EmptyState({ icon, title, sub }) {
  return (
    <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-14 text-center">
      <div className="text-4xl mb-3">{icon}</div>
      <h3 className="font-semibold text-gray-700 text-sm">{title}</h3>
      {sub && <p className="text-xs text-gray-400 mt-1">{sub}</p>}
    </div>
  )
}

const PRIORITY_COLORS = {
  0: 'bg-gray-100 text-gray-600',
  1: 'bg-blue-100 text-blue-700',
  2: 'bg-amber-100 text-amber-700',
  3: 'bg-red-100 text-red-700',
  Low: 'bg-gray-100 text-gray-600',
  Medium: 'bg-blue-100 text-blue-700',
  High: 'bg-amber-100 text-amber-700',
  Critical: 'bg-red-100 text-red-700',
}
const PRIORITY_LABELS = { 0: 'Low', 1: 'Medium', 2: 'High', 3: 'Critical' }

function PriorityBadge({ priority }) {
  const label = typeof priority === 'number' ? (PRIORITY_LABELS[priority] ?? priority) : priority
  const color = PRIORITY_COLORS[priority] ?? 'bg-gray-100 text-gray-600'
  return <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${color}`}>{label}</span>
}

// ─── ICONS ────────────────────────────────────────────────────────────────────

function CategoryIcon({ className }) {
  return (
    <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z" />
    </svg>
  )
}

function SLAIcon({ className }) {
  return (
    <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
    </svg>
  )
}

function ShieldIcon({ className }) {
  return (
    <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
    </svg>
  )
}

function UserIcon({ className }) {
  return (
    <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
    </svg>
  )
}

function BranchIcon({ className }) {
  return (
    <svg className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0H5m14 0h2M5 21H3M9 7h1m-1 4h1m4-4h1m-1 4h1M9 21v-4a1 1 0 011-1h4a1 1 0 011 1v4"/>
    </svg>
  )
}

// ─── BRANCH SWITCHER (company admin only) ─────────────────────────────────────

function BranchSwitcherSection() {
  const { tenantId, branchId, branchName, login, token, user, updateBranchContext } = useAuth()
  const [branches, setBranches] = useState([])
  const [loading, setLoading] = useState(true)
  const [switching, setSwitching] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  useEffect(() => {
    if (!tenantId) return
    api.get(`/api/v1/tenants/${tenantId}/branches`)
      .then(r => setBranches(r.data?.data ?? r.data))
      .catch(() => setError('Could not load branches.'))
      .finally(() => setLoading(false))
  }, [tenantId])

  async function handleSwitch(newBranchId) {
    if (newBranchId === branchId) return
    setSwitching(true)
    setError('')
    setSuccess('')
    try {
      const res = await api.post('/api/v1/auth/switch-branch', { branchId: newBranchId })
      const data = res.data?.data ?? res.data
      // Re-login with the new token (preserves all other user fields)
      login(data.token, {
        ...user,
        branchId: data.branchId ?? newBranchId,
        branchName: branches.find(b => b.id === (data.branchId ?? newBranchId))?.name ?? null,
        isCompanyAdmin: data.branchId == null,
      })
      setSuccess('Branch switched successfully.')
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to switch branch.')
    } finally {
      setSwitching(false)
    }
  }

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-6">
      <h2 className="text-lg font-bold text-zinc-900 mb-1">Branch Access</h2>
      <p className="text-sm text-gray-500 mb-6">
        As a company administrator, you can switch your active branch context. This affects which branch's data you see across the platform.
      </p>

      {error && (
        <div className="mb-4 text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-4 py-3">{error}</div>
      )}
      {success && (
        <div className="mb-4 text-sm text-green-700 bg-green-50 border border-green-200 rounded-lg px-4 py-3">{success}</div>
      )}

      {/* Current branch */}
      <div className="mb-4 flex items-center gap-3 p-3 rounded-lg bg-amber-50 border border-amber-200">
        <BranchIcon className="w-4 h-4 text-amber-600 shrink-0" />
        <div>
          <p className="text-xs font-semibold text-amber-700 uppercase tracking-wide">Current branch</p>
          <p className="text-sm font-bold text-amber-900 mt-0.5">{branchName ?? 'Company Admin (all branches)'}</p>
        </div>
      </div>

      {loading ? (
        <p className="text-sm text-gray-400">Loading branches…</p>
      ) : (
        <div className="flex flex-col gap-2">
          {/* Company-wide (no branch) option */}
          <label className={`flex items-center gap-3 p-3 rounded-lg border cursor-pointer transition-colors ${!branchId ? 'border-amber-400 bg-amber-50' : 'border-gray-200 hover:bg-gray-50'}`}>
            <input type="radio" name="branch" checked={!branchId} onChange={() => handleSwitch(null)}
              disabled={switching} className="accent-amber-500" />
            <div>
              <p className="text-sm font-semibold text-zinc-900">Company Admin View</p>
              <p className="text-xs text-gray-500">Access all branches</p>
            </div>
            {!branchId && <span className="ml-auto text-xs font-bold text-amber-600 bg-amber-100 px-2 py-0.5 rounded-full">Active</span>}
          </label>

          {branches.filter(b => b.isActive).map(b => (
            <label key={b.id} className={`flex items-center gap-3 p-3 rounded-lg border cursor-pointer transition-colors ${branchId === b.id ? 'border-amber-400 bg-amber-50' : 'border-gray-200 hover:bg-gray-50'}`}>
              <input type="radio" name="branch" checked={branchId === b.id} onChange={() => handleSwitch(b.id)}
                disabled={switching} className="accent-amber-500" />
              <div>
                <div className="flex items-center gap-2">
                  <p className="text-sm font-semibold text-zinc-900">{b.name}</p>
                  {b.isHeadOffice && <span className="text-xs font-bold text-amber-600 bg-amber-100 px-1.5 py-0.5 rounded">HQ</span>}
                </div>
                <p className="text-xs text-gray-500 font-mono">{b.code}</p>
              </div>
              {branchId === b.id && <span className="ml-auto text-xs font-bold text-amber-600 bg-amber-100 px-2 py-0.5 rounded-full">Active</span>}
            </label>
          ))}
        </div>
      )}

      {switching && (
        <p className="text-sm text-gray-400 mt-4">Switching branch, please wait…</p>
      )}
    </div>
  )
}
