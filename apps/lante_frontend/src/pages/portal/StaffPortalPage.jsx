import { useState, useRef } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import api from '../../api/axios.js'
import usePortalTenant from '../../hooks/usePortalTenant'

const DEPARTMENTS = [
  'Calibration Lab',
  'Construction',
  'CRM',
  'Finance',
  'Fleet',
  'General',
  'HR',
  'IT',
  'Quality',
  'Safety',
  'Sales',
  'Technical',
  'Technical Service',
]

const CATEGORIES = [
  'IT Support',
  'Facilities & Maintenance',
  'HR & Payroll',
  'Finance & Expenses',
  'Health & Safety',
  'Equipment & Tools',
  'Transport & Fleet',
  'Training & Development',
  'Other',
]

const PRIORITIES = [
  { value: 0, label: 'Low',      desc: 'Minor inconvenience, can wait',     color: '#22c55e' },
  { value: 1, label: 'Medium',   desc: 'Affects my work but not critical',   color: '#f59e0b' },
  { value: 2, label: 'High',     desc: 'Significantly blocking my work',     color: '#f97316' },
  { value: 3, label: 'Critical', desc: 'Complete work stoppage / safety risk', color: '#ef4444' },
]

const MAX_PHOTOS = 5
const MAX_SIZE   = 2 * 1024 * 1024

