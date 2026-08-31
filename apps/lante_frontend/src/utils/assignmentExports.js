import { jsPDF } from 'jspdf'
import autoTable from 'jspdf-autotable'
import * as XLSX from 'xlsx'
import { kes } from '../theme/tokens.js'
import api from '../api/axios.js'

// ── Brand palette ─────────────────────────────────────────────────────────────
const AMBER      = [245, 158,  11]
const AMBER_DARK = [180, 110,   0]
const BLACK      = [ 17,  24,  39]
const DARK       = [ 31,  41,  55]
const MUTED      = [107, 114, 128]
const WHITE      = [255, 255, 255]
const LIGHT      = [249, 250, 251]
const BORDER     = [200, 200, 200]
const LABEL_BG   = [245, 245, 245]

// ── Status badge colours ──────────────────────────────────────────────────────
const STATUS_COLORS = {
  Pending:    AMBER,
  Accepted:   [22,  163,  74],
  InProgress: [99,  102, 241],
  Completed:  [22,  163,  74],
  Cancelled:  [239,  68,  68],
}

// ── Shared utilities ──────────────────────────────────────────────────────────

async function loadLogo() {
  try {
    let src = '/qc-logo.png'
    try {
      const res = await api.get('/api/v1/system-settings')
      const logoSetting = (res.data?.data ?? []).find(s => s.key === 'branding.logo_url')
      if (logoSetting?.value) src = logoSetting.value
    } catch { /* fall back to the static default */ }

    const resp = await fetch(src)
    if (!resp.ok) return null
    const blob = await resp.blob()
    return new Promise(resolve => {
      const reader = new FileReader()
      reader.onloadend = () => resolve(reader.result)
      reader.onerror   = () => resolve(null)
      reader.readAsDataURL(blob)
    })
  } catch { return null }
}

function fmtNow() {
  return new Date().toLocaleString('en-KE', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

export function fmtDate(d) {
  return d ? new Date(d).toLocaleDateString('en-KE', { dateStyle: 'medium' }) : '—'
}

// Delegates to the single money formatter in theme/tokens.js (#205).
function fmtKES(n) {
  return kes(n)
}

function parseReqForm(description) {
  try {
    const p = JSON.parse(description)
    if (p.formType && Array.isArray(p.items)) return p
  } catch {}
  return null
}

function reqFormLabel(description) {
  const p = parseReqForm(description)
  if (!p) return description
  const filled = p.items.filter(i => i.description?.trim()).length
  return `Form ${p.formType === 'A1' ? 'A-1' : 'A-2'} · ${p.projectName || ''}${filled ? ` · ${filled} item${filled !== 1 ? 's' : ''}` : ''}`
}

function sectionTitle(doc, label, y, W) {
  doc.setFontSize(9)
  doc.setFont('helvetica', 'bold')
  doc.setTextColor(...AMBER_DARK)
  const tw = doc.getTextWidth(label)
  doc.text(label, W / 2, y, { align: 'center' })
  doc.setDrawColor(...AMBER_DARK)
  doc.setLineWidth(0.4)
  doc.line(W / 2 - tw / 2, y + 0.8, W / 2 + tw / 2, y + 0.8)
  return y + 6
}

function tableHeader(doc, label, y, W, M = 14) {
  doc.setFillColor(...AMBER)
  doc.roundedRect(M, y, W - M * 2, 6.5, 1.5, 1.5, 'F')
  doc.setTextColor(...BLACK)
  doc.setFontSize(8)
  doc.setFont('helvetica', 'bold')
  doc.text(label, W / 2, y + 4.5, { align: 'center' })
  return y + 6.5
}

function textBlock(doc, text, y, W, M = 14, maxLines = 14) {
  const lines  = doc.splitTextToSize(text, W - M * 2 - 12)
  const shown  = lines.slice(0, maxLines)
  const blockH = shown.length * 4.8 + 10

  doc.setFillColor(255, 255, 255)
  doc.setDrawColor(...BORDER)
  doc.setLineWidth(0.3)
  doc.roundedRect(M, y, W - M * 2, blockH, 1.5, 1.5, 'FD')
  doc.setFillColor(...AMBER)
  doc.roundedRect(M, y, 3, blockH, 1.5, 1.5, 'F')
  doc.setTextColor(...DARK)
  doc.setFontSize(8)
  doc.setFont('helvetica', 'normal')
  doc.text(shown, M + 7, y + 6)

  if (lines.length > maxLines) {
    doc.setFontSize(7)
    doc.setTextColor(...MUTED)
    doc.text('… (truncated)', M + 7, y + blockH - 3)
  }
  return y + blockH + 7
}

function pageFooter(doc, logoB64, W, H, companyName = 'Qalibrated Systems') {
  const y = H - 14
  doc.setDrawColor(...BORDER)
  doc.setLineWidth(0.3)
  doc.line(14, y, W - 14, y)

  if (logoB64) {
    try { doc.addImage(logoB64, 'PNG', 14, y + 2, 8, 8) } catch { /* ignore */ }
  }
  doc.setFontSize(7.5)
  doc.setFont('helvetica', 'bold')
  doc.setTextColor(...BLACK)
  doc.text(`Powered by ${companyName}`, logoB64 ? 25 : 14, y + 6.5)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...MUTED)
  doc.text('www.qalibrated.co.ke', logoB64 ? 25 : 14, y + 10)

  const tagW = 38, tagH = 7
  doc.setFillColor(...AMBER)
  doc.roundedRect(W - 14 - tagW, y + 3, tagW, tagH, 1.5, 1.5, 'F')
  doc.setTextColor(...BLACK)
  doc.setFontSize(7)
  doc.setFont('helvetica', 'bold')
  doc.text('Inventing and Making Happen', W - 14 - tagW / 2, y + 7.3, { align: 'center' })
}

function pageHeader(doc, logoB64, statusLabel, statusColor, W, M, companyName = 'QALIBRATED SYSTEMS LIMITED') {
  if (logoB64) {
    try { doc.addImage(logoB64, 'PNG', M, 8, 16, 16) } catch { /* ignore */ }
  } else {
    doc.setFillColor(...AMBER)
    doc.circle(M + 8, 16, 8, 'F')
    doc.setTextColor(...BLACK)
    doc.setFontSize(10)
    doc.setFont('helvetica', 'bold')
    doc.text('Q', M + 8, 19.5, { align: 'center' })
  }

  doc.setTextColor(...BLACK)
  doc.setFontSize(13)
  doc.setFont('helvetica', 'bold')
  doc.text(companyName, M + 20, 13)
  doc.setFontSize(8)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...MUTED)
  doc.text('PO BOX 34463-00100, NAIROBI', M + 20, 18.5)
  doc.setFontSize(8)
  doc.setTextColor(...MUTED)
  doc.text(fmtNow(), W - M, 13, { align: 'right' })

  if (statusLabel) {
    const sLabel = statusLabel.toUpperCase()
    const sW     = doc.getTextWidth(sLabel) + 7
    doc.setFillColor(...(statusColor ?? AMBER))
    doc.roundedRect(W - M - sW, 17, sW, 6, 1.5, 1.5, 'F')
    doc.setTextColor(...WHITE)
    doc.setFontSize(7)
    doc.setFont('helvetica', 'bold')
    doc.text(sLabel, W - M - sW / 2, 21.2, { align: 'center' })
  }

  doc.setDrawColor(...BORDER)
  doc.setLineWidth(0.3)
  doc.line(M, 27, W - M, 27)
  doc.setFillColor(...AMBER)
  doc.rect(M, 27, W - M * 2, 1, 'F')
  return 36
}

function finalize(doc, logoB64, W, H, filename, companyName) {
  const total = doc.getNumberOfPages()
  for (let i = 1; i <= total; i++) {
    doc.setPage(i)
    pageFooter(doc, logoB64, W, H, companyName)
    doc.setFontSize(7.5)
    doc.setTextColor(...MUTED)
    doc.setFont('helvetica', 'normal')
    doc.text(`Page ${i} of ${total}`, W / 2, H - 5, { align: 'center' })
  }
  doc.save(filename)
}

const gridStyle = {
  styles:         { fontSize: 8, cellPadding: { top: 3, bottom: 3, left: 3.5, right: 3.5 }, valign: 'top', overflow: 'linebreak', lineColor: BORDER, lineWidth: 0.3 },
  tableLineColor: BORDER,
  tableLineWidth: 0.3,
  theme:          'grid',
}

const listHeadStyle = { fillColor: AMBER, textColor: BLACK, fontStyle: 'bold', fontSize: 8 }

// ── Excel header rows helper ──────────────────────────────────────────────────

function xlsxHeader(title, ref, companyName = 'QALIBRATED SYSTEMS LIMITED') {
  return [
    [companyName, '', '', '', '', '', '', ''],
    [title, ref ? `Ref: ${ref}` : '', '', '', '', fmtNow(), '', ''],
    [],
  ]
}

// ── PDF line items sub-table helper ──────────────────────────────────────────

function addPdfLineItems(doc, items, y, W, M, label, head, rowFn, colStyles) {
  if (!items || items.length === 0) return y
  const H = doc.internal.pageSize.getHeight()
  if (y > H - 50) { doc.addPage(); y = 20 }
  doc.setFontSize(7.5)
  doc.setFont('helvetica', 'bold')
  doc.setTextColor(...MUTED)
  doc.text(label, M + 4, y + 3)
  y += 6
  autoTable(doc, {
    ...gridStyle,
    startY: y,
    head: [head],
    headStyles: { fillColor: DARK, textColor: WHITE, fontStyle: 'bold', fontSize: 7 },
    body: items.map(rowFn),
    columnStyles: colStyles,
    margin: { left: M + 4, right: M + 4 },
  })
  return doc.lastAutoTable.finalY + 4
}

// ── Parse DetailsJson key-value pairs ─────────────────────────────────────────

function parseDetailsJson(json) {
  if (!json) return []
  try {
    const obj = JSON.parse(json)
    if (!obj || typeof obj !== 'object') return []
    return Object.entries(obj)
      .filter(([, v]) => v !== null && v !== undefined && v !== '')
      .map(([k, v]) => [
        k.replace(/([A-Z])/g, ' $1').trim().toUpperCase(),
        typeof v === 'object' ? JSON.stringify(v) : String(v),
      ])
  } catch { return [] }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ASSIGNMENTS LIST
// ═══════════════════════════════════════════════════════════════════════════════

export async function exportAssignmentsPDF(assignments, companyName) {
  const logoB64 = await loadLogo()
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()
  const M = 14

  let y = pageHeader(doc, logoB64, null, null, W, M, companyName)
  y = sectionTitle(doc, 'ASSIGNMENTS', y, W)
  doc.setFontSize(7.5)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...MUTED)
  doc.text(`${assignments.length} record${assignments.length !== 1 ? 's' : ''}`, W / 2, y, { align: 'center' })
  y += 6

  autoTable(doc, {
    ...gridStyle,
    startY: y,
    head: [['TITLE', 'LOCATION', 'DEPARTMENT', 'NATURE OF VISIT', 'SERVICE TYPE', 'PRIORITY', 'STATUS', 'TECHNICIANS', 'CREATED']],
    headStyles: listHeadStyle,
    alternateRowStyles: { fillColor: LIGHT },
    body: assignments.map(a => [
      a.title,
      a.locationName ?? '—',
      a.departmentType,
      a.natureOfVisit,
      a.serviceType ?? '—',
      a.priority,
      a.status,
      (a.technicians ?? []).map(t => t.userName).join(', ') || 'Unassigned',
      fmtDate(a.createdAt),
    ]),
    margin: { left: M, right: M },
  })

  finalize(doc, logoB64, W, H, 'assignments.pdf', companyName)
}

