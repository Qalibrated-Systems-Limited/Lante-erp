import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import * as opsApi from '../../services/operations.js'

// ── Constants ─────────────────────────────────────────────────────────────────

const STATUS_BADGE = {
  PendingVerification : 'bg-gray-100 text-gray-500',
  Submitted           : 'bg-blue-100 text-blue-700',
  UnderReview         : 'bg-amber-100 text-amber-700',
  QuotationDraft      : 'bg-purple-100 text-purple-700',
  QuotationSent       : 'bg-indigo-100 text-indigo-700',
  QuotationApproved   : 'bg-green-100 text-green-700',
  QuotationRejected   : 'bg-red-100 text-red-600',
  InProgress          : 'bg-cyan-100 text-cyan-700',
  Completed           : 'bg-green-100 text-green-800',
  Rejected            : 'bg-red-100 text-red-700',
  Cancelled           : 'bg-gray-100 text-gray-500',
}

const FORM_LABEL = {
  SRF      : 'Service Request Form (SRF)',
  CRF_NAWI : 'Calibration Request — NAWI',
  CRF_MASS : 'Calibration Request — Mass Standards',
}

const emptyLineItem = () => ({ description: '', quantity: 1, unitPrice: 0, amount: 0 })

// ── Helpers ───────────────────────────────────────────────────────────────────

function SectionCard({ title, children }) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
        <h3 className="text-sm font-bold text-gray-700">{title}</h3>
      </div>
      <div className="p-5">{children}</div>
    </div>
  )
}

function Field({ label, value }) {
  return (
    <div>
      <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-0.5">{label}</p>
      <p className="text-sm text-gray-800">{value || '—'}</p>
    </div>
  )
}

