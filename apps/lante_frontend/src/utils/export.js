import { jsPDF } from 'jspdf'
import autoTable from 'jspdf-autotable'
import * as XLSX from 'xlsx'
import api from '../api/axios.js'
import { kes } from '../theme/tokens.js'

// ── Brand palette ──────────────────────────────────────────────────────────────
const AMBER      = [245, 158,  11]   // #f59e0b
const AMBER_DARK = [180, 110,   0]   // underline/border amber
const NAVY       = [ 27,  58, 107]   // #1B3A6B — Stores module accent
const GOLD       = [200, 169,  81]   // #C8A951 — Stores module accent
const BLACK      = [ 17,  24,  39]   // #111827
const DARK       = [ 31,  41,  55]   // #1f2937
const MUTED      = [107, 114, 128]   // #6b7280
const WHITE      = [255, 255, 255]
const LIGHT      = [249, 250, 251]   // #f9fafb
const BORDER     = [200, 200, 200]   // table cell borders
const LABEL_BG   = [245, 245, 245]   // label cell background

// Admin-configurable footer note / terms text (Administration → Document Templates), scoped to
// just the two doc types that have a free-text footer area. Never throws — a broken/slow
// template fetch must never block PDF generation, so callers always get a safe fallback.
async function fetchDocTemplate(docType) {
  try {
    const res = await api.get(`/api/v1/document-templates/${docType}`)
    return res.data?.data ?? { footerNote: null, termsText: null }
  } catch {
    return { footerNote: null, termsText: null }
  }
}

// Draws the optional footer note / terms text just above the page footer, only when set —
// pure addition, zero effect on layout when a doc type has no override configured.
function drawDocTemplateNote(doc, template, pageW, pageH, margin = 14) {
  if (!template?.footerNote && !template?.termsText) return
  let y = pageH - 22
  doc.setFontSize(7)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...MUTED)
  if (template.termsText) {
    const lines = doc.splitTextToSize(template.termsText, pageW - margin * 2)
    doc.text(lines, margin, y)
    y -= (lines.length * 3)
  }
  if (template.footerNote) {
    doc.setFont('helvetica', 'italic')
    doc.text(template.footerNote, pageW / 2, pageH - 17, { align: 'center' })
  }
}

// Document accent themes — `amber` is the original app-wide default (unchanged
// for existing callers); `navy` is used for Stores-module documents.
const THEMES = {
  amber: { bg: AMBER, text: BLACK, underline: AMBER_DARK, pillText: BLACK },
  navy:  { bg: NAVY,  text: WHITE, underline: GOLD,       pillText: WHITE },
}
function resolveTheme(theme) {
  return (typeof theme === 'string' ? THEMES[theme] : theme) ?? THEMES.amber
}

// ── Helpers ────────────────────────────────────────────────────────────────────

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
  } catch {
    return null
  }
}

function imageDims(dataUri) {
  return new Promise(resolve => {
    if (!dataUri) { resolve(null); return }
    const img = new Image()
    img.onload  = () => resolve({ w: img.naturalWidth, h: img.naturalHeight })
    img.onerror = () => resolve(null)
    img.src = dataUri
  })
}

// Full "mark + QALIBRATED SYSTEMS" wordmark — used only in the QSL Stores
// document header (qslHeader), which has room for it; unrelated to the
// icon-only `/qc-logo.png` used everywhere else in the app.
async function loadQslLogo() {
  try {
    const resp = await fetch('/brand/qsl-logo-full.png')
    if (!resp.ok) return null
    const blob = await resp.blob()
    const dataUri = await new Promise(resolve => {
      const reader = new FileReader()
      reader.onloadend = () => resolve(reader.result)
      reader.onerror   = () => resolve(null)
      reader.readAsDataURL(blob)
    })
    return dataUri ? { dataUri, dims: await imageDims(dataUri) } : null
  } catch {
    return null
  }
}