export function exportAssignmentsExcel(assignments, companyName) {
  const rows = [
    ...xlsxHeader('ASSIGNMENTS', undefined, companyName),
    ['Title', 'Location', 'Department', 'Nature of Visit', 'Service Type', 'Source Type', 'Priority', 'Status', 'Technicians', 'Created', 'Deadline', 'Description'],
    ...assignments.map(a => [
      a.title,
      a.locationName ?? '',
      a.departmentType,
      a.natureOfVisit,
      a.serviceType ?? '',
      a.sourceType ?? '',
      a.priority,
      a.status,
      (a.technicians ?? []).map(t => t.userName).join(', '),
      fmtDate(a.createdAt),
      a.deadline ? fmtDate(a.deadline) : '',
      a.description ?? '',
    ]),
  ]
  const ws = XLSX.utils.aoa_to_sheet(rows)
  ws['!cols'] = [
    { wch: 35 }, { wch: 22 }, { wch: 16 }, { wch: 18 }, { wch: 18 }, { wch: 14 },
    { wch: 12 }, { wch: 14 }, { wch: 28 }, { wch: 14 }, { wch: 14 }, { wch: 40 },
  ]
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Assignments')
  XLSX.writeFile(wb, 'assignments.xlsx')
}

// ═══════════════════════════════════════════════════════════════════════════════
// OVERVIEW
// ═══════════════════════════════════════════════════════════════════════════════

export async function exportOverviewPDF(assignment, companyName) {
  const logoB64 = await loadLogo()
  const doc = new jsPDF({ unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()
  const M = 14

  const ref = assignment.id.slice(0, 8).toUpperCase()
  const sBg = STATUS_COLORS[assignment.status] ?? AMBER

  let y = pageHeader(doc, logoB64, assignment.status, sBg, W, M, companyName)
  y = sectionTitle(doc, 'ASSIGNMENT DETAILS', y, W)

  doc.setFontSize(9)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...DARK)
  doc.text(assignment.title, W / 2, y, { align: 'center' })
  y += 8

  y = tableHeader(doc, 'ASSIGNMENT DETAILS', y, W, M)
  autoTable(doc, {
    ...gridStyle,
    startY: y,
    body: [
      ['ASSIGNMENT REF', ref,                                        'STATUS',        assignment.status ?? '—'],
      ['DEPARTMENT',     assignment.departmentType,                  'PRIORITY',      assignment.priority ?? '—'],
      ['NATURE OF VISIT',assignment.natureOfVisit,                   'SERVICE TYPE',  assignment.serviceType ?? '—'],
      ['SOURCE',         assignment.sourceType ?? '—',               'LOCATION',      assignment.locationName ?? '—'],
      ['ADDRESS',        assignment.locationAddress ?? '—',          'CREATED',       fmtDate(assignment.createdAt)],
      ['TECHNICIANS',    (assignment.technicians ?? []).map(t => t.userName).join(', ') || 'Unassigned',
       'DEADLINE',       fmtDate(assignment.deadline)],
      ...(assignment.startedAt || assignment.completedAt
        ? [['STARTED', fmtDate(assignment.startedAt), 'COMPLETED', fmtDate(assignment.completedAt)]]
        : []),
      ...(assignment.cancelledAt
        ? [['CANCELLED AT', fmtDate(assignment.cancelledAt), 'CANCELLATION REASON', assignment.cancellationReason ?? '—']]
        : []),
      ...(assignment.linkedProjectId
        ? [['LINKED PROJECT', assignment.linkedProjectId, 'LINKED AT', fmtDate(assignment.linkedAt)]]
        : []),
      ...(assignment.linkedBy ? [['LINKED BY', assignment.linkedBy, '', '']] : []),
    ],
    columnStyles: {
      0: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 38 },
      1: { textColor: DARK,                                           cellWidth: 57 },
      2: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 38 },
      3: { textColor: DARK,                                           cellWidth: 53 },
    },
    margin: { left: M, right: M },
  })
  y = doc.lastAutoTable.finalY + 7

  if (assignment.description) {
    if (y > H - 60) { doc.addPage(); y = 20 }
    y = tableHeader(doc, 'DESCRIPTION', y, W, M)
    y = textBlock(doc, assignment.description, y, W, M)
  }

  if (assignment.notes) {
    if (y > H - 50) { doc.addPage(); y = 20 }
    y = tableHeader(doc, 'NOTES', y, W, M)
    y = textBlock(doc, assignment.notes, y, W, M)
  }

  finalize(doc, logoB64, W, H, `QC-ASSIGNMENT-${ref}-overview.pdf`, companyName)
}

export function exportOverviewExcel(assignment, companyName) {
  const ref = assignment.id.slice(0, 8).toUpperCase()
  const rows = [
    ...xlsxHeader('ASSIGNMENT OVERVIEW', ref, companyName),
    ['Field', 'Value'],
    ['Reference', ref],
    ['Title', assignment.title],
    ['Department', assignment.departmentType],
    ['Nature of Visit', assignment.natureOfVisit],
    ['Service Type', assignment.serviceType ?? ''],
    ['Source Type', assignment.sourceType ?? ''],
    ['Priority', assignment.priority],
    ['Status', assignment.status],
    ['Location', assignment.locationName ?? ''],
    ['Address', assignment.locationAddress ?? ''],
    ['Technicians', (assignment.technicians ?? []).map(t => t.userName).join(', ')],
    ['Created', fmtDate(assignment.createdAt)],
    ['Deadline', fmtDate(assignment.deadline)],
    ['Started', fmtDate(assignment.startedAt)],
    ['Completed', fmtDate(assignment.completedAt)],
    ...(assignment.cancelledAt ? [
      ['Cancelled At', fmtDate(assignment.cancelledAt)],
      ['Cancellation Reason', assignment.cancellationReason ?? ''],
    ] : []),
    ...(assignment.linkedProjectId ? [
      ['Linked Project ID', assignment.linkedProjectId],
      ['Linked At', fmtDate(assignment.linkedAt)],
      ['Linked By', assignment.linkedBy ?? ''],
    ] : []),
    ['Description', assignment.description ?? ''],
    ['Notes', assignment.notes ?? ''],
  ]
  const ws = XLSX.utils.aoa_to_sheet(rows)
  ws['!cols'] = [{ wch: 22 }, { wch: 55 }]
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Overview')
  XLSX.writeFile(wb, `QC-ASSIGNMENT-${ref}-overview.xlsx`)
}

// ═══════════════════════════════════════════════════════════════════════════════
// CHECK-INS
// ═══════════════════════════════════════════════════════════════════════════════

export async function exportCheckInsPDF(checkIns, assignment, companyName) {
  const logoB64 = await loadLogo()
  const doc = new jsPDF({ unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()
  const M = 14

  const ref = assignment.id.slice(0, 8).toUpperCase()
  let y = pageHeader(doc, logoB64, assignment.status, STATUS_COLORS[assignment.status], W, M, companyName)
  y = sectionTitle(doc, 'CHECK-IN LOG', y, W)
  doc.setFontSize(8)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...MUTED)
  doc.text(`${assignment.title} · Ref: ${ref} · ${checkIns.length} record${checkIns.length !== 1 ? 's' : ''}`, W / 2, y, { align: 'center' })
  y += 7

  y = tableHeader(doc, `CHECK-INS (${checkIns.length})`, y, W, M)
  autoTable(doc, {
    ...gridStyle,
    startY: y,
    head: [['CHECK-IN TIME', 'CHECK-OUT TIME', 'DURATION', 'NOTES', 'COORDINATES']],
    headStyles: listHeadStyle,
    alternateRowStyles: { fillColor: LIGHT },
    body: checkIns.map(c => {
      const inT  = new Date(c.checkedInAt)
      const outT = c.checkedOutAt ? new Date(c.checkedOutAt) : null
      const mins = outT ? Math.round((outT - inT) / 60000) : null
      return [
        inT.toLocaleString('en-KE'),
        outT ? outT.toLocaleString('en-KE') : 'Active',
        mins !== null ? `${Math.floor(mins / 60)}h ${mins % 60}m` : '—',
        c.notes || '—',
        c.checkInLatitude != null ? `${c.checkInLatitude.toFixed(4)}, ${c.checkInLongitude?.toFixed(4)}` : '—',
      ]
    }),
    margin: { left: M, right: M },
  })

  finalize(doc, logoB64, W, H, `QC-ASSIGNMENT-${ref}-checkins.pdf`, companyName)
}

export function exportCheckInsExcel(checkIns, assignment, companyName) {
  const ref = assignment.id.slice(0, 8).toUpperCase()
  const rows = [
    ...xlsxHeader('CHECK-IN LOG', ref, companyName),
    ['Check-In Time', 'Check-Out Time', 'Duration (min)', 'Notes', 'Latitude', 'Longitude'],
    ...checkIns.map(c => {
      const inT  = new Date(c.checkedInAt)
      const outT = c.checkedOutAt ? new Date(c.checkedOutAt) : null
      const mins = outT ? Math.round((outT - inT) / 60000) : null
      return [inT.toLocaleString('en-KE'), outT ? outT.toLocaleString('en-KE') : 'Active', mins ?? '', c.notes || '', c.checkInLatitude ?? '', c.checkInLongitude ?? '']
    }),
  ]
  const ws = XLSX.utils.aoa_to_sheet(rows)
  ws['!cols'] = [{ wch: 22 }, { wch: 22 }, { wch: 16 }, { wch: 35 }, { wch: 12 }, { wch: 12 }]
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Check-Ins')
  XLSX.writeFile(wb, `QC-ASSIGNMENT-${ref}-checkins.xlsx`)
}

// ═══════════════════════════════════════════════════════════════════════════════
// DAILY SUMMARIES
// ═══════════════════════════════════════════════════════════════════════════════

export async function exportSummariesPDF(summaries, assignment, companyName) {
  const logoB64 = await loadLogo()
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()
  const M = 14

  const ref = assignment.id.slice(0, 8).toUpperCase()
  let y = pageHeader(doc, logoB64, assignment.status, STATUS_COLORS[assignment.status], W, M, companyName)
  y = sectionTitle(doc, 'DAILY SUMMARIES', y, W)
  doc.setFontSize(8)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...MUTED)
  doc.text(`${assignment.title} · Ref: ${ref} · ${summaries.length} entr${summaries.length !== 1 ? 'ies' : 'y'}`, W / 2, y, { align: 'center' })
  y += 7

  y = tableHeader(doc, `DAILY SUMMARIES (${summaries.length})`, y, W, M)
  autoTable(doc, {
    ...gridStyle,
    startY: y,
    head: [['DATE', 'TECHNICIAN', 'HRS', 'EXPENSES', 'SUMMARY', 'CHALLENGES', 'NEXT DAY PLAN']],
    headStyles: listHeadStyle,
    alternateRowStyles: { fillColor: LIGHT },
    body: summaries.map(s => [
      fmtDate(s.date ?? s.createdAt),
      s.submittedByName ?? '—',
      s.hoursWorked ?? '—',
      s.expensesIncurred > 0 ? fmtKES(s.expensesIncurred) : '—',
      s.summary || '—',
      s.challenges || '—',
      s.nextDayPlan || '—',
    ]),
    columnStyles: {
      0: { cellWidth: 24 }, 1: { cellWidth: 30 }, 2: { cellWidth: 12 }, 3: { cellWidth: 28 },
      4: { cellWidth: 55 }, 5: { cellWidth: 50 }, 6: { cellWidth: 50 },
    },
    margin: { left: M, right: M },
  })

  finalize(doc, logoB64, W, H, `QC-ASSIGNMENT-${ref}-summaries.pdf`, companyName)
}