function StatusBadge({ status }) {
  return (
    <span className={`inline-block text-xs font-bold px-2.5 py-1 rounded-full ${STATUS_BADGE[status] ?? 'bg-gray-100 text-gray-500'}`}>
      {status.replace(/([A-Z])/g, ' $1').trim()}
    </span>
  )
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function ServiceRequestDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [sr, setSr]           = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState('')
  const [saving, setSaving]   = useState(false)
  const [toast, setToast]     = useState('')

  // Review panel — 6-item checklist
  const REVIEW_ITEMS = [
    'Requested service within scope',
    'Is the requested service feasible',
    'Qualified personnel available',
    'Adequate resources available',
    'Risks to impartiality identified',
    'Quotation required',
  ]
  const initChecklist = () => REVIEW_ITEMS.map(item => ({ item, answer: '', remarks: '' }))
  const [reviewOpen, setReviewOpen]             = useState(false)
  const [checklist, setChecklist]               = useState(initChecklist)
  const [reviewComments, setReviewComments]     = useState('')
  const [rejectReason, setRejectReason]         = useState('')
  const [plannedServiceDate, setPlannedServiceDate] = useState('')
  const [authorizingName, setAuthorizingName]   = useState('')
  const [reviewLoading, setReviewLoading]       = useState(false)

  // Quotation builder
  const [lineItems, setLineItems]   = useState([emptyLineItem()])
  const [vatRate, setVatRate]       = useState(0.16)
  const [validUntil, setValidUntil] = useState('')
  const [qtNotes, setQtNotes]       = useState('')
  const [qtSaving, setQtSaving]     = useState(false)

  // LPO panel
  const [lpoOpen, setLpoOpen]   = useState(false)
  const [lpoNumber, setLpoNumber] = useState('')
  const [lpoLoading, setLpoLoading] = useState(false)

  // Shared reason modal — 'declineQuote' (client declines a sent quote) | 'cancel' (cancel the request)
  const [reasonModal, setReasonModal]     = useState(null)
  const [reasonText, setReasonText]       = useState('')
  const [reasonLoading, setReasonLoading] = useState(false)

  // Create assignment panel
  const [assignOpen, setAssignOpen]         = useState(false)
  const [assignTechIds, setAssignTechIds]   = useState([''])
  const [assignTechNames, setAssignTechNames] = useState([''])
  const [assignDeadline, setAssignDeadline] = useState('')
  const [assignNotes, setAssignNotes]       = useState('')
  const [assignNature, setAssignNature]     = useState('CorrectiveMaintenance')
  const [assignLoading, setAssignLoading]   = useState(false)
  const [users, setUsers]                   = useState([])

  const openAssignModal = () => {
    setAssignNature(sr?.serviceLocation === 'InLab' ? 'Calibration' : 'CorrectiveMaintenance')
    setAssignOpen(true)
  }

  useEffect(() => {
    opsApi.getUsers({ pageSize: 100 }).then(raw => setUsers(raw?.items ?? raw ?? [])).catch(() => {})
  }, [])

  const showToast = msg => { setToast(msg); setTimeout(() => setToast(''), 3500) }

  // ── Load ────────────────────────────────────────────────────────────────────

  useEffect(() => {
    setLoading(true)
    opsApi.getServiceRequest(id)
      .then(data => {
        setSr(data)
        if (data?.quotation) {
          setLineItems(data.quotation.lineItems?.length ? data.quotation.lineItems : [emptyLineItem()])
          setVatRate(data.quotation.vatRate ?? 0.16)
          setValidUntil(data.quotation.validUntil ? data.quotation.validUntil.slice(0, 10) : '')
          setQtNotes(data.quotation.notes ?? '')
        }
      })
      .catch(() => setError('Failed to load service request.'))
      .finally(() => setLoading(false))
  }, [id])

  // ── Line item helpers ───────────────────────────────────────────────────────

  const updateLine = (i, field, val) => {
    setLineItems(prev => prev.map((li, idx) => {
      if (idx !== i) return li
      const updated = { ...li, [field]: val }
      if (field === 'quantity' || field === 'unitPrice') {
        updated.amount = Math.round(
          (field === 'quantity' ? Number(val) : Number(li.quantity))
          * (field === 'unitPrice' ? Number(val) : Number(li.unitPrice))
          * 100
        ) / 100
      }
      return updated
    }))
  }

  const addLine    = () => setLineItems(prev => [...prev, emptyLineItem()])
  const removeLine = i  => setLineItems(prev => prev.filter((_, idx) => idx !== i))

  const subtotal   = lineItems.reduce((s, li) => s + (Number(li.amount) || 0), 0)
  const vatAmount  = Math.round(subtotal * vatRate * 100) / 100
  const total      = subtotal + vatAmount

  // ── Actions ─────────────────────────────────────────────────────────────────

  const handleReview = async (approve) => {
    if (!approve && !rejectReason.trim()) return
    setReviewLoading(true)
    try {
      await opsApi.reviewServiceRequest(id, {
        approve,
        tmComments: reviewComments || undefined,
        rejectionReason: !approve ? rejectReason : undefined,
        reviewChecklistJson: JSON.stringify(checklist),
        plannedServiceDate: approve && plannedServiceDate ? plannedServiceDate : undefined,
        authorizingName: approve && authorizingName ? authorizingName : undefined,
      })
      showToast(approve ? 'Request accepted.' : 'Request rejected.')
      setReviewOpen(false)
      setSr(prev => ({
        ...prev,
        status: approve ? 'UnderReview' : 'Rejected',
        tmComments: reviewComments,
        rejectionReason: rejectReason,
        reviewChecklistJson: JSON.stringify(checklist),
        plannedServiceDate: approve ? plannedServiceDate : prev.plannedServiceDate,
        authorizingName: approve ? authorizingName : prev.authorizingName,
      }))
    } catch (err) {
      showToast(err.response?.data?.message || 'Action failed.')
    } finally {
      setReviewLoading(false)
    }
  }

  const saveQuotation = async () => {
    if (lineItems.length === 0 || !lineItems.some(li => li.description.trim()))
      return showToast('Add at least one line item with a description.')
    setQtSaving(true)
    try {
      const payload = {
        lineItems: lineItems.map(li => ({ ...li, quantity: Number(li.quantity), unitPrice: Number(li.unitPrice), amount: Number(li.amount) })),
        vatRate: Number(vatRate),
        validUntil: validUntil || undefined,
        notes: qtNotes || undefined,
      }
      let quotation
      if (sr?.quotation) {
        quotation = await opsApi.updateQuotation(id, payload)
        showToast('Quotation updated.')
      } else {
        quotation = await opsApi.createQuotation(id, payload)
        showToast('Quotation draft created.')
      }
      setSr(prev => ({ ...prev, quotation, status: 'QuotationDraft' }))
    } catch (err) {
      showToast(err.response?.data?.message || 'Failed to save quotation.')
    } finally {
      setQtSaving(false)
    }
  }

  const sendQuotation = async () => {
    setSaving(true)
    try {
      await opsApi.sendQuotation(id)
      showToast('Quotation emailed to client.')
      setSr(prev => ({ ...prev, status: 'QuotationSent', quotation: { ...prev.quotation, status: 'Sent', sentAt: new Date().toISOString() } }))
    } catch (err) {
      showToast(err.response?.data?.message || 'Failed to send quotation.')
    } finally {
      setSaving(false)
    }
  }

  // #12 — reopen a sent/rejected/expired quotation for editing (→ Draft)
  const reviseQuotation = async () => {
    setSaving(true)
    try {
      const revised = await opsApi.reviseQuotation(id)
      showToast('Quotation reopened for editing.')
      setSr(prev => ({ ...prev, status: 'QuotationDraft', quotation: revised ?? { ...prev.quotation, status: 'Draft' } }))
    } catch (err) {
      showToast(err.response?.data?.message || 'Failed to revise quotation.')
    } finally {
      setSaving(false)
    }
  }

  const recordLpo = async () => {
    if (!lpoNumber.trim()) return showToast('LPO number is required.')
    setLpoLoading(true)
    try {
      await opsApi.recordLpo(id, { lpoNumber })
      showToast(`LPO ${lpoNumber} recorded. Quotation accepted.`)
      setLpoOpen(false)
      setSr(prev => ({ ...prev, status: 'QuotationApproved', quotation: { ...prev.quotation, status: 'Accepted', lpoNumber } }))
    } catch (err) {
      showToast(err.response?.data?.message || 'Failed to record LPO.')
    } finally {
      setLpoLoading(false)
    }
  }

  // Client declines a sent quotation (→ QuotationRejected; revise to reopen).
  const rejectQuotation = async () => {
    setReasonLoading(true)
    try {
      await opsApi.rejectQuotation(id, { reason: reasonText.trim() || undefined })
      showToast('Quotation marked as declined by client.')
      setReasonModal(null); setReasonText('')
      setSr(prev => ({
        ...prev,
        status: 'QuotationRejected',
        rejectionReason: reasonText.trim() || prev.rejectionReason,
        quotation: prev.quotation ? { ...prev.quotation, status: 'Rejected' } : prev.quotation,
      }))
    } catch (err) {
      showToast(err.response?.data?.message || 'Failed to decline quotation.')
    } finally {
      setReasonLoading(false)
    }
  }

  // Cancel the request before field work begins (→ Cancelled, terminal).
  const cancelRequest = async () => {
    setReasonLoading(true)
    try {
      await opsApi.cancelServiceRequest(id, { reason: reasonText.trim() || undefined })
      showToast('Service request cancelled.')
      setReasonModal(null); setReasonText('')
      setSr(prev => ({ ...prev, status: 'Cancelled', rejectionReason: reasonText.trim() || prev.rejectionReason }))
    } catch (err) {
      showToast(err.response?.data?.message || 'Failed to cancel request.')
    } finally {
      setReasonLoading(false)
    }
  }

  const createAssignment = async () => {
    const ids   = assignTechIds.filter(x => x.trim())
    const names = assignTechNames.filter(x => x.trim())
    if (ids.length === 0) return showToast('Select at least one technician.')
    setAssignLoading(true)
    try {
      const created = await opsApi.createSrAssignment(id, {
        technicianIds:   ids,
        technicianNames: names,
        deadline:        assignDeadline || undefined,
        notes:           assignNotes || undefined,
        natureOfVisit:   assignNature,
      })
      const assignmentId = created?.assignmentId
      showToast('Assignment created. SR is now In Progress.')
      setAssignOpen(false)
      setSr(prev => ({ ...prev, status: 'InProgress', operationsAssignmentId: assignmentId }))
    } catch (err) {
      showToast(err.response?.data?.message || 'Failed to create assignment.')
    } finally {
      setAssignLoading(false)
    }
  }

  // ── Render ──────────────────────────────────────────────────────────────────

  if (loading) return <><div className="p-10 text-center text-sm text-gray-400">Loading…</div></>
  if (error || !sr) return <><div className="p-10 text-center text-sm text-red-500">{error || 'Not found.'}</div></>

  const canReview  = ['Submitted', 'UnderReview'].includes(sr.status)
  const canQuote   = ['UnderReview', 'QuotationDraft'].includes(sr.status)
  const canSend    = sr.quotation && ['Draft'].includes(sr.quotation?.status) && sr.status !== 'Rejected'
  const canSendResend = sr.quotation && ['Sent'].includes(sr.quotation?.status)
  const canLpo     = sr.quotation && ['Sent'].includes(sr.quotation?.status)
  const canRevise  = sr.quotation && ['Sent', 'Rejected', 'Expired'].includes(sr.quotation?.status) && !sr.operationsAssignmentId
  const canAssign  = sr.status === 'QuotationApproved' && !sr.operationsAssignmentId
  // Client declines a sent quotation; cancellation allowed until field work begins (matches backend guards).
  const canRejectQuote = sr.status === 'QuotationSent'
  const canCancel  = ['Submitted', 'UnderReview', 'QuotationDraft', 'QuotationSent', 'QuotationApproved', 'QuotationRejected']
    .includes(sr.status) && !sr.operationsAssignmentId
  const isInLab    = sr.serviceLocation === 'InLab'

  return (
    <>
      {/* Toast */}
      {toast && (
        <div className="fixed bottom-6 right-6 z-50 bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg animate-fade-in">
          {toast}
        </div>
      )}

      <div className="p-6 max-w-5xl mx-auto space-y-6">

        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4">
          <div>
            <button onClick={() => navigate('/modules/operations/service-requests')}
              className="text-xs text-amber-600 hover:text-amber-800 font-medium mb-2 block">
              ← Back to Queue
            </button>
            <div className="flex items-center gap-3 flex-wrap">
              <h1 className="text-xl font-extrabold text-zinc-950 font-mono">{sr.referenceNumber}</h1>
              <StatusBadge status={sr.status} />
              <span className="text-xs font-semibold text-gray-500 bg-gray-100 px-2 py-0.5 rounded-full">
                {FORM_LABEL[sr.formType] ?? sr.formType}
              </span>
              {sr.serviceLocation && (
                <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${
                  sr.serviceLocation === 'InLab'
                    ? 'bg-purple-100 text-purple-700'
                    : 'bg-cyan-100 text-cyan-700'
                }`}>
                  {sr.serviceLocation === 'InLab' ? '🔬 In-Lab' : '🚗 On-Site'}
                </span>
              )}
            </div>
            <p className="text-xs text-gray-400 mt-1">
              Submitted {new Date(sr.createdAt).toLocaleDateString('en-GB', { day: '2-digit', month: 'long', year: 'numeric' })}
              {sr.otpVerifiedAt && ' · OTP verified'}
              {sr.hasSignature && ' · Signed'}
            </p>
          </div>

          {/* Action buttons */}
          <div className="flex gap-2 flex-wrap shrink-0">
            {canReview && (
              <button onClick={() => setReviewOpen(true)}
                className="px-4 py-2 text-sm font-semibold bg-amber-400 hover:bg-amber-500 text-black rounded-lg transition-colors">
                Review
              </button>
            )}
            {(canSend || canSendResend) && (
              <button onClick={sendQuotation} disabled={saving}
                className="px-4 py-2 text-sm font-semibold bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg disabled:opacity-50 transition-colors">
                {saving ? 'Sending…' : canSendResend ? 'Resend Quotation' : 'Send Quotation'}
              </button>
            )}
            {canLpo && (
              <button onClick={() => setLpoOpen(true)}
                className="px-4 py-2 text-sm font-semibold bg-green-600 hover:bg-green-700 text-white rounded-lg transition-colors">
                Record LPO
              </button>
            )}
            {canRevise && (
              <button onClick={reviseQuotation} disabled={saving}
                className="px-4 py-2 text-sm font-semibold border border-amber-300 text-amber-700 hover:bg-amber-50 rounded-lg disabled:opacity-50 transition-colors">
                {saving ? 'Revising…' : 'Revise Quotation'}
              </button>
            )}
            {canRejectQuote && (
              <button onClick={() => { setReasonText(''); setReasonModal('declineQuote') }}
                className="px-4 py-2 text-sm font-semibold border border-orange-300 text-orange-700 hover:bg-orange-50 rounded-lg transition-colors">
                Decline Quotation
              </button>
            )}
            {canAssign && (
              <button onClick={openAssignModal}
                className="px-4 py-2 text-sm font-semibold bg-cyan-600 hover:bg-cyan-700 text-white rounded-lg transition-colors">
                Create Assignment
              </button>
            )}
            {sr.operationsAssignmentId && (
              <button onClick={() => navigate(`/modules/operations/assignments/${sr.operationsAssignmentId}`)}
                className="px-4 py-2 text-sm font-semibold border border-cyan-300 text-cyan-700 hover:bg-cyan-50 rounded-lg transition-colors">
                View Assignment →
              </button>
            )}
            {sr.ticketId && (
              <button onClick={() => navigate(`/modules/ticketing/${sr.ticketId}`)}
                className="px-4 py-2 text-sm font-semibold border border-gray-300 text-gray-700 hover:bg-gray-50 rounded-lg transition-colors">
                View Ticket
              </button>
            )}
            {canCancel && (
              <button onClick={() => { setReasonText(''); setReasonModal('cancel') }}
                className="px-4 py-2 text-sm font-semibold border border-red-300 text-red-700 hover:bg-red-50 rounded-lg transition-colors">
                Cancel Request
              </button>
            )}
          </div>
        </div>

        {/* Terminal / decline reason — label reflects the actual status */}
        {sr.rejectionReason && (
          <div className="bg-red-50 border border-red-200 rounded-xl px-5 py-3">
            <p className="text-sm font-semibold text-red-700">
              {sr.status === 'Cancelled' ? 'Cancelled'
                : sr.status === 'QuotationRejected' ? 'Quotation declined by client'
                : 'Rejected'}
            </p>
            <p className="text-sm text-red-600 mt-0.5">{sr.rejectionReason}</p>
          </div>
        )}
        {sr.tmComments && !sr.rejectionReason && (
          <div className="bg-amber-50 border border-amber-200 rounded-xl px-5 py-3">
            <p className="text-xs font-semibold text-amber-700 uppercase tracking-wide">TM Comments</p>
            <p className="text-sm text-amber-800 mt-0.5">{sr.tmComments}</p>
          </div>
        )}
        {sr.reviewChecklistJson && (() => {
          let cl = null
          try { cl = JSON.parse(sr.reviewChecklistJson) } catch { return null }
          if (!Array.isArray(cl)) return null
          return (
            <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
              <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                <h3 className="text-sm font-bold text-gray-700">Review Checklist</h3>
                <div className="flex gap-3 text-xs text-gray-500">
                  {sr.tmReviewedAt && <span>Reviewed {new Date(sr.tmReviewedAt).toLocaleDateString('en-GB')}</span>}
                  {sr.plannedServiceDate && <span>· Planned: {new Date(sr.plannedServiceDate).toLocaleDateString('en-GB')}</span>}
                  {sr.authorizingName && <span>· Auth: <strong className="text-gray-700">{sr.authorizingName}</strong></span>}
                </div>
              </div>
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100">
                    <th className="text-left px-4 py-2 text-xs font-semibold text-gray-500 w-[50%]">Item</th>
                    <th className="text-center px-3 py-2 text-xs font-semibold text-gray-500 w-20">Answer</th>
                    <th className="text-left px-4 py-2 text-xs font-semibold text-gray-500">Remarks</th>
                  </tr>
                </thead>
                <tbody>
                  {cl.map((row, i) => (
                    <tr key={i} className="border-b border-gray-50 last:border-0">
                      <td className="px-4 py-2.5 text-gray-700">{row.item}</td>
                      <td className="px-3 py-2.5 text-center">
                        <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${
                          row.answer === 'Yes' ? 'bg-green-100 text-green-700' :
                          row.answer === 'No'  ? 'bg-red-100 text-red-600' : 'bg-gray-100 text-gray-400'
                        }`}>{row.answer || '—'}</span>
                      </td>
                      <td className="px-4 py-2.5 text-gray-500 text-xs">{row.remarks || '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        })()}

        {/* Client & Site */}
        <SectionCard title="Client Information">
          <div className="grid grid-cols-2 sm:grid-cols-3 gap-5">
            <Field label="Name"         value={sr.clientName} />
            <Field label="Email"        value={sr.clientEmail} />
            <Field label="Phone"        value={sr.clientPhone} />
            <Field label="Organisation" value={sr.clientOrganization} />
            <Field label="Address"      value={sr.clientAddress} />
            <Field label={sr.serviceLocation === 'InLab' ? 'Collection / Return Address' : 'Site / Location'} value={sr.siteLocation} />
            {(sr.latitude || sr.longitude) && (
              <div>
                <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-0.5">GPS</p>
                <a href={`https://maps.google.com/?q=${sr.latitude},${sr.longitude}`}
                  target="_blank" rel="noreferrer"
                  className="text-sm text-amber-600 hover:underline font-mono">
                  {sr.latitude?.toFixed(5)}, {sr.longitude?.toFixed(5)}
                </a>
              </div>
            )}
          </div>
          {sr.description && (
            <div className="mt-4 pt-4 border-t border-gray-100">
              <Field label="Description / Nature of Request" value={sr.description} />
            </div>
          )}
          {sr.specialInstructions && (
            <div className="mt-3">
              <Field label="Special Instructions" value={sr.specialInstructions} />
            </div>
          )}
        </SectionCard>

        {/* Instruments */}
        <SectionCard title={`Instruments (${sr.instruments?.length ?? 0})`}>
          {(!sr.instruments || sr.instruments.length === 0) ? (
            <p className="text-sm text-gray-400">No instruments recorded.</p>
          ) : (
            <div className="space-y-4">
              {sr.instruments.map((inst, i) => (
                <div key={inst.id} className="border border-gray-200 rounded-xl p-4 bg-gray-50">
                  <p className="text-xs font-bold text-amber-700 uppercase tracking-wide mb-3">
                    Instrument {inst.rowNumber}
                  </p>
                  <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
                    <Field label="Description"  value={inst.description} />
                    <Field label="Manufacturer" value={inst.manufacturer} />
                    <Field label="Model"        value={inst.model} />
                    <Field label="Serial No."   value={inst.serialNumber} />
                    <Field label="Tag / Asset"  value={inst.tagNumber} />

                    {/* SRF */}
                    {sr.formType === 'SRF' && <Field label="Service Type" value={inst.serviceType} />}

                    {/* NAWI */}
                    {sr.formType === 'CRF_NAWI' && (<>
                      <Field label="Instrument Type"   value={inst.nawiInstrumentType} />
                      <Field label="Max Capacity"      value={inst.nawiCapacity} />
                      <Field label="Scale Interval (e)" value={inst.nawiScaleInterval} />
                      <Field label="Accuracy Class"    value={inst.nawiAccuracyClass} />
                    </>)}

                    {/* MASS */}
                    {sr.formType === 'CRF_MASS' && (<>
                      <Field label="Nominal Value"   value={inst.massNominalValue} />
                      <Field label="Accuracy Class"  value={inst.massAccuracyClass} />
                    </>)}

                    {(sr.formType === 'CRF_NAWI' || sr.formType === 'CRF_MASS') && (<>
                      <Field label="Last Calibration" value={inst.lastCalibrationDate ? new Date(inst.lastCalibrationDate).toLocaleDateString('en-GB') : null} />
                      <Field label="Certificate No."  value={inst.certificateNumber} />
                    </>)}

                    <Field label="Condition" value={inst.condition} />
                    {inst.remarks && <div className="col-span-2"><Field label="Remarks" value={inst.remarks} /></div>}
                  </div>
                </div>
              ))}
            </div>
          )}
        </SectionCard>

        {/* Quotation Builder */}
        {(canQuote || sr.quotation) && (
          <SectionCard title="Quotation Builder">
            {sr.quotation && (
              <div className="flex items-center gap-3 mb-5 pb-4 border-b border-gray-100">
                <span className={`text-xs font-bold px-2.5 py-1 rounded-full ${
                  sr.quotation.status === 'Draft'    ? 'bg-purple-100 text-purple-700' :
                  sr.quotation.status === 'Sent'     ? 'bg-indigo-100 text-indigo-700' :
                  sr.quotation.status === 'Accepted' ? 'bg-green-100 text-green-700'  :
                  'bg-gray-100 text-gray-500'}`}>
                  {sr.quotation.status}
                </span>
                <span className="text-sm font-mono text-gray-600">{sr.quotation.quotationNumber}</span>
                {sr.quotation.sentAt && (
                  <span className="text-xs text-gray-400">
                    Sent {new Date(sr.quotation.sentAt).toLocaleDateString('en-GB')}
                  </span>
                )}
                {sr.quotation.lpoNumber && (
                  <span className="text-xs font-semibold text-green-700 bg-green-50 px-2 py-0.5 rounded-full">
                    LPO: {sr.quotation.lpoNumber}
                  </span>
                )}
              </div>
            )}

            {/* Line items */}
            <div className="mb-4">
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Line Items</p>
              <div className="space-y-2">
                {lineItems.map((li, i) => (
                  <div key={i} className="flex gap-2 items-start">
                    <input
                      placeholder="Description"
                      value={li.description}
                      onChange={e => updateLine(i, 'description', e.target.value)}
                      className="flex-1 border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
                    />
                    <input
                      type="number" min="0" step="0.01" placeholder="Qty"
                      value={li.quantity}
                      onChange={e => updateLine(i, 'quantity', e.target.value)}
                      className="w-20 border border-gray-300 rounded-lg px-2 py-1.5 text-sm text-center focus:outline-none focus:ring-2 focus:ring-amber-400"
                    />
                    <input
                      type="number" min="0" step="0.01" placeholder="Unit price"
                      value={li.unitPrice}
                      onChange={e => updateLine(i, 'unitPrice', e.target.value)}
                      className="w-28 border border-gray-300 rounded-lg px-2 py-1.5 text-sm text-right focus:outline-none focus:ring-2 focus:ring-amber-400"
                    />
                    <div className="w-28 border border-gray-200 rounded-lg px-2 py-1.5 text-sm text-right bg-gray-50 text-gray-600">
                      {Number(li.amount).toLocaleString('en-KE', { minimumFractionDigits: 2 })}
                    </div>
                    <button onClick={() => removeLine(i)} disabled={lineItems.length === 1}
                      className="text-red-400 hover:text-red-600 disabled:opacity-30 text-lg leading-none mt-1 px-1">
                      ×
                    </button>
                  </div>
                ))}
              </div>
              <div className="flex gap-2 text-xs font-semibold text-gray-400 mt-1 pl-0">
                <span className="flex-1">Description</span>
                <span className="w-20 text-center">Qty</span>
                <span className="w-28 text-right">Unit Price (KES)</span>
                <span className="w-28 text-right">Amount (KES)</span>
                <span className="w-6" />
              </div>
              <button onClick={addLine}
                className="mt-3 px-4 py-1.5 border-2 border-dashed border-amber-300 rounded-lg text-xs font-semibold text-amber-600 hover:bg-amber-50 transition-colors">
                + Add Line Item
              </button>
            </div>

            {/* Totals */}
            <div className="flex flex-col sm:flex-row gap-6 mt-5 pt-4 border-t border-gray-100">
              <div className="flex-1 grid grid-cols-2 gap-3">
                <div>
                  <label className="text-xs font-semibold text-gray-500 block mb-1">VAT Rate</label>
                  <select value={vatRate} onChange={e => setVatRate(Number(e.target.value))}
                    className="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-amber-400">
                    <option value={0}>0%</option>
                    <option value={0.08}>8%</option>
                    <option value={0.16}>16%</option>
                  </select>
                </div>
                <div>
                  <label className="text-xs font-semibold text-gray-500 block mb-1">Valid Until</label>
                  <input type="date" value={validUntil} onChange={e => setValidUntil(e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                </div>
                <div className="col-span-2">
                  <label className="text-xs font-semibold text-gray-500 block mb-1">Notes</label>
                  <textarea value={qtNotes} onChange={e => setQtNotes(e.target.value)} rows={2}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-amber-400"
                    placeholder="Payment terms, scope exclusions, etc." />
                </div>
              </div>

              <div className="sm:w-56 shrink-0 space-y-2">
                <div className="flex justify-between text-sm text-gray-600">
                  <span>Subtotal</span>
                  <span className="font-medium">KES {subtotal.toLocaleString('en-KE', { minimumFractionDigits: 2 })}</span>
                </div>
                <div className="flex justify-between text-sm text-gray-600">
                  <span>VAT ({(vatRate * 100).toFixed(0)}%)</span>
                  <span className="font-medium">KES {vatAmount.toLocaleString('en-KE', { minimumFractionDigits: 2 })}</span>
                </div>
                <div className="flex justify-between text-sm font-bold text-zinc-950 border-t border-gray-200 pt-2">
                  <span>Total</span>
                  <span>KES {total.toLocaleString('en-KE', { minimumFractionDigits: 2 })}</span>
                </div>
              </div>
            </div>

            <div className="mt-4 flex gap-3 flex-wrap">
              <button onClick={saveQuotation} disabled={qtSaving}
                className="px-5 py-2 text-sm font-bold bg-amber-400 hover:bg-amber-500 text-black rounded-lg disabled:opacity-50 transition-colors">
                {qtSaving ? 'Saving…' : sr.quotation ? 'Update Draft' : 'Save Draft'}
              </button>
              {(canSend || canSendResend) && (
                <button onClick={sendQuotation} disabled={saving}
                  className="px-5 py-2 text-sm font-bold bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg disabled:opacity-50 transition-colors">
                  {saving ? 'Sending…' : canSendResend ? 'Resend to Client' : 'Send to Client'}
                </button>
              )}
              {sr.quotation && (
                <a href={`/modules/operations/service-requests/${id}/quotation/print`} target="_blank" rel="noreferrer"
                  className="px-5 py-2 text-sm font-bold bg-white border border-gray-300 hover:bg-gray-50 text-gray-700 rounded-lg transition-colors inline-flex items-center gap-2">
                  <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    <path strokeLinecap="round" strokeLinejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                  </svg>
                  View / Print
                </a>
              )}
            </div>
          </SectionCard>
        )}

      </div>

      {/* ── Review modal ──────────────────────────────────────────────────── */}
      {reviewOpen && (
        <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[92vh] overflow-y-auto">
            <div className="flex items-center justify-between px-6 pt-5 pb-3 border-b border-gray-100 sticky top-0 bg-white z-10">
              <div>
                <h2 className="text-base font-bold text-gray-800">Service Request Review</h2>
                <p className="text-xs text-gray-400">Section 6 — For Service and Calibration Dept. Use Only</p>
              </div>
              <button onClick={() => setReviewOpen(false)} className="text-gray-400 hover:text-gray-600 text-xl leading-none">&times;</button>
            </div>

            <div className="px-6 py-5 space-y-5">
              {/* 6-item checklist */}
              <div>
                <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">Review Checklist</p>
                <div className="border border-gray-200 rounded-xl overflow-hidden">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="bg-gray-50 border-b border-gray-200">
                        <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-600 w-[45%]">Review Item</th>
                        <th className="text-center px-3 py-2.5 text-xs font-semibold text-gray-600 w-24">Yes / No</th>
                        <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-600">Remarks</th>
                      </tr>
                    </thead>
                    <tbody>
                      {checklist.map((row, i) => (
                        <tr key={i} className="border-b border-gray-100 last:border-0">
                          <td className="px-4 py-3 text-gray-700">{row.item}</td>
                          <td className="px-3 py-3">
                            <div className="flex gap-2 justify-center">
                              {['Yes', 'No'].map(opt => (
                                <label key={opt} className="flex items-center gap-1 cursor-pointer">
                                  <input type="radio" name={`cl-${i}`} value={opt}
                                    checked={row.answer === opt}
                                    onChange={() => setChecklist(prev => prev.map((r, j) => j === i ? { ...r, answer: opt } : r))}
                                    className="accent-amber-500" />
                                  <span className="text-xs text-gray-600">{opt}</span>
                                </label>
                              ))}
                            </div>
                          </td>
                          <td className="px-4 py-2">
                            <input value={row.remarks}
                              onChange={e => setChecklist(prev => prev.map((r, j) => j === i ? { ...r, remarks: e.target.value } : r))}
                              placeholder="Optional…"
                              className="w-full border border-gray-200 rounded-lg px-2.5 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-amber-400" />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Decision */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-semibold text-gray-600 uppercase tracking-wide mb-1">Planned Service Date</label>
                  <input type="date" value={plannedServiceDate} onChange={e => setPlannedServiceDate(e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-600 uppercase tracking-wide mb-1">Authorised By (Name)</label>
                  <input type="text" value={authorizingName} onChange={e => setAuthorizingName(e.target.value)}
                    placeholder="Reviewed and Approved by…"
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-600 uppercase tracking-wide mb-1">Comments</label>
                <textarea value={reviewComments} onChange={e => setReviewComments(e.target.value)} rows={2}
                  placeholder="General notes for the team…"
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-amber-400" />
              </div>

              <div>
                <label className="block text-xs font-semibold text-red-500 uppercase tracking-wide mb-1">Rejection Reason <span className="text-gray-400 font-normal normal-case">(required to reject)</span></label>
                <textarea value={rejectReason} onChange={e => setRejectReason(e.target.value)} rows={2}
                  placeholder="State why this request cannot be processed…"
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-red-300" />
              </div>
            </div>

            <div className="flex gap-3 px-6 pb-5 sticky bottom-0 bg-white pt-3 border-t border-gray-100">
              <button onClick={() => handleReview(false)} disabled={!rejectReason.trim() || reviewLoading}
                className="flex-1 py-2.5 text-sm font-semibold bg-red-50 hover:bg-red-100 text-red-700 border border-red-200 rounded-xl disabled:opacity-40 transition-colors">
                Reject
              </button>
              <button onClick={() => handleReview(true)} disabled={reviewLoading}
                className="flex-1 py-2.5 text-sm font-bold bg-amber-400 hover:bg-amber-500 text-black rounded-xl disabled:opacity-50 transition-colors">
                {reviewLoading ? 'Saving…' : 'Accept → Under Review'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── LPO modal ─────────────────────────────────────────────────────── */}
      {lpoOpen && (
        <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm">
            <div className="flex items-center justify-between px-6 pt-5 pb-3 border-b border-gray-100">
              <h2 className="text-base font-bold text-gray-800">Record LPO</h2>
              <button onClick={() => setLpoOpen(false)} className="text-gray-400 hover:text-gray-600 text-xl leading-none">&times;</button>
            </div>
            <div className="px-6 py-5">
              <label className="block text-sm font-semibold text-gray-700 mb-1">LPO Number <span className="text-red-500">*</span></label>
              <input value={lpoNumber} onChange={e => setLpoNumber(e.target.value)}
                placeholder="e.g. LPO-2026-0042"
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
              <p className="text-xs text-gray-400 mt-2">This will mark the quotation as Accepted and advance the request to Quotation Approved.</p>
            </div>
            <div className="flex gap-3 px-6 pb-5">
              <button onClick={() => setLpoOpen(false)}
                className="flex-1 py-2 text-sm font-medium text-gray-600 bg-gray-100 hover:bg-gray-200 rounded-lg transition-colors">
                Cancel
              </button>
              <button onClick={recordLpo} disabled={!lpoNumber.trim() || lpoLoading}
                className="flex-1 py-2 text-sm font-bold bg-green-600 hover:bg-green-700 text-white rounded-lg disabled:opacity-50 transition-colors">
                {lpoLoading ? 'Saving…' : 'Confirm LPO'}
              </button>
            </div>
          </div>
        </div>
      )}
      {/* ── Decline Quotation / Cancel Request (shared reason modal) ───────── */}
      {reasonModal && (
        <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm">
            <div className="flex items-center justify-between px-6 pt-5 pb-3 border-b border-gray-100">
              <h2 className="text-base font-bold text-gray-800">
                {reasonModal === 'declineQuote' ? 'Decline Quotation' : 'Cancel Service Request'}
              </h2>
              <button onClick={() => setReasonModal(null)} className="text-gray-400 hover:text-gray-600 text-xl leading-none">&times;</button>
            </div>
            <div className="px-6 py-5">
              <label className="block text-sm font-semibold text-gray-700 mb-1">Reason <span className="text-gray-400 font-normal">(optional)</span></label>
              <textarea value={reasonText} onChange={e => setReasonText(e.target.value)} rows={3}
                placeholder={reasonModal === 'declineQuote' ? 'e.g. Price too high, client requested changes…' : 'e.g. Client withdrew the request…'}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
              <p className="text-xs text-gray-400 mt-2">
                {reasonModal === 'declineQuote'
                  ? 'Marks the quotation as declined by the client. You can revise and resend it afterwards.'
                  : 'Cancels the request. This is final and cannot be undone.'}
              </p>
            </div>
            <div className="flex gap-3 px-6 pb-5">
              <button onClick={() => setReasonModal(null)}
                className="flex-1 py-2 text-sm font-medium text-gray-600 bg-gray-100 hover:bg-gray-200 rounded-lg transition-colors">
                Back
              </button>
              <button onClick={reasonModal === 'declineQuote' ? rejectQuotation : cancelRequest} disabled={reasonLoading}
                className={`flex-1 py-2 text-sm font-bold text-white rounded-lg disabled:opacity-50 transition-colors ${
                  reasonModal === 'declineQuote' ? 'bg-orange-600 hover:bg-orange-700' : 'bg-red-600 hover:bg-red-700'}`}>
                {reasonLoading ? 'Saving…' : reasonModal === 'declineQuote' ? 'Confirm Decline' : 'Confirm Cancel'}
              </button>
            </div>
          </div>
        </div>
      )}
      {/* ── Create Assignment modal ───────────────────────────────────────── */}
      {assignOpen && (
        <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg">
            <div className="flex items-center justify-between px-6 pt-5 pb-3 border-b border-gray-100">
              <div>
                <h2 className="text-base font-bold text-gray-800">Create Operations Assignment</h2>
                <p className="text-xs text-gray-400 mt-0.5">
                  {isInLab ? '🔬 In-Lab — instruments brought to Lante laboratory' : '🚗 On-Site — technician travels to client location'}
                </p>
              </div>
              <button onClick={() => setAssignOpen(false)} className="text-gray-400 hover:text-gray-600 text-xl leading-none">&times;</button>
            </div>
            <div className="px-6 py-5 space-y-4">

              {/* Technicians */}
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-2">
                  Technician(s) <span className="text-red-500">*</span>
                </label>
                {assignTechIds.map((tid, i) => (
                  <div key={i} className="flex gap-2 mb-2">
                    <select
                      value={tid}
                      onChange={e => {
                        const user = users.find(u => u.id === e.target.value)
                        const newIds   = [...assignTechIds];   newIds[i]   = e.target.value
                        const newNames = [...assignTechNames]; newNames[i] = user ? `${user.firstName} ${user.lastName}`.trim() : ''
                        setAssignTechIds(newIds); setAssignTechNames(newNames)
                      }}
                      className="flex-1 border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 bg-white"
                    >
                      <option value="">— Select technician —</option>
                      {users.map(u => (
                        <option key={u.id} value={u.id}>{u.firstName} {u.lastName} {u.email ? `(${u.email})` : ''}</option>
                      ))}
                    </select>
                    {assignTechIds.length > 1 && (
                      <button onClick={() => {
                        setAssignTechIds(prev => prev.filter((_, idx) => idx !== i))
                        setAssignTechNames(prev => prev.filter((_, idx) => idx !== i))
                      }} className="text-red-400 hover:text-red-600 px-2 text-lg leading-none">×</button>
                    )}
                  </div>
                ))}
                <button onClick={() => { setAssignTechIds(p => [...p, '']); setAssignTechNames(p => [...p, '']) }}
                  className="text-xs text-amber-600 hover:text-amber-800 font-medium">
                  + Add technician
                </button>
              </div>

              {/* Nature of visit */}
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1">Nature of Visit</label>
                <select value={assignNature} onChange={e => setAssignNature(e.target.value)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 bg-white">
                  {isInLab ? (
                    <>
                      <option value="Calibration">Calibration</option>
                      <option value="Inspection">Inspection</option>
                      <option value="CorrectiveMaintenance">Corrective Maintenance</option>
                      <option value="Other">Other</option>
                    </>
                  ) : (
                    <>
                      <option value="CorrectiveMaintenance">Corrective Maintenance</option>
                      <option value="PreventiveMaintenance">Preventive Maintenance</option>
                      <option value="Calibration">Calibration</option>
                      <option value="Installation">Installation</option>
                      <option value="Inspection">Inspection</option>
                      <option value="Commissioning">Commissioning</option>
                      <option value="Training">Training</option>
                      <option value="Other">Other</option>
                    </>
                  )}
                </select>
              </div>

              {/* Deadline */}
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1">Deadline (optional)</label>
                <input type="date" value={assignDeadline} onChange={e => setAssignDeadline(e.target.value)}
                  min={new Date().toISOString().slice(0, 10)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
              </div>

              {/* Notes */}
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1">Assignment Notes (optional)</label>
                <textarea value={assignNotes} onChange={e => setAssignNotes(e.target.value)} rows={2}
                  placeholder="Any specific instructions for the technician…"
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-amber-400" />
              </div>
            </div>
            <div className="flex gap-3 px-6 pb-5">
              <button onClick={() => setAssignOpen(false)}
                className="flex-1 py-2 text-sm font-medium text-gray-600 bg-gray-100 hover:bg-gray-200 rounded-lg transition-colors">
                Cancel
              </button>
              <button onClick={createAssignment} disabled={assignLoading || assignTechIds.every(x => !x.trim())}
                className="flex-1 py-2 text-sm font-bold bg-cyan-600 hover:bg-cyan-700 text-white rounded-lg disabled:opacity-50 transition-colors">
                {assignLoading ? 'Creating…' : 'Create Assignment'}
              </button>
            </div>
          </div>
        </div>
      )}

    </>
  )
}