function fmtNow() {
  return new Date().toLocaleString('en-KE', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

// Centered underlined section header (matches weighing-ticket style)
function sectionTitle(doc, label, y, pageW, theme = THEMES.amber) {
  doc.setFontSize(9)
  doc.setFont('helvetica', 'bold')
  doc.setTextColor(...theme.underline)
  const tw = doc.getTextWidth(label)
  const cx = pageW / 2
  doc.text(label, cx, y, { align: 'center' })
  // Underline
  doc.setDrawColor(...theme.underline)
  doc.setLineWidth(0.4)
  doc.line(cx - tw / 2, y + 0.8, cx + tw / 2, y + 0.8)
  return y + 6
}

// Full-width themed-background table section header (for data tables)
function tableHeader(doc, label, y, pageW, margin = 14, theme = THEMES.amber) {
  doc.setFillColor(...theme.bg)
  doc.rect(margin, y, pageW - margin * 2, 6.5, 'F')
  doc.setTextColor(...theme.text)
  doc.setFontSize(8)
  doc.setFont('helvetica', 'bold')
  const tw = doc.getTextWidth(label)
  doc.text(label, pageW / 2, y + 4.5, { align: 'center' })
  // themed underline in cell
  doc.setDrawColor(...theme.underline)
  doc.setLineWidth(0.4)
  doc.line(pageW / 2 - tw / 2, y + 5.5, pageW / 2 + tw / 2, y + 5.5)
  return y + 6.5
}

function pageFooter(doc, logoB64, pageW, pageH, theme = THEMES.amber, companyName = 'Qalibrated Systems') {
  const y = pageH - 14
  // thin line
  doc.setDrawColor(...BORDER)
  doc.setLineWidth(0.3)
  doc.line(14, y, pageW - 14, y)

  // logo
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

  // tagline pill on right
  const tagW = 38, tagH = 7
  doc.setFillColor(...theme.bg)
  doc.roundedRect(pageW - 14 - tagW, y + 3, tagW, tagH, 1.5, 1.5, 'F')
  doc.setTextColor(...theme.pillText)
  doc.setFontSize(7)
  doc.setFont('helvetica', 'bold')
  doc.text('Inventing and Making Happen', pageW - 14 - tagW / 2, y + 7.3, { align: 'center' })
}

// ── QSL document design system ──────────────────────────────────────────────
// Mirrors the QSL "Quality Record" template (navy header band, gold-underlined
// left-aligned section headers, shaded info tables, minimal ref/page footer) —
// used for every Stores-module document (theme: 'navy'). Kept fully separate
// from the amber helpers above so other modules' exports are unaffected.
const QSL_MARGIN = 16
const QSL_INFO_BORDER = [220, 224, 234]  // #DCE0EA
const QSL_INFO_LABEL_BG = [245, 246, 250] // #F5F6FA

function qslHeader(doc, { title, docRef, badge, logo, pageW }) {
  const bandH = 30
  doc.setFillColor(...NAVY)
  doc.rect(0, 0, pageW, bandH, 'F')

  // Logo badge — white rounded box on the left, matching .logo-badge. Sized
  // to the actual logo aspect ratio so the mark+wordmark never distorts.
  const boxW = 24, boxH = bandH - 8
  const boxX = QSL_MARGIN, boxY = (bandH - boxH) / 2
  doc.setFillColor(...WHITE)
  doc.roundedRect(boxX, boxY, boxW, boxH, 1.5, 1.5, 'F')
  if (logo?.dataUri && logo?.dims?.w && logo?.dims?.h) {
    const pad = 2
    const maxW = boxW - pad * 2, maxH = boxH - pad * 2
    const scale = Math.min(maxW / logo.dims.w, maxH / logo.dims.h)
    const drawW = logo.dims.w * scale, drawH = logo.dims.h * scale
    try {
      doc.addImage(logo.dataUri, 'PNG', boxX + (boxW - drawW) / 2, boxY + (boxH - drawH) / 2, drawW, drawH)
    } catch { /* ignore */ }
  } else {
    doc.setTextColor(...NAVY)
    doc.setFontSize(13)
    doc.setFont('helvetica', 'bold')
    doc.text('Q', boxX + boxW / 2, boxY + boxH / 2 + 2, { align: 'center' })
  }

  // Title / doc-ref / badge — right-aligned, matching .header-right
  const rightX = pageW - QSL_MARGIN
  doc.setTextColor(...WHITE)
  doc.setFontSize(15)
  doc.setFont('helvetica', 'bold')
  doc.text(title.toUpperCase(), rightX, 13, { align: 'right' })

  doc.setTextColor(...GOLD)
  doc.setFontSize(9)
  doc.setFont('helvetica', 'bold')
  doc.text(docRef, rightX, 19, { align: 'right' })

  if (badge) {
    doc.setFontSize(7)
    doc.setFont('helvetica', 'bold')
    const bw = doc.getTextWidth(badge.label.toUpperCase()) + 7
    doc.setFillColor(...badge.color)
    doc.roundedRect(rightX - bw, 22, bw, 5.5, 1.2, 1.2, 'F')
    doc.setTextColor(...WHITE)
    doc.text(badge.label.toUpperCase(), rightX - bw / 2, 25.6, { align: 'center' })
  }

  return bandH + 8
}

// Left-aligned, gold-underlined section label — matches `h2.sec`.
function qslSectionHeader(doc, label, y) {
  doc.setFontSize(9.5)
  doc.setFont('helvetica', 'bold')
  doc.setTextColor(...NAVY)
  doc.text(label.toUpperCase(), QSL_MARGIN, y)
  const tw = doc.getTextWidth(label.toUpperCase())
  doc.setDrawColor(...GOLD)
  doc.setLineWidth(0.6)
  doc.line(QSL_MARGIN, y + 1.3, QSL_MARGIN + tw, y + 1.3)
  return y + 6
}

// Shaded key/value pairs — matches `table.info` (label cells: #F5F6FA, navy bold).
function qslInfoTable(doc, y, pairs, pageW) {
  autoTable(doc, {
    startY: y,
    body: pairs,
    columnStyles: {
      0: { fillColor: QSL_INFO_LABEL_BG, textColor: NAVY, fontStyle: 'bold', cellWidth: (pageW - QSL_MARGIN * 2) * 0.24 },
      1: { textColor: DARK,                                                  cellWidth: (pageW - QSL_MARGIN * 2) * 0.26 },
      2: { fillColor: QSL_INFO_LABEL_BG, textColor: NAVY, fontStyle: 'bold', cellWidth: (pageW - QSL_MARGIN * 2) * 0.24 },
      3: { textColor: DARK,                                                  cellWidth: (pageW - QSL_MARGIN * 2) * 0.26 },
    },
    styles: { fontSize: 8.2, cellPadding: { top: 3, bottom: 3, left: 3, right: 3 }, valign: 'top', lineColor: QSL_INFO_BORDER, lineWidth: 0.3 },
    tableLineColor: QSL_INFO_BORDER,
    tableLineWidth: 0.3,
    margin: { left: QSL_MARGIN, right: QSL_MARGIN },
    theme: 'grid',
  })
  return doc.lastAutoTable.finalY + 5
}

// Navy-headed data table — matches `table.actions`.
function qslListTable(doc, y, { head, body, columnStyles }, pageW) {
  autoTable(doc, {
    startY: y,
    head: [head],
    body,
    columnStyles,
    headStyles: { fillColor: NAVY, textColor: WHITE, fontStyle: 'bold', fontSize: 7.8, textTransform: 'uppercase' },
    styles: { fontSize: 8.4, cellPadding: { top: 4, bottom: 4, left: 3, right: 3 }, lineColor: [221, 221, 221], lineWidth: 0.2 },
    alternateRowStyles: { fillColor: [250, 251, 252] },
    margin: { left: QSL_MARGIN, right: QSL_MARGIN },
    theme: 'plain',
  })
  return doc.lastAutoTable.finalY + 5
}

// Minimal ref/page footer — matches the template's `@page { @bottom-center }`.
function qslFooter(doc, pageW, pageH, docRef, pageNum, totalPages, recordLabel = 'Stores Record') {
  const y = pageH - 10
  doc.setDrawColor(...BORDER)
  doc.setLineWidth(0.3)
  doc.line(QSL_MARGIN, y - 4, pageW - QSL_MARGIN, y - 4)
  doc.setFontSize(7.2)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(153, 153, 153)
  doc.text(`${docRef}  ·  Page ${pageNum} of ${totalPages}  ·  Confidential — Internal ${recordLabel}`, pageW / 2, y, { align: 'center' })
}

function qslDocRef(prefix, module = 'STORES', docPrefix = 'LT') {
  const d = new Date()
  const stamp = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`
  return `${docPrefix}/${module}/${prefix.toUpperCase().replace(/\s+/g, '-')}/${stamp}`
}

function qslRecordLabel(module) {
  return `${module.charAt(0)}${module.slice(1).toLowerCase()} Record`
}

/* ── Single-ticket PDF export ──────────────────────────────────────────────── */

export async function exportTicketPdf({
  ticket,
  comments    = [],
  history     = [],
  departments = [],
  attachments = [],
  companyName = 'QALIBRATED SYSTEMS LIMITED',
}) {
  const logoB64 = await loadLogo()

  const doc    = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' })
  const W      = doc.internal.pageSize.getWidth()   // 210
  const H      = doc.internal.pageSize.getHeight()  // 297
  const M      = 14   // margin

  // ── PAGE 1 HEADER ──────────────────────────────────────────────
  // Logo
  if (logoB64) {
    try { doc.addImage(logoB64, 'PNG', M, 8, 16, 16) } catch { /* ignore */ }
  } else {
    // Fallback circle Q
    doc.setFillColor(...AMBER)
    doc.circle(M + 8, 16, 8, 'F')
    doc.setTextColor(...BLACK)
    doc.setFontSize(10)
    doc.setFont('helvetica', 'bold')
    doc.text('Q', M + 8, 19.5, { align: 'center' })
  }

  // Company name block
  doc.setTextColor(...BLACK)
  doc.setFontSize(13)
  doc.setFont('helvetica', 'bold')
  doc.text(companyName, M + 20, 13)
  doc.setFontSize(8)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...MUTED)
  doc.text('PO BOX 34463-00100, NAIROBI', M + 20, 18.5)

  // Date/time top-right
  doc.setFontSize(8)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...MUTED)
  doc.text(fmtNow(), W - M, 13, { align: 'right' })

  // Status badge top-right
  const statusColors = {
    New:        BLACK,           Assigned:   [99, 102, 241],
    InProgress: AMBER,           Pending:    MUTED,
    Escalated:  [239, 68, 68],   Resolved:   [22, 163, 74],
    Closed:     DARK,            Reopened:   [234, 88, 12],
  }
  const sBg    = statusColors[ticket.statusLabel] ?? AMBER
  const sLabel = (ticket.statusLabel ?? 'New').toUpperCase()
  const sW     = doc.getTextWidth(sLabel) + 7
  doc.setFillColor(...sBg)
  doc.roundedRect(W - M - sW, 17, sW, 6, 1.5, 1.5, 'F')
  doc.setTextColor(...WHITE)
  doc.setFontSize(7)
  doc.setFont('helvetica', 'bold')
  doc.text(sLabel, W - M - sW / 2, 21.2, { align: 'center' })

  // Separator line + amber stripe
  doc.setDrawColor(...BORDER)
  doc.setLineWidth(0.3)
  doc.line(M, 27, W - M, 27)
  doc.setFillColor(...AMBER)
  doc.rect(M, 27, W - M * 2, 1, 'F')

  // ── DOCUMENT TITLE ─────────────────────────────────────────────
  let y = 36
  y = sectionTitle(doc, 'HELPDESK TICKET', y, W)

  // Ticket title as subtitle
  doc.setFontSize(9)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...DARK)
  const titleWrapped = doc.splitTextToSize(ticket.title ?? 'Untitled', W - M * 2)
  doc.text(titleWrapped[0], W / 2, y, { align: 'center' })
  y += 7

  // ── TICKET DETAILS TABLE ────────────────────────────────────────
  y = tableHeader(doc, 'TICKET DETAILS', y, W)

  const deptName  = departments.find(d => d.id === ticket.departmentId)?.name ?? ticket.departmentId ?? '—'
  const sourceMap = ['Manual', 'System Triggered', 'CRM', 'Safety Report', 'Scheduled']

  const detailRows = [
    ['TICKET NO',      ticket.id?.slice(0, 8).toUpperCase() ?? '—',
     'STATUS',         ticket.statusLabel ?? '—'],
    ['CATEGORY',       ticket.categoryName ?? '—',
     'PRIORITY',       ticket.priorityLabel ?? '—'],
    ['ASSIGNED TO',    ticket.assigneeName ?? ticket.assignedToUserId ?? 'Unassigned',
     'DEPARTMENT',     deptName],
    ['SOURCE',         sourceMap[ticket.source] ?? '—',
     'ESCALATED',      ticket.isEscalated ? 'Yes' : 'No'],
    ['CREATED',        fmtDate(ticket.createdAt),
     'RESPONSE DUE',   fmtDate(ticket.responseDueAt)],
    ['RESOLUTION DUE', fmtDate(ticket.resolutionDueAt),
     'RESOLVED AT',    fmtDate(ticket.resolvedAt)],
  ]

  const tblStart = y
  autoTable(doc, {
    startY: y,
    body: detailRows,
    columnStyles: {
      0: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 38 },
      1: { textColor: DARK,                                          cellWidth: 67 },
      2: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 38 },
      3: { textColor: DARK,                                          cellWidth: 43 },
    },
    styles:         { fontSize: 8, cellPadding: { top: 3, bottom: 3, left: 3.5, right: 3.5 }, valign: 'middle', lineColor: BORDER, lineWidth: 0.3 },
    tableLineColor: BORDER,
    tableLineWidth: 0.3,
    margin:         { left: M, right: M },
    theme:          'grid',
  })
  y = doc.lastAutoTable.finalY + 7

  // ── DESCRIPTION ────────────────────────────────────────────────
  if (ticket.description) {
    if (y > H - 60) { doc.addPage(); y = 20 }
    y = tableHeader(doc, 'DESCRIPTION', y, W)

    const rawDesc  = ticket.description.replace(/={3,}.*?={3,}/gs, '').trim()
    const descLines = doc.splitTextToSize(rawDesc, W - M * 2 - 12)
    const shown    = descLines.slice(0, 16)
    const blockH   = shown.length * 4.8 + 10

    doc.setFillColor(255, 255, 255)
    doc.setDrawColor(...BORDER)
    doc.setLineWidth(0.3)
    doc.rect(M, y, W - M * 2, blockH, 'FD')

    // Amber left bar
    doc.setFillColor(...AMBER)
    doc.rect(M, y, 3, blockH, 'F')

    doc.setTextColor(...DARK)
    doc.setFontSize(8)
    doc.setFont('helvetica', 'normal')
    doc.text(shown, M + 7, y + 6)

    if (descLines.length > 16) {
      doc.setFontSize(7)
      doc.setTextColor(...MUTED)
      doc.text('… (truncated)', M + 7, y + blockH - 3)
    }
    y += blockH + 7
  }

  // ── RESOLUTION NOTES ───────────────────────────────────────────
  if (ticket.resolutionNotes) {
    if (y > H - 60) { doc.addPage(); y = 20 }
    y = tableHeader(doc, 'RESOLUTION NOTES', y, W)

    const resLines  = doc.splitTextToSize(ticket.resolutionNotes, W - M * 2 - 12)
    const resShown  = resLines.slice(0, 10)
    const resH      = resShown.length * 4.8 + 10

    doc.setFillColor(240, 253, 244)
    doc.setDrawColor(187, 247, 208)
    doc.setLineWidth(0.3)
    doc.rect(M, y, W - M * 2, resH, 'FD')

    doc.setFillColor(22, 163, 74)
    doc.rect(M, y, 3, resH, 'F')

    doc.setTextColor(22, 101, 52)
    doc.setFontSize(8)
    doc.setFont('helvetica', 'normal')
    doc.text(resShown, M + 7, y + 6)
    y += resH + 7
  }

  // ── WEIGHT / MEASUREMENTS table (Attachments as "evidence list") ──
  if (attachments.length > 0) {
    if (y > H - 50) { doc.addPage(); y = 20 }
    y = tableHeader(doc, `ATTACHMENTS (${attachments.length})`, y, W)

    autoTable(doc, {
      startY: y,
      head:   [['FILE NAME', 'SIZE', 'TYPE', 'UPLOADED']],
      body:   attachments.map(a => [
        a.fileName ?? '—',
        a.fileSize ? `${(a.fileSize / 1024).toFixed(0)} KB` : '—',
        a.contentType ?? '—',
        fmtDate(a.uploadedAt ?? a.createdAt),
      ]),
      headStyles:  { fillColor: AMBER, textColor: BLACK, fontStyle: 'bold', fontSize: 8 },
      styles:      { fontSize: 7.5, cellPadding: 2.5, lineColor: BORDER, lineWidth: 0.3 },
      alternateRowStyles: { fillColor: LIGHT },
      tableLineColor: BORDER, tableLineWidth: 0.3,
      margin: { left: M, right: M },
      theme:  'grid',
    })
    y = doc.lastAutoTable.finalY + 7
  }

  // ── COMMENTS ───────────────────────────────────────────────────
  const visComments = comments.filter(c => !c.isInternal)
  if (visComments.length > 0) {
    if (y > H - 50) { doc.addPage(); y = 20 }
    y = tableHeader(doc, `COMMENTS (${visComments.length})`, y, W)

    autoTable(doc, {
      startY: y,
      head:   [['DATE', 'AUTHOR', 'COMMENT']],
      body:   visComments.map(c => [
        fmtDate(c.createdAt),
        c.authorName ?? c.authorUserId ?? '—',
        c.content ?? '—',
      ]),
      headStyles:  { fillColor: AMBER, textColor: BLACK, fontStyle: 'bold', fontSize: 8 },
      styles:      { fontSize: 7.5, cellPadding: 2.5, lineColor: BORDER, lineWidth: 0.3 },
      columnStyles: { 0: { cellWidth: 24 }, 1: { cellWidth: 32 } },
      alternateRowStyles: { fillColor: LIGHT },
      tableLineColor: BORDER, tableLineWidth: 0.3,
      margin: { left: M, right: M },
      theme:  'grid',
    })
    y = doc.lastAutoTable.finalY + 7
  }

  // ── ACTIVITY HISTORY ───────────────────────────────────────────
  if (history.length > 0) {
    if (y > H - 50) { doc.addPage(); y = 20 }
    y = tableHeader(doc, `ACTIVITY HISTORY (${history.length})`, y, W)

    autoTable(doc, {
      startY: y,
      head:   [['DATE', 'ACTION', 'FROM', 'TO', 'NOTES']],
      body:   history.map(h => [
        fmtDate(h.occurredAt),
        h.action ?? '—',
        h.fromValue ?? '—',
        h.toValue ?? '—',
        h.notes ?? '—',
      ]),
      headStyles:  { fillColor: AMBER, textColor: BLACK, fontStyle: 'bold', fontSize: 8 },
      styles:      { fontSize: 7.5, cellPadding: 2.5, lineColor: BORDER, lineWidth: 0.3 },
      columnStyles: { 0: { cellWidth: 24 }, 1: { cellWidth: 30 }, 2: { cellWidth: 22 }, 3: { cellWidth: 22 } },
      alternateRowStyles: { fillColor: LIGHT },
      tableLineColor: BORDER, tableLineWidth: 0.3,
      margin: { left: M, right: M },
      theme:  'grid',
    })
  }

  // ── FOOTER on every page ───────────────────────────────────────
  const ticketTemplate = await fetchDocTemplate('ticket')
  const totalPages = doc.getNumberOfPages()
  for (let i = 1; i <= totalPages; i++) {
    doc.setPage(i)
    if (i === totalPages) drawDocTemplateNote(doc, ticketTemplate, W, H)
    pageFooter(doc, logoB64, W, H, undefined, companyName)
    doc.setFontSize(7.5)
    doc.setTextColor(...MUTED)
    doc.setFont('helvetica', 'normal')
    doc.text(`Page ${i} of ${totalPages}`, W / 2, H - 5, { align: 'center' })
  }

  doc.save(`QC-TICKET-${(ticket.id ?? 'export').slice(0, 8).toUpperCase()}.pdf`)
}

/* ── Single-GRN PDF ────────────────────────────────────────────────────────── */

export async function exportGrnPdf({ grn, stockUnits = [], docPrefix = 'LT' }) {
  const logo = await loadQslLogo()

  const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()

  const docRef = `${docPrefix}/GRN/${(grn.id ?? '').slice(0, 8).toUpperCase() || 'DRAFT'}`
  const statusColors = { Pending: MUTED, Passed: [22, 163, 74], Failed: [239, 68, 68] }
  let y = qslHeader(doc, {
    title: 'Goods Received Note', docRef, logo, pageW: W,
    badge: { label: grn.inspectionStatus ?? 'Pending', color: statusColors[grn.inspectionStatus] ?? MUTED },
  })

  y = qslSectionHeader(doc, 'GRN Details', y)
  y = qslInfoTable(doc, y, [
    ['ITEM',         `${grn.itemCode ?? '—'} — ${grn.itemName ?? '—'}`,
     'SUPPLIER',     grn.supplierName ?? '—'],
    ['LOCATION',     grn.locationName ?? '—',
     'QTY RECEIVED', String(grn.qtyReceived ?? '—')],
    ['LANDED COST',  fmtKES(grn.landedCost),
     'UNIT COST',    fmtKES(grn.unitCost)],
    ['PRICE VARIANCE', grn.alertLevel && grn.alertLevel !== 'None' ? `+${grn.variancePct?.toFixed(1)}%` : '—',
     'STATUS',       grn.inspectionStatus ?? '—'],
    ['INSPECTED BY', grn.inspectedBy || '—',
     'INSPECTED AT', grn.inspectedAt ? fmtDate(grn.inspectedAt) : '—'],
    ['CREATED',      fmtDate(grn.createdAt),
     'NOTES',        grn.notes || '—'],
  ], W)

  if (y > H - 60) { doc.addPage(); y = 20 }
  y = qslSectionHeader(doc, `Stock Units (${stockUnits.length})`, y)
  qslListTable(doc, y, {
    head: ['Unit', 'Location', 'Qty Remaining', 'Status'],
    body: stockUnits.length
      ? stockUnits.map(u => [u.serialNo || 'Bulk lot', u.locationName || '—', String(u.qty), u.status])
      : [['—', 'Stock units are created once this GRN passes inspection.', '—', '—']],
  }, W)

  const grnTemplate = await fetchDocTemplate('grn')
  const totalPages = doc.getNumberOfPages()
  for (let i = 1; i <= totalPages; i++) {
    doc.setPage(i)
    if (i === totalPages) drawDocTemplateNote(doc, grnTemplate, W, H)
    qslFooter(doc, W, H, docRef, i, totalPages)
  }

  doc.save(`GRN-${(grn.id ?? 'export').slice(0, 8).toUpperCase()}.pdf`)
}

/* ── Generic list PDF ──────────────────────────────────────────────────────── */

export async function exportToPdf({ title, subtitle, columns, rows, filename, theme = 'amber', docRef, docModule = 'STORES', companyName = 'QALIBRATED SYSTEMS LIMITED', docPrefix = 'LT' }) {
  if (theme === 'navy') {
    // QSL document design system — same header band / section style / table
    // style / footer as the Stores single-record documents (exportGrnPdf).
    const logo = await loadQslLogo()
    const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' })
    const W = doc.internal.pageSize.getWidth()
    const H = doc.internal.pageSize.getHeight()
    const ref = docRef ?? qslDocRef(title, docModule, docPrefix)

    let y = qslHeader(doc, { title, docRef: ref, logo, pageW: W })
    if (subtitle) {
      doc.setFontSize(8)
      doc.setFont('helvetica', 'normal')
      doc.setTextColor(...MUTED)
      doc.text(subtitle, QSL_MARGIN, y)
      y += 6
    }
    qslListTable(doc, y, {
      head: columns.map(c => c.header),
      body: rows.map(row => columns.map(c => c.accessor(row) ?? '—')),
    }, W)

    const totalPages = doc.getNumberOfPages()
    for (let i = 1; i <= totalPages; i++) {
      doc.setPage(i)
      qslFooter(doc, W, H, ref, i, totalPages, qslRecordLabel(docModule))
    }

    doc.save(`${filename ?? title}.pdf`)
    return
  }

  const t = resolveTheme(theme)
  const logoB64 = await loadLogo()

  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' })
  const W   = doc.internal.pageSize.getWidth()
  const H   = doc.internal.pageSize.getHeight()
  const M   = 14

  // Header
  if (logoB64) {
    try { doc.addImage(logoB64, 'PNG', M, 6, 12, 12) } catch { /* ignore */ }
  } else {
    doc.setFillColor(...t.bg)
    doc.circle(M + 6, 12, 6, 'F')
    doc.setTextColor(...t.text)
    doc.setFontSize(8)
    doc.setFont('helvetica', 'bold')
    doc.text('Q', M + 6, 15, { align: 'center' })
  }

  doc.setFontSize(12)
  doc.setFont('helvetica', 'bold')
  doc.setTextColor(...BLACK)
  doc.text(companyName, M + 16, 11)
  doc.setFontSize(7.5)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...MUTED)
  doc.text('PO BOX 34463-00100, NAIROBI', M + 16, 16)

  doc.setFontSize(8)
  doc.setTextColor(...MUTED)
  doc.text(fmtNow(), W - M, 11, { align: 'right' })

  doc.setDrawColor(...BORDER)
  doc.setLineWidth(0.3)
  doc.line(M, 21, W - M, 21)
  doc.setFillColor(...t.bg)
  doc.rect(M, 21, W - M * 2, 1, 'F')

  // Report title
  let y = 30
  y = sectionTitle(doc, title.toUpperCase(), y, W, t)
  if (subtitle) {
    doc.setFontSize(7.5)
    doc.setFont('helvetica', 'normal')
    doc.setTextColor(...MUTED)
    doc.text(subtitle, W / 2, y, { align: 'center' })
    y += 5
  }

  autoTable(doc, {
    startY: y,
    head:   [columns.map(c => c.header)],
    body:   rows.map(row => columns.map(c => c.accessor(row) ?? '—')),
    headStyles:  { fillColor: t.bg, textColor: t.text, fontStyle: 'bold', fontSize: 8 },
    styles:      { fontSize: 7.5, cellPadding: 2.5, lineColor: BORDER, lineWidth: 0.3 },
    alternateRowStyles: { fillColor: LIGHT },
    tableLineColor: BORDER, tableLineWidth: 0.3,
    margin: { left: M, right: M },
    theme:  'grid',
  })

  const totalPages = doc.getNumberOfPages()
  for (let i = 1; i <= totalPages; i++) {
    doc.setPage(i)
    pageFooter(doc, logoB64, W, H, t, companyName)
    doc.setFontSize(7.5)
    doc.setTextColor(...MUTED)
    doc.setFont('helvetica', 'normal')
    doc.text(`Page ${i} of ${totalPages}`, W / 2, H - 5, { align: 'center' })
  }

  doc.save(`${filename ?? title}.pdf`)
}

/* ── Report PDF export (Reporting & Analytics tabs) ──────────────────────────
   Generic multi-section report PDF built from the same QSL document design
   system helpers (qslHeader/qslSectionHeader/qslInfoTable/qslListTable/
   qslFooter) used everywhere else, so exported reports match the rest of the
   app's PDFs and the in-app navy/gold theme rather than a one-off style. */
export async function exportReportToPdf({ title, docRef, filename, summary, sections = [], docPrefix = 'LT' }) {
  const logo = await loadQslLogo()
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()
  const ref = docRef ?? qslDocRef(title, 'REPORTS', docPrefix)

  let y = qslHeader(doc, { title, docRef: ref, logo, pageW: W })

  if (summary?.length) {
    y = qslSectionHeader(doc, 'Summary', y)
    const pairs = []
    for (let i = 0; i < summary.length; i += 2) {
      const a = summary[i], b = summary[i + 1]
      pairs.push([
        a.label.toUpperCase(), String(a.value ?? '—'),
        b ? b.label.toUpperCase() : '', b ? String(b.value ?? '—') : '',
      ])
    }
    y = qslInfoTable(doc, y, pairs, W) + 4
  }

  for (const section of sections) {
    if (y > H - 55) { doc.addPage(); y = 20 }
    y = qslSectionHeader(doc, section.heading, y)
    if (!section.rows?.length) {
      doc.setFontSize(8)
      doc.setFont('helvetica', 'italic')
      doc.setTextColor(...MUTED)
      doc.text(section.emptyText ?? 'No data.', QSL_MARGIN, y + 3)
      y += 10
      continue
    }
    y = qslListTable(doc, y, {
      head: section.columns.map(c => c.header),
      body: section.rows.map(row => section.columns.map(c => c.accessor(row) ?? '—')),
    }, W) + 3
  }

  const totalPages = doc.getNumberOfPages()
  for (let i = 1; i <= totalPages; i++) {
    doc.setPage(i)
    qslFooter(doc, W, H, ref, i, totalPages, 'Report')
  }

  doc.save(`${filename ?? title}.pdf`)
}

/* ── Excel ─────────────────────────────────────────────────────────────────── */

/** Excel display format for money: shows "Kshs 1,234.56" while the cell value stays numeric. */
export const MONEY_NUM_FMT = '"Kshs" #,##0.00'

/**
 * Same shape as `MONEY_NUM_FMT`, but for a specific currency (#288) — a fixed column format
 * can't get this right on its own when rows carry different currencies (e.g. a mixed-currency
 * invoice list), since `numFmt` below is applied per column. Mirrors `fmt.money`'s label rule:
 * no code, or the base currency `'KES'`, still renders "Kshs", matching every existing export.
 */
export const moneyNumFmt = (ccy) => `"${(!ccy || ccy === 'KES') ? 'Kshs' : ccy}" #,##0.00`

/**
 * Writes an .xlsx file from a column definition and rows.
 *
 * A column may declare an optional `raw(row)` returning the underlying number. When it
 * does, the cell is written as a **real number** with `MONEY_NUM_FMT` applied, so the
 * recipient can sum, chart and pivot it — previously every money column arrived as text
 * like "Kshs 1,234.56" and had to be cleaned by hand before any of that worked (#205).
 *
 * `accessor` is still the display string and is what PDF export uses, so the two stay in
 * step: Excel shows the same thing, it is just backed by a number rather than a string.
 * A `raw` that returns null/undefined/NaN falls back to `accessor`, so a missing figure
 * renders exactly as it does on screen rather than becoming a misleading numeric 0.
 */
export function exportToExcel({ title, columns, rows, filename, sheetName }) {
  const header = columns.map(c => c.header)
  const data   = rows.map(row => columns.map(c => {
    if (typeof c.raw === 'function') {
      const rawValue = c.raw(row)
      // Check for absence BEFORE Number(): Number(null) and Number('') are both 0, so
      // coercing first would write a confident numeric zero for a value that was never
      // recorded — the exact thing this fallback exists to prevent.
      if (rawValue !== null && rawValue !== undefined && rawValue !== '') {
        const v = Number(rawValue)
        if (Number.isFinite(v)) return v
      }
    }
    return c.accessor(row) ?? ''
  }))

  const ws = XLSX.utils.aoa_to_sheet([header, ...data])

  // Apply the display format to numeric cells. Guarded on cell.t === 'n' so a row whose
  // raw value was missing — and therefore fell back to a string — is left alone.
  //
  // `numFmt` may be a function of the row instead of a fixed string (#288) — a static
  // per-column format can't label each row's own currency correctly when rows don't all
  // share one, e.g. `numFmt: (row) => moneyNumFmt(row.currencyCode)`.
  columns.forEach((col, colIdx) => {
    if (typeof col.raw !== 'function') return
    for (let rowIdx = 1; rowIdx <= rows.length; rowIdx++) {
      const cell = ws[XLSX.utils.encode_cell({ c: colIdx, r: rowIdx })]
      if (!cell || cell.t !== 'n') continue
      const fmt = typeof col.numFmt === 'function' ? col.numFmt(rows[rowIdx - 1]) : col.numFmt
      cell.z = fmt ?? MONEY_NUM_FMT
    }
  })

  ws['!cols']  = columns.map(c => ({ wch: c.width ?? 20 }))
  const wb     = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, sheetName ?? title.slice(0, 31))
  XLSX.writeFile(wb, `${filename ?? title}.xlsx`)
}

/* ── Shared utilities ──────────────────────────────────────────────────────── */

// Delegates to the single money formatter in theme/tokens.js (#205). This used to be a
// separate Intl.NumberFormat with maximumFractionDigits: 0, which rounded every exported
// figure to whole shillings, and coerced missing values to a real "Ksh 0".
export function fmtKES(n) {
  return kes(n)
}

export function fmtDate(d) {
  return d ? new Date(d).toLocaleDateString('en-KE', { dateStyle: 'medium' }) : '—'
}

export const PROJECT_COLUMNS = [
  { header: 'Project Name',    accessor: r => r.name,                                    width: 30 },
  { header: 'Client',          accessor: r => r.clientName,                               width: 22 },
  { header: 'Type',            accessor: r => ['Service','Construction','Calibration'][r.type], width: 15 },
  { header: 'Status',          accessor: r => ['Draft','Planning','Pending Approval','Active','On Hold','Completed','Closed','Cancelled'][r.status], width: 18 },
  { header: 'Risk',            accessor: r => ['Low','Medium','High'][r.riskLevel],       width: 12 },
  { header: 'Contract Value',  accessor: r => fmtKES(r.contractValue), raw: r => r.contractValue,                   width: 20 },
  { header: 'Planned Budget',  accessor: r => fmtKES(r.plannedBudget), raw: r => r.plannedBudget,                   width: 20 },
  { header: 'Actual Cost',     accessor: r => fmtKES(r.actualCost), raw: r => r.actualCost,                      width: 18 },
  { header: 'Start Date',      accessor: r => fmtDate(r.startDate),                      width: 16 },
  { header: 'Expected End',    accessor: r => fmtDate(r.expectedEndDate),                width: 16 },
  { header: 'Milestones',      accessor: r => r.milestoneCount,                          width: 13 },
]

export const TICKET_COLUMNS = [
  { header: 'Title',           accessor: r => r.title,                                   width: 35 },
  { header: 'Category',        accessor: r => r.categoryName,                            width: 20 },
  { header: 'Status',          accessor: r => r.statusLabel,                             width: 15 },
  { header: 'Priority',        accessor: r => r.priorityLabel,                           width: 14 },
  { header: 'Assignee',        accessor: r => r.assigneeName,                            width: 20 },
  { header: 'Escalated',       accessor: r => r.isEscalated ? 'Yes' : 'No',             width: 12 },
  { header: 'Response Due',    accessor: r => fmtDate(r.responseDueAt),                 width: 18 },
  { header: 'Resolution Due',  accessor: r => fmtDate(r.resolutionDueAt),               width: 18 },
  { header: 'Created',         accessor: r => fmtDate(r.createdAt),                     width: 16 },
  { header: 'Rating',          accessor: r => r.satisfactionRating ? `${r.satisfactionRating.rating}/5` : '—', width: 12 },
]

export const PERMISSION_COLUMNS = [
  { header: 'Permission Key', accessor: r => r.name,                                    width: 35 },
  { header: 'Description',    accessor: r => r.description ?? '',                       width: 55 },
]

export const ROLE_COLUMNS = [
  { header: 'Role Name',    accessor: r => r.name,                                      width: 28 },
  { header: 'Description', accessor: r => r.description ?? '',                          width: 48 },
  { header: 'Permissions', accessor: r => (r.permissions ?? []).length,                 width: 16 },
  { header: 'Users',       accessor: r => r.userCount ?? 0,                             width: 12 },
]

export const USER_COLUMNS = [
  { header: 'First Name',  accessor: r => r.firstName,                                  width: 20 },
  { header: 'Last Name',   accessor: r => r.lastName,                                   width: 20 },
  { header: 'Email',       accessor: r => r.email,                                      width: 32 },
  { header: 'Department',  accessor: r => r.departmentName ?? '',                       width: 22 },
  { header: 'Roles',       accessor: r => (r.roles ?? []).join(', '),                   width: 28 },
  { header: 'Status',      accessor: r => r.isActive ? 'Active' : 'Inactive',           width: 12 },
  { header: 'Created',     accessor: r => fmtDate(r.createdAt),                         width: 16 },
]

export const DEPARTMENT_COLUMNS = [
  { header: 'Name',        accessor: r => r.name,                                       width: 30 },
  { header: 'Description', accessor: r => r.description ?? '',                          width: 48 },
  { header: 'Users',       accessor: r => r.userCount ?? 0,                             width: 12 },
  { header: 'Status',      accessor: r => r.isActive ? 'Active' : 'Inactive',           width: 14 },
]

export const GRN_COLUMNS = [
  { header: 'Item Code',    accessor: r => r.itemCode,                                   width: 16 },
  { header: 'Item Name',    accessor: r => r.itemName,                                    width: 26 },
  { header: 'Supplier',     accessor: r => r.supplierName,                                width: 22 },
  { header: 'Location',     accessor: r => r.locationName,                                width: 18 },
  { header: 'Qty Received', accessor: r => r.qtyReceived,                                 width: 14 },
  { header: 'Landed Cost',  accessor: r => fmtKES(r.landedCost), raw: r => r.landedCost,                          width: 18 },
  { header: 'Unit Cost',    accessor: r => fmtKES(r.unitCost), raw: r => r.unitCost,                            width: 16 },
  { header: 'Variance %',   accessor: r => r.alertLevel && r.alertLevel !== 'None' ? `+${r.variancePct?.toFixed(1)}%` : '—', width: 14 },
  { header: 'Status',       accessor: r => r.inspectionStatus,                            width: 14 },
  { header: 'Inspected By', accessor: r => r.inspectedBy ?? '',                           width: 18 },
  { header: 'Inspected At', accessor: r => fmtDate(r.inspectedAt),                        width: 16 },
]

export const STORE_ISSUE_COLUMNS = [
  { header: 'Item Code',   accessor: r => r.itemCode,                                    width: 16 },
  { header: 'Location',    accessor: r => r.locationName ?? '',                          width: 18 },
  { header: 'Qty Issued',  accessor: r => r.qtyIssued,                                    width: 12 },
  { header: 'Cost Center', accessor: r => r.costCenter,                                   width: 18 },
  { header: 'Issue Type',  accessor: r => r.issueType,                                     width: 14 },
  { header: 'Issued To',   accessor: r => r.issuedTo,                                      width: 20 },
  { header: 'Issued On',   accessor: r => fmtDate(r.issuedOn),                            width: 16 },
]

export const ITEM_COLUMNS = [
  { header: 'Item Code',   accessor: r => r.itemCode,                                    width: 16 },
  { header: 'Description', accessor: r => r.description,                                 width: 30 },
  { header: 'Category',    accessor: r => r.categoryName,                                width: 18 },
  { header: 'Supplier',    accessor: r => r.supplierName,                                width: 20 },
  { header: 'UoM',         accessor: r => r.uom,                                          width: 10 },
  { header: 'Qty on Hand', accessor: r => r.qtyOnHand,                                    width: 14 },
  { header: 'Avg Cost',    accessor: r => fmtKES(r.avgWeightedCost), raw: r => r.avgWeightedCost,                      width: 16 },
  { header: 'Min Stock',   accessor: r => r.minStockLevel,                                width: 12 },
  { header: 'Reorder Qty', accessor: r => r.reorderQty,                                   width: 14 },
  { header: 'Status',      accessor: r => r.isActive ? 'Active' : 'Inactive',            width: 12 },
]

export const SUPPLIER_COLUMNS = [
  { header: 'Name',           accessor: r => r.name,                                     width: 26 },
  { header: 'KRA PIN',        accessor: r => r.kraPin ?? '',                             width: 16 },
  { header: 'Contact Person', accessor: r => r.contactPerson ?? '',                      width: 20 },
  { header: 'Phone',          accessor: r => r.phone ?? '',                              width: 16 },
  { header: 'Email',          accessor: r => r.email ?? '',                              width: 24 },
  { header: 'Rating',         accessor: r => r.rating ?? '',                             width: 10 },
  { header: 'Items Supplied', accessor: r => r.itemCount ?? 0,                           width: 14 },
]

export const SOLD_ITEM_COLUMNS = [
  { header: 'Item Code',  accessor: r => r.itemCode,                                     width: 16 },
  { header: 'Invoice No', accessor: r => r.invoiceNo,                                     width: 18 },
  { header: 'Qty',        accessor: r => r.qty,                                           width: 10 },
  { header: 'Sale Price', accessor: r => fmtKES(r.salePrice), raw: r => r.salePrice,                            width: 16 },
  { header: 'Cost',       accessor: r => fmtKES(r.costAtSale), raw: r => r.costAtSale,                           width: 16 },
  { header: 'Margin',     accessor: r => fmtKES(r.grossMargin), raw: r => r.grossMargin,                          width: 16 },
  { header: 'Sold On',    accessor: r => fmtDate(r.soldOn),                              width: 16 },
]

export const TRANSFER_COLUMNS = [
  { header: 'Item Code',    accessor: r => r.itemCode,                                   width: 16 },
  { header: 'From',         accessor: r => r.fromLocationName,                           width: 18 },
  { header: 'To',           accessor: r => r.toLocationName,                             width: 18 },
  { header: 'Qty',          accessor: r => r.qty,                                         width: 10 },
  { header: 'Status',       accessor: r => r.status,                                     width: 14 },
  { header: 'Approved By',  accessor: r => r.approvedBy ?? '',                           width: 20 },
  { header: 'Approved At',  accessor: r => fmtDate(r.approvedAt),                        width: 16 },
]

export const STOCK_TAKE_COLUMNS = [
  { header: 'Item Code', accessor: r => r.itemCode,                                       width: 16 },
  { header: 'Location',  accessor: r => r.locationName ?? '',                            width: 18 },
  { header: 'Physical',  accessor: r => r.physicalCount,                                  width: 12 },
  { header: 'System',    accessor: r => r.systemCount,                                    width: 12 },
  { header: 'Variance',  accessor: r => r.variance,                                        width: 12 },
  { header: 'Reason',    accessor: r => r.reasonCode,                                      width: 16 },
  { header: 'Status',    accessor: r => r.status,                                          width: 14 },
]

export const TRIP_COLUMNS = [
  { header: 'Date',      accessor: r => fmtDate(r.date),                                  width: 14 },
  { header: 'Route',     accessor: r => `${r.startLocation ?? '—'} -> ${r.endLocation ?? '—'}`, width: 30 },
  { header: 'Trip Type', accessor: r => r.tripTypeName ?? '—',                            width: 16 },
  { header: 'Driver',    accessor: r => r.driverLabel ?? '—',                             width: 22 },
  { header: 'Truck',     accessor: r => r.truckLabel ?? '—',                              width: 20 },
  { header: 'Status',    accessor: r => r.statusLabel ?? r.status,                        width: 14 },
  { header: 'Mileage',   accessor: r => r.totalMileage ?? '—',                             width: 12 },
  { header: 'Revenue',   accessor: r => fmtKES(r.revenue), raw: r => r.revenue,                                width: 14 },
  { header: 'Cost',      accessor: r => fmtKES(r.totalCost), raw: r => r.totalCost,                              width: 14 },
  { header: 'Profit',    accessor: r => fmtKES(r.profit ?? ((r.revenue ?? 0) - (r.totalCost ?? 0))), raw: r => r.profit ?? ((r.revenue ?? 0) - (r.totalCost ?? 0)), width: 14 },
  { header: 'Banked',    accessor: r => r.bankedLabel ?? '—',                            width: 12 },
]

// Mirrors FleetService.Core.Entities.TruckStatus — the API returns this as a number.
const TRUCK_STATUS_LABEL = { 0: 'Active', 1: 'In Maintenance', 2: 'Decommissioned', 3: 'Out of Service' }

export const TRUCK_COLUMNS = [
  { header: 'Reg No',           accessor: r => r.licensePlate,                             width: 18 },
  { header: 'Make',             accessor: r => r.model,                                    width: 22 },
  { header: 'Class',            accessor: r => r.vehicleClass ?? '—',                      width: 14 },
  { header: 'Driver',           accessor: r => r.driverName ?? '—',                        width: 28 },
  { header: 'Insurance Expiry', accessor: r => fmtDate(r.insuranceExpiryDate),             width: 18 },
  { header: 'Next Service',     accessor: r => fmtDate(r.nextServiceDate),                 width: 18 },
  { header: 'Odometer',         accessor: r => r.odometer != null ? `${Number(r.odometer).toLocaleString()} km` : '—', width: 16 },
  { header: 'Status',           accessor: r => TRUCK_STATUS_LABEL[r.status] ?? r.status ?? 'Active', width: 14 },
]

export const MATERIAL_COLUMNS = [
  { header: 'Material Name', accessor: r => r.name,                                        width: 30 },
  { header: 'Description',   accessor: r => r.description ?? '',                           width: 50 },
]

export const DRIVER_COLUMNS = [
  { header: 'Name',           accessor: r => r.fullName || '—',                            width: 22 },
  { header: 'Email',          accessor: r => r.email ?? '',                                width: 26 },
  { header: 'Phone',          accessor: r => r.phoneNumber ?? '—',                          width: 16 },
  { header: 'ID No.',         accessor: r => r.idNumber ?? '—',                             width: 16 },
  { header: 'License No.',    accessor: r => r.licenseNumber ?? '—',                        width: 18 },
  { header: 'License Expiry', accessor: r => fmtDate(r.licenseExpiryDate),                 width: 16 },
  { header: 'Status',         accessor: r => r.statusLabel ?? '—',                          width: 14 },
]

export const FIELD_VEHICLE_COLUMNS = [
  { header: 'Reg No',            accessor: r => r.registrationNumber,                        width: 16 },
  { header: 'Make',              accessor: r => r.make,                                      width: 16 },
  { header: 'Model',             accessor: r => r.model,                                      width: 16 },
  { header: 'Type',              accessor: r => r.type,                                       width: 12 },
  { header: 'Status',            accessor: r => r.status,                                     width: 14 },
  { header: 'Odometer',          accessor: r => r.currentOdometer != null ? `${Number(r.currentOdometer).toLocaleString()} km` : '—', width: 14 },
  { header: 'Last Service',      accessor: r => fmtDate(r.lastServiceDate),                  width: 14 },
  { header: 'Next Service',      accessor: r => fmtDate(r.nextServiceDate),                  width: 14 },
  { header: 'Insurance Expiry',  accessor: r => r.insuranceExpiry ? fmtDate(r.insuranceExpiry) : '—', width: 16 },
]
