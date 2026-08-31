import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'

const TYPES = [
  { label: 'Service',      value: 0 },
  { label: 'Construction', value: 1 },
  { label: 'Calibration',  value: 2 },
]

const RISKS = [
  { label: 'Low',    value: 0 },
  { label: 'Medium', value: 1 },
  { label: 'High',   value: 2 },
]

export default function CreateProjectPage() {
  const navigate = useNavigate()

  const [departments, setDepartments] = useState([])
  const [submitting, setSubmitting]   = useState(false)
  const [error, setError]             = useState('')

  const [form, setForm] = useState({
    name: '',
    clientName: '',
    clientReference: '',
    scopeSummary: '',
    contractValue: '',
    startDate: '',
    expectedEndDate: '',
    type: 0,
    riskLevel: 0,
    departmentId: '',
    tenderReference: '',
    notes: '',
  })

  useEffect(() => {
    api.get('/api/v1/departments')
      .then(res => {
        const raw = res.data?.data
        setDepartments(Array.isArray(raw) ? raw : raw?.departments ?? [])
      })
      .catch(() => {})
  }, [])

  function set(field, value) {
    setForm(f => ({ ...f, [field]: value }))
  }

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')
    if (!form.name.trim())         { setError('Project name is required.'); return }
    if (!form.departmentId)        { setError('Department is required.'); return }
    if (!form.startDate)           { setError('Start date is required.'); return }
    if (!form.expectedEndDate)     { setError('Expected end date is required.'); return }
    if (!form.contractValue || isNaN(Number(form.contractValue))) { setError('Valid contract value is required.'); return }

    setSubmitting(true)
    try {
      const payload = {
        name:            form.name.trim(),
        clientName:      form.clientName.trim() || null,
        clientReference: form.clientReference.trim() || null,
        scopeSummary:    form.scopeSummary.trim() || null,
        contractValue:   Number(form.contractValue),
        startDate:       new Date(form.startDate).toISOString(),
        expectedEndDate: new Date(form.expectedEndDate).toISOString(),
        type:            Number(form.type),
        riskLevel:       Number(form.riskLevel),
        departmentId:    form.departmentId,
        tenderReference: form.tenderReference.trim() || null,
        notes:           form.notes.trim() || null,
      }
      const res = await api.post('/api/v1/projects', payload)
      const id  = res.data?.data?.id
      navigate(id ? `/modules/projects/${id}` : '/modules/projects')
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to create project.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <>

      <main className="flex-1 max-w-2xl mx-auto w-full px-4 sm:px-6 py-8">
        <div className="flex items-center gap-3 mb-6">
          <button
            onClick={() => navigate('/modules/projects')}
            className="p-2 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors"
          >
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
          </button>
          <div>
            <h1 className="text-xl font-extrabold text-navy">New Project</h1>
            <p className="text-sm text-gray-500">Project will be created in Draft status</p>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="bg-white rounded-xl border border-gray-200 p-6 space-y-5">
          {error && (
            <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm">{error}</div>
          )}

          <Field label="Project Name *">
            <input type="text" value={form.name} onChange={e => set('name', e.target.value)} className="input" placeholder="e.g. Kilimani Office Fit-Out" />
          </Field>

          <div className="grid grid-cols-2 gap-4">
            <Field label="Client Name">
              <input type="text" value={form.clientName} onChange={e => set('clientName', e.target.value)} className="input" placeholder="Company / individual" />
            </Field>
            <Field label="Client Reference">
              <input type="text" value={form.clientReference} onChange={e => set('clientReference', e.target.value)} className="input" placeholder="e.g. PO-2026-001" />
            </Field>
          </div>

          <Field label="Scope Summary">
            <textarea value={form.scopeSummary} onChange={e => set('scopeSummary', e.target.value)} rows={3} className="input resize-none" placeholder="Brief description of project scope…" />
          </Field>

          <div className="grid grid-cols-2 gap-4">
            <Field label="Contract Value (KES) *">
              <input type="number" min="0" step="0.01" value={form.contractValue} onChange={e => set('contractValue', e.target.value)} className="input" placeholder="0.00" />
            </Field>
            <Field label="Tender Reference">
              <input type="text" value={form.tenderReference} onChange={e => set('tenderReference', e.target.value)} className="input" placeholder="e.g. TENDER-2026-04" />
            </Field>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <Field label="Start Date *">
              <input type="date" value={form.startDate} onChange={e => set('startDate', e.target.value)} className="input" />
            </Field>
            <Field label="Expected End Date *">
              <input type="date" value={form.expectedEndDate} onChange={e => set('expectedEndDate', e.target.value)} className="input" />
            </Field>
          </div>

          <div className="grid grid-cols-3 gap-4">
            <Field label="Type *">
              <select value={form.type} onChange={e => set('type', e.target.value)} className="input">
                {TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </Field>
            <Field label="Risk Level *">
              <select value={form.riskLevel} onChange={e => set('riskLevel', e.target.value)} className="input">
                {RISKS.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
              </select>
            </Field>
            <Field label="Department *">
              <select value={form.departmentId} onChange={e => set('departmentId', e.target.value)} className="input">
                <option value="">Select…</option>
                {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
            </Field>
          </div>

          <Field label="Notes">
            <textarea value={form.notes} onChange={e => set('notes', e.target.value)} rows={2} className="input resize-none" placeholder="Any additional notes…" />
          </Field>

          <div className="flex items-center justify-end gap-3 pt-2">
            <button type="button" onClick={() => navigate('/modules/projects')}
              className="px-5 py-2.5 text-sm font-medium border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">
              Cancel
            </button>
            <button type="submit" disabled={submitting}
              className="px-6 py-2.5 bg-navy hover:bg-navy-dark disabled:opacity-50 text-white text-sm font-semibold rounded-lg transition-colors">
              {submitting ? 'Creating…' : 'Create Project'}
            </button>
          </div>
        </form>
      </main>
    </>
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
