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
const GREEN      = [ 22, 163,  74]
const RED        = [239,  68,  68]
const BLUE       = [ 59, 130, 246]

const STATUS_COLORS = {
  Draft:                  MUTED,
  Planning:               AMBER,
  PendingMdApproval:      [249, 115, 22],
  PendingFinanceApproval: [249, 115, 22],
  Active:                 GREEN,
  OnHold:                 RED,
  Completed:              BLUE,
  Closed:                 [55, 65, 81],
  Cancelled:              RED,
}

const MILESTONE_COLORS = {
  NotStarted: MUTED,
  InProgress: BLUE,
  Completed:  GREEN,
  Delayed:    RED,
}

const TASK_COLORS = {
  NotStarted: MUTED,
  InProgress: BLUE,
  Done:       GREEN,
  Blocked:    RED,
}

// ── Shared helpers ────────────────────────────────────────────────────────────

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
  doc.rect(M, y, W - M * 2, 6.5, 'F')
  doc.setTextColor(...BLACK)
  doc.setFontSize(8)
  doc.setFont('helvetica', 'bold')
  doc.text(label, W / 2, y + 4.5, { align: 'center' })
  return y + 6.5
}

function textBlock(doc, text, y, W, M = 14) {
  const lines  = doc.splitTextToSize(text, W - M * 2 - 12)
  const shown  = lines.slice(0, 14)
  const blockH = shown.length * 4.8 + 10
  doc.setFillColor(255, 255, 255)
  doc.setDrawColor(...BORDER)
  doc.setLineWidth(0.3)
  doc.rect(M, y, W - M * 2, blockH, 'FD')
  doc.setFillColor(...AMBER)
  doc.rect(M, y, 3, blockH, 'F')
  doc.setTextColor(...DARK)
  doc.setFontSize(8)
  doc.setFont('helvetica', 'normal')
  doc.text(shown, M + 7, y + 6)
  if (lines.length > 14) {
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
  styles:         { fontSize: 8, cellPadding: { top: 3, bottom: 3, left: 3.5, right: 3.5 }, valign: 'middle', lineColor: BORDER, lineWidth: 0.3 },
  tableLineColor: BORDER,
  tableLineWidth: 0.3,
  theme:          'grid',
}

const listHeadStyle = { fillColor: AMBER, textColor: BLACK, fontStyle: 'bold', fontSize: 8 }

function xlsxHeader(title, ref, companyName = 'QALIBRATED SYSTEMS LIMITED') {
  return [
    [companyName, '', '', '', '', '', '', ''],
    [title, ref ? `Ref: ${ref}` : '', '', '', '', fmtNow(), '', ''],
    [],
  ]
}

// ═══════════════════════════════════════════════════════════════════════════════
// PROJECTS LIST
// ═══════════════════════════════════════════════════════════════════════════════

export async function exportProjectsListPDF(projects, companyName) {
  const logoB64 = await loadLogo()
  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()
  const M = 14

  let y = pageHeader(doc, logoB64, null, null, W, M, companyName)
  y = sectionTitle(doc, 'PROJECTS', y, W)
  doc.setFontSize(7.5)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...MUTED)
  doc.text(`${projects.length} project${projects.length !== 1 ? 's' : ''}  ·  Generated ${fmtNow()}`, W / 2, y, { align: 'center' })
  y += 6

  autoTable(doc, {
    ...gridStyle,
    startY: y,
    head: [['PROJECT NAME', 'CLIENT', 'TYPE', 'STATUS', 'RISK', 'PLANNED BUDGET', 'CONTRACT VALUE', 'START DATE', 'END DATE', 'MILESTONES']],
    headStyles: listHeadStyle,
    alternateRowStyles: { fillColor: LIGHT },
    body: projects.map(p => [
      p.name,
      p.clientName ?? '—',
      p.type,
      p.status,
      p.riskLevel,
      fmtKES(p.plannedBudget),
      fmtKES(p.contractValue),
      fmtDate(p.startDate),
      fmtDate(p.expectedEndDate),
      p.milestoneCount ?? 0,
    ]),
    columnStyles: {
      0: { cellWidth: 42 },
      1: { cellWidth: 28 },
      2: { cellWidth: 20 },
      3: { cellWidth: 22 },
      4: { cellWidth: 14 },
      5: { cellWidth: 28 },
      6: { cellWidth: 28 },
      7: { cellWidth: 20 },
      8: { cellWidth: 20 },
      9: { cellWidth: 16 },
    },
    margin: { left: M, right: M },
  })

  finalize(doc, logoB64, W, H, 'QC-PROJECTS-LIST.pdf', companyName)
}

