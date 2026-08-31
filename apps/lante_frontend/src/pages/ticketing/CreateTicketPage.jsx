import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { getCategories, getDepartments, suggestKb, searchCustomers, createTicket } from '../../services/ticketing.js'
import { useAuth } from '../../context/AuthContext.jsx'
import api from '../../api/axios.js'

const PRIORITIES = [
  { label: 'Low', value: 0 },
  { label: 'Medium', value: 1 },
  { label: 'High', value: 2 },
  { label: 'Critical', value: 3 },
]

// Source is auto-captured as Manual for a staff-raised ticket — the other sources (CRM, System,
// Scheduled, Safety) are set by the portal/automation, never chosen by hand. Created-by is captured
// server-side from the JWT.
const SOURCE_MANUAL = 0

export default function CreateTicketPage() {
  const navigate = useNavigate()
  const { departmentId } = useAuth()

  const [categories, setCategories] = useState([])
  const [departments, setDepartments] = useState([])
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  const [form, setForm] = useState({
    title: '',
    description: '',
    categoryId: '',
    priority: 1,
    departmentId: departmentId ?? '',
    dueDate: '',
  })

  // D1-3 — client combobox. `clientQuery` is what's typed; picking an existing client sets
  // `customer` ({id,name}); typing a name with no pick sends it as a new client (resolve-or-create
  // server-side). `newClient` reveals optional company/reference inputs when adding one.
  const [clientQuery, setClientQuery] = useState('')
  const [clientResults, setClientResults] = useState([])
  const [customer, setCustomer] = useState(null)
  const [clientOpen, setClientOpen] = useState(false)
  const [newClient, setNewClient] = useState({ company: '', reference: '' })
  const clientBoxRef = useRef(null)

  // D7-1 — IT-helpdesk fields, shown only when an IT category is selected.
  const [itFields, setItFields] = useState({ employeeId: '', branchId: '', systemAffected: '' })
  const isItCategory = form.categoryId.startsWith('cat-it-')

  // D7-4 — KB deflection suggestions as the title is typed.
  const [kbSuggestions, setKbSuggestions] = useState([])

  // Default the department to the creator's own once the auth context resolves (still editable).
  useEffect(() => {
    if (departmentId) setForm(f => (f.departmentId ? f : { ...f, departmentId }))
  }, [departmentId])

  // Selecting a category adopts its default priority (unless the user has already picked one).
  function onCategoryChange(catId) {
    const cat = categories.find(c => c.id === catId)
    setForm(f => ({ ...f, categoryId: catId, priority: cat ? cat.defaultPriority : f.priority }))
  }

  useEffect(() => {
    Promise.all([getCategories(), getDepartments()]).then(([cats, raw]) => {
      setCategories(cats ?? [])
      setDepartments(Array.isArray(raw) ? raw : raw?.departments ?? [])
    }).catch(() => {})
  }, [])

  function set(field, value) {
    setForm(f => ({ ...f, [field]: value }))
  }

  // D7-4 — debounced KB deflection search on the ticket title.
  useEffect(() => {
    const q = form.title.trim()
    if (q.length < 4) { setKbSuggestions([]); return }
    const t = setTimeout(() => {
      suggestKb(q)
        .then(list => setKbSuggestions(list ?? []))
        .catch(() => setKbSuggestions([]))
    }, 350)
    return () => clearTimeout(t)
  }, [form.title])

  // Debounced client search. Selecting a result pins `customer`; editing the text again clears the
  // pin so the typed value is treated as a (possibly new) client name.
  useEffect(() => {
    const q = clientQuery.trim()
    const t = setTimeout(() => {
      searchCustomers(q)
        .then(list => setClientResults(list ?? []))
        .catch(() => setClientResults([]))
    }, 250)
    return () => clearTimeout(t)
  }, [clientQuery])

  // Close the dropdown when clicking outside the combobox.
  useEffect(() => {
    function onDocClick(e) {
      if (clientBoxRef.current && !clientBoxRef.current.contains(e.target)) setClientOpen(false)
    }
    document.addEventListener('mousedown', onDocClick)
    return () => document.removeEventListener('mousedown', onDocClick)
  }, [])

  function pickClient(c) {
    setCustomer(c)
    setClientQuery(c.company ? `${c.name} — ${c.company}` : c.name)
    setNewClient({ company: '', reference: '' })
    setClientOpen(false)
  }

  function onClientInput(value) {
    setClientQuery(value)
    setCustomer(null)   // typing after a pick reverts to "new/typed client" mode
    setClientOpen(true)
  }

  // An exact case-insensitive name match means "＋ Add new" would be a duplicate — hide it then.
  const trimmedQuery = clientQuery.trim()
  const hasExactMatch = clientResults.some(c => c.name.toLowerCase() === trimmedQuery.toLowerCase())
  const showAddNew = trimmedQuery.length > 0 && !customer && !hasExactMatch

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')

    if (!form.title.trim()) { setError('Title is required.'); return }
    if (!form.categoryId)   { setError('Category is required.'); return }
    if (!form.departmentId) { setError('Department is required.'); return }

    setSubmitting(true)
    try {
      const payload = {
        title: form.title.trim(),
        description: form.description.trim(),
        categoryId: form.categoryId,
        priority: Number(form.priority),
        source: SOURCE_MANUAL,
        departmentId: form.departmentId,
        linkedEntityType: 7, // None
        dueDate: form.dueDate || null,
      }
      // D8-1/D1-3 — client: a CRM-master pick sends crmCustomerId (links to the org-wide customer);
      // a local pick sends customerId; a typed-in name resolves-or-creates.
      if (customer) {
        if (customer.crmCustomerId) payload.crmCustomerId = customer.crmCustomerId
        else payload.customerId = customer.id
      } else if (trimmedQuery) {
        payload.clientName = trimmedQuery
        if (newClient.company.trim()) payload.clientCompany = newClient.company.trim()
        if (newClient.reference.trim()) payload.clientReference = newClient.reference.trim()
      }
      // D7-1 — IT-helpdesk context for IT-category tickets.
      if (isItCategory) {
        if (itFields.employeeId.trim()) payload.employeeId = itFields.employeeId.trim()
        if (itFields.branchId.trim()) payload.branchId = itFields.branchId.trim()
        if (itFields.systemAffected.trim()) payload.systemAffected = itFields.systemAffected.trim()
      }
      const created = await createTicket(payload)
      navigate(created?.id ? `/modules/ticketing/${created.id}` : '/modules/ticketing')
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to create ticket. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <>

      <main className="flex-1 max-w-2xl mx-auto w-full px-4 sm:px-6 py-8">
        {/* Header */}
        <div className="flex items-center gap-3 mb-6">
          <button
            onClick={() => navigate('/modules/ticketing')}
            className="p-2 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors"
          >
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
          </button>
          <div>
            <h1 className="text-xl font-extrabold text-zinc-950">New Ticket</h1>
            <p className="text-sm text-gray-500">Fill in the details below to raise a ticket</p>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="bg-white rounded-xl border border-gray-200 p-6 space-y-5">
          {error && (
            <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm">
              {error}
            </div>
          )}

          {/* Title */}
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-1.5">
              Title <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={form.title}
              onChange={e => set('title', e.target.value)}
              placeholder="Brief summary of the issue"
              className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 focus:border-transparent"
            />
            {/* D7-4 — KB deflection: suggest existing articles before a ticket is raised */}
            {kbSuggestions.length > 0 && (
              <div className="mt-2 rounded-lg border border-blue-200 bg-blue-50 p-3">
                <p className="text-xs font-semibold text-blue-800 mb-1.5">💡 These articles might already answer this:</p>
                <ul className="space-y-1">
                  {kbSuggestions.map(a => (
                    <li key={a.id}>
                      <a
                        href={`/modules/ticketing/kb/${a.id}`}
                        onClick={e => { e.preventDefault(); window.open(`/modules/ticketing/kb/${a.id}`, '_blank') }}
                        className="text-sm text-blue-700 hover:underline"
                      >
                        {a.title} <span className="text-blue-400">· {a.category}</span>
                      </a>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>

          {/* Description */}
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-1.5">Description</label>
            <textarea
              value={form.description}
              onChange={e => set('description', e.target.value)}
              rows={4}
              placeholder="Detailed description of the issue…"
              className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 focus:border-transparent resize-none"
            />
          </div>

          {/* Client (D1-3) — searchable, typable to add a new one */}
          <div ref={clientBoxRef} className="relative">
            <label className="block text-sm font-semibold text-gray-700 mb-1.5">
              Client <span className="text-gray-400 font-normal">(optional)</span>
            </label>
            <input
              type="text"
              value={clientQuery}
              onChange={e => onClientInput(e.target.value)}
              onFocus={() => setClientOpen(true)}
              placeholder="Search a client or type a new name…"
              className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 focus:border-transparent"
            />
            {customer && (
              <p className="mt-1 text-xs text-emerald-600">✓ Existing client selected</p>
            )}

            {clientOpen && (clientResults.length > 0 || showAddNew) && (
              <div className="absolute z-10 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg max-h-60 overflow-auto">
                {clientResults.map(c => (
                  <button
                    key={c.id}
                    type="button"
                    onClick={() => pickClient(c)}
                    className="w-full text-left px-3 py-2 text-sm hover:bg-amber-50 flex flex-col"
                  >
                    <span className="font-medium text-gray-800">{c.name}</span>
                    {(c.company || c.clientReference) && (
                      <span className="text-xs text-gray-500">
                        {[c.company, c.clientReference].filter(Boolean).join(' · ')}
                      </span>
                    )}
                  </button>
                ))}
                {showAddNew && (
                  <button
                    type="button"
                    onClick={() => { setCustomer(null); setClientOpen(false) }}
                    className="w-full text-left px-3 py-2 text-sm text-amber-700 hover:bg-amber-50 border-t border-gray-100 font-medium"
                  >
                    ＋ Add “{trimmedQuery}” as a new client
                  </button>
                )}
              </div>
            )}

            {/* Optional details when adding a brand-new client */}
            {showAddNew && !clientOpen && (
              <div className="mt-2 grid grid-cols-2 gap-3">
                <input
                  type="text"
                  value={newClient.company}
                  onChange={e => setNewClient(n => ({ ...n, company: e.target.value }))}
                  placeholder="Company (optional)"
                  className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                />
                <input
                  type="text"
                  value={newClient.reference}
                  onChange={e => setNewClient(n => ({ ...n, reference: e.target.value }))}
                  placeholder="Client reference (optional)"
                  className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                />
              </div>
            )}
          </div>

          {/* Category + Priority */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1.5">
                Category <span className="text-red-500">*</span>
              </label>
              <select
                value={form.categoryId}
                onChange={e => onCategoryChange(e.target.value)}
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
              >
                <option value="">Select category</option>
                {categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>

            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1.5">Priority</label>
              <select
                value={form.priority}
                onChange={e => set('priority', e.target.value)}
                className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
              >
                {PRIORITIES.map(p => <option key={p.value} value={p.value}>{p.label}</option>)}
              </select>
            </div>
          </div>

          {/* D7-1 — IT-helpdesk context, only for IT categories */}
          {isItCategory && (
            <div className="rounded-lg border border-gray-200 bg-gray-50 p-4 space-y-3">
              <p className="text-xs font-semibold text-gray-600 uppercase tracking-wider">IT Helpdesk Details</p>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <input type="text" value={itFields.employeeId} onChange={e => setItFields(f => ({ ...f, employeeId: e.target.value }))}
                  placeholder="Employee ID"
                  className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                <input type="text" value={itFields.branchId} onChange={e => setItFields(f => ({ ...f, branchId: e.target.value }))}
                  placeholder="Branch"
                  className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                <input type="text" value={itFields.systemAffected} onChange={e => setItFields(f => ({ ...f, systemAffected: e.target.value }))}
                  placeholder="System affected"
                  className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
              </div>
            </div>
          )}

          {/* Department (defaults to yours; re-route if needed) */}
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-1.5">
              Department <span className="text-red-500">*</span>
            </label>
            <select
              value={form.departmentId}
              onChange={e => set('departmentId', e.target.value)}
              className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
            >
              <option value="">Select department</option>
              {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </div>

          {/* Due Date */}
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-1.5">Due Date <span className="text-gray-400 font-normal">(optional)</span></label>
            <input
              type="date"
              value={form.dueDate}
              onChange={e => set('dueDate', e.target.value)}
              className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
            />
          </div>

          {/* Actions */}
          <div className="flex items-center justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={() => navigate('/modules/ticketing')}
              className="px-5 py-2.5 text-sm font-medium text-gray-600 hover:text-gray-800 border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="px-6 py-2.5 bg-amber-500 hover:bg-amber-600 disabled:opacity-50 text-white text-sm font-semibold rounded-lg transition-colors"
            >
              {submitting ? 'Creating…' : 'Create Ticket'}
            </button>
          </div>
        </form>
      </main>
    </>
  )
}
