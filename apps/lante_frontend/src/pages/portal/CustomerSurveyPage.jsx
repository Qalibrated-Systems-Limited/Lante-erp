import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import usePortalTenant from '../../hooks/usePortalTenant'

const FEEDBACK_TYPES = [
  { value: 'Compliment',      label: 'Compliment' },
  { value: 'Complaint',       label: 'Complaint' },
  { value: 'GeneralFeedback', label: 'General Feedback' },
]

const RATINGS = [
  { value: 'Outstanding', label: 'Outstanding' },
  { value: 'Good',        label: 'Good' },
  { value: 'Average',     label: 'Average' },
  { value: 'Poor',        label: 'Poor' },
  { value: 'VeryPoor',    label: 'Very Poor' },
]

export default function CustomerSurveyPage() {
  const navigate = useNavigate()
  const { slug, name: brandName, logoUrl } = usePortalTenant()

  const [form, setForm] = useState({
    respondentName:  '',
    corporationName: '',
    email:           '',
    counsellorName:  '',
    feedbackType:    'Compliment',
    details:         '',
    rating:          'Outstanding',
  })

  const [submitting, setSubmitting] = useState(false)
  const [error, setError]           = useState('')
  const [done, setDone]             = useState(false)

  function set(field, value) {
    setForm(f => ({ ...f, [field]: value }))
  }

  function isValid() {
    return form.respondentName.trim()
      && form.corporationName.trim()
      && form.email.trim() && /\S+@\S+\.\S+/.test(form.email)
      && form.counsellorName.trim()
      && form.details.trim()
  }

  async function handleSubmit(e) {
    e.preventDefault()
    if (!isValid()) {
      setError('Please fill in all required fields with a valid email address.')
      return
    }
    setError('')
    setSubmitting(true)
    try {
      await api.post('/api/v1/portal/customer-survey', {
        respondentName:  form.respondentName.trim(),
        corporationName: form.corporationName.trim(),
        email:           form.email.trim(),
        counsellorName:  form.counsellorName.trim(),
        feedbackType:    form.feedbackType,
        details:         form.details.trim(),
        rating:          form.rating,
      }, { params: slug ? { slug } : undefined })
      setDone(true)
    } catch (err) {
      setError(err.response?.data?.message ?? 'We could not submit your feedback. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  if (done) {
    return (
      <SurveyShell onBack={() => navigate('/portal')} brandName={brandName} logoUrl={logoUrl}>
        <div className="text-center py-6">
          <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-5">
            <svg className="w-8 h-8 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h2 className="text-2xl font-extrabold text-zinc-950 mb-2">Thank you for your feedback!</h2>
          <p className="text-gray-500 mb-6 max-w-md mx-auto">
            We appreciate you taking the time to share your experience with us. Your feedback helps us
            continue to improve the quality of our service.
          </p>
          <button
            onClick={() => navigate('/portal')}
            className="px-5 py-2.5 bg-zinc-950 text-white text-sm font-semibold rounded-xl transition-colors hover:opacity-90"
          >
            Back to Portal
          </button>
        </div>
      </SurveyShell>
    )
  }

  return (
    <SurveyShell onBack={() => navigate('/portal')} brandName={brandName} logoUrl={logoUrl}>
      <div className="text-center mb-8">
        <div className="w-14 h-14 bg-amber-50 rounded-2xl flex items-center justify-center mx-auto mb-4">
          <svg className="w-7 h-7 text-amber-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.657 18.657A8 8 0 016.343 7.343S7 9 9 10c0-2 .5-5 2.986-7C14 5 16.09 5.777 17.656 7.343A7.975 7.975 0 0120 13a7.975 7.975 0 01-2.343 5.657z" />
          </svg>
        </div>
        <h2 className="text-2xl font-extrabold text-zinc-950 mb-2">Customer Satisfaction Survey</h2>
        <p className="text-sm text-gray-500 max-w-md mx-auto">
          We value your feedback. Let us know about your experience with our team — it takes less than a minute.
        </p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-7">
        <FormSection label="Your Details">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Field label="Your Name *">
              <input type="text" value={form.respondentName} onChange={e => set('respondentName', e.target.value)} className="input" placeholder="Jane Kamau" autoFocus />
            </Field>
            <Field label="Company / Corporation *">
              <input type="text" value={form.corporationName} onChange={e => set('corporationName', e.target.value)} className="input" placeholder="Your organisation" />
            </Field>
            <Field label="Email Address *">
              <input type="email" value={form.email} onChange={e => set('email', e.target.value)} className="input" placeholder="jane@company.co.ke" />
            </Field>
            <Field label="Staff Member / Counsellor Who Served You *">
              <input type="text" value={form.counsellorName} onChange={e => set('counsellorName', e.target.value)} className="input" placeholder="Name of the staff member" />
            </Field>
          </div>
        </FormSection>

        <FormSection label="Your Feedback">
          <Field label="I would like to log a *">
            <div className="grid grid-cols-3 gap-2">
              {FEEDBACK_TYPES.map(t => (
                <button
                  type="button"
                  key={t.value}
                  onClick={() => set('feedbackType', t.value)}
                  className={`px-3 py-2.5 rounded-xl border-2 text-sm font-semibold transition-all ${
                    form.feedbackType === t.value ? 'border-amber-500 bg-amber-50 text-amber-700' : 'border-gray-200 hover:border-gray-300 bg-white text-gray-600'
                  }`}
                >
                  {t.label}
                </button>
              ))}
            </div>
          </Field>

          <Field label="Please provide details *">
            <textarea
              value={form.details}
              onChange={e => set('details', e.target.value)}
              rows={5}
              className="input resize-none"
              placeholder="Tell us more about your compliment, complaint, or feedback…"
            />
          </Field>

          <Field label="How would you rate the quality of service you received? *">
            <div className="grid grid-cols-1 sm:grid-cols-5 gap-2">
              {RATINGS.map(r => (
                <button
                  type="button"
                  key={r.value}
                  onClick={() => set('rating', r.value)}
                  className={`px-3 py-2.5 rounded-xl border-2 text-sm font-semibold transition-all ${
                    form.rating === r.value ? 'border-amber-500 bg-amber-50 text-amber-700' : 'border-gray-200 hover:border-gray-300 bg-white text-gray-600'
                  }`}
                >
                  {r.label}
                </button>
              ))}
            </div>
          </Field>
        </FormSection>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm">{error}</div>
        )}

        <div className="flex justify-end pt-6 border-t border-gray-100">
          <button
            type="submit"
            disabled={submitting}
            className="px-6 py-2.5 bg-zinc-950 hover:opacity-90 disabled:opacity-50 text-white text-sm font-semibold rounded-xl transition-colors flex items-center gap-2"
          >
            {submitting && (
              <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/>
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z"/>
              </svg>
            )}
            {submitting ? 'Submitting…' : 'Submit Feedback'}
          </button>
        </div>
      </form>
    </SurveyShell>
  )
}