export function exportSummariesExcel(summaries, assignment, companyName) {
  const ref = assignment.id.slice(0, 8).toUpperCase()
  const rows = [
    ...xlsxHeader('DAILY SUMMARIES', ref, companyName),
    ['Date', 'Technician', 'Hours Worked', 'Expenses (KES)', 'Summary', 'Challenges', 'Next Day Plan'],
    ...summaries.map(s => [
      fmtDate(s.date ?? s.createdAt),
      s.submittedByName ?? '',
      s.hoursWorked ?? '',
      s.expensesIncurred ?? '',
      s.summary || '',
      s.challenges || '',
      s.nextDayPlan || '',
    ]),
  ]
  const ws = XLSX.utils.aoa_to_sheet(rows)
  ws['!cols'] = [{ wch: 14 }, { wch: 22 }, { wch: 14 }, { wch: 16 }, { wch: 45 }, { wch: 35 }, { wch: 35 }]
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Daily Summaries')
  XLSX.writeFile(wb, `QC-ASSIGNMENT-${ref}-summaries.xlsx`)
}

// ═══════════════════════════════════════════════════════════════════════════════
// SERVICE REPORT
// ═══════════════════════════════════════════════════════════════════════════════

export async function exportServiceReportPDF(serviceReport, assignment, companyName) {
  const logoB64 = await loadLogo()
  const doc = new jsPDF({ unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()
  const M = 14

  // Parse detailsJson — the technical form stores structured fields here
  let details = {}
  if (serviceReport.detailsJson) {
    try {
      const parsed = JSON.parse(serviceReport.detailsJson)
      details = typeof parsed === 'string' ? JSON.parse(parsed) : parsed
    } catch { /* ignore */ }
  }

  const ref = assignment.id.slice(0, 8).toUpperCase()
  let y = pageHeader(doc, logoB64, serviceReport.status, STATUS_COLORS[serviceReport.status] ?? MUTED, W, M, companyName)
  y = sectionTitle(doc, 'SERVICE REPORT', y, W)
  doc.setFontSize(9)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...DARK)
  doc.text(assignment.title, W / 2, y, { align: 'center' })
  y += 8

  // ── REPORT DETAILS ────────────────────────────────────────────────────────
  y = tableHeader(doc, 'REPORT DETAILS', y, W, M)
  autoTable(doc, {
    ...gridStyle,
    styles: { ...gridStyle.styles, cellPadding: { top: 1.8, bottom: 1.8, left: 3, right: 3 } },
    startY: y,
    body: [
      ['STATUS',          serviceReport.status ?? '—',          'DEPARTMENT',     serviceReport.departmentType ?? '—'],
      ['NATURE OF VISIT', serviceReport.natureOfVisit ?? '—',   'TECHNICIAN',     serviceReport.technicianName ?? '—'],
      ['TECHNICIAN TEL.', details.technicianPhone || '—',       'VEHICLE NO.',    details.vehicleNo || '—'],
      ['CUSTOMER',        serviceReport.customerName ?? '—',    'LOCATION',       serviceReport.locationName ?? '—'],
      ['ADDRESS',         serviceReport.locationAddress ?? '—', 'CONTACT PERSON', serviceReport.contactPerson ?? '—'],
      ['CONTACT PHONE',   serviceReport.contactPhone ?? '—',    'CONTACT EMAIL',  serviceReport.contactEmail ?? '—'],
      ...(serviceReport.startDay
        ? [['START DATE', fmtDate(serviceReport.startDay), 'END DATE', fmtDate(serviceReport.endDay)]]
        : []),
      ...(serviceReport.totalMinutes
        ? [['DURATION', `${Math.floor(serviceReport.totalMinutes / 60)}h ${serviceReport.totalMinutes % 60}m`, 'SIGNED AT', serviceReport.signedAt ? fmtDate(serviceReport.signedAt) : '—']]
        : []),
    ],
    columnStyles: {
      0: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 34 },
      1: { textColor: DARK,                                           cellWidth: 57 },
      2: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 34 },
      3: { textColor: DARK,                                           cellWidth: 57 },
    },
    margin: { left: M, right: M },
  })
  y = doc.lastAutoTable.finalY + 7

  // ── MACHINE DETAILS ───────────────────────────────────────────────────────
  if (y > H - 60) { doc.addPage(); y = 20 }
  y = tableHeader(doc, 'MACHINE DETAILS', y, W, M)
  y = textBlock(doc, details.machineDetails || '—', y, W, M, 8)

  // ── JOB DETAILS ───────────────────────────────────────────────────────────
  if (y > H - 80) { doc.addPage(); y = 20 }
  y = tableHeader(doc, 'JOB DETAILS', y, W, M)
  autoTable(doc, {
    ...gridStyle,
    startY: y,
    body: [
      ['FAULT REPORTED',            details.faultReported    || '—'],
      ['FINDINGS',                  details.findings         || '—'],
      ['CORRECTION / ACTION TAKEN', details.correction       || '—'],
      ['FINAL RESULT',              details.finalResult      || '—'],
      ['PARTS TO ORDER',            details.partsToOrder     || '—'],
    ],
    columnStyles: {
      0: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 52 },
      1: { textColor: DARK, cellWidth: 130 },
    },
    margin: { left: M, right: M },
  })
  y = doc.lastAutoTable.finalY + 7

  // ── MILEAGE TABLE ─────────────────────────────────────────────────────────
  const mileageRows = Array.isArray(details.mileageRows)
    ? details.mileageRows.filter(r => r.lpoNo || r.timeIn || r.timeOut || r.kmOut || r.kmIn || r.kmCovered)
    : []
  if (mileageRows.length > 0) {
    if (y > H - 60) { doc.addPage(); y = 20 }
    y = tableHeader(doc, 'FIELD JOB TIME — MILEAGE DETAILS', y, W, M)
    autoTable(doc, {
      ...gridStyle,
      startY: y,
      head: [['MV/EV/DN/LPO NO.', 'TIME-IN', 'TIME-OUT', 'TIME-SPENT', 'KM-OUT', 'KM-IN', 'KM-COVERED']],
      headStyles: listHeadStyle,
      alternateRowStyles: { fillColor: LIGHT },
      body: mileageRows.map(r => [
        r.lpoNo || '—', r.timeIn || '—', r.timeOut || '—',
        r.timeSpent || '—', r.kmOut || '—', r.kmIn || '—', r.kmCovered || '—',
      ]),
      columnStyles: {
        0: { cellWidth: 38 }, 1: { cellWidth: 22 }, 2: { cellWidth: 22 },
        3: { cellWidth: 22 }, 4: { cellWidth: 20 }, 5: { cellWidth: 20 }, 6: { cellWidth: 22 },
      },
      margin: { left: M, right: M },
    })
    y = doc.lastAutoTable.finalY + 7
  }

  // ── CUSTOMER COMMENTS ─────────────────────────────────────────────────────
  const comments = serviceReport.customerComments || details.customerComments
  if (comments) {
    if (y > H - 50) { doc.addPage(); y = 20 }
    y = tableHeader(doc, 'CUSTOMER COMMENTS', y, W, M)
    y = textBlock(doc, comments, y, W, M)
  }

  // ── SIGNATORIES ───────────────────────────────────────────────────────────
  const hasSigs = serviceReport.customerSignatureData || serviceReport.technicianSignatureData
    || serviceReport.customerSignatureName || serviceReport.technicianSignatureName
  if (hasSigs) {
    const colW = (W - 2 * M - 8) / 2
    const sigImgH = 32
    const totalBlockH = 8 + sigImgH + 12 // label + box + name/date

    if (y > H - totalBlockH - 20) { doc.addPage(); y = 20 }
    y = tableHeader(doc, 'SIGNATORIES', y, W, M)
    y += 2

    const leftX  = M
    const rightX = M + colW + 8

    for (const [label, name, data, x] of [
      ['CUSTOMER',    serviceReport.customerSignatureName,   serviceReport.customerSignatureData,   leftX],
      ['TECHNICIAN',  serviceReport.technicianSignatureName, serviceReport.technicianSignatureData, rightX],
    ]) {
      doc.setFontSize(7.5); doc.setFont('helvetica', 'bold'); doc.setTextColor(...DARK)
      doc.text(label, x, y)

      const boxY = y + 2
      doc.setDrawColor(...BORDER); doc.setLineWidth(0.3)
      doc.roundedRect(x, boxY, colW, sigImgH, 1.5, 1.5)

      if (data) {
        try { doc.addImage(data, 'PNG', x + 2, boxY + 2, colW - 4, sigImgH - 4) } catch {}
      }

      doc.setFontSize(8); doc.setFont('helvetica', 'normal'); doc.setTextColor(...DARK)
      doc.text(`Name: ${name || '—'}`, x, boxY + sigImgH + 4)
      if (label === 'CUSTOMER' && serviceReport.signedAt) {
        doc.text(`Date: ${fmtDate(serviceReport.signedAt)}`, x, boxY + sigImgH + 8)
      }
    }
    y += totalBlockH + 6
  }

  // ── ADDITIONAL DETAILS (any extra detailsJson fields not already shown) ──────
  const KNOWN_DETAIL_KEYS = new Set([
    'machinedetails', 'faultreported', 'findings', 'correction', 'finalresult',
    'partstoorder', 'technicianphone', 'vehicleno', 'customercomments',
    'customeraddress', 'customeremail', 'displaynature', 'mileagerows',
  ])
  const extraDetails = parseDetailsJson(serviceReport.detailsJson)
    .filter(([k]) => !KNOWN_DETAIL_KEYS.has(k.toLowerCase().replace(/ /g, '')))
  if (extraDetails.length > 0) {
    if (y > H - 50) { doc.addPage(); y = 20 }
    y = tableHeader(doc, 'ADDITIONAL DETAILS', y, W, M)
    autoTable(doc, {
      ...gridStyle,
      startY: y,
      body: extraDetails,
      columnStyles: {
        0: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 52 },
        1: { textColor: DARK, cellWidth: 130 },
      },
      margin: { left: M, right: M },
    })
    y = doc.lastAutoTable.finalY + 7
  }

  // ── APPROVAL ──────────────────────────────────────────────────────────────
  if (serviceReport.approvedBy || serviceReport.rejectionReason) {
    if (y > H - 50) { doc.addPage(); y = 20 }
    y = tableHeader(doc, 'APPROVAL', y, W, M)
    autoTable(doc, {
      ...gridStyle,
      startY: y,
      body: [
        ...(serviceReport.approvedBy
          ? [['APPROVED BY', serviceReport.approvedBy, 'APPROVED AT', serviceReport.approvedAt ? fmtDate(serviceReport.approvedAt) : '—']]
          : []),
        ...(serviceReport.rejectionReason
          ? [['REJECTION REASON', serviceReport.rejectionReason, '', '']]
          : []),
      ],
      columnStyles: {
        0: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 34 },
        1: { textColor: DARK,                                           cellWidth: 57 },
        2: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 34 },
        3: { textColor: DARK,                                           cellWidth: 57 },
      },
      margin: { left: M, right: M },
    })
    y = doc.lastAutoTable.finalY + 7
  }

  finalize(doc, logoB64, W, H, `QC-ASSIGNMENT-${ref}-service-report.pdf`, companyName)
}

