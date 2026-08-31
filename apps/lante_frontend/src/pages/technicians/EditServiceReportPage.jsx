import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext.jsx'
import api from '../../api/axios.js'
import ImagePicker from '../../components/ImagePicker.jsx'

const NATURE_OPTIONS = ['Planned Maintenance', 'Service', 'Repairs', 'Normal Customer Visit']
const NATURE_ENUM = { 'Planned Maintenance': 2, 'Service': 8, 'Repairs': 1, 'Normal Customer Visit': 7 }
const NATURE_FROM_ENUM = { 2: 'Planned Maintenance', 8: 'Service', 1: 'Repairs', 7: 'Normal Customer Visit' }

const EMPTY_MILEAGE = () => ({ lpoNo: '', timeIn: '', timeOut: '', timeSpent: '', kmOut: '', kmIn: '', kmCovered: '' })

const cls = {
  input: 'w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400',
  textarea: 'w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 resize-none',
}

function Block({ title, children, yellow }) {
  return (
    <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
      <div className={`px-5 py-2.5 border-b border-gray-100 ${yellow ? 'bg-amber-500' : 'bg-gray-50'}`}>
        <h3 className={`text-sm font-bold uppercase tracking-wide ${yellow ? 'text-black' : 'text-gray-600'}`}>{title}</h3>
      </div>
      <div className="p-5 space-y-4">{children}</div>
    </div>
  )
}

function Row({ children }) {
  return <div className="grid grid-cols-2 gap-4">{children}</div>
}

function F({ label, required, children }) {
  return (
    <div>
      <label className="block text-xs font-semibold text-gray-500 mb-1.5">
        {label}{required && <span className="text-red-400 ml-0.5">*</span>}
      </label>
      {children}
    </div>
  )
}

