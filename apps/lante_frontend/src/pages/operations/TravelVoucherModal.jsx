import { useState } from 'react'
import { exportTravelVoucherPDF } from '../../utils/assignmentExports.js'

const inputStyle = {
  width: '100%', padding: '8px 10px', borderRadius: 7, border: '1px solid #e5e7eb',
  fontSize: 15, outline: 'none', boxSizing: 'border-box', background: '#fff',
}
const labelStyle = { fontSize: 13, fontWeight: 600, color: '#374151', display: 'block', marginBottom: 4 }

function Field({ label, value, onChange, type = 'text', placeholder }) {
  return (
    <div>
      <label style={labelStyle}>{label}</label>
      <input type={type} value={value} onChange={e => onChange(e.target.value)}
        placeholder={placeholder} style={inputStyle} />
    </div>
  )
}

const makeRow = () => ({
  date: '', journeyDetails: '', carRegNo: '', cc: '', mileage: '', ratePerKm: '',
  faresOrCarAllce: '', hotelAcs: '', meals: '', medical: '', incidentals: '',
})

export default function TravelVoucherModal({ assignment, onClose, onSubmitForm }) {
  const [header, setHeader] = useState({ name: '', dateFrom: '', dateTo: '' })
  const [rows, setRows] = useState([makeRow()])
  const [bottom, setBottom] = useState({
    receivedAmountWords: '',
    signature: '',
    signatureDate: new Date().toLocaleDateString('en-KE', { dateStyle: 'medium' }),
    authorisedBy: '',
    chargedTo: '',
    receiptNo: '',
  })
  const [submitting, setSub] = useState(false)
  const [submitted, setDone] = useState(false)
  const [error, setError] = useState('')

  const setH = (k, v) => setHeader(h => ({ ...h, [k]: v }))
  const setB = (k, v) => setBottom(b => ({ ...b, [k]: v }))
  const setRow = (i, k, v) => setRows(rs => rs.map((r, idx) => idx === i ? { ...r, [k]: v } : r))
  const addRow = () => setRows(rs => [...rs, makeRow()])
  const removeRow = i => setRows(rs => rs.filter((_, idx) => idx !== i))

  const sum = key => rows.reduce((s, r) => s + (parseFloat(r[key]) || 0), 0)
  const fareEtc   = sum('faresOrCarAllce')
  const hotelAc   = sum('hotelAcs')
  const mealsTot  = sum('meals')
  const medTot    = sum('medical')
  const incidsTot = sum('incidentals')
  const grandTotal = fareEtc + hotelAc + mealsTot + medTot + incidsTot

  const fmt = n => n > 0 ? `KES ${n.toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}` : '—'

  const buildFormData = () => ({
    ...header,
    rows,
    bottom,
    totals: { fareEtc, hotelAc, meals: mealsTot, medical: medTot, incids: incidsTot, total: grandTotal },
  })

  const handleSubmit = async () => {
    const filled = rows.filter(r => r.journeyDetails.trim() || r.date.trim())
    if (filled.length === 0) { setError('Please fill in at least one journey.'); return }
    setSub(true); setError('')
    try {
      await onSubmitForm({
        type: 'MaterialRequisition',
        description: JSON.stringify(buildFormData()),
        amount: grandTotal,
        justification: 'Travelling Expenses Voucher',
      })
      setDone(true)
    } catch (e) {
      setError(e?.response?.data?.message ?? 'Submission failed. Please try again.')
    } finally { setSub(false) }
  }

  const handleExport = () => exportTravelVoucherPDF(buildFormData(), assignment)

  const divider = <div style={{ height: 1, background: '#f0f1f3', margin: '20px 0' }} />

  const sectionTitle = (text) => (
    <div style={{ fontSize: 13, fontWeight: 700, color: '#f59e0b', textTransform: 'uppercase', letterSpacing: '0.5px', marginBottom: 14 }}>{text}</div>
  )

  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,.55)', display: 'flex', alignItems: 'flex-start', justifyContent: 'center', zIndex: 300, overflowY: 'auto', padding: '20px 12px' }}>
      <div style={{ background: '#fff', borderRadius: 16, width: '100%', maxWidth: 680, margin: '0 auto' }}>

        {/* Header */}
        <div style={{ background: '#0f1e45', borderRadius: '16px 16px 0 0', padding: '16px 24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div style={{ width: 30, height: 30, background: '#f59e0b', borderRadius: 7, display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 900, color: '#fff', fontSize: 17 }}>L</div>
            <span style={{ color: '#fff', fontWeight: 700, fontSize: 16 }}>Lante</span>
          </div>
          <div style={{ color: '#f59e0b', fontWeight: 800, fontSize: 15, letterSpacing: 0.5 }}>TRAVELLING EXPENSES VOUCHER</div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', color: '#9ca3af', fontSize: 24, cursor: 'pointer', lineHeight: 1 }}>×</button>
        </div>

        <div style={{ padding: '24px 28px', display: 'flex', flexDirection: 'column', gap: 0 }}>

          {/* ── Section 1: Basic Info ── */}
          {sectionTitle('Employee & Trip Details')}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 14, marginBottom: 20 }}>
            <div style={{ gridColumn: '1 / -1' }}>
              <Field label="Employee Name" value={header.name} onChange={v => setH('name', v)} placeholder="Full name" />
            </div>
            <Field label="Date From" value={header.dateFrom} onChange={v => setH('dateFrom', v)} placeholder="e.g. 01 Jan 2026" />
            <Field label="Date To" value={header.dateTo} onChange={v => setH('dateTo', v)} placeholder="e.g. 05 Jan 2026" />
            <Field label="Receipt No." value={bottom.receiptNo} onChange={v => setB('receiptNo', v)} placeholder="Receipt number" />
          </div>

          {divider}

          {/* ── Section 2: Journeys ── */}
          {sectionTitle(`Journey Entries (${rows.length})`)}

          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            {rows.map((r, i) => (
              <div key={i} style={{ background: '#f9fafb', border: '1px solid #f0f1f3', borderRadius: 10, padding: '16px 18px', position: 'relative' }}>
                <div style={{ fontSize: 14, fontWeight: 700, color: '#374151', marginBottom: 12 }}>Journey {i + 1}</div>

                {rows.length > 1 && (
                  <button onClick={() => removeRow(i)}
                    style={{ position: 'absolute', top: 12, right: 12, background: 'none', border: 'none', color: '#ef4444', fontSize: 16, cursor: 'pointer', lineHeight: 1 }}>✕</button>
                )}

                {/* Date + Journey details */}
                <div style={{ display: 'grid', gridTemplateColumns: '140px 1fr', gap: 12, marginBottom: 12 }}>
                  <Field label="Date" value={r.date} onChange={v => setRow(i, 'date', v)} placeholder="DD/MM/YYYY" />
                  <Field label="Full Details of Journey (From → To)" value={r.journeyDetails} onChange={v => setRow(i, 'journeyDetails', v)} placeholder="e.g. Nairobi → Mombasa (client site visit)" />
                </div>

                {/* Car details */}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 12, marginBottom: 12 }}>
                  <Field label="Car Reg. No." value={r.carRegNo} onChange={v => setRow(i, 'carRegNo', v)} placeholder="KAA 000A" />
                  <Field label="C.C." value={r.cc} onChange={v => setRow(i, 'cc', v)} placeholder="1500cc" />
                  <Field label="Mileage (km)" value={r.mileage} onChange={v => setRow(i, 'mileage', v)} type="number" placeholder="0" />
                  <Field label="Rate per KM" value={r.ratePerKm} onChange={v => setRow(i, 'ratePerKm', v)} type="number" placeholder="0.00" />
                </div>

                {/* Expense amounts */}
                <div style={{ marginBottom: 6 }}>
                  <label style={labelStyle}>Expenses</label>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 10 }}>
                    {[
                      ['Fares / Car Allowance', 'faresOrCarAllce'],
                      ['Hotel A/Cs', 'hotelAcs'],
                      ['Meals', 'meals'],
                      ['Medical', 'medical'],
                      ['Incidentals', 'incidentals'],
                    ].map(([lbl, key]) => (
                      <div key={key}>
                        <label style={{ fontSize: 13, fontWeight: 600, color: '#6b7280', display: 'block', marginBottom: 3 }}>{lbl}</label>
                        <input type="number" value={r[key]} onChange={e => setRow(i, key, e.target.value)}
                          placeholder="0.00"
                          style={{ ...inputStyle, fontSize: 14 }} />
                      </div>
                    ))}
                  </div>
                </div>

                {/* Row subtotal */}
                {(() => {
                  const sub = (parseFloat(r.faresOrCarAllce) || 0) + (parseFloat(r.hotelAcs) || 0) +
                    (parseFloat(r.meals) || 0) + (parseFloat(r.medical) || 0) + (parseFloat(r.incidentals) || 0)
                  return sub > 0 ? (
                    <div style={{ fontSize: 13, color: '#6b7280', marginTop: 8, textAlign: 'right' }}>
                      Journey subtotal: <strong style={{ color: '#111827' }}>KES {sub.toLocaleString('en-KE', { minimumFractionDigits: 2 })}</strong>
                    </div>
                  ) : null
                })()}
              </div>
            ))}
          </div>

          <button onClick={addRow} style={{ alignSelf: 'flex-start', marginTop: 12, fontSize: 14, color: '#f59e0b', background: 'none', border: '1px solid #f59e0b', borderRadius: 7, padding: '6px 16px', cursor: 'pointer', fontWeight: 600 }}>
            + Add Journey
          </button>

          {divider}

          {/* ── Section 3: Summary ── */}
          {sectionTitle('Summary')}
          <div style={{ background: '#fffbeb', border: '1px solid #fcd34d', borderRadius: 10, padding: '16px 20px', marginBottom: 20 }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
              {[
                ['Fare Etc', fareEtc],
                ['Hotel A/C', hotelAc],
                ['Meals', mealsTot],
                ['Medical', medTot],
                ["Incidentals", incidsTot],
              ].map(([lbl, val]) => (
                <div key={lbl}>
                  <div style={{ fontSize: 13, fontWeight: 600, color: '#92400e', marginBottom: 2 }}>{lbl}</div>
                  <div style={{ fontSize: 15, fontWeight: 600, color: '#111827' }}>{fmt(val)}</div>
                </div>
              ))}
            </div>
            <div style={{ marginTop: 14, paddingTop: 12, borderTop: '1px solid #fcd34d', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ fontSize: 15, fontWeight: 700, color: '#374151' }}>TOTAL</span>
              <span style={{ fontSize: 20, fontWeight: 900, color: '#0f1e45' }}>
                {grandTotal > 0 ? `KES ${grandTotal.toLocaleString('en-KE', { minimumFractionDigits: 2 })}` : '—'}
              </span>
            </div>
          </div>

          {divider}

          {/* ── Section 4: Authorisation ── */}
          {sectionTitle('Authorisation')}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, marginBottom: 20 }}>
            <div style={{ gridColumn: '1 / -1' }}>
              <label style={labelStyle}>Received the Amount of (in words)</label>
              <textarea value={bottom.receivedAmountWords} onChange={e => setB('receivedAmountWords', e.target.value)}
                rows={2} placeholder="e.g. Kenya Shillings Twelve Thousand Only"
                style={{ ...inputStyle, resize: 'vertical' }} />
            </div>
            <Field label="Signature" value={bottom.signature} onChange={v => setB('signature', v)} placeholder="Name / signature" />
            <Field label="Date" value={bottom.signatureDate} onChange={v => setB('signatureDate', v)} />
            <Field label="Authorised By" value={bottom.authorisedBy} onChange={v => setB('authorisedBy', v)} placeholder="Authoriser name" />
            <Field label="Charged To" value={bottom.chargedTo} onChange={v => setB('chargedTo', v)} placeholder="Department / project" />
          </div>

          {/* ── Action bar ── */}
          {error && <p style={{ color: '#ef4444', fontSize: 14, margin: '0 0 12px' }}>{error}</p>}
          <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
            {submitted ? (
              <>
                <span style={{ fontSize: 15, color: '#22c55e', fontWeight: 600, alignSelf: 'center' }}>✓ Saved</span>
                <button onClick={handleExport}
                  style={{ padding: '9px 18px', borderRadius: 8, border: '1px solid #e5e7eb', background: '#fff', fontSize: 15, fontWeight: 600, cursor: 'pointer' }}>
                  ↓ Export PDF
                </button>
                <button onClick={onClose}
                  style={{ padding: '9px 18px', borderRadius: 8, border: 'none', background: '#f59e0b', color: '#000', fontSize: 15, fontWeight: 600, cursor: 'pointer' }}>
                  Done
                </button>
              </>
            ) : (
              <>
                <button onClick={onClose}
                  style={{ padding: '9px 18px', borderRadius: 8, border: '1px solid #e5e7eb', background: '#fff', fontSize: 15, cursor: 'pointer' }}>
                  Cancel
                </button>
                <button onClick={handleSubmit} disabled={submitting}
                  style={{ padding: '9px 18px', borderRadius: 8, border: 'none', background: '#f59e0b', color: '#000', fontSize: 15, fontWeight: 600, cursor: submitting ? 'default' : 'pointer', opacity: submitting ? 0.7 : 1 }}>
                  {submitting ? 'Saving...' : 'Save & Submit'}
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
