import { useState, useEffect } from 'react'
import { useAuth } from '../../context/AuthContext.jsx'
import * as ticketApi from '../../services/ticketing.js'
import { exportTicketPdf, fmtDate } from '../../utils/export.js'
import * as XLSX from 'xlsx'

const STATUS_INT = { New: 0, Assigned: 1, InProgress: 2, Pending: 3, Escalated: 4, Resolved: 5, Closed: 6, Reopened: 7 }

// All data-loading + actions for the ticket detail page. Returns everything the page's JSX needs;
// the page destructures with the same names so its markup is unchanged (pure presentational shell).
export function useTicketDetail(id) {
  const { user: currentUser } = useAuth()

  const [ticket, setTicket] = useState(null)
  const [comments, setComments] = useState([])
  const [history, setHistory] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [activeTab, setActiveTab] = useState('comments')

  // Action modals
  const [actionModal, setActionModal] = useState(null)
  const [actionData, setActionData] = useState({})
  const [actionLoading, setActionLoading] = useState(false)
  const [actionError, setActionError] = useState('')

  // Comment / export / reference data
  const [commentText, setCommentText] = useState('')
  const [commentLoading, setCommentLoading] = useState(false)
  const [exportMenu, setExportMenu] = useState(false)
  const [exportLoading, setExportLoading] = useState(false)
  const [departments, setDepartments] = useState([])
  const [users, setUsers] = useState([])
  const [allTags, setAllTags] = useState([])
  const [ticketTags, setTicketTags] = useState([])
  const [macros, setMacros] = useState([])
  const [showMacroPicker, setShowMacroPicker] = useState(false)
  const [macroApplying, setMacroApplying] = useState(false)
  const [rating, setRating] = useState(null)
  const [ratingHover, setRatingHover] = useState(0)
  const [ratingComment, setRatingComment] = useState('')
  const [ratingSubmitting, setRatingSubmitting] = useState(false)

  // Attachments / photos
  const [attachments, setAttachments] = useState([])
  const [photoLightbox, setPhotoLightbox] = useState(null)

  // Escalations
  const [escalations, setEscalations] = useState([])
  const [ackLoading, setAckLoading] = useState(null)

  // Watchers
  const [watcherIds, setWatcherIds] = useState([])
  const [watchLoading, setWatchLoading] = useState(false)

  // #19 — duplicate detection & merge / D3-1 children / D5 complaint workflow
  const [duplicates, setDuplicates] = useState([])
  const [children, setChildren] = useState([])
  const [complaintSteps, setComplaintSteps] = useState([])
  const [stepForm, setStepForm] = useState({})
  const [stepBusy, setStepBusy] = useState(false)
  const [stepError, setStepError] = useState('')
  const [dupModalOpen, setDupModalOpen] = useState(false)
  const [dupLoading, setDupLoading] = useState(false)
  const [mergeLoading, setMergeLoading] = useState(null)

  const isWatching = currentUser?.id && watcherIds.includes(currentUser.id)

  useEffect(() => {
    loadAll()
    ticketApi.getDepartments().then(raw => {
      setDepartments(Array.isArray(raw) ? raw : raw?.departments ?? [])
    }).catch(() => {})
    ticketApi.getUsers().then(raw => {
      setUsers(Array.isArray(raw) ? raw : raw?.items ?? [])
    }).catch(() => {})
    ticketApi.listTags().then(list => setAllTags(list ?? [])).catch(() => {})
    ticketApi.listMacros().then(list => setMacros(list ?? [])).catch(() => {})
    ticketApi.getTicketTags(id).then(list => setTicketTags(list ?? [])).catch(() => {})
    ticketApi.getRating(id).then(r => setRating(r ?? null)).catch(() => {})
    ticketApi.getEscalations(id).then(list => setEscalations(list ?? [])).catch(() => {})
    ticketApi.getWatchers(id).then(list => setWatcherIds(list ?? [])).catch(() => {})
  }, [id])

  async function loadAll() {
    setLoading(true)
    setError('')
    try {
      const [ticket, comments, history, attach, children, complaint] = await Promise.all([
        ticketApi.getTicket(id),
        ticketApi.getComments(id),
        ticketApi.getHistory(id),
        ticketApi.getAttachments(id).catch(() => null),
        ticketApi.getChildren(id).catch(() => null),   // D3-1
        ticketApi.getComplaintSteps(id).catch(() => null),   // D5
      ])
      setTicket(ticket)
      setComments(comments ?? [])
      setHistory(history ?? [])
      setAttachments(attach ?? [])
      setChildren(children ?? [])
      setComplaintSteps(complaint ?? [])
    } catch {
      setError('Failed to load ticket.')
    } finally {
      setLoading(false)
    }
  }

  // D5 — complete the active complaint step with its step-specific payload.
  async function completeStep(step) {
    setStepError('')
    const payload = { notes: stepForm.notes ?? '' }
    if (step.stepTypeLabel === 'SatisfactionCheck') {
      if (stepForm.satisfactionMet == null) { setStepError('Select whether the client was satisfied.'); return }
      payload.satisfactionMet = stepForm.satisfactionMet
      if (stepForm.satisfactionMet === false) {
        if (!(stepForm.signOffByUserId ?? '').trim()) { setStepError('A sign-off user (MD/Dept Head) is required.'); return }
        payload.signOffByUserId = stepForm.signOffByUserId.trim()
      }
    }
    if (step.stepTypeLabel === 'Close') {
      if (!(stepForm.rootCause ?? '').trim() || !(stepForm.preventiveAction ?? '').trim()) {
        setStepError('Root cause and preventive action are both required to close.'); return
      }
      payload.rootCause = stepForm.rootCause.trim()
      payload.preventiveAction = stepForm.preventiveAction.trim()
    }
    setStepBusy(true)
    try {
      const steps = await ticketApi.completeComplaintStep(id, step.stepNumber, payload)
      setComplaintSteps(steps ?? [])
      setStepForm({})
      loadAll()   // status may have changed (final step closes the ticket)
    } catch (err) {
      setStepError(err.response?.data?.message ?? 'Failed to complete the step.')
    } finally {
      setStepBusy(false)
    }
  }

  async function reloadAttachments() {
    const list = await ticketApi.getAttachments(id).catch(() => null)
    setAttachments(list ?? [])
  }

  async function acknowledgeEscalation(escalationId) {
    setAckLoading(escalationId)
    try {
      await ticketApi.acknowledgeEscalation(id, escalationId)
      setEscalations(prev => prev.map(e =>
        e.id === escalationId ? { ...e, isAcknowledged: true, acknowledgedAt: new Date().toISOString() } : e
      ))
    } catch { /* ignore */ }
    finally { setAckLoading(null) }
  }

  // #19 — surface likely duplicates (same requester, still open)
  async function findDuplicates() {
    setDupModalOpen(true)
    setDupLoading(true)
    try {
      setDuplicates(await ticketApi.getDuplicates(id) ?? [])
    } catch {
      setDuplicates([])
    } finally {
      setDupLoading(false)
    }
  }

  // #19 — merge a duplicate INTO this ticket: closes the duplicate, links it here.
  async function mergeDuplicate(sourceId) {
    setMergeLoading(sourceId)
    try {
      await ticketApi.mergeTicket(sourceId, { intoTicketId: id })
      setDuplicates(prev => prev.filter(d => d.id !== sourceId))
      await loadAll()
    } catch { /* ignore */ }
    finally { setMergeLoading(null) }
  }

  async function toggleWatch() {
    if (!currentUser?.id) return
    setWatchLoading(true)
    try {
      if (isWatching) {
        await ticketApi.removeWatcher(id, currentUser.id)
        setWatcherIds(prev => prev.filter(uid => uid !== currentUser.id))
      } else {
        await ticketApi.addWatcher(id, currentUser.id)
        setWatcherIds(prev => [...prev, currentUser.id])
      }
    } catch { /* ignore */ }
    finally { setWatchLoading(false) }
  }

  async function handleAction() {
    setActionError('')
    setActionLoading(true)
    try {
      if (actionModal === 'resolve') {
        if (!(actionData.rootCause ?? '').trim()) {
          setActionError('A root cause is required to resolve this ticket.')
          setActionLoading(false)
          return
        }
        await ticketApi.resolveTicket(id, {
          resolutionNotes: actionData.notes ?? '',
          rootCause: actionData.rootCause ?? '',
        })
      } else if (actionModal === 'assign') {
        await ticketApi.assignTicket(id, {
          assignedToUserId: actionData.userId ?? '',
          assigneeName: actionData.assigneeName ?? null,
          departmentId: actionData.departmentId ?? null,
          notes: actionData.notes ?? '',
          isPrimary: true,
        })
      } else if (actionModal === 'escalate') {
        await ticketApi.escalateTicket(id, {
          escalationLevel: Number(actionData.level ?? 0),
          escalatedToUserId: actionData.userId ?? '',
          reason: actionData.reason ?? '',
        })
      } else if (actionModal === 'status') {
        await ticketApi.setTicketStatus(id, {
          newStatus: STATUS_INT[actionData.newStatus],
          notes: actionData.notes ?? '',
        })
      } else if (actionModal === 'close') {
        await ticketApi.closeTicket(id)
      } else if (actionModal === 'reopen') {
        await ticketApi.reopenTicket(id)
      } else if (actionModal === 'department') {
        await ticketApi.assignDepartment(id, {
          departmentId: actionData.departmentId ?? '',
          notes: actionData.notes ?? '',
        })
      }
      setActionModal(null)
      setActionData({})
      loadAll()
    } catch (err) {
      setActionError(err.response?.data?.message ?? 'Action failed.')
    } finally {
      setActionLoading(false)
    }
  }

  async function submitComment(e) {
    e.preventDefault()
    if (!commentText.trim()) return
    setCommentLoading(true)
    try {
      await ticketApi.addComment(id, { ticketId: id, content: commentText.trim() })
      setCommentText('')
      setComments(await ticketApi.getComments(id) ?? [])
    } catch {
      // silently fail — user can retry
    } finally {
      setCommentLoading(false)
    }
  }

  async function addTag(tagId) {
    try {
      await ticketApi.addTicketTag(id, { tagId })
      const tag = allTags.find(t => t.id === tagId)
      if (tag && !ticketTags.find(t => t.id === tagId)) setTicketTags(ts => [...ts, tag])
    } catch { /* ignore */ }
  }

  async function removeTag(tagId) {
    try {
      await ticketApi.removeTicketTag(id, tagId)
      setTicketTags(ts => ts.filter(t => t.id !== tagId))
    } catch { /* ignore */ }
  }

  async function applyMacro(macroId) {
    setMacroApplying(true)
    setShowMacroPicker(false)
    try {
      await ticketApi.applyMacro(id, { macroId, isInternal: false })
      setComments(await ticketApi.getComments(id) ?? [])
    } catch { /* ignore */ }
    finally { setMacroApplying(false) }
  }

  async function submitRating(stars) {
    setRatingSubmitting(true)
    try {
      setRating(await ticketApi.submitRating(id, { rating: stars, comment: ratingComment || null }))
    } catch { /* ignore */ }
    finally { setRatingSubmitting(false) }
  }

  function openModal(type, defaults = {}) {
    setActionModal(type)
    setActionData(defaults)
    setActionError('')
  }

  function handleExport(format) {
    if (!ticket) return
    setExportMenu(false)
    const filename = `lante-ticket-${ticket.id}`

    const overviewCols = [
      { header: 'Field', accessor: r => r.field, width: 22 },
      { header: 'Value', accessor: r => r.value, width: 50 },
    ]
    const overviewRows = [
      { field: 'Ticket ID',      value: ticket.id },
      { field: 'Title',          value: ticket.title },
      { field: 'Status',         value: ticket.statusLabel },
      { field: 'Priority',       value: ticket.priorityLabel },
      { field: 'Category',       value: ticket.categoryName },
      { field: 'Department',     value: departments.find(d => d.id === ticket.departmentId)?.name ?? ticket.departmentId },
      { field: 'Source',         value: ticket.source != null ? ['Manual','System','CRM','Safety Report','Scheduled'][ticket.source] : '—' },
      { field: 'Assigned To',    value: (() => { const u = users.find(u => u.id === ticket.assignedToUserId); return u ? `${u.firstName} ${u.lastName}` : ticket.assigneeName || ticket.assignedToUserId || 'Unassigned' })() },
      { field: 'Created',        value: fmtDate(ticket.createdAt) },
      { field: 'Response Due',   value: fmtDate(ticket.responseDueAt) },
      { field: 'Resolution Due', value: fmtDate(ticket.resolutionDueAt) },
      { field: 'Resolved At',    value: fmtDate(ticket.resolvedAt) },
      { field: 'Escalated',      value: ticket.isEscalated ? 'Yes' : 'No' },
      { field: 'Description',    value: ticket.description },
      { field: 'Resolution Notes', value: ticket.resolutionNotes ?? '—' },
      ...(attachments.length > 0 ? [
        { field: 'Attached Photos', value: attachments.map(a => a.fileName).join(', ') },
      ] : []),
    ]

    const commentCols = [
      { header: 'Author',   accessor: r => r.authorUserId,  width: 22 },
      { header: 'Internal', accessor: r => r.isInternal ? 'Yes' : 'No', width: 12 },
      { header: 'Content',  accessor: r => r.content,       width: 60 },
      { header: 'Date',     accessor: r => fmtDate(r.createdAt), width: 18 },
    ]

    const historyCols = [
      { header: 'Action',   accessor: r => r.action,    width: 28 },
      { header: 'From',     accessor: r => r.fromValue ?? '—', width: 16 },
      { header: 'To',       accessor: r => r.toValue ?? '—',   width: 16 },
      { header: 'Notes',    accessor: r => r.notes ?? '—',     width: 30 },
      { header: 'Date',     accessor: r => fmtDate(r.occurredAt), width: 18 },
    ]

    if (format === 'pdf') {
      setExportLoading(true)
      exportTicketPdf({ ticket, comments, history, departments, attachments })
        .finally(() => setExportLoading(false))
      return
    } else {
      const buildSheet = (cols, rows) => {
        const header = cols.map(c => c.header)
        const data = rows.map(row => cols.map(c => c.accessor(row) ?? ''))
        const ws = {}
        ;[header, ...data].forEach((row, ri) => {
          row.forEach((val, ci) => {
            ws[String.fromCharCode(65 + ci) + (ri + 1)] = { v: String(val), t: 's' }
          })
        })
        ws['!ref'] = `A1:${String.fromCharCode(65 + cols.length - 1)}${data.length + 1}`
        ws['!cols'] = cols.map(c => ({ wch: c.width ?? 20 }))
        return ws
      }
      const wb = XLSX.utils.book_new()
      XLSX.utils.book_append_sheet(wb, buildSheet(overviewCols, overviewRows), 'Overview')
      if (comments.length)  XLSX.utils.book_append_sheet(wb, buildSheet(commentCols, comments),  'Comments')
      if (history.length)   XLSX.utils.book_append_sheet(wb, buildSheet(historyCols, history),   'History')
      XLSX.writeFile(wb, `${filename}.xlsx`)
    }
  }

  return {
    currentUser, isWatching,
    ticket, setTicket, comments, setComments, history, setHistory, loading, error,
    activeTab, setActiveTab,
    actionModal, setActionModal, actionData, setActionData, actionLoading, actionError,
    commentText, setCommentText, commentLoading,
    exportMenu, setExportMenu, exportLoading,
    departments, users, allTags, ticketTags, macros,
    showMacroPicker, setShowMacroPicker, macroApplying,
    rating, ratingHover, setRatingHover, ratingComment, setRatingComment, ratingSubmitting,
    attachments, photoLightbox, setPhotoLightbox,
    escalations, ackLoading,
    watcherIds, watchLoading,
    duplicates, children, complaintSteps,
    stepForm, setStepForm, stepBusy, stepError,
    dupModalOpen, setDupModalOpen, dupLoading, mergeLoading,
    // actions
    loadAll, completeStep, reloadAttachments, acknowledgeEscalation, findDuplicates,
    mergeDuplicate, toggleWatch, handleAction, submitComment, addTag, removeTag,
    applyMacro, submitRating, openModal, handleExport,
  }
}