export function exportProjectsListExcel(projects, companyName) {
  const rows = [
    ...xlsxHeader('PROJECTS LIST', undefined, companyName),
    ['Project Name', 'Client', 'Type', 'Status', 'Risk', 'Planned Budget (KES)', 'Contract Value (KES)', 'Start Date', 'End Date', 'Milestones'],
    ...projects.map(p => [
      p.name,
      p.clientName ?? '',
      p.type,
      p.status,
      p.riskLevel,
      p.plannedBudget ?? 0,
      p.contractValue ?? 0,
      fmtDate(p.startDate),
      fmtDate(p.expectedEndDate),
      p.milestoneCount ?? 0,
    ]),
  ]
  const ws = XLSX.utils.aoa_to_sheet(rows)
  ws['!cols'] = [
    { wch: 38 }, { wch: 22 }, { wch: 16 }, { wch: 20 }, { wch: 10 },
    { wch: 20 }, { wch: 20 }, { wch: 14 }, { wch: 14 }, { wch: 12 },
  ]
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Projects')
  XLSX.writeFile(wb, 'QC-PROJECTS-LIST.xlsx')
}

// ═══════════════════════════════════════════════════════════════════════════════
// PROJECT DETAIL  (overview + milestones/tasks + budget + resources)
// ═══════════════════════════════════════════════════════════════════════════════

