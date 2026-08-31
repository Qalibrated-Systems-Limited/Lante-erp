import { useState, useRef } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import api from '../../api/axios.js'
import usePortalTenant from '../../hooks/usePortalTenant'

const TYPES = [
  { value: 'Complaint',      label: 'Complaint',        icon: '📣', desc: 'Something went wrong or below expectations' },
  { value: 'Feedback',       label: 'Feedback',         icon: '💬', desc: 'Share your experience or suggestions' },
  { value: 'ProjectInquiry', label: 'Project Inquiry',  icon: '🏗️', desc: 'Get in touch about a new project' },
  { value: 'General',        label: 'General Message',  icon: '✉️', desc: 'Any other query or message' },
]

const PROJECT_SERVICES = [
  'Construction & Civil Works',
  'MEP (Mechanical, Electrical & Plumbing)',
  'Technical Service & Maintenance',
  'Calibration Services',
  'Fire Safety Systems',
  'Road & Infrastructure',
  'Other / Not sure',
]

const STEPS = ['Type', 'Your Details', 'Message', 'Review']
const MAX_PHOTOS   = 3
const MAX_SIZE     = 1 * 1024 * 1024  // 1 MB
const ALLOWED_MIME = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']

export default function PortalSubmitPage() {
  const navigate   = useNavigate()
  const location   = useLocation()
  const { slug, name: brandName, logoUrl } = usePortalTenant()
  const preselType = location.state?.type ?? 'General'

  const [step, setStep] = useState(preselType !== 'General' ? 1 : 0)

  const [form, setForm] = useState({
    type:         preselType,
    name:         '',
    email:        '',
    company:      '',
    phone:        '',
    subject:      '',
    message:      '',
    serviceType:  '',
    budget:       '',
    timeline:     '',
  })

  // photo state: array of { file, preview, error }
  const [photos, setPhotos]       = useState([])
  const fileInputRef               = useRef(null)

  const [submitting, setSubmitting] = useState(false)
  const [error, setError]           = useState('')
  const [result, setResult]         = useState(null)

  function set(field, value) {
    setForm(f => ({ ...f, [field]: value }))
  }

  function canAdvance() {
    if (step === 0) return !!form.type
    if (step === 1) return form.name.trim() && form.email.trim() && /\S+@\S+\.\S+/.test(form.email)
    if (step === 2) return form.subject.trim() && form.message.trim()
    return true
  }

  function handlePhotoSelect(e) {
    const incoming = Array.from(e.target.files ?? [])
    e.target.value = ''
    const combined = [...photos]
    for (const file of incoming) {
      if (combined.length >= MAX_PHOTOS) break
      const err = !ALLOWED_MIME.includes(file.type)
        ? 'Not a supported image type.'
        : file.size > MAX_SIZE
        ? 'Exceeds 1 MB limit.'
        : null
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

  async function handleSubmit() {
    setError('')
    setSubmitting(true)
    try {
      let fullMessage = form.message.trim()
      if (form.type === 'ProjectInquiry') {
        const extras = [
          form.serviceType && `Service of Interest: ${form.serviceType}`,
          form.budget      && `Approximate Budget: ${form.budget}`,
          form.timeline    && `Desired Timeline: ${form.timeline}`,
        ].filter(Boolean).join('\n')
        if (extras) fullMessage = `${fullMessage}\n\n---\n${extras}`
      }

      const res = await api.post('/api/v1/portal/submit', {
        name:    form.name.trim(),
        email:   form.email.trim(),
        company: form.company.trim() || null,
        phone:   form.phone.trim()   || null,
        type:    ['Complaint','Feedback','ProjectInquiry','General'].indexOf(form.type),
        subject: form.subject.trim(),
        message: fullMessage,
      }, { params: slug ? { slug } : undefined })

      // `?? {}` matters: `res.data?.data` yields undefined if the envelope is missing, and
      // destructuring undefined throws a TypeError that the catch below reports as
      // "Submission failed" — telling the user their ticket failed when it was created,
      // which invites a duplicate submission. The `?.` alone gave false safety.
      const { reference, ticketId } = res.data?.data ?? {}

      // Upload photos if any (best-effort — don't fail the submission)
      const validPhotos = photos.filter(p => !p.error)
      if (validPhotos.length > 0) {
        const fd = new FormData()
        validPhotos.forEach(p => fd.append('files', p.file))
        try {
          await api.post(`/api/v1/portal/attachments/${ticketId}`, fd, { params: slug ? { slug } : undefined })
        } catch {
          // Photos failed but ticket was created — still show success
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
      <PortalShell onBack={() => navigate('/portal')} brandName={brandName}>
        <div className="max-w-lg mx-auto text-center py-8">
          <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-5">
            <svg className="w-8 h-8 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h2 className="text-2xl font-extrabold text-zinc-950 mb-2">Submission received!</h2>
          <p className="text-gray-500 mb-6">
            Thank you, <span className="font-semibold text-gray-700">{form.name}</span>. Our team will be in touch with you at{' '}
            <span className="font-semibold text-gray-700">{form.email}</span>.
          </p>
          <div className="bg-amber-50 border border-amber-200 rounded-2xl p-6 mb-6">
            <p className="text-xs font-semibold text-amber-600 uppercase tracking-wider mb-2">Your Reference Number</p>
            <p className="text-3xl font-extrabold text-zinc-950 tracking-widest">{result.reference}</p>
            <p className="text-xs text-gray-400 mt-2">Save this to track your submission</p>
          </div>
          <div className="flex flex-col sm:flex-row gap-3 justify-center">
            <button
              onClick={() => navigate('/portal/track', { state: { reference: result.reference } })}
              className="px-5 py-2.5 bg-zinc-950 text-white text-sm font-semibold rounded-xl transition-colors hover:opacity-90"
            >
              Track My Submission
            </button>
            <button
              onClick={() => navigate('/portal')}
              className="px-5 py-2.5 border border-gray-200 text-gray-700 hover:bg-gray-50 text-sm font-semibold rounded-xl transition-colors"
            >
              Back to Portal
            </button>
          </div>
        </div>
      </PortalShell>
    )
  }

  // ── Form steps ───────────────────────────────────────────────────────────
  return (
    <PortalShell onBack={() => step === 0 ? navigate('/portal') : setStep(s => s - 1)} backLabel={step === 0 ? '← Portal Home' : '← Back'} brandName={brandName}>
      {/* Step indicator */}
      <div className="flex items-center gap-1 mb-8">
        {STEPS.map((s, i) => (
          <div key={s} className="flex items-center gap-1 flex-1">
            <div className={`w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold shrink-0 transition-colors ${
              i < step ? 'bg-green-500 text-white' : i === step ? 'bg-amber-500 text-white' : 'bg-gray-100 text-gray-400'
            }`}>
              {i < step ? '✓' : i + 1}
            </div>
            <span className={`text-xs font-medium hidden sm:block ${i === step ? 'text-amber-700' : 'text-gray-400'}`}>{s}</span>
            {i < STEPS.length - 1 && <div className={`flex-1 h-px mx-1 ${i < step ? 'bg-green-400' : 'bg-gray-200'}`} />}
          </div>
        ))}
      </div>

      {/* Step 0 — Type */}
      {step === 0 && (
        <div>
          <h2 className="text-xl font-extrabold text-zinc-950 mb-1">What would you like to do?</h2>
          <p className="text-sm text-gray-500 mb-6">Select the type of submission that best describes your message.</p>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {TYPES.map(t => (
              <button
                key={t.value}
                onClick={() => { set('type', t.value); setStep(1) }}
                className={`text-left p-4 rounded-xl border-2 transition-all ${form.type === t.value ? 'border-amber-500 bg-amber-50' : 'border-gray-200 hover:border-gray-300 bg-white'}`}
              >
                <div className="text-2xl mb-2">{t.icon}</div>
                <p className="font-semibold text-gray-900 text-sm">{t.label}</p>
                <p className="text-xs text-gray-500 mt-0.5">{t.desc}</p>
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Step 1 — Contact details */}
      {step === 1 && (
        <div>
          <h2 className="text-xl font-extrabold text-zinc-950 mb-1">Your details</h2>
          <p className="text-sm text-gray-500 mb-6">So we know how to reach you with a response.</p>
          <div className="space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Field label="Full Name *">
                <input type="text" value={form.name} onChange={e => set('name', e.target.value)} className="input" placeholder="Jane Kamau" autoFocus />
              </Field>
              <Field label="Email Address *">
                <input type="email" value={form.email} onChange={e => set('email', e.target.value)} className="input" placeholder="jane@company.co.ke" />
              </Field>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Field label="Company / Organisation">
                <input type="text" value={form.company} onChange={e => set('company', e.target.value)} className="input" placeholder="Optional" />
              </Field>
              <Field label="Phone Number">
                <input type="tel" value={form.phone} onChange={e => set('phone', e.target.value)} className="input" placeholder="+254 7XX XXX XXX" />
              </Field>
            </div>
          </div>
        </div>
      )}

      {/* Step 2 — Message */}
      {step === 2 && (
        <div>
          <h2 className="text-xl font-extrabold text-zinc-950 mb-1">
            {form.type === 'ProjectInquiry' ? 'Tell us about your project' : 'Your message'}
          </h2>
          <p className="text-sm text-gray-500 mb-6">
            {form.type === 'Complaint'      && 'Please describe what happened and your expectations.'}
            {form.type === 'Feedback'       && 'We appreciate any details you can share.'}
            {form.type === 'ProjectInquiry' && 'Share as much detail as you can — we will get back with a tailored response.'}
            {form.type === 'General'        && 'What would you like to tell us?'}
          </p>
          <div className="space-y-4">
            <Field label="Subject *">
              <input type="text" value={form.subject} onChange={e => set('subject', e.target.value)} className="input" autoFocus placeholder={
                form.type === 'Complaint' ? 'e.g. Delay on Kilimani site delivery'
                : form.type === 'ProjectInquiry' ? 'e.g. Office fit-out in Westlands'
                : 'Brief summary of your message'
              } />
            </Field>
            <Field label="Message *">
              <textarea
                value={form.message}
                onChange={e => set('message', e.target.value)}
                rows={5}
                className="input resize-none"
                placeholder={
                  form.type === 'ProjectInquiry'
                    ? 'Describe the project scope, location, and any specific requirements…'
                    : 'Please provide as much detail as possible…'
                }
              />
            </Field>
            {form.type === 'ProjectInquiry' && (
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 pt-2 border-t border-gray-100">
                <Field label="Service of Interest">
                  <select value={form.serviceType} onChange={e => set('serviceType', e.target.value)} className="input">
                    <option value="">Select…</option>
                    {PROJECT_SERVICES.map(s => <option key={s} value={s}>{s}</option>)}
                  </select>
                </Field>
                <Field label="Approximate Budget">
                  <input type="text" value={form.budget} onChange={e => set('budget', e.target.value)} className="input" placeholder="e.g. KES 10–20M" />
                </Field>
                <Field label="Desired Timeline">
                  <input type="text" value={form.timeline} onChange={e => set('timeline', e.target.value)} className="input" placeholder="e.g. Q3 2026" />
                </Field>
              </div>
            )}

            {/* Photo upload — optional */}
            <div className="pt-2 border-t border-gray-100">
              <div className="flex items-center justify-between mb-2">
                <p className="text-sm font-semibold text-gray-700">
                  Attach Photos <span className="text-gray-400 font-normal">(optional — up to {MAX_PHOTOS}, max 1 MB each)</span>
                </p>
                {photos.length < MAX_PHOTOS && (
                  <button
                    type="button"
                    onClick={() => fileInputRef.current?.click()}
                    className="text-xs font-semibold text-amber-600 hover:text-amber-800 transition-colors"
                  >
                    + Add photo
                  </button>
                )}
              </div>
              <input
                ref={fileInputRef}
                type="file"
                accept="image/*"
                multiple
                className="hidden"
                onChange={handlePhotoSelect}
              />
              {photos.length === 0 ? (
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  className="w-full border-2 border-dashed border-gray-200 rounded-xl p-5 text-center hover:border-amber-300 hover:bg-amber-50 transition-all"
                >
                  <div className="text-2xl mb-1">📷</div>
                  <p className="text-sm text-gray-500">Click to upload images (JPG, PNG, WebP, GIF)</p>
                  <p className="text-xs text-gray-400 mt-0.5">Max {MAX_PHOTOS} photos · 1 MB each</p>
                </button>
              ) : (
                <div className="grid grid-cols-3 gap-3">
                  {photos.map((p, i) => (
                    <div key={i} className="relative group rounded-xl overflow-hidden border border-gray-200 bg-gray-50 aspect-square">
                      <img src={p.preview} alt="" className="w-full h-full object-cover" />
                      {p.error && (
                        <div className="absolute inset-0 bg-red-900/60 flex items-center justify-center p-2">
                          <p className="text-white text-xs text-center font-medium">{p.error}</p>
                        </div>
                      )}
                      <button
                        type="button"
                        onClick={() => removePhoto(i)}
                        className="absolute top-1 right-1 w-6 h-6 bg-black/60 hover:bg-black/80 rounded-full flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity"
                      >
                        <svg className="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.5">
                          <path d="M6 18L18 6M6 6l12 12"/>
                        </svg>
                      </button>
                    </div>
                  ))}
                  {photos.length < MAX_PHOTOS && (
                    <button
                      type="button"
                      onClick={() => fileInputRef.current?.click()}
                      className="aspect-square border-2 border-dashed border-gray-200 rounded-xl flex flex-col items-center justify-center hover:border-amber-300 hover:bg-amber-50 transition-all"
                    >
                      <svg className="w-5 h-5 text-gray-400 mb-1" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.5">
                        <path d="M12 4v16m8-8H4"/>
                      </svg>
                      <span className="text-xs text-gray-400">Add</span>
                    </button>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Step 3 — Review */}
      {step === 3 && (
        <div>
          <h2 className="text-xl font-extrabold text-zinc-950 mb-1">Review & submit</h2>
          <p className="text-sm text-gray-500 mb-6">Please confirm your details before sending.</p>
          <div className="bg-gray-50 rounded-xl border border-gray-200 divide-y divide-gray-200 text-sm mb-4">
            <ReviewRow label="Type"    value={TYPES.find(t => t.value === form.type)?.label ?? form.type} />
            <ReviewRow label="Name"    value={form.name} />
            <ReviewRow label="Email"   value={form.email} />
            {form.company && <ReviewRow label="Company" value={form.company} />}
            {form.phone   && <ReviewRow label="Phone"   value={form.phone} />}
            <ReviewRow label="Subject" value={form.subject} />
            <ReviewRow label="Message" value={form.message} multiline />
            {form.serviceType && <ReviewRow label="Service"  value={form.serviceType} />}
            {form.budget      && <ReviewRow label="Budget"   value={form.budget} />}
            {form.timeline    && <ReviewRow label="Timeline" value={form.timeline} />}
          </div>

          {/* Photo preview on review */}
          {photos.filter(p => !p.error).length > 0 && (
            <div className="mb-4">
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
                Attached Photos ({photos.filter(p => !p.error).length})
              </p>
              <div className="flex gap-2">
                {photos.filter(p => !p.error).map((p, i) => (
                  <div key={i} className="w-20 h-20 rounded-xl overflow-hidden border border-gray-200">
                    <img src={p.preview} alt="" className="w-full h-full object-cover" />
                  </div>
                ))}
              </div>
            </div>
          )}

          {error && (
            <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm mb-4">{error}</div>
          )}
        </div>
      )}

      {/* Navigation */}
      {step > 0 && (
        <div className="flex items-center justify-between mt-8 pt-5 border-t border-gray-100">
          <button
            onClick={() => setStep(s => s - 1)}
            className="px-5 py-2.5 border border-gray-200 text-gray-700 hover:bg-gray-50 text-sm font-semibold rounded-xl transition-colors"
          >
            ← Back
          </button>
          {step < 3 ? (
            <button
              onClick={() => setStep(s => s + 1)}
              disabled={!canAdvance()}
              className="px-6 py-2.5 bg-amber-500 hover:bg-amber-600 disabled:opacity-40 text-white text-sm font-semibold rounded-xl transition-colors"
            >
              Continue →
            </button>
          ) : (
            <button
              onClick={handleSubmit}
              disabled={submitting}
              className="px-6 py-2.5 bg-zinc-950 hover:opacity-90 disabled:opacity-50 text-white text-sm font-semibold rounded-xl transition-colors flex items-center gap-2"
            >
              {submitting && (
                <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/>
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z"/>
                </svg>
              )}
              {submitting ? 'Submitting…' : 'Submit'}
            </button>
          )}
        </div>
      )}
    </PortalShell>
  )
}

/* ── Shared layout ──────────────────────────────────────────── */
function PortalShell({ children, onBack, backLabel = '← Portal Home', brandName }) {
  const navigate = useNavigate()
  const { logoUrl } = usePortalTenant()
  const brand = brandName || 'Lante'
  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      <header className="bg-white border-b border-gray-200">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
          <button onClick={() => navigate('/portal')} className="flex items-center gap-3 group">
            <img src={logoUrl || "/qc-logo.png"} alt={brand} className="w-9 h-9 object-contain" />
            <div className="text-left">
              <p className="font-extrabold text-zinc-950 text-sm leading-none">{brand}</p>
              <p className="text-xs text-gray-400">Client Portal</p>
            </div>
          </button>
          <button onClick={onBack} className="text-sm text-gray-400 hover:text-gray-600 transition-colors">
            {backLabel}
          </button>
        </div>
      </header>
      <main className="flex-1 max-w-2xl mx-auto w-full px-4 sm:px-6 py-10">
        {children}
      </main>
      <footer className="border-t border-gray-200 bg-white py-4 px-6 text-center">
        <p className="text-xs text-gray-400">{brand} &copy; {new Date().getFullYear()}</p>
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

function ReviewRow({ label, value, multiline }) {
  return (
    <div className={`flex gap-4 px-4 py-3 ${multiline ? 'flex-col' : 'items-start'}`}>
      <span className="text-xs font-semibold text-gray-400 uppercase tracking-wider shrink-0 w-20">{label}</span>
      <span className={`text-sm text-gray-800 ${multiline ? 'whitespace-pre-wrap' : ''}`}>{value}</span>
    </div>
  )
}