export function exportServiceReportExcel(serviceReport, assignment, companyName) {
  const ref = assignment.id.slice(0, 8).toUpperCase()

  let details = {}
  if (serviceReport.detailsJson) {
    try {
      const parsed = JSON.parse(serviceReport.detailsJson)
      details = typeof parsed === 'string' ? JSON.parse(parsed) : parsed
    } catch { /* ignore */ }
  }

  const mileageRows = Array.isArray(details.mileageRows)
    ? details.mileageRows.filter(r => r.lpoNo || r.timeIn || r.timeOut || r.kmOut || r.kmIn || r.kmCovered)
    : []

  const rows = [
    ...xlsxHeader('SERVICE REPORT', ref, companyName),
    ['Field', 'Value'],
    // ── Basic info ──────────────────────────────────────────────────────────
    ['Status', serviceReport.status ?? ''],
    ['Department', serviceReport.departmentType ?? ''],
    ['Nature of Visit', serviceReport.natureOfVisit ?? ''],
    ['Technician', serviceReport.technicianName ?? ''],
    ['Technician Tel.', details.technicianPhone ?? ''],
    ['Vehicle No.', details.vehicleNo ?? ''],
    ['Customer', serviceReport.customerName ?? ''],
    ['Location', serviceReport.locationName ?? ''],
    ['Address', serviceReport.locationAddress ?? ''],
    ['Contact Person', serviceReport.contactPerson ?? ''],
    ['Contact Phone', serviceReport.contactPhone ?? ''],
    ['Contact Email', serviceReport.contactEmail ?? ''],
    ['Start Date', fmtDate(serviceReport.startDay)],
    ['End Date', fmtDate(serviceReport.endDay)],
    ['Duration (min)', serviceReport.totalMinutes ?? ''],
    [],
    ['--- Signatories ---', ''],
    ['Customer Name', serviceReport.customerSignatureName ?? ''],
    ['Customer Signed At', serviceReport.signedAt ? fmtDate(serviceReport.signedAt) : ''],
    ['Technician Name', serviceReport.technicianSignatureName ?? ''],
    ['Approved By', serviceReport.approvedBy ?? ''],
    ['Approved At', serviceReport.approvedAt ? fmtDate(serviceReport.approvedAt) : ''],
    ['Rejection Reason', serviceReport.rejectionReason ?? ''],
    // ── Technical job details ────────────────────────────────────────────────
    [],
    ['--- Job Details ---', ''],
    ['Machine Details', details.machineDetails ?? ''],
    ['Fault Reported', details.faultReported ?? ''],
    ['Findings', details.findings ?? ''],
    ['Correction / Action Taken', details.correction ?? ''],
    ['Final Result', details.finalResult ?? ''],
    ['Parts to Order', details.partsToOrder ?? ''],
    ['Customer Comments', serviceReport.customerComments || details.customerComments || ''],
    // ── Mileage ──────────────────────────────────────────────────────────────
    ...(mileageRows.length > 0 ? [
      [],
      ['--- Mileage Details ---', '', '', '', '', '', ''],
      ['MV/EV/DN/LPO NO.', 'TIME-IN', 'TIME-OUT', 'TIME-SPENT', 'KM-OUT', 'KM-IN', 'KM-COVERED'],
      ...mileageRows.map(r => [r.lpoNo ?? '', r.timeIn ?? '', r.timeOut ?? '', r.timeSpent ?? '', r.kmOut ?? '', r.kmIn ?? '', r.kmCovered ?? '']),
    ] : []),
    // ── Additional (any non-standard detailsJson fields) ─────────────────────
    ...(() => {
      const KNOWN = new Set(['machinedetails','faultreported','findings','correction','finalresult',
        'partstoorder','technicianphone','vehicleno','customercomments',
        'customeraddress','customeremail','displaynature','mileagerows'])
      const extra = parseDetailsJson(serviceReport.detailsJson)
        .filter(([k]) => !KNOWN.has(k.toLowerCase().replace(/ /g, '')))
      if (extra.length === 0) return []
      return [[], ['--- Additional Details ---', ''], ...extra]
    })(),
  ]
  const ws = XLSX.utils.aoa_to_sheet(rows)
  ws['!cols'] = [{ wch: 30 }, { wch: 55 }, { wch: 14 }, { wch: 14 }, { wch: 12 }, { wch: 12 }, { wch: 16 }]
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Service Report')
  XLSX.writeFile(wb, `QC-ASSIGNMENT-${ref}-service-report.xlsx`)
}

// ═══════════════════════════════════════════════════════════════════════════════
// FINANCIALS
// ═══════════════════════════════════════════════════════════════════════════════

