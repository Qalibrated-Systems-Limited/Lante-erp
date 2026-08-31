import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { createCustomer, checkCustomerDuplicate } from '../../services/crm.js'
import { TIERS, CUSTOMER_TYPES } from '../../hooks/crm/useCustomers.js'

export default function CreateCustomerPage() {
  const navigate = useNavigate()
  const [f, setF] = useState({
    name: '', customerType: 'Company', industry: '', segment: '', accountTier: 'Standard',
    geography: '', businessLine: '', email: '', phone: '', clientReference: '', kraPin: '', introducedBy: '',
    contactFirst: '', contactLast: '', contactTitle: '', contactEmail: '', contactPhone: '',
  })
  const set = (k) => (e) => setF(s => ({ ...s, [k]: e.target.value }))
  const [dupes, setDupes] = useState([])
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  // Live duplicate check on blur of name/email/phone.
  const runDupCheck = async () => {
    if (!f.name.trim() && !f.email.trim() && !f.phone.trim() && !f.kraPin.trim()) return setDupes([])
    try {
      const res = await checkCustomerDuplicate({ name: f.name || undefined, email: f.email || undefined,
                                                 phone: f.phone || undefined, kraPin: f.kraPin || undefined })
      setDupes(res?.matches ?? [])
    } catch { /* non-blocking */ }
  }

  const submit = async () => {
    if (!f.name.trim()) return setError('Customer name is required.')
    // Mirrors CrmFieldRules.NormaliseKraPin on the server, which re-validates regardless.
    if (f.kraPin.trim() && !/^[A-Za-z]\d{9}[A-Za-z]$/.test(f.kraPin.trim().replace(/[\s-]/g, '')))
      return setError('KRA PIN must be one letter, nine digits and one letter, e.g. P051234567M.')
    setError(''); setSaving(true)
    try {
      const contacts = f.contactFirst.trim() ? [{
        firstName: f.contactFirst, lastName: f.contactLast, jobTitle: f.contactTitle || undefined,
        email: f.contactEmail || undefined, phone: f.contactPhone || undefined, isPrimary: true,
      }] : []
      const created = await createCustomer({
        name: f.name, customerType: f.customerType, industry: f.industry || undefined,
        segment: f.segment || undefined, accountTier: f.accountTier, geography: f.geography || undefined,
        businessLine: f.businessLine || undefined, email: f.email || undefined, phone: f.phone || undefined,
        clientReference: f.clientReference || undefined, kraPin: f.kraPin || undefined,
        introducedBy: f.introducedBy || undefined, contacts,
      })
      navigate(`/modules/crm/customers/${created.id}`)
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to create customer.')
      setSaving(false)
    }
  }

  return (
    <>
      <main className="flex-1 max-w-4xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        <button onClick={() => navigate('/modules/crm')} className="text-sm text-gray-500 hover:text-navy mb-4">← Back to Commercial</button>
        <h1 className="text-2xl font-extrabold text-navy">Onboard New Client</h1>
        <p className="text-sm text-gray-500 mt-0.5 mb-6">Submitted for the 4-stage approval chain: Line Manager → Head of BD → CFO → MD.</p>

        {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-3 text-sm mb-5">{error}</div>}

        {dupes.length > 0 && (
          <div className="bg-amber-50 border border-amber-200 rounded-xl px-5 py-3 text-sm mb-5">
            <p className="font-semibold text-amber-700">Possible duplicate{dupes.length > 1 ? 's' : ''} — check before submitting:</p>
            <ul className="mt-1 space-y-0.5">
              {dupes.map(d => (
                <li key={d.id}>
                  <button onClick={() => navigate(`/modules/crm/customers/${d.id}`)} className="text-amber-800 underline">{d.name}</button>
                  <span className="text-amber-600"> · {d.status} · {d.email ?? '—'}</span>
                </li>
              ))}
            </ul>
          </div>
        )}

        <section className="bg-white border border-gray-200 rounded-xl p-5 mb-5">
          <h2 className="text-sm font-bold text-gray-700 mb-3">Client Details</h2>
          <div className="grid sm:grid-cols-2 gap-4">
            <Field label="Client name *"><input value={f.name} onChange={set('name')} onBlur={runDupCheck} className="input" placeholder="Company / client name" /></Field>
            <Field label="Type">
              <select value={f.customerType} onChange={set('customerType')} className="input">
                {CUSTOMER_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
              </select>
            </Field>
            <Field label="Industry"><input value={f.industry} onChange={set('industry')} className="input" /></Field>
            <Field label="Segment"><input value={f.segment} onChange={set('segment')} className="input" /></Field>
            <Field label="Account tier">
              <select value={f.accountTier} onChange={set('accountTier')} className="input">
                {TIERS.map(t => <option key={t} value={t}>{t}</option>)}
              </select>
            </Field>
            <Field label="Business line"><input value={f.businessLine} onChange={set('businessLine')} className="input" placeholder="e.g. Calibration" /></Field>
            <Field label="Email"><input type="email" value={f.email} onChange={set('email')} onBlur={runDupCheck} className="input" /></Field>
            <Field label="Phone"><input value={f.phone} onChange={set('phone')} onBlur={runDupCheck} className="input" /></Field>
            <Field label="Geography"><input value={f.geography} onChange={set('geography')} className="input" placeholder="e.g. Nairobi" /></Field>
            <Field label="Client reference"><input value={f.clientReference} onChange={set('clientReference')} className="input" /></Field>
            <Field label="KRA PIN" note="Required on tax invoices. One letter, nine digits, one letter.">
              <input value={f.kraPin} onChange={set('kraPin')} onBlur={runDupCheck} className="input"
                     placeholder="e.g. P051234567M" style={{ textTransform: 'uppercase' }} />
            </Field>
            <Field label="Introduced by" note="Locks after MD approval (referral attribution)."><input value={f.introducedBy} onChange={set('introducedBy')} className="input" /></Field>
          </div>
        </section>

        <section className="bg-white border border-gray-200 rounded-xl p-5 mb-5">
          <h2 className="text-sm font-bold text-gray-700 mb-3">Primary Contact <span className="text-gray-400 font-normal">(optional)</span></h2>
          <div className="grid sm:grid-cols-2 gap-4">
            <Field label="First name"><input value={f.contactFirst} onChange={set('contactFirst')} className="input" /></Field>
            <Field label="Last name"><input value={f.contactLast} onChange={set('contactLast')} className="input" /></Field>
            <Field label="Job title"><input value={f.contactTitle} onChange={set('contactTitle')} className="input" /></Field>
            <Field label="Email"><input type="email" value={f.contactEmail} onChange={set('contactEmail')} className="input" /></Field>
            <Field label="Phone"><input value={f.contactPhone} onChange={set('contactPhone')} className="input" /></Field>
          </div>
        </section>

        <div className="flex justify-end gap-3">
          <button onClick={() => navigate('/modules/crm')} className="px-5 py-2.5 text-sm font-semibold border border-gray-200 rounded-lg hover:bg-gray-50">Cancel</button>
          <button onClick={submit} disabled={saving} className="px-5 py-2.5 text-sm font-bold bg-navy hover:bg-navy-dark text-white rounded-lg disabled:opacity-50">
            {saving ? 'Submitting…' : 'Submit for Approval'}
          </button>
        </div>
      </main>
    </>
  )
}

function Field({ label, note, children }) {
  return (
    <label className="block">
      <span className="block text-xs font-medium text-gray-500 mb-1">{label}</span>
      {children}
      {note && <span className="block text-[11px] text-gray-400 mt-1">{note}</span>}
    </label>
  )
}