export default function StaffPortalPage() {
  const navigate = useNavigate()
  const fileRef  = useRef(null)
  const { slug, name: brandName } = usePortalTenant()
  const slugParams = slug ? { slug } : undefined

  const [form, setForm] = useState({
    fullName:    '',
    workEmail:   '',
    department:  '',
    category:    '',
    subject:     '',
    description: '',
    priority:    1,
  })
  const [photos, setPhotos]       = useState([])
  const [submitting, setSubmitting] = useState(false)
  const [error, setError]           = useState('')
  const [result, setResult]         = useState(null)

  function set(field, value) {
    setForm(f => ({ ...f, [field]: value }))
  }

  const canSubmit =
    form.fullName.trim() &&
    form.workEmail.trim() &&
    /\S+@\S+\.\S+/.test(form.workEmail) &&
    form.department &&
    form.subject.trim() &&
    form.description.trim()

  function handlePhotoSelect(e) {
    const incoming = Array.from(e.target.files ?? [])
    e.target.value = ''
    const combined = [...photos]
    for (const file of incoming) {
      if (combined.length >= MAX_PHOTOS) break
      const err = file.size > MAX_SIZE ? 'Exceeds 2 MB limit.' : null
      combined.push({ file, preview: URL.createObjectURL(file), error: err })
    }
    setPhotos(combined)
  }

  function removePhoto(idx) {
    setPhotos(prev => {
      URL.revokeObjectURL(prev[idx].preview)
      return prev.filter((_, i) => i !== idx)
    })
  }

  async function handleSubmit(e) {
    e.preventDefault()
    if (!canSubmit) return
    setError('')
    setSubmitting(true)
    try {
      const res = await api.post('/api/v1/portal/internal/submit', {
        fullName:    form.fullName.trim(),
        workEmail:   form.workEmail.trim(),
        department:  form.department,
        category:    form.category || null,
        subject:     form.subject.trim(),
        description: form.description.trim(),
        priority:    form.priority,
      }, { params: slugParams })

      // `?? {}` matters: `res.data?.data` yields undefined if the envelope is missing, and
      // destructuring undefined throws a TypeError that the catch below reports as
      // "Submission failed" — telling the user their ticket failed when it was created,
      // which invites a duplicate submission. The `?.` alone gave false safety.
      const { reference, ticketId } = res.data?.data ?? {}

      const validPhotos = photos.filter(p => !p.error)
      if (validPhotos.length > 0) {
        const fd = new FormData()
        validPhotos.forEach(p => fd.append('files', p.file))
        try {
          await api.post(`/api/v1/portal/internal/attachments/${ticketId}`, fd, { params: slugParams })
        } catch {
          // best-effort — ticket is already created
        }
      }

      setResult({ reference, ticketId })
    } catch (err) {
      setError(err.response?.data?.message ?? 'Submission failed. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  // ── Success screen ───────────────────────────────────────────────────────
  if (result) {
    return (
      <StaffShell brandName={brandName}>
        <div className="max-w-lg mx-auto text-center py-8">
          <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-5">
            <svg className="w-8 h-8 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h2 className="text-2xl font-extrabold text-zinc-950 mb-2">Ticket submitted!</h2>
          <p className="text-gray-500 mb-6">
            Hi <span className="font-semibold text-gray-700">{form.fullName}</span>, your ticket has been logged and the relevant team will be in touch.
          </p>
          <div className="bg-amber-50 border border-amber-200 rounded-2xl p-6 mb-6">
            <p className="text-xs font-semibold text-amber-600 uppercase tracking-wider mb-2">Reference Number</p>
            <p className="text-3xl font-extrabold text-zinc-950 tracking-widest">{result.reference}</p>
            <p className="text-xs text-gray-400 mt-2">Save this number to track your ticket</p>
          </div>
          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <button
              onClick={() => navigate('/portal/track', { state: { reference: result.reference } })}
              className="px-5 py-2.5 bg-zinc-950 text-white text-sm font-semibold rounded-xl hover:opacity-90 transition-colors"
            >
              Track My Ticket
            </button>
            <button
              onClick={() => {
                setResult(null)
                setPhotos([])
                setForm({ fullName: '', workEmail: '', department: '', category: '', subject: '', description: '', priority: 1 })
              }}
              className="px-5 py-2.5 border border-gray-200 text-gray-700 hover:bg-gray-50 text-sm font-semibold rounded-xl transition-colors"
            >
              Submit Another
            </button>
          </div>
        </div>
      </StaffShell>
    )
  }

  // ── Form ─────────────────────────────────────────────────────────────────
  return (
    <StaffShell brandName={brandName}>
      <div className="max-w-2xl mx-auto">
        <div className="mb-8">
          <h1 className="text-2xl font-extrabold text-zinc-950 mb-1">Raise an Internal Ticket</h1>
          <p className="text-sm text-gray-500">
            Use this form to raise a ticket, request support, or flag something that needs attention. No login required.
          </p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Contact */}
          <section className="bg-white rounded-2xl border border-gray-200 p-6">
            <h2 className="text-sm font-bold text-gray-700 uppercase tracking-wide mb-4">Your Details</h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Field label="Full Name *">
                <input
                  type="text" value={form.fullName}
                  onChange={e => set('fullName', e.target.value)}
                  className="input" placeholder="Jane Kamau" autoFocus
                />
              </Field>
              <Field label="Work Email *">
                <input
                  type="email" value={form.workEmail}
                  onChange={e => set('workEmail', e.target.value)}
                  className="input" placeholder="jane@company.co.ke"
                />
              </Field>
            </div>
          </section>

          {/* Ticket details */}
          <section className="bg-white rounded-2xl border border-gray-200 p-6">
            <h2 className="text-sm font-bold text-gray-700 uppercase tracking-wide mb-4">Ticket Details</h2>
            <div className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <Field label="Your Department *">
                  <select value={form.department} onChange={e => set('department', e.target.value)} className="input">
                    <option value="">Select department…</option>
                    {DEPARTMENTS.map(d => <option key={d} value={d}>{d}</option>)}
                  </select>
                </Field>
                <Field label="Category">
                  <select value={form.category} onChange={e => set('category', e.target.value)} className="input">
                    <option value="">Select category…</option>
                    {CATEGORIES.map(c => <option key={c} value={c}>{c}</option>)}
                  </select>
                </Field>
              </div>
              <Field label="Subject *">
                <input
                  type="text" value={form.subject}
                  onChange={e => set('subject', e.target.value)}
                  className="input" placeholder="Brief summary of the ticket"
                />
              </Field>
              <Field label="Description *">
                <textarea
                  value={form.description}
                  onChange={e => set('description', e.target.value)}
                  rows={5} className="input resize-none"
                  placeholder="Describe the ticket in detail — what happened, when it started, what impact it's having…"
                />
              </Field>
            </div>
          </section>

          {/* Priority */}
          <section className="bg-white rounded-2xl border border-gray-200 p-6">
            <h2 className="text-sm font-bold text-gray-700 uppercase tracking-wide mb-4">Priority</h2>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              {PRIORITIES.map(p => (
                <button
                  key={p.value} type="button"
                  onClick={() => set('priority', p.value)}
                  className={`text-left p-3 rounded-xl border-2 transition-all ${
                    form.priority === p.value
                      ? 'border-amber-500 bg-amber-50'
                      : 'border-gray-200 hover:border-gray-300 bg-white'
                  }`}
                >
                  <p className="text-xs font-bold mb-0.5" style={{ color: p.color }}>{p.label}</p>
                  <p className="text-xs text-gray-500 leading-snug">{p.desc}</p>
                </button>
              ))}
            </div>
          </section>

          {/* Photos */}
          <section className="bg-white rounded-2xl border border-gray-200 p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-sm font-bold text-gray-700 uppercase tracking-wide">
                Attach Photos <span className="text-gray-400 font-normal normal-case">(optional — up to {MAX_PHOTOS}, max 2 MB each)</span>
              </h2>
              {photos.length < MAX_PHOTOS && (
                <button type="button" onClick={() => fileRef.current?.click()}
                  className="text-xs font-semibold text-amber-600 hover:text-amber-800 transition-colors">
                  + Add photo
                </button>
              )}
            </div>
            <input ref={fileRef} type="file" accept="image/*" multiple className="hidden" onChange={handlePhotoSelect} />
            {photos.length === 0 ? (
              <button type="button" onClick={() => fileRef.current?.click()}
                className="w-full border-2 border-dashed border-gray-200 rounded-xl p-6 text-center hover:border-amber-300 hover:bg-amber-50 transition-all">
                <div className="text-3xl mb-1">📷</div>
                <p className="text-sm text-gray-500">Click to upload images (JPG, PNG, WebP)</p>
                <p className="text-xs text-gray-400 mt-0.5">Screenshots, photos of the ticket, etc.</p>
              </button>
            ) : (
              <div className="grid grid-cols-4 sm:grid-cols-5 gap-3">
                {photos.map((p, i) => (
                  <div key={i} className="relative group rounded-xl overflow-hidden border border-gray-200 bg-gray-50 aspect-square">
                    <img src={p.preview} alt="" className="w-full h-full object-cover" />
                    {p.error && (
                      <div className="absolute inset-0 bg-red-900/60 flex items-center justify-center p-1">
                        <p className="text-white text-xs text-center">{p.error}</p>
                      </div>
                    )}
                    <button type="button" onClick={() => removePhoto(i)}
                      className="absolute top-1 right-1 w-6 h-6 bg-black/60 hover:bg-black/80 rounded-full flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
                      <svg className="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.5">
                        <path d="M6 18L18 6M6 6l12 12"/>
                      </svg>
                    </button>
                  </div>
                ))}
                {photos.length < MAX_PHOTOS && (
                  <button type="button" onClick={() => fileRef.current?.click()}
                    className="aspect-square border-2 border-dashed border-gray-200 rounded-xl flex flex-col items-center justify-center hover:border-amber-300 hover:bg-amber-50 transition-all">
                    <svg className="w-5 h-5 text-gray-400 mb-1" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.5">
                      <path d="M12 4v16m8-8H4"/>
                    </svg>
                    <span className="text-xs text-gray-400">Add</span>
                  </button>
                )}
              </div>
            )}
          </section>

          {/* Submit */}
          {error && (
            <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-4 py-3 text-sm">{error}</div>
          )}
          <div className="flex items-center justify-between">
            <p className="text-xs text-gray-400">
              Your submission will be routed to the appropriate team automatically.
            </p>
            <button
              type="submit" disabled={!canSubmit || submitting}
              className="px-7 py-3 bg-amber-500 hover:bg-amber-600 disabled:opacity-40 text-white text-sm font-bold rounded-xl transition-colors flex items-center gap-2"
            >
              {submitting && (
                <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/>
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z"/>
                </svg>
              )}
              {submitting ? 'Submitting…' : 'Submit Ticket'}
            </button>
          </div>
        </form>
      </div>
    </StaffShell>
  )
}

function StaffShell({ children, brandName }) {
  const navigate = useNavigate()
  const { logoUrl } = usePortalTenant()
  const brand = brandName || 'Lante'
  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      <header className="bg-white border-b border-gray-200">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
          <button onClick={() => navigate('/')} className="flex items-center gap-3 group">
            <img src={logoUrl || "/qc-logo.png"} alt={brand} className="w-9 h-9 object-contain" />
            <div className="text-left">
              <p className="font-extrabold text-zinc-950 text-sm leading-none">{brand}</p>
              <p className="text-xs text-gray-400">Staff Ticket Portal</p>
            </div>
          </button>
          <div className="flex items-center gap-4">
            <Link to="/portal/track" className="text-sm text-gray-400 hover:text-gray-600 transition-colors">
              Track a ticket →
            </Link>
          </div>
        </div>
      </header>
      <main className="flex-1 px-4 sm:px-6 py-10">
        {children}
      </main>
      <footer className="border-t border-gray-200 bg-white py-4 px-6 text-center">
        <p className="text-xs text-gray-400">{brand} &copy; {new Date().getFullYear()} · Internal Staff Portal</p>
      </footer>
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