export default function EditServiceReportPage() {
  const { id, reportId } = useParams()
  const navigate = useNavigate()
  const { user } = useAuth()

  const [submitting, setSubmitting] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [pendingFiles, setPendingFiles] = useState([])
  const [assignmentTitle, setAssignmentTitle] = useState('')

  const [form, setForm] = useState({
    date:             new Date().toISOString().slice(0, 10),
    vehicleNo:        '',
    technicianName:   '',
    technicianPhone:  '',
    customerName:     '',
    customerAddress:  '',
    customerEmail:    '',
    customerPhone:    '',
    contactPerson:    '',
    natureOfVisit:    'Repairs',
    machineDetails:   '',
    faultReported:    '',
    findings:         '',
    correction:       '',
    finalResult:      '',
    partsToOrder:     '',
    customerComments: '',
    // O11.6 — 8-section FSR structured fields
    recommendations:  '',
    followUpRequired: false,
    followUpNotes:    '',
    clientRating:     '',
    equipment:        [],
    mileageRows: [EMPTY_MILEAGE(), EMPTY_MILEAGE(), EMPTY_MILEAGE()],
  })

  useEffect(() => {
    Promise.all([
      api.get(`/api/v1/service-reports/${reportId}`),
      api.get(`/api/v1/assignments/${id}`).catch(() => ({ data: { data: null } })),
    ]).then(([srRes, aRes]) => {
      const sr = srRes.data?.data
      if (!sr) { setError('Service report not found.'); setLoading(false); return }

      setAssignmentTitle(aRes.data?.data?.title ?? '')

      // Parse detailsJson — guard against double-encoded legacy records
      let details = {}
      if (sr.detailsJson) {
        try {
          const parsed = JSON.parse(sr.detailsJson)
          details = typeof parsed === 'string' ? JSON.parse(parsed) : parsed
        } catch { /* ignore */ }
      }

      // Resolve nature of visit — stored as enum int or display string
      let nature = 'Repairs'
      if (details.displayNature && NATURE_OPTIONS.includes(details.displayNature)) {
        nature = details.displayNature
      } else if (typeof sr.natureOfVisit === 'number' && NATURE_FROM_ENUM[sr.natureOfVisit]) {
        nature = NATURE_FROM_ENUM[sr.natureOfVisit]
      } else if (typeof sr.natureOfVisit === 'string' && NATURE_OPTIONS.includes(sr.natureOfVisit)) {
        nature = sr.natureOfVisit
      }

      setForm({
        date:             details.date ?? new Date().toISOString().slice(0, 10),
        vehicleNo:        details.vehicleNo ?? '',
        technicianName:   sr.technicianName ?? (user ? `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() : ''),
        technicianPhone:  details.technicianPhone ?? '',
        customerName:     sr.customerName ?? '',
        customerAddress:  details.customerAddress ?? sr.locationAddress ?? '',
        customerEmail:    details.customerEmail ?? sr.contactEmail ?? '',
        customerPhone:    details.customerPhone ?? sr.contactPhone ?? '',
        contactPerson:    sr.contactPerson ?? '',
        natureOfVisit:    nature,
        machineDetails:   details.machineDetails ?? '',
        faultReported:    details.faultReported ?? '',
        findings:         details.findings ?? '',
        correction:       details.correction ?? '',
        finalResult:      details.finalResult ?? '',
        partsToOrder:     details.partsToOrder ?? '',
        customerComments: sr.customerComments ?? '',
        // O11.6 — load the structured 8-section fields (fall back to the derived work text)
        recommendations:  sr.recommendations ?? '',
        followUpRequired: sr.followUpRequired ?? false,
        followUpNotes:    sr.followUpNotes ?? '',
        clientRating:     sr.clientRating ?? '',
        equipment:        (sr.equipment ?? []).map(e => ({
          serialNumber: e.serialNumber ?? '', manufacturer: e.manufacturer ?? '', model: e.model ?? '',
          description: e.description ?? '', conditionBefore: e.conditionBefore ?? '', conditionAfter: e.conditionAfter ?? '', workDone: e.workDone ?? '',
        })),
        mileageRows:      details.mileageRows?.length > 0
                            ? details.mileageRows
                            : [EMPTY_MILEAGE(), EMPTY_MILEAGE(), EMPTY_MILEAGE()],
      })
    }).catch(() => setError('Could not load service report.'))
      .finally(() => setLoading(false))
  }, [id, reportId, user])

  const set = (field, val) => setForm(f => ({ ...f, [field]: val }))
  const setMil = (i, field, val) => setForm(f => {
    const rows = [...f.mileageRows]
    rows[i] = { ...rows[i], [field]: val }
    return { ...f, mileageRows: rows }
  })
  // O11.6 — equipment-serviced rows
  const addEquip = () => setForm(f => ({ ...f, equipment: [...f.equipment, { serialNumber: '', manufacturer: '', model: '', description: '', conditionBefore: '', conditionAfter: '', workDone: '' }] }))
  const setEquip = (i, field, val) => setForm(f => { const rows = [...f.equipment]; rows[i] = { ...rows[i], [field]: val }; return { ...f, equipment: rows } })
  const rmEquip = (i) => setForm(f => ({ ...f, equipment: f.equipment.filter((_, j) => j !== i) }))

  async function handleSubmit(e) {
    e.preventDefault()
    if (!form.faultReported.trim()) { setError('Fault Reported is required.'); return }
    setError('')
    setSubmitting(true)
    try {
      const details = JSON.stringify({
        date:            form.date,
        vehicleNo:       form.vehicleNo,
        machineDetails:  form.machineDetails,
        faultReported:   form.faultReported,
        findings:        form.findings,
        correction:      form.correction,
        finalResult:     form.finalResult,
        partsToOrder:    form.partsToOrder,
        technicianPhone: form.technicianPhone,
        customerAddress: form.customerAddress,
        customerEmail:   form.customerEmail,
        displayNature:   form.natureOfVisit,
        mileageRows:     form.mileageRows,
      })
      await api.put(`/api/v1/service-reports/${reportId}`, {
        customerName:     form.customerName,
        locationName:     form.customerName,
        locationAddress:  form.customerAddress,
        contactPerson:    form.contactPerson,
        contactPhone:     form.customerPhone,
        contactEmail:     form.customerEmail,
        natureOfVisit:    NATURE_ENUM[form.natureOfVisit] ?? 8,
        customerComments: form.customerComments,
        details,
        // O11.6 — 8-section FSR structured columns
        workSummary:      [form.findings, form.correction, form.finalResult].filter(Boolean).join('\n\n') || null,
        materialsUsed:    form.partsToOrder || null,
        recommendations:  form.recommendations || null,
        followUpRequired: form.followUpRequired,
        followUpNotes:    form.followUpNotes || null,
        clientRating:     form.clientRating ? Number(form.clientRating) : null,
        equipment:        form.equipment
          .filter(e => (e.serialNumber || e.description || e.manufacturer))
          .map(e => ({
            serialNumber: e.serialNumber || null, manufacturer: e.manufacturer || null, model: e.model || null,
            description: e.description || null, conditionBefore: e.conditionBefore || null,
            conditionAfter: e.conditionAfter || null, workDone: e.workDone || null,
          })),
      })
      if (pendingFiles.length > 0) {
        for (const file of pendingFiles) {
          const fd = new FormData()
          fd.append('file', file); fd.append('entityType', 'ServiceReport'); fd.append('entityId', reportId)
          fd.append('assignmentId', id)
          await api.post('/api/v1/attachments', fd).catch(() => {})
        }
      }
      navigate(`/modules/operations/assignments/${id}`)
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to save report.')
    } finally {
      setSubmitting(false)
    }
  }

  if (loading) return <><div className="p-10 text-sm text-gray-400">Loading…</div></>

  return (
    <>
      <main className="max-w-3xl mx-auto w-full px-4 sm:px-6 py-6">

        <div className="flex items-center justify-between gap-3 mb-6">
          <div className="flex items-center gap-3">
            <button onClick={() => navigate(`/modules/operations/assignments/${id}`)}
              className="p-2 hover:bg-gray-100 rounded-xl text-gray-400 transition-colors">
              <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
              </svg>
            </button>
            <div>
              <h1 className="text-2xl font-extrabold text-zinc-950">Edit Technical Report</h1>
              {assignmentTitle && <p className="text-sm text-gray-400 mt-0.5">{assignmentTitle}</p>}
            </div>
          </div>
        </div>

        {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-2xl px-5 py-3 text-sm mb-4">{error}</div>}

        <form onSubmit={handleSubmit} className="space-y-4">

          <Block title="Technical Report" yellow>
            <Row>
              <F label="Date"><input type="date" value={form.date} onChange={e => set('date', e.target.value)} className={cls.input} /></F>
              <F label="Vehicle No."><input type="text" value={form.vehicleNo} onChange={e => set('vehicleNo', e.target.value)} placeholder="e.g. KCB 123A" className={cls.input} /></F>
            </Row>
            <Row>
              <F label="Customer *"><input type="text" value={form.customerName} onChange={e => set('customerName', e.target.value)} placeholder="Client / Company name" required className={cls.input} /></F>
              <F label="Address"><input type="text" value={form.customerAddress} onChange={e => set('customerAddress', e.target.value)} placeholder="Customer address" className={cls.input} /></F>
            </Row>
            <Row>
              <F label="Email"><input type="email" value={form.customerEmail} onChange={e => set('customerEmail', e.target.value)} placeholder="customer@email.com" className={cls.input} /></F>
              <F label="Tel. No."><input type="text" value={form.customerPhone} onChange={e => set('customerPhone', e.target.value)} placeholder="Phone number" className={cls.input} /></F>
            </Row>
            <F label="Contact Person">
              <input type="text" value={form.contactPerson} onChange={e => set('contactPerson', e.target.value)} placeholder="Name of on-site contact" className={cls.input} />
            </F>
          </Block>

          <Block title="Nature of Visit">
            <div className="flex flex-wrap gap-x-8 gap-y-3">
              {NATURE_OPTIONS.map(opt => (
                <label key={opt} className="flex items-center gap-2.5 cursor-pointer" onClick={() => set('natureOfVisit', opt)}>
                  <div className={`w-5 h-5 border-2 rounded flex items-center justify-center flex-shrink-0 transition-colors ${form.natureOfVisit === opt ? 'bg-amber-500 border-amber-500' : 'border-gray-300'}`}>
                    {form.natureOfVisit === opt && (
                      <svg className="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={3} d="M5 13l4 4L19 7" />
                      </svg>
                    )}
                  </div>
                  <span className="text-sm font-medium text-gray-700">{opt}</span>
                </label>
              ))}
            </div>
          </Block>

          <Block title="Machine Details">
            <textarea rows={3} value={form.machineDetails} onChange={e => set('machineDetails', e.target.value)}
              placeholder="Make, model, serial number, and other machine details…" className={cls.textarea} />
          </Block>

          <Block title="Job Details">
            <F label="Fault Reported *">
              <textarea rows={3} value={form.faultReported} onChange={e => set('faultReported', e.target.value)}
                placeholder="Describe the fault as reported by the customer" className={cls.textarea} required />
            </F>
            <F label="Findings">
              <textarea rows={3} value={form.findings} onChange={e => set('findings', e.target.value)}
                placeholder="What did you find on inspection?" className={cls.textarea} />
            </F>
            <F label="Correction / Action Taken">
              <textarea rows={3} value={form.correction} onChange={e => set('correction', e.target.value)}
                placeholder="What was done to resolve the issue?" className={cls.textarea} />
            </F>
            <F label="Final Result">
              <input type="text" value={form.finalResult} onChange={e => set('finalResult', e.target.value)}
                placeholder="e.g. Machine fully operational" className={cls.input} />
            </F>
            <F label="Parts to Order">
              <textarea rows={2} value={form.partsToOrder} onChange={e => set('partsToOrder', e.target.value)}
                placeholder="List parts used or recommended for ordering" className={cls.textarea} />
            </F>
            <F label="Customer Comments">
              <textarea rows={2} value={form.customerComments} onChange={e => set('customerComments', e.target.value)}
                placeholder="Any comments from the customer" className={cls.textarea} />
            </F>
          </Block>

          {/* ── O11.6 — Equipment, Recommendations & Rating (8-section FSR) ── */}
          <Block title="Equipment Serviced">
            <div className="overflow-x-auto -mx-1">
              <table className="w-full text-xs border-collapse min-w-[720px]">
                <thead>
                  <tr className="bg-gray-50">
                    {['Description', 'Serial No.', 'Manufacturer', 'Model', 'Condition (before)', 'Condition (after)', 'Work done', ''].map(h => (
                      <th key={h} className="border border-gray-200 px-2 py-2 text-left font-bold text-gray-600 whitespace-nowrap">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {form.equipment.length === 0 ? (
                    <tr><td colSpan={8} className="border border-gray-200 px-2 py-3 text-gray-400 text-center">No equipment added.</td></tr>
                  ) : form.equipment.map((e, i) => (
                    <tr key={i} className="hover:bg-amber-50">
                      {['description', 'serialNumber', 'manufacturer', 'model', 'conditionBefore', 'conditionAfter', 'workDone'].map(field => (
                        <td key={field} className="border border-gray-200 p-0">
                          <input value={e[field]} onChange={ev => setEquip(i, field, ev.target.value)} className="w-full px-2 py-1.5 text-xs outline-none bg-transparent" />
                        </td>
                      ))}
                      <td className="border border-gray-200 p-0 text-center">
                        <button type="button" onClick={() => rmEquip(i)} className="text-red-500 px-2">×</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <button type="button" onClick={addEquip} className="mt-2 text-xs font-semibold text-amber-600 hover:text-amber-800">+ Add equipment</button>
          </Block>

          <Block title="Recommendations, Follow-up & Rating">
            <F label="Recommendations">
              <textarea rows={2} value={form.recommendations} onChange={e => set('recommendations', e.target.value)}
                placeholder="Recommendations for the customer" className={cls.textarea} />
            </F>
            <F label="Follow-up">
              <label className="flex items-center gap-2 text-sm text-gray-600">
                <input type="checkbox" checked={form.followUpRequired} onChange={e => set('followUpRequired', e.target.checked)} />
                Follow-up visit required
              </label>
            </F>
            {form.followUpRequired && (
              <F label="Follow-up notes">
                <textarea rows={2} value={form.followUpNotes} onChange={e => set('followUpNotes', e.target.value)}
                  placeholder="What follow-up is needed?" className={cls.textarea} />
              </F>
            )}
            <F label="Client satisfaction rating">
              <select value={form.clientRating} onChange={e => set('clientRating', e.target.value)} className={cls.input}>
                <option value="">Not rated</option>
                {[1, 2, 3, 4, 5].map(n => <option key={n} value={n}>{'★'.repeat(n)} ({n}/5)</option>)}
              </select>
            </F>
          </Block>

          <Block title="Field Job Time — Mileage Details">
            <div className="overflow-x-auto -mx-1">
              <table className="w-full text-xs border-collapse min-w-[600px]">
                <thead>
                  <tr className="bg-gray-50">
                    {['MV/EV/DN/LPO NO.', 'TIME-IN', 'TIME-OUT', 'TIME-SPENT', 'KM-OUT', 'KM-IN', 'KM-COVERED'].map(h => (
                      <th key={h} className="border border-gray-200 px-2 py-2 text-left font-bold text-gray-600 whitespace-nowrap">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {form.mileageRows.map((row, i) => (
                    <tr key={i} className="hover:bg-amber-50">
                      {(['lpoNo', 'timeIn', 'timeOut', 'timeSpent', 'kmOut', 'kmIn', 'kmCovered']).map(field => (
                        <td key={field} className="border border-gray-200 p-0">
                          <input type="text" value={row[field]} onChange={e => setMil(i, field, e.target.value)}
                            className="w-full px-2 py-2 text-xs outline-none bg-transparent" />
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Block>

          <Block title="Technician Details">
            <Row>
              <F label="Name of Technician">
                <input type="text" value={form.technicianName} onChange={e => set('technicianName', e.target.value)}
                  placeholder="Full name" className={cls.input} />
              </F>
              <F label="Tel. No.">
                <input type="text" value={form.technicianPhone} onChange={e => set('technicianPhone', e.target.value)}
                  placeholder="Phone number" className={cls.input} />
              </F>
            </Row>
          </Block>

          <Block title="Images / Attachments">
            <ImagePicker files={pendingFiles} onChange={setPendingFiles} />
          </Block>

          <div className="flex gap-3 pt-2 pb-6">
            <button type="button" onClick={() => navigate(`/modules/operations/assignments/${id}`)}
              className="flex-1 py-3.5 border-2 border-zinc-200 text-zinc-700 text-base font-semibold rounded-2xl hover:bg-zinc-50 transition-colors">
              Cancel
            </button>
            <button type="submit" disabled={submitting}
              className="flex-1 py-3.5 bg-zinc-950 text-white text-base font-semibold rounded-2xl transition-colors disabled:opacity-50 shadow-lg">
              {submitting ? 'Saving…' : 'Save Changes'}
            </button>
          </div>
        </form>
      </main>
    </>
  )
}
