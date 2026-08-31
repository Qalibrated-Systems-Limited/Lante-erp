import { useState } from 'react'
import { exportRequisitionFormA1, exportRequisitionFormA2 } from '../../utils/assignmentExports.js'
import useCompanyBranding from '../../hooks/useCompanyBranding.js'

// ── Type chooser ──────────────────────────────────────────────────────────────

export function RequisitionTypeChooser({ onChoose, onClose }) {
  const branding = useCompanyBranding()
  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,.45)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 200 }}>
      <div style={{ background: '#fff', borderRadius: 16, padding: 28, width: 480 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
          <h2 style={{ margin: 0, fontSize: 16, fontWeight: 700, color: '#111827' }}>New Requisition</h2>
          <button onClick={onClose} style={{ background: 'none', border: 'none', fontSize: 22, cursor: 'pointer', color: '#9ca3af' }}>×</button>
        </div>
        <p style={{ fontSize: 15, color: '#6b7280', margin: '0 0 20px' }}>Choose the requisition form type:</p>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
          <button onClick={() => onChoose('A1')}
            style={{ background: '#fff', border: '2px solid #e5e7eb', borderRadius: 12, padding: '20px 16px', textAlign: 'left', cursor: 'pointer', transition: 'border-color .15s' }}
            onMouseEnter={e => e.currentTarget.style.borderColor = '#f59e0b'}
            onMouseLeave={e => e.currentTarget.style.borderColor = '#e5e7eb'}>
            <div style={{ fontSize: 22, fontWeight: 900, color: '#f59e0b', marginBottom: 6 }}>A-1</div>
            <div style={{ fontSize: 15, fontWeight: 700, color: '#111827', marginBottom: 4 }}>Request Form</div>
            <div style={{ fontSize: 13, color: '#6b7280', lineHeight: 1.5 }}>
              Material request with description, quantity, unit and priority. No cost columns.
            </div>
            <div style={{ fontSize: 13, color: '#9ca3af', marginTop: 8 }}>{branding.docPrefix}-R1</div>
          </button>
          <button onClick={() => onChoose('A2')}
            style={{ background: '#fff', border: '2px solid #e5e7eb', borderRadius: 12, padding: '20px 16px', textAlign: 'left', cursor: 'pointer', transition: 'border-color .15s' }}
            onMouseEnter={e => e.currentTarget.style.borderColor = '#f59e0b'}
            onMouseLeave={e => e.currentTarget.style.borderColor = '#e5e7eb'}>
            <div style={{ fontSize: 22, fontWeight: 900, color: '#f59e0b', marginBottom: 6 }}>A-2</div>
            <div style={{ fontSize: 15, fontWeight: 700, color: '#111827', marginBottom: 4 }}>Costing Form</div>
            <div style={{ fontSize: 13, color: '#6b7280', lineHeight: 1.5 }}>
              Detailed costing with qty, unit price, estimated cost and purchase actual cost / variation.
            </div>
            <div style={{ fontSize: 13, color: '#9ca3af', marginTop: 8 }}>{branding.docPrefix}/QP/003/MRF</div>
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Cell input ────────────────────────────────────────────────────────────────

function CInput({ value, onChange, placeholder, align = 'left', type = 'text' }) {
  return (
    <input
      type={type}
      value={value}
      onChange={e => onChange(e.target.value)}
      placeholder={placeholder}
      style={{ border: 'none', outline: 'none', width: '100%', fontSize: 13, padding: '2px', background: 'transparent', textAlign: align }}
    />
  )
}

// ── Inline header input ───────────────────────────────────────────────────────

function HInput({ value, onChange, underline = false, boxed = false, style }) {
  return (
    <input
      value={value}
      onChange={e => onChange(e.target.value)}
      style={{
        border: 'none',
        borderBottom: underline ? '1px solid #888' : 'none',
        outline: 'none',
        fontSize: 13,
        background: 'transparent',
        ...(boxed ? { border: '1px solid #000', padding: '2px 6px' } : {}),
        ...style,
      }}
    />
  )
}

// ── Shared table styles ───────────────────────────────────────────────────────

const TD = { border: '1px solid #000', padding: '4px 6px', fontSize: 13 }
const TH = { ...TD, background: '#c8c8c8', fontWeight: 'bold', textAlign: 'center', fontSize: 13, verticalAlign: 'middle' }

// ── Main form modal ───────────────────────────────────────────────────────────

export default function RequisitionFormModal({ formType, assignment, onClose, onSubmitForm }) {
  const branding = useCompanyBranding()
  const ref = assignment.id.slice(0, 8).toUpperCase()

  const [header, setHeader] = useState({
    projectName:   assignment.title,
    projectNumber: ref,
    storeName:     '',
    approvedBy:    '',
    preparedBy:    '',
    checkedBy:     '',
    date:          new Date().toLocaleDateString('en-KE', { dateStyle: 'medium' }),
  })
  const setH = (k, v) => setHeader(h => ({ ...h, [k]: v }))

  const makeRowA1 = () => ({ description: '', qty: '', unit: '', priority: 'Normal', remarks: '' })
  const makeRowA2 = () => ({ description: '', qty: '', unit: '', amountForEach: '', estimatedCost: '', actualCost: '', variation: '' })

  const [rows, setRows]       = useState(Array(6).fill(null).map(formType === 'A1' ? makeRowA1 : makeRowA2))
  const [submitting, setSub]  = useState(false)
  const [submitted, setDone]  = useState(false)
  const [error, setError]     = useState('')

  const setRow = (i, k, v) => setRows(rs => rs.map((r, idx) => idx === i ? { ...r, [k]: v } : r))
  const addRow    = () => setRows(rs => [...rs, formType === 'A1' ? makeRowA1() : makeRowA2()])
  const removeRow = i  => setRows(rs => rs.filter((_, idx) => idx !== i))

  const filledRows    = rows.filter(r => r.description.trim())
  const totalEstimated = rows.reduce((s, r) => s + (parseFloat(r.estimatedCost) || 0), 0)
  const totalActual    = rows.reduce((s, r) => s + (parseFloat(r.actualCost)    || 0), 0)

  const buildFormData = () => ({
    formType,
    ...header,
    items: rows.map((r, i) => ({ sNo: i + 1, ...r })),
  })

  const handleSubmit = async () => {
    if (filledRows.length === 0) return
    setSub(true); setError('')
    try {
      const formData = buildFormData()
      await onSubmitForm({
        type:          'MaterialRequisition',
        description:   JSON.stringify(formData),
        amount:        formType === 'A2' ? totalEstimated : 0,
        justification: formType === 'A1'
          ? `Material Requisition Form A-1 (${branding.docPrefix}-R1)`
          : `Material Requisition Form A-2 (${branding.docPrefix}/QP/003/MRF)`,
      })
      setDone(true)
    } catch (e) {
      setError(e?.response?.data?.message ?? 'Submission failed. Please try again.')
    } finally { setSub(false) }
  }

  const handleExport = () => {
    const fd = buildFormData()
    if (formType === 'A1') exportRequisitionFormA1(fd, assignment, branding.docPrefix)
    else                   exportRequisitionFormA2(fd, assignment, branding.docPrefix)
  }

  // ── Form A-1 table ──────────────────────────────────────────────────────────
  const tableA1 = (
    <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: 12 }}>
      <thead>
        <tr>
          <th style={{ ...TH, width: '6%'  }}>S/No</th>
          <th style={{ ...TH, width: '31%' }}>Material Description</th>
          <th style={{ ...TH, width: '14%' }}>Quantity<br/>Required</th>
          <th style={{ ...TH, width: '12%' }}>Unit of<br/>Measure</th>
          <th style={{ ...TH, width: '16%' }}>Priority<br/><span style={{ fontWeight: 'normal', fontSize: 9 }}>Urgent/Normal</span></th>
          <th style={{ ...TH, width: '21%' }}>Remarks</th>
          <th style={{ border: 'none', width: '4%', background: 'transparent' }} />
        </tr>
      </thead>
      <tbody>
        {rows.map((r, i) => (
          <tr key={i}>
            <td style={{ ...TD, textAlign: 'center', color: '#888', fontSize: 13 }}>{i + 1}</td>
            <td style={TD}><CInput value={r.description} onChange={v => setRow(i, 'description', v)} /></td>
            <td style={TD}><CInput value={r.qty} onChange={v => setRow(i, 'qty', v)} align="center" /></td>
            <td style={TD}><CInput value={r.unit} onChange={v => setRow(i, 'unit', v)} align="center" /></td>
            <td style={{ ...TD, textAlign: 'center' }}>
              <select value={r.priority} onChange={e => setRow(i, 'priority', e.target.value)}
                style={{ border: 'none', outline: 'none', background: 'transparent', fontSize: 13, width: '100%', cursor: 'pointer' }}>
                <option>Normal</option>
                <option>Urgent</option>
              </select>
            </td>
            <td style={TD}><CInput value={r.remarks} onChange={v => setRow(i, 'remarks', v)} /></td>
            <td style={{ border: 'none', background: 'transparent', textAlign: 'center' }}>
              {rows.length > 1 && (
                <button type="button" onClick={() => removeRow(i)}
                  style={{ background: 'none', border: 'none', color: '#ef4444', cursor: 'pointer', fontSize: 16, lineHeight: 1, padding: '0 4px' }}>×</button>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )

  // ── Form A-2 table ──────────────────────────────────────────────────────────
  const tableA2 = (
    <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: 12 }}>
      <thead>
        <tr>
          <th style={{ ...TH, width: '5%'  }} rowSpan={2}>S/No</th>
          <th style={{ ...TH, width: '27%' }} rowSpan={2}>Description</th>
          <th style={{ ...TH, width: '7%'  }} rowSpan={2}>Qty</th>
          <th style={{ ...TH, width: '7%'  }} rowSpan={2}>Unit</th>
          <th style={{ ...TH, width: '12%' }} rowSpan={2}>Amount<br/>for Each</th>
          <th style={{ ...TH, width: '13%' }} rowSpan={2}>Estimated<br/>Cost</th>
          <th style={{ ...TH, width: '25%' }} colSpan={2}>
            Purchase Use Only<br/>
            <span style={{ fontWeight: 'normal', fontSize: 9 }}>(Attach RECEIPTS)</span>
          </th>
          <th style={{ border: 'none', width: '4%', background: 'transparent' }} rowSpan={2} />
        </tr>
        <tr>
          <th style={{ ...TH, width: '12%' }}>Actual Cost</th>
          <th style={{ ...TH, width: '13%' }}>Variation</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((r, i) => (
          <tr key={i}>
            <td style={{ ...TD, textAlign: 'center', color: '#888', fontSize: 13 }}>{i + 1}</td>
            <td style={TD}><CInput value={r.description} onChange={v => setRow(i, 'description', v)} /></td>
            <td style={TD}><CInput value={r.qty} onChange={v => setRow(i, 'qty', v)} align="center" /></td>
            <td style={TD}><CInput value={r.unit} onChange={v => setRow(i, 'unit', v)} align="center" /></td>
            <td style={TD}><CInput value={r.amountForEach} onChange={v => setRow(i, 'amountForEach', v)} placeholder="0.00" align="right" type="number" /></td>
            <td style={TD}><CInput value={r.estimatedCost} onChange={v => setRow(i, 'estimatedCost', v)} placeholder="0.00" align="right" type="number" /></td>
            <td style={{ ...TD, background: '#fafafa' }}><CInput value={r.actualCost} onChange={v => setRow(i, 'actualCost', v)} placeholder="0.00" align="right" type="number" /></td>
            <td style={{ ...TD, background: '#fafafa' }}><CInput value={r.variation} onChange={v => setRow(i, 'variation', v)} placeholder="0.00" align="right" type="number" /></td>
            <td style={{ border: 'none', background: 'transparent', textAlign: 'center' }}>
              {rows.length > 1 && (
                <button type="button" onClick={() => removeRow(i)}
                  style={{ background: 'none', border: 'none', color: '#ef4444', cursor: 'pointer', fontSize: 16, lineHeight: 1, padding: '0 4px' }}>×</button>
              )}
            </td>
          </tr>
        ))}
        {/* Totals */}
        <tr style={{ background: '#f9fafb' }}>
          <td colSpan={5} style={{ ...TD, textAlign: 'right', fontWeight: 'bold', fontSize: 13 }}>Total</td>
          <td style={{ ...TD, fontWeight: 'bold', textAlign: 'right' }}>
            {totalEstimated > 0 ? `KES ${totalEstimated.toLocaleString('en-KE')}` : ''}
          </td>
          <td style={{ ...TD, background: '#f0f0f0', fontWeight: 'bold', textAlign: 'right' }}>
            {totalActual > 0 ? `KES ${totalActual.toLocaleString('en-KE')}` : ''}
          </td>
          <td style={{ ...TD, background: '#f0f0f0' }} />
          <td style={{ border: 'none' }} />
        </tr>
      </tbody>
    </table>
  )

  // ── Render ──────────────────────────────────────────────────────────────────
  return (
    <div style={{
      position: 'fixed', inset: 0, background: 'rgba(0,0,0,.5)',
      display: 'flex', alignItems: 'flex-start', justifyContent: 'center',
      zIndex: 200, overflowY: 'auto', padding: '24px 16px',
    }}>
      <div style={{ background: '#fff', borderRadius: 16, width: '100%', maxWidth: 920, boxShadow: '0 20px 60px rgba(0,0,0,.2)', marginBottom: 24 }}>

        {/* Modal title bar */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '16px 24px', borderBottom: '1px solid #f0f1f3' }}>
          <div>
            <h2 style={{ margin: 0, fontSize: 16, fontWeight: 700, color: '#111827' }}>
              Material Requisition Form {formType === 'A1' ? 'A-1' : 'A-2'}
            </h2>
            <p style={{ margin: '2px 0 0', fontSize: 13, color: '#9ca3af' }}>
              {formType === 'A1' ? `${branding.docPrefix}-R1 · Request Form` : `${branding.docPrefix}/QP/003/MRF · Costing Form`}
            </p>
          </div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', fontSize: 22, cursor: 'pointer', color: '#9ca3af' }}>×</button>
        </div>

        {/* Paper form area */}
        <div style={{ padding: '24px 28px', fontFamily: 'Arial, sans-serif' }}>

          {/* Brand header row */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 }}>
            <img src={branding.logoUrl || "/qc-logo.png"} alt={branding.displayName} style={{ width: 50, height: 50, objectFit: 'contain' }}
              onError={e => { e.target.style.display = 'none' }} />
            <div style={{ flex: 1, textAlign: 'center', margin: '0 16px' }}>
              <span style={{ display: 'inline-block', background: '#f59e0b', padding: '6px 22px', borderRadius: 3, fontSize: 14, fontWeight: 700, color: '#000' }}>
                MATERIAL REQUISITION FORM {formType === 'A1' ? 'A-1' : 'A-2'}
              </span>
            </div>
            <div style={{ textAlign: 'right' }}>
              <div style={{ background: '#111827', padding: '4px 12px', borderRadius: 3, marginBottom: 6, display: 'inline-block' }}>
                <span style={{ fontSize: 13, fontWeight: 700, color: '#fff' }}>Inventing and Making Happen</span>
              </div>
              <div style={{ fontSize: 18, fontWeight: 900, color: '#f59e0b' }}>{branding.docPrefix}/QP/003/MRF</div>
            </div>
          </div>

          {/* Header fields — A-1 */}
          {formType === 'A1' && (
            <div style={{ marginBottom: 12 }}>
              <div style={{ display: 'flex', gap: 24, marginBottom: 10 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span style={{ fontSize: 13, fontWeight: 700, whiteSpace: 'nowrap' }}>Project Name</span>
                  <div style={{ border: '1px solid #000', padding: '2px 8px', minWidth: 160 }}>
                    <HInput value={header.projectName} onChange={v => setH('projectName', v)} style={{ width: 160 }} />
                  </div>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span style={{ fontSize: 13, fontWeight: 700, whiteSpace: 'nowrap' }}>Project Number</span>
                  <div style={{ border: '1px solid #000', padding: '2px 8px', minWidth: 90 }}>
                    <HInput value={header.projectNumber} onChange={v => setH('projectNumber', v)} style={{ width: 90 }} />
                  </div>
                </div>
              </div>
              <div style={{ textAlign: 'center', marginBottom: 6 }}>
                <span style={{ fontSize: 13, fontWeight: 700 }}>Store Name: &nbsp;</span>
                <HInput value={header.storeName} onChange={v => setH('storeName', v)} underline style={{ minWidth: 220 }} />
              </div>
              <div style={{ textAlign: 'center' }}>
                <span style={{ fontSize: 13, fontWeight: 700 }}>Approved By: &nbsp;</span>
                <HInput value={header.approvedBy} onChange={v => setH('approvedBy', v)} underline style={{ minWidth: 220 }} />
              </div>
            </div>
          )}

          {/* Header fields — A-2 */}
          {formType === 'A2' && (
            <div style={{ marginBottom: 12 }}>
              <div style={{ display: 'flex', gap: 24, marginBottom: 10 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span style={{ fontSize: 13, fontWeight: 700, whiteSpace: 'nowrap' }}>Project Name</span>
                  <div style={{ border: '1px solid #000', padding: '2px 8px', minWidth: 150 }}>
                    <HInput value={header.projectName} onChange={v => setH('projectName', v)} style={{ width: 150 }} />
                  </div>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span style={{ fontSize: 13, fontWeight: 700, whiteSpace: 'nowrap' }}>Project Number</span>
                  <div style={{ border: '1px solid #000', padding: '2px 8px', minWidth: 90 }}>
                    <HInput value={header.projectNumber} onChange={v => setH('projectNumber', v)} style={{ width: 90 }} />
                  </div>
                </div>
              </div>
              <div style={{ textAlign: 'center', marginBottom: 8 }}>
                <span style={{ fontSize: 13, fontWeight: 700 }}>Store Name: &nbsp;</span>
                <HInput value={header.storeName} onChange={v => setH('storeName', v)} underline style={{ minWidth: 200 }} />
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 6 }}>
                <div>
                  <span style={{ fontSize: 13, fontWeight: 700 }}>Prepared by: &nbsp;</span>
                  <HInput value={header.preparedBy} onChange={v => setH('preparedBy', v)} underline style={{ minWidth: 130 }} />
                </div>
                <div>
                  <span style={{ fontSize: 13, fontWeight: 700 }}>Approved By: &nbsp;</span>
                  <HInput value={header.approvedBy} onChange={v => setH('approvedBy', v)} underline style={{ minWidth: 130 }} />
                </div>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <div>
                  <span style={{ fontSize: 13, fontWeight: 700 }}>Checked By: &nbsp;</span>
                  <HInput value={header.checkedBy} onChange={v => setH('checkedBy', v)} underline style={{ minWidth: 130 }} />
                </div>
                <div>
                  <span style={{ fontSize: 13, fontWeight: 700 }}>Date: &nbsp;</span>
                  <HInput value={header.date} onChange={v => setH('date', v)} underline style={{ minWidth: 120 }} />
                </div>
              </div>
            </div>
          )}

          {/* Table */}
          {formType === 'A1' ? tableA1 : tableA2}

          {/* Add row */}
          <div style={{ marginTop: 8 }}>
            <button type="button" onClick={addRow}
              style={{ background: 'none', border: '1px dashed #d1d5db', borderRadius: 6, padding: '4px 14px', fontSize: 13, color: '#6b7280', cursor: 'pointer' }}>
              + Add Row
            </button>
          </div>

          {/* Footer bar */}
          <div style={{ background: '#f59e0b', padding: '5px 10px', marginTop: 16, borderRadius: 3, textAlign: 'center' }}>
            <span style={{ fontSize: 9, color: '#000' }}>
              Birdi Singh Complex 1st Floor, off Mombasa Road, P.O BOX 34463 - 00100, Nairobi, Kenya&nbsp;·&nbsp;
              Tel: +254 714 999 996, +254 756 999 996&nbsp;·&nbsp;info@qalibrated.co.ke | www.qalibrated.co.ke
            </span>
          </div>
        </div>

        {/* Action bar */}
        <div style={{ padding: '14px 24px', borderTop: '1px solid #f0f1f3', display: 'flex', gap: 10, justifyContent: 'flex-end', alignItems: 'center' }}>
          {error && <span style={{ fontSize: 14, color: '#ef4444', flex: 1 }}>{error}</span>}
          {submitted ? (
            <>
              <span style={{ fontSize: 15, color: '#22c55e', fontWeight: 600, marginRight: 8 }}>✓ Saved successfully</span>
              <button onClick={handleExport}
                style={{ background: '#f59e0b', color: '#000', border: 'none', borderRadius: 8, padding: '9px 18px', fontSize: 15, fontWeight: 600, cursor: 'pointer' }}>
                ↓ Export PDF
              </button>
              <button onClick={onClose}
                style={{ background: '#111827', color: '#fff', border: 'none', borderRadius: 8, padding: '9px 18px', fontSize: 15, fontWeight: 600, cursor: 'pointer' }}>
                Done
              </button>
            </>
          ) : (
            <>
              <button onClick={onClose}
                style={{ background: '#fff', color: '#374151', border: '1px solid #e5e7eb', borderRadius: 8, padding: '9px 18px', fontSize: 15, cursor: 'pointer' }}>
                Cancel
              </button>
              <button onClick={handleSubmit} disabled={filledRows.length === 0 || submitting}
                style={{ background: '#f59e0b', color: '#000', border: 'none', borderRadius: 8, padding: '9px 18px', fontSize: 15, fontWeight: 600, cursor: filledRows.length === 0 || submitting ? 'default' : 'pointer', opacity: filledRows.length === 0 || submitting ? 0.55 : 1 }}>
                {submitting ? 'Saving…' : 'Save & Submit'}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