export async function exportFinancialsPDF(financials, assignment, companyName) {
  const logoB64 = await loadLogo()
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()   // 297
  const H = doc.internal.pageSize.getHeight()  // 210
  const M = 14

  const ref = assignment.id.slice(0, 8).toUpperCase()
  let y = pageHeader(doc, logoB64, assignment.status, STATUS_COLORS[assignment.status], W, M, companyName)
  y = sectionTitle(doc, 'FINANCIALS', y, W)
  doc.setFontSize(8)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...MUTED)
  doc.text(`${assignment.title} · Ref: ${ref}`, W / 2, y, { align: 'center' })
  y += 7

  // Summary
  y = tableHeader(doc, 'SUMMARY', y, W, M)
  autoTable(doc, {
    ...gridStyle,
    startY: y,
    head: [['CATEGORY', 'COUNT', 'TOTAL']],
    headStyles: listHeadStyle,
    alternateRowStyles: { fillColor: LIGHT },
    body: [
      ['Requisitions', financials.requisitions.length, fmtKES(financials.requisitions.reduce((s, r) => s + (r.amount ?? 0), 0))],
      ['Claims',       financials.claims.length,       fmtKES(financials.claims.reduce((s, c) => s + (c.amount ?? 0), 0))],
      ['Petty Cash',   financials.pettyCash.length,    fmtKES(financials.pettyCash.reduce((s, p) => s + (p.amount ?? 0), 0))],
      ['Per Diem',     financials.perDiem.length,      fmtKES(financials.perDiem.reduce((s, p) => s + (p.totalSpent ?? 0), 0))],
      ['Adv. Returns', financials.advanceReturns.length, fmtKES(financials.advanceReturns.reduce((s, a) => s + (a.totalAdvanced ?? 0), 0))],
      ['Refunds',      financials.refunds.length,      fmtKES(financials.refunds.reduce((s, r) => s + (r.amount ?? 0), 0))],
    ],
    columnStyles: { 0: { cellWidth: 40 }, 1: { cellWidth: 20 } },
    margin: { left: M, right: M },
  })
  y = doc.lastAutoTable.finalY + 8

  // ── REQUISITIONS ──────────────────────────────────────────────────────────
  if (financials.requisitions.length > 0) {
    if (y > H - 60) { doc.addPage(); y = 20 }
    y = tableHeader(doc, `REQUISITIONS (${financials.requisitions.length})`, y, W, M)
    autoTable(doc, {
      ...gridStyle,
      startY: y,
      head: [['#', 'TYPE', 'DESCRIPTION', 'REQUESTED BY', 'AMOUNT', 'APPROVED AMT', 'MGR REVIEW', 'CFO REVIEW', 'PAID AT', 'STATUS']],
      headStyles: listHeadStyle,
      alternateRowStyles: { fillColor: LIGHT },
      body: financials.requisitions.map((r, i) => [
        i + 1, r.type, reqFormLabel(r.description), r.requestedByName ?? '—',
        fmtKES(r.amount),
        r.approvedAmount != null ? fmtKES(r.approvedAmount) : '—',
        r.managerReviewedAt ? fmtDate(r.managerReviewedAt) : '—',
        r.cfoReviewedAt ? fmtDate(r.cfoReviewedAt) : '—',
        r.paidAt ? fmtDate(r.paidAt) : '—',
        r.status,
      ]),
      columnStyles: {
        0: { cellWidth: 10, halign: 'center' }, 1: { cellWidth: 26 }, 2: { cellWidth: 52 },
        3: { cellWidth: 32 }, 4: { cellWidth: 26, halign: 'right' }, 5: { cellWidth: 26, halign: 'right' },
        6: { cellWidth: 24 }, 7: { cellWidth: 24 }, 8: { cellWidth: 22 }, 9: { cellWidth: 20 },
      },
      margin: { left: M, right: M },
    })
    y = doc.lastAutoTable.finalY + 4

    for (const [i, r] of financials.requisitions.entries()) {
      const formData = parseReqForm(r.description)
      const label = reqFormLabel(r.description)
      const hasNotes = r.managerComments || r.cfoComments
      if (hasNotes) {
        if (y > H - 30) { doc.addPage(); y = 20 }
        doc.setFontSize(7.5); doc.setFont('helvetica', 'bold'); doc.setTextColor(...DARK)
        doc.text(`Req #${i + 1} — ${label}:`, M + 4, y); y += 4
        doc.setFont('helvetica', 'normal'); doc.setFontSize(7); doc.setTextColor(...MUTED)
        if (r.managerComments) { doc.text(`Manager: ${r.managerComments}`, M + 8, y); y += 4 }
        if (r.cfoComments)     { doc.text(`CFO: ${r.cfoComments}`,         M + 8, y); y += 4 }
      }
      const lineItems = formData
        ? formData.items.filter(it => it.description?.trim())
        : (r.lineItems ?? [])
      if (lineItems.length > 0) {
        if (formData?.formType === 'A1') {
          y = addPdfLineItems(
            doc, lineItems, y, W, M,
            `Req #${i + 1} Items (${formData.projectName}):`,
            ['#', 'DESCRIPTION', 'QTY', 'UNIT', 'PRIORITY', 'REMARKS'],
            (it, j) => [j + 1, it.description, it.qty || '', it.unit || '', it.priority || 'Normal', it.remarks || ''],
            { 0: { cellWidth: 10, halign: 'center' }, 1: { cellWidth: 70 }, 2: { cellWidth: 18, halign: 'center' }, 3: { cellWidth: 20, halign: 'center' }, 4: { cellWidth: 24, halign: 'center' }, 5: { cellWidth: 65 } }
          )
        } else if (formData?.formType === 'A2') {
          y = addPdfLineItems(
            doc, lineItems, y, W, M,
            `Req #${i + 1} Items (${formData.projectName}):`,
            ['#', 'DESCRIPTION', 'QTY', 'UNIT', 'AMT EACH', 'EST. COST', 'ACTUAL', 'VARIATION'],
            (it, j) => [j + 1, it.description, it.qty || '', it.unit || '', it.amountForEach || '', it.estimatedCost || '', it.actualCost || '', it.variation || ''],
            { 0: { cellWidth: 10, halign: 'center' }, 1: { cellWidth: 60 }, 2: { cellWidth: 14, halign: 'center' }, 3: { cellWidth: 14, halign: 'center' }, 4: { cellWidth: 22, halign: 'right' }, 5: { cellWidth: 22, halign: 'right' }, 6: { cellWidth: 22, halign: 'right' }, 7: { cellWidth: 22, halign: 'right' } }
          )
        } else {
          y = addPdfLineItems(
            doc, lineItems, y, W, M,
            `Req #${i + 1} Line Items:`,
            ['#', 'DESCRIPTION', 'QUANTITY', 'UNIT PRICE', 'TOTAL'],
            (item, j) => [j + 1, item.description, item.quantity, fmtKES(item.unitPrice), fmtKES((item.quantity ?? 0) * (item.unitPrice ?? 0))],
            { 0: { cellWidth: 10, halign: 'center' }, 1: { cellWidth: 115 }, 2: { cellWidth: 18, halign: 'center' }, 3: { cellWidth: 32, halign: 'right' }, 4: { cellWidth: 32, halign: 'right' } }
          )
        }
      }
    }
    y += 4
  }

  // ── CLAIMS ───────────────────────────────────────────────────────────────
  if (financials.claims.length > 0) {
    if (y > H - 60) { doc.addPage(); y = 20 }
    y = tableHeader(doc, `CLAIMS (${financials.claims.length})`, y, W, M)
    autoTable(doc, {
      ...gridStyle,
      startY: y,
      head: [['DESCRIPTION', 'CLAIMANT', 'AMOUNT', 'APPROVED AMT', 'MGR REVIEW', 'CFO REVIEW', 'DISBURSED AT', 'STATUS']],
      headStyles: listHeadStyle,
      alternateRowStyles: { fillColor: LIGHT },
      body: financials.claims.map(c => [
        c.description, c.claimantName ?? '—',
        fmtKES(c.amount),
        c.approvedAmount != null ? fmtKES(c.approvedAmount) : '—',
        c.managerReviewedAt ? fmtDate(c.managerReviewedAt) : '—',
        c.cfoReviewedAt ? fmtDate(c.cfoReviewedAt) : '—',
        c.disbursedAt ? fmtDate(c.disbursedAt) : '—',
        c.status,
      ]),
      columnStyles: {
        0: { cellWidth: 60 }, 1: { cellWidth: 34 },
        2: { cellWidth: 28, halign: 'right' }, 3: { cellWidth: 28, halign: 'right' },
        4: { cellWidth: 28 }, 5: { cellWidth: 28 }, 6: { cellWidth: 26 }, 7: { cellWidth: 20 },
      },
      margin: { left: M, right: M },
    })
    y = doc.lastAutoTable.finalY + 8
  }

  // ── PETTY CASH ────────────────────────────────────────────────────────────
  if (financials.pettyCash.length > 0) {
    if (y > H - 60) { doc.addPage(); y = 20 }
    y = tableHeader(doc, `PETTY CASH (${financials.pettyCash.length})`, y, W, M)
    autoTable(doc, {
      ...gridStyle,
      startY: y,
      head: [['PURPOSE', 'REQUESTED BY', 'AMOUNT', 'APPROVED AMT', 'REVIEW COMMENTS', 'MGR REVIEW', 'CFO REVIEW', 'DISBURSED AT', 'STATUS']],
      headStyles: listHeadStyle,
      alternateRowStyles: { fillColor: LIGHT },
      body: financials.pettyCash.map(p => [
        p.purpose, p.requestedByName ?? '—',
        fmtKES(p.amount),
        p.approvedAmount != null ? fmtKES(p.approvedAmount) : '—',
        p.reviewComments ?? '—',
        p.managerReviewedAt ? fmtDate(p.managerReviewedAt) : '—',
        p.cfoReviewedAt ? fmtDate(p.cfoReviewedAt) : '—',
        p.disbursedAt ? fmtDate(p.disbursedAt) : '—',
        p.status,
      ]),
      columnStyles: {
        0: { cellWidth: 44 }, 1: { cellWidth: 30 },
        2: { cellWidth: 24, halign: 'right' }, 3: { cellWidth: 24, halign: 'right' },
        4: { cellWidth: 50 }, 5: { cellWidth: 24 }, 6: { cellWidth: 24 }, 7: { cellWidth: 24 }, 8: { cellWidth: 18 },
      },
      margin: { left: M, right: M },
    })
    y = doc.lastAutoTable.finalY + 8
  }

  // ── PER DIEM ──────────────────────────────────────────────────────────────
  if (financials.perDiem.length > 0) {
    if (y > H - 60) { doc.addPage(); y = 20 }
    y = tableHeader(doc, `PER DIEM (${financials.perDiem.length})`, y, W, M)
    autoTable(doc, {
      ...gridStyle,
      startY: y,
      head: [['SUBMITTED BY', 'TOTAL ADVANCED', 'TOTAL SPENT', 'BALANCE', 'REF / EMPLOYEE / PERIOD', 'MGR COMMENTS', 'CFO COMMENTS', 'STATUS']],
      headStyles: listHeadStyle,
      alternateRowStyles: { fillColor: LIGHT },
      body: financials.perDiem.map(p => {
        let d = {}
        try { d = p.detailsJson ? JSON.parse(p.detailsJson) : {} } catch { /**/ }
        const summary = [
          d.refNo ? `Ref: ${d.refNo}` : null,
          d.employeeName ? `Employee: ${d.employeeName}` : null,
          (d.periodFrom && d.periodTo) ? `Period: ${d.periodFrom} – ${d.periodTo}` : null,
          d.perDiemRate ? `Rate: KES ${d.perDiemRate}/day` : null,
          p.notes ? `Notes: ${p.notes}` : null,
        ].filter(Boolean).join('\n') || '—'
        return [
          p.submittedByName ?? '—',
          fmtKES(p.totalAdvanced), fmtKES(p.totalSpent),
          fmtKES(p.balance ?? ((p.totalAdvanced ?? 0) - (p.totalSpent ?? 0))),
          summary,
          p.managerComments ?? '—',
          p.cfoComments ?? '—',
          p.status,
        ]
      }),
      columnStyles: {
        0: { cellWidth: 34 }, 1: { cellWidth: 28, halign: 'right' }, 2: { cellWidth: 28, halign: 'right' },
        3: { cellWidth: 28, halign: 'right' }, 4: { cellWidth: 45 }, 5: { cellWidth: 45 }, 6: { cellWidth: 22 }, 7: { cellWidth: 20 },
      },
      margin: { left: M, right: M },
    })
    y = doc.lastAutoTable.finalY + 4

    for (const [i, p] of financials.perDiem.entries()) {
      if (p.lineItems && p.lineItems.length > 0) {
        y = addPdfLineItems(
          doc, p.lineItems, y, W, M,
          `Per Diem #${i + 1} (${p.submittedByName ?? 'Submission'}) — Line Items:`,
          ['#', 'DESCRIPTION', 'AMOUNT'],
          (item, j) => [j + 1, item.description, fmtKES(item.amount)],
          { 0: { cellWidth: 10, halign: 'center' }, 1: { cellWidth: 175 }, 2: { cellWidth: 35, halign: 'right' } }
        )
      }
    }
    y += 4
  }

  // ── ADVANCE RETURNS ───────────────────────────────────────────────────────
  if (financials.advanceReturns.length > 0) {
    if (y > H - 60) { doc.addPage(); y = 20 }
    y = tableHeader(doc, `ADVANCE RETURNS (${financials.advanceReturns.length})`, y, W, M)
    autoTable(doc, {
      ...gridStyle,
      startY: y,
      head: [['SUBMITTED BY', 'TOTAL ADVANCED', 'ACCOUNTED FOR', 'AMT RETURNED', 'REF / RETURNED BY / PROJECT', 'MGR COMMENTS', 'CFO COMMENTS', 'STATUS']],
      headStyles: listHeadStyle,
      alternateRowStyles: { fillColor: LIGHT },
      body: financials.advanceReturns.map(a => {
        let d = {}
        try { d = a.detailsJson ? JSON.parse(a.detailsJson) : {} } catch { /**/ }
        const summary = [
          d.refNo ? `Ref: ${d.refNo}` : null,
          d.returnedBy ? `Returned By: ${d.returnedBy}` : null,
          d.projectAccount ? `Project: ${d.projectAccount}` : null,
          d.amountInWords ? `In Words: ${d.amountInWords}` : null,
          a.notes ? `Notes: ${a.notes}` : null,
        ].filter(Boolean).join('\n') || '—'
        return [
          a.submittedByName ?? '—',
          fmtKES(a.totalAdvanced), fmtKES(a.totalAccountedFor), fmtKES(a.amountReturned),
          summary,
          a.managerComments ?? '—',
          a.cfoComments ?? '—',
          a.status,
        ]
      }),
      columnStyles: {
        0: { cellWidth: 34 }, 1: { cellWidth: 28, halign: 'right' }, 2: { cellWidth: 28, halign: 'right' },
        3: { cellWidth: 28, halign: 'right' }, 4: { cellWidth: 41 }, 5: { cellWidth: 45 }, 6: { cellWidth: 22 }, 7: { cellWidth: 20 },
      },
      margin: { left: M, right: M },
    })
    y = doc.lastAutoTable.finalY + 4

    for (const [i, a] of financials.advanceReturns.entries()) {
      if (a.lineItems && a.lineItems.length > 0) {
        y = addPdfLineItems(
          doc, a.lineItems, y, W, M,
          `Advance Return #${i + 1} (${a.submittedByName ?? 'Submission'}) — Line Items:`,
          ['#', 'DESCRIPTION', 'AMOUNT'],
          (item, j) => [j + 1, item.description, fmtKES(item.amount)],
          { 0: { cellWidth: 10, halign: 'center' }, 1: { cellWidth: 175 }, 2: { cellWidth: 35, halign: 'right' } }
        )
      }
    }
    y += 4
  }

  // ── REFUNDS ───────────────────────────────────────────────────────────────
  if (financials.refunds.length > 0) {
    if (y > H - 60) { doc.addPage(); y = 20 }
    y = tableHeader(doc, `REFUNDS (${financials.refunds.length})`, y, W, M)
    autoTable(doc, {
      ...gridStyle,
      startY: y,
      head: [['REASON', 'REQUESTED BY', 'AMOUNT', 'REVIEW COMMENTS', 'MGR REVIEW', 'PROCESSED AT', 'STATUS']],
      headStyles: listHeadStyle,
      alternateRowStyles: { fillColor: LIGHT },
      body: financials.refunds.map(r => [
        r.reason, r.requestedByName ?? '—',
        fmtKES(r.amount),
        r.reviewComments ?? '—',
        r.managerReviewedAt ? fmtDate(r.managerReviewedAt) : '—',
        r.processedAt ? fmtDate(r.processedAt) : '—',
        r.status,
      ]),
      columnStyles: {
        0: { cellWidth: 60 }, 1: { cellWidth: 34 }, 2: { cellWidth: 28, halign: 'right' },
        3: { cellWidth: 68 }, 4: { cellWidth: 28 }, 5: { cellWidth: 26 }, 6: { cellWidth: 20 },
      },
      margin: { left: M, right: M },
    })
    y = doc.lastAutoTable.finalY + 8
  }

  finalize(doc, logoB64, W, H, `QC-ASSIGNMENT-${ref}-financials.pdf`, companyName)
}

