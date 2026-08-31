import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext.jsx'
import api from '../../api/axios.js'
import { jsPDF } from 'jspdf'
import autoTable from 'jspdf-autotable'
import ImagePicker from '../../components/ImagePicker.jsx'
import useCompanyBranding from '../../hooks/useCompanyBranding.js'

// PDF form nature options → backend NatureOfVisit enum index
const NATURE_OPTIONS = ['Planned Maintenance', 'Service', 'Repairs', 'Normal Customer Visit']
const NATURE_ENUM = { 'Planned Maintenance': 2, 'Service': 8, 'Repairs': 1, 'Normal Customer Visit': 7 }

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

export default function CreateServiceReportPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { user } = useAuth()
  const branding = useCompanyBranding()

  const [assignment, setAssignment] = useState(null)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [pendingFiles, setPendingFiles] = useState([])

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
    // O11.6 — 8-section FSR structured fields (operations FIELD_SERVICE_REPORT)
    recommendations:  '',
    followUpRequired: false,
    followUpNotes:    '',
    clientRating:     '',
    equipment:        [],
    mileageRows: [EMPTY_MILEAGE(), EMPTY_MILEAGE(), EMPTY_MILEAGE()],
  })

  useEffect(() => {
    api.get(`/api/v1/assignments/${id}`)
      .then(res => {
        const a = res.data?.data
        setAssignment(a)
        setForm(f => ({
          ...f,
          customerName:    a?.locationName ?? '',
          customerAddress: a?.locationAddress ?? '',
          technicianName:  user ? `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() : '',
        }))
      })
      .catch(() => setError('Could not load assignment.'))
  }, [id, user])

  const set = (field, val) => setForm(f => ({ ...f, [field]: val }))
  const setMil = (i, field, val) => setForm(f => {
    const rows = [...f.mileageRows]
    rows[i] = { ...rows[i], [field]: val }
    return { ...f, mileageRows: rows }
  })
  // O11.6 — equipment-serviced rows (FSR_EQUIPMENT)
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
        machineDetails:   form.machineDetails,
        faultReported:    form.faultReported,
        findings:         form.findings,
        correction:       form.correction,
        finalResult:      form.finalResult,
        partsToOrder:     form.partsToOrder,
        technicianPhone:  form.technicianPhone,
        vehicleNo:        form.vehicleNo,
        customerAddress:  form.customerAddress,
        customerEmail:    form.customerEmail,
        displayNature:    form.natureOfVisit,
        mileageRows:      form.mileageRows,
      })
      const res = await api.post('/api/v1/service-reports', {
        assignmentId:     id,
        customerName:     form.customerName,
        locationName:     form.customerName,
        locationAddress:  form.customerAddress,
        contactPerson:    form.contactPerson,
        contactPhone:     form.customerPhone,
        contactEmail:     form.customerEmail,
        natureOfVisit:    NATURE_ENUM[form.natureOfVisit] ?? 8,
        customerComments: form.customerComments,
        details,
        // O11.6 — 8-section FSR structured columns (§4 work / §5 materials / §6 recommendations /
        // §8 rating + §3 equipment). Work & materials are derived from the job-details inputs above.
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
      const reportId = res.data?.data?.id
      if (reportId && pendingFiles.length > 0) {
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

  function exportPDF() {
    const doc = new jsPDF({ unit: 'mm', format: 'a4' })
    const W = 210, mg = 14
    let y = 12

    // ── Company info (top right) ──
    doc.setFontSize(7.5)
    doc.setTextColor(60, 60, 60)
    ;[
      'Birdi Singh Complex 1st Floor, off Mombasa Road',
      'P.O BOX 34463-00100 Nairobi',
      'Tel: +254 714 999 996, +254 756 999 996',
      '+254 750 999 996',
      `Email. ${branding.email}`,
      'www.qalibrated.co.ke',
    ].forEach((l, i) => doc.text(l, W - mg, y + i * 3.8, { align: 'right' }))

    // ── Logo text (top left) ── tenant's own display name, split on the first space so a
    // two-word default like "QALIBRATED SYSTEMS" still lays out as the original two-line mark.
    const [brandFirst, ...brandRest] = (branding.displayName || '').split(' ')
    doc.setFontSize(15); doc.setFont('helvetica', 'bold')
    doc.setTextColor(240, 160, 10); doc.text(brandFirst || '', mg, y + 5)
    doc.setFontSize(8); doc.setFont('helvetica', 'normal')
    doc.setTextColor(120, 120, 120); doc.text(brandRest.join(' '), mg, y + 10)

    y = 38
    // ── TECHNICAL REPORT banner ──
    doc.setFillColor(240, 160, 10)
    doc.rect(mg, y, 105, 9, 'F')
    doc.setFontSize(13); doc.setFont('helvetica', 'bold'); doc.setTextColor(0)
    doc.text('TECHNICAL REPORT', mg + 3, y + 6.3)

    // Date + Vehicle (right of banner)
    doc.setFontSize(8.5); doc.setFont('helvetica', 'normal')
    doc.text('Date:', W - mg - 65, y + 4)
    doc.text(form.date, W - mg - 50, y + 4)
    doc.line(W - mg - 50, y + 4.5, W - mg, y + 4.5)
    doc.text('Vehicle No.:', W - mg - 65, y + 8.5)
    doc.text(form.vehicleNo, W - mg - 42, y + 8.5)
    doc.line(W - mg - 42, y + 9, W - mg, y + 9)

    // Serial No
    y += 12
    doc.setFontSize(11); doc.setFont('helvetica', 'bold')
    doc.text(`Serial No: ${form.date.replace(/-/g, '')}`, mg, y)

    y += 8

    // ── Helper functions ──
    function twoCol(l1, v1, l2, v2) {
      const mid = W / 2 - 5
      doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
      doc.text(`${l1}:`, mg, y)
      doc.setFont('helvetica', 'normal'); doc.text(v1 || '', mg + 26, y)
      doc.line(mg + 26, y + 0.5, mid, y + 0.5)
      doc.setFont('helvetica', 'bold')
      doc.text(`${l2}:`, mid + 4, y)
      doc.setFont('helvetica', 'normal'); doc.text(v2 || '', mid + 22, y)
      doc.line(mid + 22, y + 0.5, W - mg, y + 0.5)
      y += 7
    }
    function fullLine(label, value) {
      doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
      doc.text(`${label}:`, mg, y)
      doc.setFont('helvetica', 'normal'); doc.text(value || '', mg + 28, y)
      doc.line(mg + 28, y + 0.5, W - mg, y + 0.5)
      y += 7
    }

    twoCol('Customer', form.customerName, 'Address', form.customerAddress)
    twoCol('Email', form.customerEmail, 'Tel. No.', form.customerPhone)
    fullLine('Contact Person', form.contactPerson)

    y += 2

    // ── Nature of Visit ──
    doc.setFont('helvetica', 'bold'); doc.setFontSize(9)
    doc.text('Nature of Visit', mg, y); y += 5
    doc.setFont('helvetica', 'normal'); doc.setFontSize(8)
    let nx = mg
    NATURE_OPTIONS.forEach(opt => {
      doc.rect(nx, y - 3.2, 3.8, 3.8)
      if (form.natureOfVisit === opt) {
        doc.setFillColor(0); doc.rect(nx + 0.5, y - 2.7, 2.8, 2.8, 'F'); doc.setFillColor(255, 255, 255)
      }
      doc.text(opt, nx + 5, y)
      nx += doc.getTextWidth(opt) + 14
    })
    y += 7

    // ── Machine Details ──
    doc.setFont('helvetica', 'bold'); doc.setFontSize(9); doc.text('Machine Details', mg, y); y += 4
    doc.setFont('helvetica', 'normal'); doc.setFontSize(8.5)
    ;[1, 2, 3].forEach(() => { doc.line(mg, y, W - mg, y); y += 5 })
    if (form.machineDetails) { doc.text(doc.splitTextToSize(form.machineDetails, W - mg * 2), mg, y - 12) }
    y += 3

    // ── Job Details ──
    doc.setFont('helvetica', 'bold'); doc.setFontSize(9); doc.text('Job Details', mg, y); y += 4

    function jobLine(label, value) {
      doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
      doc.text(`${label}:`, mg, y)
      doc.line(mg + 30, y + 0.5, W - mg, y + 0.5)
      doc.setFont('helvetica', 'normal')
      if (value) doc.text(doc.splitTextToSize(value, W - mg - 32)[0], mg + 31, y)
      y += 5
      ;[1, 2].forEach(() => { doc.line(mg, y, W - mg, y); y += 4.5 })
    }

    jobLine('Fault Reported', form.faultReported)
    jobLine('Findings', form.findings)
    jobLine('Correction', form.correction)
    jobLine('Final Result', form.finalResult)
    jobLine('Parts to Order', form.partsToOrder)
    jobLine('Customer Comments', form.customerComments)

    y += 3

    // ── Mileage table ──
    doc.setFont('helvetica', 'bold'); doc.setFontSize(9)
    doc.text('Field Job Time - Mileage Details', mg, y); y += 2

    autoTable(doc, {
      startY: y,
      head: [['MV/EV/DN/LPO NO.', 'TIME-IN', 'TIME-OUT', 'TIME-SPENT', 'KM-OUT', 'KM-IN', 'KM-COVERED']],
      body: form.mileageRows.map(r => [r.lpoNo, r.timeIn, r.timeOut, r.timeSpent, r.kmOut, r.kmIn, r.kmCovered]),
      styles: { fontSize: 7.5, cellPadding: 2.5, lineColor: [180, 180, 180], lineWidth: 0.3 },
      headStyles: { fillColor: [245, 245, 245], textColor: [0, 0, 0], fontStyle: 'bold' },
      columnStyles: {
        0: { cellWidth: 36 }, 1: { cellWidth: 22 }, 2: { cellWidth: 22 },
        3: { cellWidth: 22 }, 4: { cellWidth: 20 }, 5: { cellWidth: 20 }, 6: { cellWidth: 24 },
      },
      margin: { left: mg, right: mg },
    })

    y = (doc.lastAutoTable?.finalY ?? y) + 8

    // ── Technician + Signatures ──
    function sigLine(label, value) {
      doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
      doc.text(`${label}:`, mg, y)
      doc.setFont('helvetica', 'normal'); doc.text(value || '', mg + 48, y)
      doc.line(mg + 48, y + 0.5, W - mg, y + 0.5)
      y += 8
    }
    sigLine('Name of Technician', form.technicianName)
    sigLine('Tel. No.', form.technicianPhone)
    sigLine('Signature', '')
    sigLine("Customer's Signature and Stamp", '')

    y += 4
    doc.setFontSize(7); doc.setTextColor(100)
    doc.text(`Copies: 1st Copy - Customer, 2nd Copy - ${branding.docPrefix}, 3rd Copy - Book Copy`, W / 2, y, { align: 'center' })

    y += 6
    doc.setFillColor(240, 160, 10)
    doc.rect(mg, y, W - mg * 2, 6, 'F')
    doc.setFontSize(8); doc.setFont('helvetica', 'bold'); doc.setTextColor(0)
    doc.text('Inventing and making happen', W / 2, y + 4.2, { align: 'center' })

    doc.save(`technical-report-${form.date}.pdf`)
  }

  return (
    <>
      <main className="max-w-3xl mx-auto w-full px-4 sm:px-6 py-6">

        {/* Page Header */}
        <div className="flex items-center justify-between gap-3 mb-6">
          <div className="flex items-center gap-3">
            <button onClick={() => navigate(`/modules/operations/assignments/${id}`)}
              className="p-2 hover:bg-gray-100 rounded-xl text-gray-400 transition-colors">
              <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
              </svg>
            </button>
            <div>
              <h1 className="text-2xl font-extrabold text-zinc-950">Technical Report</h1>
              {assignment && <p className="text-sm text-gray-400 mt-0.5">{assignment.title}</p>}
            </div>
          </div>
          <button type="button" onClick={exportPDF}
            className="flex items-center gap-2 px-4 py-2.5 bg-amber-500 hover:bg-amber-600 text-black text-sm font-bold rounded-xl transition-colors">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            Export PDF
          </button>
        </div>

        {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-2xl px-5 py-3 text-sm mb-4">{error}</div>}

        <form onSubmit={handleSubmit} className="space-y-4">

          {/* ── Report header card (mirrors the physical form top section) ── */}
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

          {/* ── Nature of Visit (checkbox style) ── */}
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

          {/* ── Machine Details ── */}
          <Block title="Machine Details">
            <textarea rows={3} value={form.machineDetails} onChange={e => set('machineDetails', e.target.value)}
              placeholder="Make, model, serial number, and other machine details…" className={cls.textarea} />
          </Block>

          {/* ── Job Details ── */}
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

          {/* ── Field Job Time — Mileage Details ── */}
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

          {/* ── Technician Info ── */}
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

          {/* ── Images ── */}
          <Block title="Images / Attachments">
            <ImagePicker files={pendingFiles} onChange={setPendingFiles} />
          </Block>

          {/* ── Submit ── */}
          <div className="flex gap-3 pt-2 pb-6">
            <button type="button" onClick={() => navigate(`/modules/operations/assignments/${id}`)}
              className="flex-1 py-3.5 border-2 border-zinc-200 text-zinc-700 text-base font-semibold rounded-2xl hover:bg-zinc-50 transition-colors">
              Cancel
            </button>
            <button type="submit" disabled={submitting}
              className="flex-1 py-3.5 bg-zinc-950 text-white text-base font-semibold rounded-2xl transition-colors disabled:opacity-50 shadow-lg">
              {submitting ? 'Saving…' : 'Save Report'}
            </button>
          </div>
        </form>
      </main>
    </>
  )
}