/* ── Shared layout ──────────────────────────────────────────── */
function SurveyShell({ children, onBack, brandName, logoUrl }) {
  const navigate = useNavigate()
  const brand = brandName || 'Lante'
  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      <header className="bg-white border-b border-gray-200">
        <div className="max-w-3xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
          <button onClick={() => navigate('/portal')} className="flex items-center gap-3 group">
            <img src={logoUrl || "/qc-logo.png"} alt={brand} className="w-9 h-9 object-contain" />
            <div className="text-left">
              <p className="font-extrabold text-zinc-950 text-sm leading-none">{brand}</p>
              <p className="text-xs text-gray-400">Client Portal</p>
            </div>
          </button>
          <button onClick={onBack} className="text-sm text-gray-400 hover:text-gray-600 transition-colors">
            ← Portal Home
          </button>
        </div>
      </header>
      <main className="flex-1 w-full px-4 sm:px-6 py-10 flex justify-center">
        <div className="w-full max-w-2xl bg-white rounded-2xl border border-gray-200 shadow-sm px-6 py-8 sm:px-10 sm:py-10">
          {children}
        </div>
      </main>
      <footer className="border-t border-gray-200 bg-white py-4 px-6 text-center">
        <p className="text-xs text-gray-400">{brand} &copy; {new Date().getFullYear()}</p>
      </footer>
    </div>
  )
}

function FormSection({ label, children }) {
  return (
    <div>
      <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-3">{label}</p>
      <div className="space-y-4">{children}</div>
    </div>
  )
}

function Field({ label, children }) {
  // flex-col + h-full + mt-auto on the input keeps inputs aligned to the row's bottom edge
  // even when a sibling field's label wraps to two lines (grid rows stretch items by default).
  return (
    <div className="flex flex-col h-full">
      <label className="block text-sm font-semibold text-gray-700 mb-1.5">{label}</label>
      <div className="mt-auto">{children}</div>
    </div>
  )
}