export function exportFinancialsExcel(financials, assignment, companyName) {
  const ref = assignment.id.slice(0, 8).toUpperCase()
  const wb  = XLSX.utils.book_new()

  // Summary sheet
  const summaryWs = XLSX.utils.aoa_to_sheet([
    ...xlsxHeader('FINANCIALS', ref, companyName),
    ['Category', 'Count', 'Total (KES)'],
    ['Requisitions', financials.requisitions.length, financials.requisitions.reduce((s, r) => s + (r.amount ?? 0), 0)],
    ['Claims',       financials.claims.length,       financials.claims.reduce((s, c) => s + (c.amount ?? 0), 0)],
    ['Petty Cash',   financials.pettyCash.length,    financials.pettyCash.reduce((s, p) => s + (p.amount ?? 0), 0)],
    ['Per Diem',     financials.perDiem.length,      financials.perDiem.reduce((s, p) => s + (p.totalSpent ?? 0), 0)],
    ['Adv. Returns', financials.advanceReturns.length, financials.advanceReturns.reduce((s, a) => s + (a.totalAdvanced ?? 0), 0)],
    ['Refunds',      financials.refunds.length,      financials.refunds.reduce((s, r) => s + (r.amount ?? 0), 0)],
  ])
  summaryWs['!cols'] = [{ wch: 20 }, { wch: 10 }, { wch: 18 }]
  XLSX.utils.book_append_sheet(wb, summaryWs, 'Summary')

  // Requisitions
  if (financials.requisitions.length > 0) {
    const rows = [
      ['#', 'Type', 'Description', 'Requested By', 'Amount (KES)', 'Approved Amount (KES)',
       'Justification', 'Manager Comments', 'CFO Comments',
       'Manager Reviewed At', 'CFO Reviewed At', 'Paid At', 'Status'],
      ...financials.requisitions.map((r, i) => [
        i + 1, r.type, reqFormLabel(r.description), r.requestedByName ?? '',
        r.amount ?? 0, r.approvedAmount ?? '',
        r.justification ?? '', r.managerComments ?? '', r.cfoComments ?? '',
        r.managerReviewedAt ? fmtDate(r.managerReviewedAt) : '',
        r.cfoReviewedAt ? fmtDate(r.cfoReviewedAt) : '',
        r.paidAt ? fmtDate(r.paidAt) : '',
        r.status,
      ]),
    ]
    for (const [i, r] of financials.requisitions.entries()) {
      const formData = parseReqForm(r.description)
      const lineItems = formData
        ? formData.items.filter(it => it.description?.trim())
        : (r.lineItems ?? [])
      if (lineItems.length > 0) {
        rows.push([])
        rows.push([`Items — Requisition #${i + 1}: ${reqFormLabel(r.description)}`])
        if (formData?.formType === 'A1') {
          rows.push(['#', 'Description', 'Qty', 'Unit', 'Priority', 'Remarks'])
          lineItems.forEach((it, j) => rows.push([j + 1, it.description, it.qty || '', it.unit || '', it.priority || 'Normal', it.remarks || '']))
        } else if (formData?.formType === 'A2') {
          rows.push(['#', 'Description', 'Qty', 'Unit', 'Amt Each', 'Est. Cost', 'Actual Cost', 'Variation'])
          lineItems.forEach((it, j) => rows.push([j + 1, it.description, it.qty || '', it.unit || '', it.amountForEach || '', it.estimatedCost || '', it.actualCost || '', it.variation || '']))
        } else {
          rows.push(['#', 'Description', 'Quantity', 'Unit Price (KES)', 'Total (KES)'])
          lineItems.forEach((item, j) => rows.push([j + 1, item.description, item.quantity ?? 0, item.unitPrice ?? 0, (item.quantity ?? 0) * (item.unitPrice ?? 0)]))
        }
      }
    }
    const ws = XLSX.utils.aoa_to_sheet(rows)
    ws['!cols'] = [
      { wch: 5 }, { wch: 18 }, { wch: 40 }, { wch: 22 }, { wch: 16 }, { wch: 18 },
      { wch: 30 }, { wch: 28 }, { wch: 28 }, { wch: 20 }, { wch: 18 }, { wch: 14 }, { wch: 16 },
    ]
    XLSX.utils.book_append_sheet(wb, ws, 'Requisitions')
  }

  // Claims
  if (financials.claims.length > 0) {
    const ws = XLSX.utils.aoa_to_sheet([
      ['#', 'Description', 'Claimant', 'Amount (KES)', 'Approved Amount (KES)',
       'Justification', 'Manager Comments', 'CFO Comments',
       'Manager Reviewed At', 'CFO Reviewed At', 'Disbursed At', 'Status', 'Receipt URL'],
      ...financials.claims.map((c, i) => [
        i + 1, c.description, c.claimantName ?? '',
        c.amount ?? 0, c.approvedAmount ?? '',
        c.justification ?? '', c.managerComments ?? '', c.cfoComments ?? '',
        c.managerReviewedAt ? fmtDate(c.managerReviewedAt) : '',
        c.cfoReviewedAt ? fmtDate(c.cfoReviewedAt) : '',
        c.disbursedAt ? fmtDate(c.disbursedAt) : '',
        c.status, c.receiptUrl ?? '',
      ]),
    ])
    ws['!cols'] = [
      { wch: 5 }, { wch: 40 }, { wch: 22 }, { wch: 16 }, { wch: 18 },
      { wch: 30 }, { wch: 28 }, { wch: 28 }, { wch: 20 }, { wch: 18 }, { wch: 16 }, { wch: 14 }, { wch: 30 },
    ]
    XLSX.utils.book_append_sheet(wb, ws, 'Claims')
  }

  // Petty Cash
  if (financials.pettyCash.length > 0) {
    const ws = XLSX.utils.aoa_to_sheet([
      ['#', 'Purpose', 'Requested By', 'Amount (KES)', 'Approved Amount (KES)',
       'Review Comments', 'Manager Reviewed At', 'CFO Reviewed At', 'Disbursed At', 'Status'],
      ...financials.pettyCash.map((p, i) => [
        i + 1, p.purpose, p.requestedByName ?? '',
        p.amount ?? 0, p.approvedAmount ?? '',
        p.reviewComments ?? '',
        p.managerReviewedAt ? fmtDate(p.managerReviewedAt) : '',
        p.cfoReviewedAt ? fmtDate(p.cfoReviewedAt) : '',
        p.disbursedAt ? fmtDate(p.disbursedAt) : '',
        p.status,
      ]),
    ])
    ws['!cols'] = [
      { wch: 5 }, { wch: 40 }, { wch: 22 }, { wch: 16 }, { wch: 18 },
      { wch: 30 }, { wch: 20 }, { wch: 18 }, { wch: 16 }, { wch: 14 },
    ]
    XLSX.utils.book_append_sheet(wb, ws, 'Petty Cash')
  }

  // Per Diem
  if (financials.perDiem.length > 0) {
    const rows = [
      ['#', 'Submitted By', 'Ref No', 'Employee Name', 'Period From', 'Period To', 'Days', 'Rate/Day (KES)', 'Project/Account',
       'Total Advanced (KES)', 'Total Spent (KES)', 'Balance (KES)',
       'Notes', 'Prepared By', 'Checked By', 'Approved By', 'Signed By',
       'Manager Comments', 'CFO Comments', 'Manager Reviewed At', 'CFO Reviewed At', 'Status'],
      ...financials.perDiem.map((p, i) => {
        let d = {}
        try { d = p.detailsJson ? JSON.parse(p.detailsJson) : {} } catch { /**/ }
        return [
          i + 1, p.submittedByName ?? '',
          d.refNo ?? '', d.employeeName ?? '', d.periodFrom ?? '', d.periodTo ?? '',
          d.daysCount ?? '', d.perDiemRate ?? '', d.projectAccount ?? '',
          p.totalAdvanced ?? 0, p.totalSpent ?? 0,
          p.balance ?? ((p.totalAdvanced ?? 0) - (p.totalSpent ?? 0)),
          p.notes ?? '',
          d.preparedBy ?? '', d.checkedBy ?? '', d.approvedBy ?? '', d.signedBy ?? '',
          p.managerComments ?? '', p.cfoComments ?? '',
          p.managerReviewedAt ? fmtDate(p.managerReviewedAt) : '',
          p.cfoReviewedAt ? fmtDate(p.cfoReviewedAt) : '',
          p.status,
        ]
      }),
    ]
    for (const [i, p] of financials.perDiem.entries()) {
      if (p.lineItems && p.lineItems.length > 0) {
        rows.push([])
        rows.push([`Line Items — Per Diem #${i + 1}: ${p.submittedByName ?? 'Submission'}`])
        rows.push(['#', 'Description', 'Amount (KES)'])
        p.lineItems.forEach((item, j) => rows.push([j + 1, item.description, item.amount ?? 0]))
      }
    }
    const ws = XLSX.utils.aoa_to_sheet(rows)
    ws['!cols'] = [
      { wch: 5 }, { wch: 22 }, { wch: 14 }, { wch: 22 }, { wch: 14 }, { wch: 14 },
      { wch: 8 }, { wch: 14 }, { wch: 20 }, { wch: 20 }, { wch: 18 }, { wch: 18 },
      { wch: 30 }, { wch: 18 }, { wch: 18 }, { wch: 18 }, { wch: 18 },
      { wch: 28 }, { wch: 28 }, { wch: 20 }, { wch: 20 }, { wch: 14 },
    ]
    XLSX.utils.book_append_sheet(wb, ws, 'Per Diem')
  }

  // Advance Returns
  if (financials.advanceReturns.length > 0) {
    const rows = [
      ['#', 'Submitted By', 'Ref No', 'Returned By', 'Project/Account', 'Amount In Words',
       'Total Advanced (KES)', 'Accounted For (KES)', 'Amount Returned (KES)',
       'Notes', 'Prepared By', 'Checked By', 'Approved By', 'Received By',
       'Manager Comments', 'CFO Comments', 'Manager Reviewed At', 'CFO Reviewed At', 'Status'],
      ...financials.advanceReturns.map((a, i) => {
        let d = {}
        try { d = a.detailsJson ? JSON.parse(a.detailsJson) : {} } catch { /**/ }
        return [
          i + 1, a.submittedByName ?? '',
          d.refNo ?? '', d.returnedBy ?? '', d.projectAccount ?? '', d.amountInWords ?? '',
          a.totalAdvanced ?? 0, a.totalAccountedFor ?? 0, a.amountReturned ?? 0,
          a.notes ?? '',
          d.preparedBy ?? '', d.checkedBy ?? '', d.approvedBy ?? '', d.receivedBy ?? '',
          a.managerComments ?? '', a.cfoComments ?? '',
          a.managerReviewedAt ? fmtDate(a.managerReviewedAt) : '',
          a.cfoReviewedAt ? fmtDate(a.cfoReviewedAt) : '',
          a.status,
        ]
      }),
    ]
    for (const [i, a] of financials.advanceReturns.entries()) {
      if (a.lineItems && a.lineItems.length > 0) {
        rows.push([])
        rows.push([`Line Items — Advance Return #${i + 1}: ${a.submittedByName ?? 'Submission'}`])
        rows.push(['#', 'Description', 'Amount (KES)'])
        a.lineItems.forEach((item, j) => rows.push([j + 1, item.description, item.amount ?? 0]))
      }
    }
    const ws = XLSX.utils.aoa_to_sheet(rows)
    ws['!cols'] = [
      { wch: 5 }, { wch: 22 }, { wch: 14 }, { wch: 22 }, { wch: 20 }, { wch: 24 },
      { wch: 20 }, { wch: 20 }, { wch: 20 },
      { wch: 30 }, { wch: 18 }, { wch: 18 }, { wch: 18 }, { wch: 18 },
      { wch: 28 }, { wch: 28 }, { wch: 20 }, { wch: 20 }, { wch: 14 },
    ]
    XLSX.utils.book_append_sheet(wb, ws, 'Advance Returns')
  }

  // Refunds
  if (financials.refunds.length > 0) {
    const ws = XLSX.utils.aoa_to_sheet([
      ['#', 'Reason', 'Requested By', 'Amount (KES)', 'Review Comments', 'Manager Reviewed At', 'Processed At', 'Status', 'Receipt URL'],
      ...financials.refunds.map((r, i) => [
        i + 1, r.reason, r.requestedByName ?? '',
        r.amount ?? 0,
        r.reviewComments ?? '',
        r.managerReviewedAt ? fmtDate(r.managerReviewedAt) : '',
        r.processedAt ? fmtDate(r.processedAt) : '',
        r.status, r.receiptUrl ?? '',
      ]),
    ])
    ws['!cols'] = [{ wch: 5 }, { wch: 40 }, { wch: 22 }, { wch: 16 }, { wch: 30 }, { wch: 20 }, { wch: 16 }, { wch: 14 }, { wch: 30 }]
    XLSX.utils.book_append_sheet(wb, ws, 'Refunds')
  }

  XLSX.writeFile(wb, `QC-ASSIGNMENT-${ref}-financials.xlsx`)
}

// ═══════════════════════════════════════════════════════════════════════════════
// REQUISITION FORM A-1  (LT-R1 — request form)
// ═══════════════════════════════════════════════════════════════════════════════