export async function exportProjectDetailPDF(project, milestones, milestoneTasks, budget, resources, companyName) {
  const logoB64 = await loadLogo()
  const doc = new jsPDF({ unit: 'mm', format: 'a4' })
  const W = doc.internal.pageSize.getWidth()
  const H = doc.internal.pageSize.getHeight()
  const M = 14
  const ref = project.id.slice(0, 8).toUpperCase()

  // ── Page 1: Header + Overview ─────────────────────────────────────────────
  let y = pageHeader(doc, logoB64, project.status, STATUS_COLORS[project.status] ?? AMBER, W, M, companyName)
  y = sectionTitle(doc, 'PROJECT REPORT', y, W)

  doc.setFontSize(11)
  doc.setFont('helvetica', 'bold')
  doc.setTextColor(...BLACK)
  doc.text(project.name, W / 2, y, { align: 'center' })
  y += 5
  doc.setFontSize(8)
  doc.setFont('helvetica', 'normal')
  doc.setTextColor(...MUTED)
  doc.text(`${project.clientName ?? 'No client'} · ${project.type} · ${project.riskLevel} Risk · Ref: ${ref}`, W / 2, y, { align: 'center' })
  y += 8

  // Project details grid
  y = tableHeader(doc, 'PROJECT OVERVIEW', y, W, M)
  autoTable(doc, {
    ...gridStyle,
    startY: y,
    body: [
      ['PROJECT REF',    ref,                              'STATUS',         project.status ?? '—'],
      ['TYPE',           project.type ?? '—',              'RISK LEVEL',     project.riskLevel ?? '—'],
      ['CLIENT',         project.clientName ?? '—',        'TENDER REF',     project.tenderReference ?? '—'],
      ['START DATE',     fmtDate(project.startDate),       'EXPECTED END',   fmtDate(project.expectedEndDate)],
      ...(project.actualEndDate ? [['ACTUAL END', fmtDate(project.actualEndDate), '', '']] : []),
      ['CONTRACT VALUE', fmtKES(project.contractValue),    'PLANNED BUDGET', fmtKES(project.plannedBudget)],
      ['MILESTONES',     String(project.milestoneCount ?? milestones.length), 'TASKS', String(project.taskCount ?? '—')],
    ],
    columnStyles: {
      0: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 38 },
      1: { textColor: DARK,                                           cellWidth: 57 },
      2: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 38 },
      3: { textColor: DARK,                                           cellWidth: 43 },
    },
    margin: { left: M, right: M },
  })
  y = doc.lastAutoTable.finalY + 7

  if (project.scopeSummary) {
    if (y > H - 60) { doc.addPage(); y = 20 }
    y = tableHeader(doc, 'SCOPE SUMMARY', y, W, M)
    y = textBlock(doc, project.scopeSummary, y, W, M)
  }

  // ── Milestones & Tasks ────────────────────────────────────────────────────
  if (milestones.length > 0) {
    if (y > H - 60) { doc.addPage(); y = 20 }
    y = tableHeader(doc, `MILESTONES (${milestones.length})`, y, W, M)
    autoTable(doc, {
      ...gridStyle,
      startY: y,
      head: [['MILESTONE', 'STATUS', 'DUE DATE', 'COMPLETED']],
      headStyles: listHeadStyle,
      alternateRowStyles: { fillColor: LIGHT },
      body: milestones.map(m => [
        m.title,
        m.status,
        fmtDate(m.plannedCompletionDate),
        fmtDate(m.actualCompletionDate),
      ]),
      columnStyles: {
        0: { cellWidth: 90 },
        1: { cellWidth: 28 },
        2: { cellWidth: 28 },
        3: { cellWidth: 30 },
      },
      margin: { left: M, right: M },
    })
    y = doc.lastAutoTable.finalY + 8

    // Tasks per milestone
    for (const m of milestones) {
      const tasks = milestoneTasks[m.id] ?? []
      if (tasks.length === 0) continue
      if (y > H - 55) { doc.addPage(); y = 20 }

      // Milestone sub-header
      doc.setFillColor(...LIGHT)
      doc.setDrawColor(...BORDER)
      doc.setLineWidth(0.3)
      doc.rect(M, y, W - M * 2, 7, 'FD')
      doc.setFillColor(...(MILESTONE_COLORS[m.status] ?? MUTED))
      doc.rect(M, y, 3, 7, 'F')
      doc.setFontSize(8)
      doc.setFont('helvetica', 'bold')
      doc.setTextColor(...DARK)
      doc.text(m.title, M + 6, y + 4.8)
      doc.setFont('helvetica', 'normal')
      doc.setTextColor(...MUTED)
      doc.text(m.status, W - M - 2, y + 4.8, { align: 'right' })
      y += 7

      autoTable(doc, {
        ...gridStyle,
        startY: y,
        head: [['TASK', 'ASSIGNED TO', 'STATUS', 'LINKED ASSIGNMENT']],
        headStyles: { fillColor: [229, 231, 235], textColor: BLACK, fontStyle: 'bold', fontSize: 7.5 },
        alternateRowStyles: { fillColor: [252, 252, 253] },
        body: tasks.map(t => [
          t.title,
          t.assignedToUserId ? t.assignedToUserId.slice(0, 8) + '…' : '—',
          t.status,
          t.linkedAssignmentId ? t.linkedAssignmentId.slice(0, 8) + '…' : '—',
        ]),
        columnStyles: {
          0: { cellWidth: 90 },
          1: { cellWidth: 32 },
          2: { cellWidth: 24 },
          3: { cellWidth: 30 },
        },
        margin: { left: M + 4, right: M + 4 },
      })
      y = doc.lastAutoTable.finalY + 6
    }
  }

  // ── Budget ────────────────────────────────────────────────────────────────
  if (budget) {
    if (y > H - 70) { doc.addPage(); y = 20 }
    y = tableHeader(doc, 'BUDGET SUMMARY', y, W, M)
    autoTable(doc, {
      ...gridStyle,
      startY: y,
      body: [
        ['PLANNED BUDGET',   fmtKES(budget.plannedBudget),   'ACTUAL COST',      fmtKES(budget.actualCost)],
        ['REMAINING',        fmtKES(budget.remaining),        'UTILISATION',      `${budget.utilizationPercent ?? 0}%`],
      ],
      columnStyles: {
        0: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 38 },
        1: { textColor: DARK,                                           cellWidth: 57 },
        2: { fillColor: LABEL_BG, textColor: BLACK, fontStyle: 'bold', cellWidth: 38 },
        3: { textColor: DARK,                                           cellWidth: 43 },
      },
      margin: { left: M, right: M },
    })
    y = doc.lastAutoTable.finalY + 7

    if (budget.lines && budget.lines.length > 0) {
      if (y > H - 55) { doc.addPage(); y = 20 }
      y = tableHeader(doc, `BUDGET LINES (${budget.lines.length})`, y, W, M)
      autoTable(doc, {
        ...gridStyle,
        startY: y,
        head: [['CATEGORY', 'DESCRIPTION', 'ESTIMATED', 'ACTUAL', 'VARIANCE']],
        headStyles: listHeadStyle,
        alternateRowStyles: { fillColor: LIGHT },
        body: budget.lines.map(l => {
          const variance = (l.estimatedAmount ?? 0) - (l.actualAmount ?? 0)
          return [
            l.category,
            l.description ?? '—',
            fmtKES(l.estimatedAmount),
            fmtKES(l.actualAmount),
            (variance >= 0 ? '+' : '') + fmtKES(variance),
          ]
        }),
        columnStyles: {
          0: { cellWidth: 28 },
          1: { cellWidth: 66 },
          2: { cellWidth: 26 },
          3: { cellWidth: 26 },
          4: { cellWidth: 30 },
        },
        margin: { left: M, right: M },
      })
      y = doc.lastAutoTable.finalY + 7
    }
  }

  // ── Resources ─────────────────────────────────────────────────────────────
  if (resources && resources.length > 0) {
    if (y > H - 55) { doc.addPage(); y = 20 }
    y = tableHeader(doc, `RESOURCES (${resources.length})`, y, W, M)
    autoTable(doc, {
      ...gridStyle,
      startY: y,
      head: [['NAME', 'ROLE', 'ADDED']],
      headStyles: listHeadStyle,
      alternateRowStyles: { fillColor: LIGHT },
      body: resources.map(r => [
        r.userName ?? r.userId ?? '—',
        r.role ?? '—',
        fmtDate(r.createdAt),
      ]),
      columnStyles: {
        0: { cellWidth: 70 },
        1: { cellWidth: 70 },
        2: { cellWidth: 36 },
      },
      margin: { left: M, right: M },
    })
  }

  finalize(doc, logoB64, W, H, `QC-PROJECT-${ref}.pdf`, companyName)
}

export function exportProjectDetailExcel(project, milestones, milestoneTasks, budget, resources, companyName) {
  const ref = project.id.slice(0, 8).toUpperCase()
  const wb  = XLSX.utils.book_new()

  // ── Sheet 1: Overview ────────────────────────────────────────────────────
  const overviewWs = XLSX.utils.aoa_to_sheet([
    ...xlsxHeader('PROJECT REPORT', ref, companyName),
    ['Field', 'Value'],
    ['Reference', ref],
    ['Project Name', project.name],
    ['Client', project.clientName ?? ''],
    ['Type', project.type],
    ['Status', project.status],
    ['Risk Level', project.riskLevel],
    ['Tender Reference', project.tenderReference ?? ''],
    ['Start Date', fmtDate(project.startDate)],
    ['Expected End Date', fmtDate(project.expectedEndDate)],
    ['Actual End Date', fmtDate(project.actualEndDate)],
    ['Contract Value (KES)', project.contractValue ?? 0],
    ['Planned Budget (KES)', project.plannedBudget ?? 0],
    ['Milestones', project.milestoneCount ?? milestones.length],
    ['Scope Summary', project.scopeSummary ?? ''],
  ])
  overviewWs['!cols'] = [{ wch: 22 }, { wch: 55 }]
  XLSX.utils.book_append_sheet(wb, overviewWs, 'Overview')

  // ── Sheet 2: Milestones & Tasks ──────────────────────────────────────────
  const msRows = [
    ...xlsxHeader('MILESTONES & TASKS', ref, companyName),
    ['Milestone', 'Status', 'Due Date', 'Completed Date', 'Task', 'Assigned To', 'Task Status', 'Linked Assignment'],
  ]
  for (const m of milestones) {
    const tasks = milestoneTasks[m.id] ?? []
    if (tasks.length === 0) {
      msRows.push([m.title, m.status, fmtDate(m.plannedCompletionDate), fmtDate(m.actualCompletionDate), '', '', '', ''])
    } else {
      tasks.forEach((t, i) => {
        msRows.push([
          i === 0 ? m.title : '',
          i === 0 ? m.status : '',
          i === 0 ? fmtDate(m.plannedCompletionDate) : '',
          i === 0 ? fmtDate(m.actualCompletionDate) : '',
          t.title,
          t.assignedToUserId ?? '',
          t.status,
          t.linkedAssignmentId ?? '',
        ])
      })
    }
  }
  const msWs = XLSX.utils.aoa_to_sheet(msRows)
  msWs['!cols'] = [
    { wch: 35 }, { wch: 16 }, { wch: 16 }, { wch: 18 },
    { wch: 35 }, { wch: 36 }, { wch: 14 }, { wch: 36 },
  ]
  XLSX.utils.book_append_sheet(wb, msWs, 'Milestones & Tasks')

  // ── Sheet 3: Budget ──────────────────────────────────────────────────────
  if (budget) {
    const budgetRows = [
      ...xlsxHeader('BUDGET', ref, companyName),
      ['Planned Budget (KES)', budget.plannedBudget ?? 0],
      ['Actual Cost (KES)',    budget.actualCost ?? 0],
      ['Remaining (KES)',      budget.remaining ?? 0],
      ['Utilisation (%)',      budget.utilizationPercent ?? 0],
      [],
      ['Category', 'Description', 'Estimated (KES)', 'Actual (KES)', 'Variance (KES)'],
      ...(budget.lines ?? []).map(l => [
        l.category,
        l.description ?? '',
        l.estimatedAmount ?? 0,
        l.actualAmount ?? 0,
        (l.estimatedAmount ?? 0) - (l.actualAmount ?? 0),
      ]),
    ]
    const budgetWs = XLSX.utils.aoa_to_sheet(budgetRows)
    budgetWs['!cols'] = [{ wch: 22 }, { wch: 40 }, { wch: 18 }, { wch: 18 }, { wch: 18 }]
    XLSX.utils.book_append_sheet(wb, budgetWs, 'Budget')
  }

  // ── Sheet 4: Resources ───────────────────────────────────────────────────
  if (resources && resources.length > 0) {
    const resWs = XLSX.utils.aoa_to_sheet([
      ...xlsxHeader('RESOURCES', ref, companyName),
      ['Name', 'Role', 'Added Date'],
      ...resources.map(r => [r.userName ?? r.userId ?? '', r.role ?? '', fmtDate(r.createdAt)]),
    ])
    resWs['!cols'] = [{ wch: 30 }, { wch: 30 }, { wch: 16 }]
    XLSX.utils.book_append_sheet(wb, resWs, 'Resources')
  }

  XLSX.writeFile(wb, `QC-PROJECT-${ref}.xlsx`)
}