function qslFormHeader(doc, logoB64, formTitle, W, M, docPrefix = 'LT') {
  if (logoB64) {
    try { doc.addImage(logoB64, 'PNG', M, 8, 20, 20) } catch { /* ignore */ }
  } else {
    doc.setFillColor(...AMBER)
    doc.circle(M + 10, 18, 10, 'F')
    doc.setTextColor(...BLACK)
    doc.setFontSize(11)
    doc.setFont('helvetica', 'bold')
    doc.text('Q', M + 10, 21.5, { align: 'center' })
  }

  const bW = 82, bH = 8, bX = (W - bW) / 2
  doc.setFillColor(...AMBER)
  doc.rect(bX, 10, bW, bH, 'F')
  doc.setTextColor(...BLACK)
  doc.setFontSize(8.5)
  doc.setFont('helvetica', 'bold')
  doc.text(formTitle, W / 2, 15.5, { align: 'center' })

  const tagW = 44, tagH = 7
  doc.setFillColor(...BLACK)
  doc.rect(W - M - tagW, 10, tagW, tagH, 'F')
  doc.setTextColor(...WHITE)
  doc.setFontSize(7)
  doc.setFont('helvetica', 'bold')
  doc.text('Inventing and Making Happen', W - M - tagW / 2, 14.8, { align: 'center' })

  doc.setFontSize(15)
  doc.setFont('helvetica', 'bold')
  doc.setTextColor(...AMBER)
  doc.text(`${docPrefix}/QP/003/MRF`, W - M, 32, { align: 'right' })
}

function qslFormFooter(doc, code, W, H, M) {
  const fY = H - 18
  doc.setFillColor(...AMBER)
  doc.rect(M, fY, W - M * 2, 10, 'F')
  doc.setTextColor(...BLACK)
  doc.setFontSize(6.5)
  doc.setFont('helvetica', 'normal')
  doc.text(
    'Birdi Singh Complex 1st Floor, off Mombasa Road, P.O BOX 34463 - 00100, Nairobi, Kenya, Tel: +254 714 999 996,',
    W / 2, fY + 4, { align: 'center' },
  )
  doc.text(
    '+ 254 756 999 996, info@qalibrated.co.ke|www.qalibrated.co.ke',
    W / 2, fY + 8.5, { align: 'center' },
  )
  doc.setFontSize(8)
  doc.setFont('helvetica', 'bold')
  doc.setTextColor(...BLACK)
  doc.text(code, W - M, H - 4, { align: 'right' })
}

export async function exportRequisitionFormA1(formData, assignment, docPrefix = 'LT') {
  const logoB64 = await loadLogo()
  const doc = new jsPDF({ unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()
  const M = 14
  const ref = assignment.id.slice(0, 8).toUpperCase()

  qslFormHeader(doc, logoB64, 'MATERIAL REQUISITION FORM A-1', W, M, docPrefix)

  let y = 30

  doc.setFontSize(8.5); doc.setFont('helvetica', 'bold'); doc.setTextColor(...BLACK)
  doc.text('Project Name', M, y + 5)
  doc.setDrawColor(...BLACK); doc.setLineWidth(0.3)
  doc.rect(M + 27, y, 52, 7)
  doc.setFont('helvetica', 'normal'); doc.setFontSize(8)
  doc.text((formData.projectName ?? assignment.title).slice(0, 28), M + 29, y + 5)

  y += 10
  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
  doc.text('Project Number', M, y + 5)
  doc.rect(M + 27, y, 52, 7)
  doc.setFont('helvetica', 'normal'); doc.setFontSize(8)
  doc.text(formData.projectNumber ?? ref, M + 29, y + 5)

  y += 14

  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
  const snLW = doc.getTextWidth('Store Name:')
  const snX  = W / 2 - snLW - 2
  doc.text('Store Name:', snX, y)
  doc.setDrawColor(100, 100, 100); doc.setLineWidth(0.3)
  if (formData.storeName) { doc.setFont('helvetica', 'normal'); doc.setFontSize(8); doc.text(formData.storeName, snX + snLW + 5, y) }
  doc.line(snX + snLW + 3, y + 0.5, W - M, y + 0.5)

  y += 8
  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
  doc.text('Approved By:', snX, y)
  if (formData.approvedBy) { doc.setFont('helvetica', 'normal'); doc.setFontSize(8); doc.text(formData.approvedBy, snX + doc.getTextWidth('Approved By:') + 5, y) }
  doc.line(snX + doc.getTextWidth('Approved By:') + 3, y + 0.5, W - M, y + 0.5)
  y += 10

  const MIN_ROWS = 14
  const items = formData.items ?? []
  const body  = items.map((r, i) => [String(i + 1), r.description || '', r.qty || '', r.unit || '', r.priority || 'Normal', r.remarks || ''])
  while (body.length < MIN_ROWS) body.push(['', '', '', '', '', ''])

  autoTable(doc, {
    startY: y,
    head: [['S/No', 'Material Description', 'Quantity\nRequired', 'Unit of\nMeasure', 'Priority\nUrgent/Normal', 'Remarks']],
    body,
    headStyles: { fillColor: [200, 200, 200], textColor: BLACK, fontStyle: 'bold', fontSize: 8, halign: 'center', valign: 'middle' },
    bodyStyles: { fontSize: 8, cellPadding: { top: 7, bottom: 7, left: 2.5, right: 2.5 }, valign: 'top' },
    styles: { lineColor: BLACK, lineWidth: 0.3 },
    tableLineColor: BLACK, tableLineWidth: 0.3, theme: 'grid',
    columnStyles: {
      0: { cellWidth: 12, halign: 'center' }, 1: { cellWidth: 55 },
      2: { cellWidth: 25, halign: 'center' }, 3: { cellWidth: 22, halign: 'center' },
      4: { cellWidth: 25, halign: 'center' }, 5: { cellWidth: 43 },
    },
    margin: { left: M, right: M },
  })

  qslFormFooter(doc, `${docPrefix}-R1`, W, H, M)
  doc.save(`QC-REQUISITION-FORM-A1-${ref}.pdf`)
}

// ═══════════════════════════════════════════════════════════════════════════════
// REQUISITION FORM A-2  (LT/QP/003/MRF — costing form)
// ═══════════════════════════════════════════════════════════════════════════════

export async function exportRequisitionFormA2(formData, assignment, docPrefix = 'LT') {
  const logoB64 = await loadLogo()
  const doc = new jsPDF({ unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()
  const M = 14
  const ref = assignment.id.slice(0, 8).toUpperCase()

  qslFormHeader(doc, logoB64, 'MATERIAL REQUISITION FORM A-2', W, M, docPrefix)

  let y = 30
  const col2X = W / 2 + 6

  doc.setFontSize(8.5)
  doc.setFont('helvetica', 'bold')
  doc.setTextColor(...BLACK)
  doc.text('Project Name', M, y + 5)
  doc.setDrawColor(...BLACK)
  doc.setLineWidth(0.3)
  doc.rect(M + 27, y, 48, 7)
  doc.setFont('helvetica', 'normal')
  doc.setFontSize(8)
  doc.text((formData.projectName ?? assignment.title).slice(0, 26), M + 29, y + 5)

  doc.setFont('helvetica', 'bold')
  doc.setFontSize(8.5)
  doc.text('Project Number', col2X, y + 5)
  doc.rect(col2X + 28, y, 34, 7)
  doc.setFont('helvetica', 'normal')
  doc.setFontSize(8)
  doc.text(formData.projectNumber ?? ref, col2X + 30, y + 5)

  y += 12

  doc.setFont('helvetica', 'bold')
  doc.setFontSize(8.5)
  const snW2 = doc.getTextWidth('Store Name:')
  const snX2 = W / 2 - snW2 - 2
  doc.text('Store Name:', snX2, y)
  doc.setDrawColor(100, 100, 100)
  doc.setLineWidth(0.3)
  if (formData.storeName) { doc.setFont('helvetica', 'normal'); doc.setFontSize(8); doc.text(formData.storeName, snX2 + snW2 + 5, y) }
  doc.line(snX2 + snW2 + 3, y + 0.5, W - M, y + 0.5)
  y += 8

  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
  doc.text('Prepared by:', M, y)
  if (formData.preparedBy) { doc.setFont('helvetica', 'normal'); doc.setFontSize(8); doc.text(formData.preparedBy, M + doc.getTextWidth('Prepared by:') + 3, y) }
  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
  doc.line(M + doc.getTextWidth('Prepared by:') + 3, y + 0.5, col2X - 4, y + 0.5)
  doc.text('Approved By:', col2X, y)
  if (formData.approvedBy) { doc.setFont('helvetica', 'normal'); doc.setFontSize(8); doc.text(formData.approvedBy, col2X + doc.getTextWidth('Approved By:') + 3, y) }
  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
  doc.line(col2X + doc.getTextWidth('Approved By:') + 3, y + 0.5, W - M, y + 0.5)
  y += 7

  doc.text('Checked By:', M, y)
  if (formData.checkedBy) { doc.setFont('helvetica', 'normal'); doc.setFontSize(8); doc.text(formData.checkedBy, M + doc.getTextWidth('Checked By:') + 3, y) }
  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
  doc.line(M + doc.getTextWidth('Checked By:') + 3, y + 0.5, col2X - 4, y + 0.5)
  doc.text('Date:', col2X, y)
  if (formData.date) { doc.setFont('helvetica', 'normal'); doc.setFontSize(8); doc.text(formData.date, col2X + doc.getTextWidth('Date:') + 3, y) }
  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
  doc.line(col2X + doc.getTextWidth('Date:') + 3, y + 0.5, W - M, y + 0.5)
  y += 10

  const MIN_ROWS = 12
  const items = formData.items ?? []
  const body = items.map((r, i) => [
    String(i + 1),
    r.description || '',
    r.qty || '',
    r.unit || '',
    r.amountForEach ? fmtKES(parseFloat(r.amountForEach) || 0) : '',
    r.estimatedCost ? fmtKES(parseFloat(r.estimatedCost) || 0) : '',
    r.actualCost ? fmtKES(parseFloat(r.actualCost) || 0) : '',
    r.variation || '',
  ])
  while (body.length < MIN_ROWS) body.push(['', '', '', '', '', '', '', ''])

  autoTable(doc, {
    startY: y,
    head: [
      [
        { content: 'S/No',            rowSpan: 2, styles: { valign: 'middle', halign: 'center' } },
        { content: 'Description',     rowSpan: 2, styles: { valign: 'middle', halign: 'center' } },
        { content: 'Qty',             rowSpan: 2, styles: { valign: 'middle', halign: 'center' } },
        { content: 'Unit',            rowSpan: 2, styles: { valign: 'middle', halign: 'center' } },
        { content: 'Amount\nfor Each',rowSpan: 2, styles: { valign: 'middle', halign: 'center' } },
        { content: 'Estimated\nCost', rowSpan: 2, styles: { valign: 'middle', halign: 'center' } },
        { content: 'Purchase Use Only\n(Attach RECEIPTS)', colSpan: 2, styles: { halign: 'center' } },
      ],
      [
        { content: 'Actual Cost', styles: { halign: 'center' } },
        { content: 'Variation',   styles: { halign: 'center' } },
      ],
    ],
    body,
    headStyles: { fillColor: [200, 200, 200], textColor: BLACK, fontStyle: 'bold', fontSize: 7.5, halign: 'center', valign: 'middle' },
    bodyStyles: { fontSize: 8, cellPadding: { top: 7, bottom: 7, left: 2.5, right: 2.5 }, valign: 'top' },
    styles: { lineColor: BLACK, lineWidth: 0.3 },
    tableLineColor: BLACK,
    tableLineWidth: 0.3,
    theme: 'grid',
    columnStyles: {
      0: { cellWidth: 11, halign: 'center' },
      1: { cellWidth: 50 },
      2: { cellWidth: 11, halign: 'center' },
      3: { cellWidth: 11, halign: 'center' },
      4: { cellWidth: 23, halign: 'right' },
      5: { cellWidth: 26, halign: 'right' },
      6: { cellWidth: 25, halign: 'right' },
      7: { cellWidth: 25, halign: 'right' },
    },
    margin: { left: M, right: M },
  })

  const tableEnd = doc.lastAutoTable.finalY
  doc.setDrawColor(...BLACK)
  doc.setLineWidth(0.3)
  doc.rect(M, tableEnd, W - M * 2, 7)
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(9)
  doc.setTextColor(...BLACK)
  doc.text('Total', W - M - 52, tableEnd + 5)
  const totalAmt = items.reduce((s, r) => s + (parseFloat(r.estimatedCost) || 0), 0)
  doc.setLineWidth(0.3)
  doc.setDrawColor(100, 100, 100)
  doc.line(W - M - 45, tableEnd + 5.5, W - M - 2, tableEnd + 5.5)
  if (totalAmt > 0) {
    doc.setFont('helvetica', 'normal')
    doc.setFontSize(8)
    doc.text(fmtKES(totalAmt), W - M - 4, tableEnd + 5, { align: 'right' })
  }

  const dotY = H - 24
  doc.setFillColor(...MUTED)
  for (let x = M; x < W - M; x += 3.5) {
    doc.circle(x, dotY, 0.35, 'F')
  }

  qslFormFooter(doc, `${docPrefix}/QP/003/MRF`, W, H, M)
  doc.save(`QC-REQUISITION-FORM-A2-${ref}.pdf`)
}

// ═══════════════════════════════════════════════════════════════════════════════
// TRAVELLING EXPENSES VOUCHER
// ═══════════════════════════════════════════════════════════════════════════════

export async function exportTravelVoucherPDF(formData, assignment, docPrefix = 'LT') {
  const logoB64 = await loadLogo()
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()
  const M = 14
  const ref    = assignment.id.slice(0, 8).toUpperCase()
  const totals = formData.totals ?? {}
  const bot    = formData.bottom ?? {}

  qslFormHeader(doc, logoB64, 'TRAVELLING EXPENSES VOUCHER', W, M, docPrefix)

  let y = 30

  doc.setFontSize(8.5); doc.setFont('helvetica', 'bold'); doc.setTextColor(...BLACK)
  doc.text('Name:', M, y + 5)
  const nameLW = doc.getTextWidth('Name:')
  doc.setDrawColor(...BLACK); doc.setLineWidth(0.3)
  doc.rect(M + nameLW + 3, y, W - M * 2 - nameLW - 3, 7)
  doc.setFont('helvetica', 'normal'); doc.setFontSize(8)
  if (formData.name) doc.text(formData.name, M + nameLW + 5, y + 5)
  y += 10

  const third = (W - M * 2) / 3
  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5); doc.setTextColor(...BLACK)
  doc.text('Date From:', M, y + 5)
  const dfLW = doc.getTextWidth('Date From:')
  doc.rect(M + dfLW + 3, y, third - dfLW - 5, 7)
  doc.setFont('helvetica', 'normal'); doc.setFontSize(8)
  if (formData.dateFrom) doc.text(formData.dateFrom, M + dfLW + 5, y + 5)

  const c2 = M + third
  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
  doc.text('Date To:', c2, y + 5)
  const dtLW = doc.getTextWidth('Date To:')
  doc.rect(c2 + dtLW + 3, y, third - dtLW - 5, 7)
  doc.setFont('helvetica', 'normal'); doc.setFontSize(8)
  if (formData.dateTo) doc.text(formData.dateTo, c2 + dtLW + 5, y + 5)

  const c3 = M + third * 2
  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
  doc.text('Receipt No.:', c3, y + 5)
  const rcLW = doc.getTextWidth('Receipt No.:')
  doc.rect(c3 + rcLW + 3, y, W - M - c3 - rcLW - 3, 7)
  doc.setFont('helvetica', 'normal'); doc.setFontSize(8)
  if (bot.receiptNo) doc.text(bot.receiptNo, c3 + rcLW + 5, y + 5)
  y += 12

  const MIN_ROWS = 8
  const journeyRows = (formData.rows ?? []).filter(r => r.journeyDetails?.trim() || r.date?.trim())
  const journeyBody = journeyRows.map(r => [
    r.date || '',
    r.journeyDetails || '',
    r.carRegNo || '',
    r.cc || '',
    r.mileage || '',
    r.ratePerKm       ? fmtKES(parseFloat(r.ratePerKm)       || 0) : '',
    r.faresOrCarAllce ? fmtKES(parseFloat(r.faresOrCarAllce) || 0) : '',
    r.hotelAcs        ? fmtKES(parseFloat(r.hotelAcs)        || 0) : '',
    r.meals           ? fmtKES(parseFloat(r.meals)           || 0) : '',
    r.medical         ? fmtKES(parseFloat(r.medical)         || 0) : '',
    r.incidentals     ? fmtKES(parseFloat(r.incidentals)     || 0) : '',
  ])
  while (journeyBody.length < MIN_ROWS) journeyBody.push(['', '', '', '', '', '', '', '', '', '', ''])

  const journeyCols = {
    0:  { cellWidth: 18, halign: 'center' },
    1:  { cellWidth: 80 },
    2:  { cellWidth: 20, halign: 'center' },
    3:  { cellWidth: 12, halign: 'center' },
    4:  { cellWidth: 18, halign: 'center' },
    5:  { cellWidth: 18, halign: 'right' },
    6:  { cellWidth: 24, halign: 'right' },
    7:  { cellWidth: 20, halign: 'right' },
    8:  { cellWidth: 18, halign: 'right' },
    9:  { cellWidth: 18, halign: 'right' },
    10: { cellWidth: 23, halign: 'right' },
  }

  autoTable(doc, {
    startY: y,
    head: [['Date', 'Full Details of Journey (From → To)', 'Car Reg.\nNo.', 'C.C.', 'Mileage\n(km)', 'Rate /\nkm', "Fares /\nCar All'ce", 'Hotel\nA/Cs', 'Meals', 'Medical', 'Incidentals']],
    body: journeyBody,
    headStyles: { fillColor: [200, 200, 200], textColor: BLACK, fontStyle: 'bold', fontSize: 7, halign: 'center', valign: 'middle' },
    bodyStyles: { fontSize: 7.5, cellPadding: { top: 4, bottom: 4, left: 2, right: 2 }, valign: 'top' },
    styles: { lineColor: BLACK, lineWidth: 0.3 },
    tableLineColor: BLACK, tableLineWidth: 0.3, theme: 'grid',
    columnStyles: journeyCols,
    margin: { left: M, right: M },
  })
  y = doc.lastAutoTable.finalY

  autoTable(doc, {
    startY: y,
    body: [[
      { content: 'TOTALS', colSpan: 6, styles: { fontStyle: 'bold', halign: 'right', fillColor: LABEL_BG, textColor: BLACK } },
      { content: totals.fareEtc > 0 ? fmtKES(totals.fareEtc) : '—', styles: { halign: 'right', fontStyle: 'bold', fillColor: LABEL_BG } },
      { content: totals.hotelAc > 0 ? fmtKES(totals.hotelAc) : '—', styles: { halign: 'right', fontStyle: 'bold', fillColor: LABEL_BG } },
      { content: totals.meals   > 0 ? fmtKES(totals.meals)   : '—', styles: { halign: 'right', fontStyle: 'bold', fillColor: LABEL_BG } },
      { content: totals.medical > 0 ? fmtKES(totals.medical) : '—', styles: { halign: 'right', fontStyle: 'bold', fillColor: LABEL_BG } },
      { content: totals.incids  > 0 ? fmtKES(totals.incids)  : '—', styles: { halign: 'right', fontStyle: 'bold', fillColor: LABEL_BG } },
    ]],
    bodyStyles: { fontSize: 7.5, cellPadding: { top: 3, bottom: 3, left: 2, right: 2 } },
    styles: { lineColor: BLACK, lineWidth: 0.3 },
    tableLineColor: BLACK, tableLineWidth: 0.3, theme: 'grid',
    columnStyles: journeyCols,
    margin: { left: M, right: M },
  })
  y = doc.lastAutoTable.finalY + 3

  doc.setFillColor(...AMBER); doc.setDrawColor(...BLACK); doc.setLineWidth(0.3)
  doc.rect(M, y, W - M * 2, 9, 'F')
  doc.rect(M, y, W - M * 2, 9)
  doc.setFont('helvetica', 'bold'); doc.setFontSize(9); doc.setTextColor(...BLACK)
  doc.text('GRAND TOTAL:', M + 4, y + 6)
  doc.text(totals.total > 0 ? fmtKES(totals.total) : '—', W - M - 4, y + 6, { align: 'right' })
  y += 13

  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5); doc.setTextColor(...BLACK)
  const rawLabel = 'Received the Amount of (in Words):'
  doc.text(rawLabel, M, y + 5)
  const rawLW = doc.getTextWidth(rawLabel)
  doc.setDrawColor(...BLACK); doc.setLineWidth(0.3)
  doc.rect(M + rawLW + 4, y, W - M * 2 - rawLW - 4, 9)
  doc.setFont('helvetica', 'normal'); doc.setFontSize(7.5)
  if (bot.receivedAmountWords) {
    const lines = doc.splitTextToSize(bot.receivedAmountWords, W - M * 2 - rawLW - 8)
    doc.text(lines[0] || '', M + rawLW + 6, y + 5)
    if (lines[1]) doc.text(lines[1], M + rawLW + 6, y + 8.5)
  }
  y += 13

  const halfW = (W - M * 2) / 2 - 4
  const rx = M + halfW + 8
  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5); doc.setTextColor(...BLACK)
  doc.text('Signature:', M, y + 5)
  const sigLW = doc.getTextWidth('Signature:')
  doc.setDrawColor(...BLACK); doc.setLineWidth(0.3)
  doc.rect(M + sigLW + 3, y, halfW - sigLW - 3, 7)
  doc.setFont('helvetica', 'normal'); doc.setFontSize(8)
  if (bot.signature) doc.text(bot.signature, M + sigLW + 5, y + 5)

  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
  doc.text('Date:', rx, y + 5)
  const dateLW = doc.getTextWidth('Date:')
  doc.rect(rx + dateLW + 3, y, W - M - rx - dateLW - 3, 7)
  doc.setFont('helvetica', 'normal'); doc.setFontSize(8)
  if (bot.signatureDate) doc.text(bot.signatureDate, rx + dateLW + 5, y + 5)
  y += 11

  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5); doc.setTextColor(...BLACK)
  doc.text('Authorised By:', M, y + 5)
  const authLW = doc.getTextWidth('Authorised By:')
  doc.setDrawColor(...BLACK); doc.setLineWidth(0.3)
  doc.rect(M + authLW + 3, y, halfW - authLW - 3, 7)
  doc.setFont('helvetica', 'normal'); doc.setFontSize(8)
  if (bot.authorisedBy) doc.text(bot.authorisedBy, M + authLW + 5, y + 5)

  doc.setFont('helvetica', 'bold'); doc.setFontSize(8.5)
  doc.text('Charged To:', rx, y + 5)
  const chargeLW = doc.getTextWidth('Charged To:')
  doc.rect(rx + chargeLW + 3, y, W - M - rx - chargeLW - 3, 7)
  doc.setFont('helvetica', 'normal'); doc.setFontSize(8)
  if (bot.chargedTo) doc.text(bot.chargedTo, rx + chargeLW + 5, y + 5)

  qslFormFooter(doc, `${docPrefix}/TEV`, W, H, M)
  doc.save(`QC-TRAVEL-VOUCHER-${ref}.pdf`)
}
