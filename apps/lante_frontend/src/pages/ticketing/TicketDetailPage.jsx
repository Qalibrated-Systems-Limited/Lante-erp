import { Fragment } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import TicketStatusBadge from '../../components/ticketing/TicketStatusBadge.jsx'
import TicketPriorityBadge from '../../components/ticketing/TicketPriorityBadge.jsx'
import { PhotosTab, CommentsTab, HistoryTab } from '../../components/ticketing/detail/TicketTabs.jsx'
import { ActionModal, HeroFact, ActionBtn, MetaRow, TicketDescription } from '../../components/ticketing/detail/TicketDetailBits.jsx'
import { useTicketDetail } from '../../hooks/ticketing/useTicketDetail.js'
import { fmtDate } from '../../utils/export.js'

const STATUS_TRANSITIONS = {
  New:        ['Assigned', 'InProgress'],
  Assigned:   ['InProgress', 'Pending'],
  InProgress: ['Pending', 'Resolved'],
  Pending:    ['InProgress', 'Resolved'],
  Escalated:  ['InProgress', 'Resolved'],
  Resolved:   ['Closed', 'Reopened'],
  Reopened:   ['InProgress', 'Resolved'],
  Closed:     [],
}

const STATUS_INT = { New:0, Assigned:1, InProgress:2, Pending:3, Escalated:4, Resolved:5, Closed:6, Reopened:7 }

// Canonical lifecycle for the header stepper. Off-track statuses map to the nearest stage.
const STEPS = ['New', 'Assigned', 'In Progress', 'Resolved', 'Closed']
const STEP_INDEX = { New:0, Assigned:1, InProgress:2, Pending:2, Escalated:2, Reopened:2, Resolved:3, Closed:4 }
const SOURCE_LABELS = ['Manual', 'System', 'CRM', 'Safety Report', 'Scheduled']

export default function TicketDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const {
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
    loadAll, completeStep, reloadAttachments, acknowledgeEscalation, findDuplicates,
    mergeDuplicate, toggleWatch, handleAction, submitComment, addTag, removeTag,
    applyMacro, submitRating, openModal, handleExport,
  } = useTicketDetail(id)
  if (loading) {
    return (
      <>
        <div className="flex-1 flex items-center justify-center">
          <div className="text-gray-400 text-sm animate-pulse">Loading ticket…</div>
        </div>
      </>
    )
  }

  if (error || !ticket) {
    return (
      <>
        <div className="flex-1 flex flex-col items-center justify-center gap-3">
          <p className="text-gray-500">{error || 'Ticket not found.'}</p>
          <button onClick={() => navigate('/modules/ticketing')} className="text-navy text-sm font-medium">
            Back to tickets
          </button>
        </div>
      </>
    )
  }

  const statusLabel = ticket.statusLabel ?? 'New'
  const transitions = STATUS_TRANSITIONS[statusLabel] ?? []
  const currentStep = STEP_INDEX[statusLabel] ?? 0
  const assigneeUser = users.find(u => u.id === ticket.assignedToUserId)
  const assigneeDisplay = assigneeUser ? `${assigneeUser.firstName} ${assigneeUser.lastName}`.trim() : (ticket.assigneeName || 'Unassigned')
  const sourceDisplay = ticket.source != null ? (SOURCE_LABELS[ticket.source] ?? 'Manual') : 'Manual'

  return (
    <>
      <main className="flex-1 w-full px-4 sm:px-6 lg:px-8 py-8" onClick={() => setExportMenu(false)}>
        {/* Back + Export */}
        <div className="flex items-center justify-between mb-5">
          <button
            onClick={() => navigate('/modules/ticketing')}
            className="flex items-center gap-1.5 text-sm text-gray-400 hover:text-gray-600 transition-colors"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            All Tickets
          </button>
          <div className="relative" onClick={e => e.stopPropagation()}>
            <button
              onClick={() => setExportMenu(v => !v)}
              disabled={exportLoading}
              className="inline-flex items-center gap-2 px-3.5 py-2 border border-gray-200 bg-white hover:bg-gray-50 text-gray-700 text-sm font-medium rounded-lg transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
            >
              {exportLoading ? (
                <svg className="w-4 h-4 animate-spin text-gold" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                </svg>
              ) : (
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                </svg>
              )}
              {exportLoading ? 'Generating…' : 'Export'}
              {!exportLoading && (
                <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                </svg>
              )}
            </button>
            {exportMenu && (
              <div className="absolute right-0 mt-1 w-44 bg-white border border-gray-200 rounded-lg shadow-lg z-10">
                <button onClick={() => handleExport('pdf')} className="flex items-center gap-2.5 w-full px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 rounded-t-lg">
                  <span className="text-red-500 font-bold text-xs">PDF</span> Export as PDF
                </button>
                <button onClick={() => handleExport('excel')} className="flex items-center gap-2.5 w-full px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 border-t border-gray-100 rounded-b-lg">
                  <span className="text-green-600 font-bold text-xs">XLS</span> Export as Excel
                </button>
              </div>
            )}
          </div>
        </div>

        {/* #19 — merged-duplicate banner */}
        {ticket.mergedIntoTicketId && (
          <div className="flex items-center justify-between gap-3 mb-4 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3">
            <p className="text-sm text-amber-800">
              <span className="font-semibold">This ticket was merged as a duplicate.</span> It has been closed and linked to the surviving ticket.
            </p>
            <button
              onClick={() => navigate(`/modules/ticketing/${ticket.mergedIntoTicketId}`)}
              className="shrink-0 px-3 py-1.5 text-xs font-semibold text-amber-800 bg-amber-100 hover:bg-amber-200 rounded-lg transition-colors"
            >
              View surviving ticket →
            </button>
          </div>
        )}

        {/* #2 — evidence-hold banner: ticket parked at Pending until field evidence is attached */}
        {ticket.requiresEvidence && statusLabel === 'Pending' && (
          <div className="flex items-center gap-3 mb-4 rounded-xl border border-blue-200 bg-blue-50 px-4 py-3">
            <svg className="w-5 h-5 text-blue-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2">
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            <p className="text-sm text-blue-800">
              <span className="font-semibold">Held for evidence.</span> This ticket requires field evidence (a photo/attachment) before it can be resolved. Add it under the Photos tab.
            </p>
          </div>
        )}

        {/* ── Hero ── */}
        <div className="rounded-2xl overflow-hidden shadow-sm bg-gradient-to-br from-navy to-navy-dark px-6 sm:px-8 py-6 mb-4">
          <div className="flex items-start justify-between gap-4 flex-wrap">
            <div className="min-w-0">
              <p className="text-[11px] font-bold uppercase tracking-[0.15em] text-gold mb-2">{ticket.categoryName ?? 'Ticket'}</p>
              <h1 className="text-2xl font-extrabold text-white leading-tight">{ticket.title}</h1>
              <div className="flex flex-wrap items-center gap-2 mt-3">
                <TicketStatusBadge status={statusLabel} />
                <TicketPriorityBadge priority={ticket.priorityLabel} />
                {ticket.isEscalated && (
                  <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-red-500/20 border border-red-400/40 text-red-100">⚠ Escalated</span>
                )}
                {ticket.isResponseBreached && (
                  <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-red-500/25 border border-red-400/40 text-red-100">⏱ Response SLA Breached</span>
                )}
                {ticket.isResolutionBreached && (
                  <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-red-500/25 border border-red-400/40 text-red-100">⏱ Resolution SLA Breached</span>
                )}
                {ticket.slaPausedAt && (
                  <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-white/10 border border-white/25 text-white/80">⏸ SLA Paused</span>
                )}
                {/* D4-3 — repeat contact (same client + category within 30 days) */}
                {ticket.isRepeat && (
                  <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-orange-500/25 border border-orange-400/40 text-orange-100">🔁 Repeat Contact</span>
                )}
                {ticket.closureTypeLabel && (
                  <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold border ${
                    ticket.closureTypeLabel === 'Completed'
                      ? 'bg-green-500/20 border-green-400/40 text-green-100'
                      : ticket.closureTypeLabel === 'Cancelled'
                        ? 'bg-red-500/20 border-red-400/40 text-red-100'
                        : 'bg-white/10 border-white/25 text-white/80'
                  }`}>
                    {ticket.closureTypeLabel === 'Completed' ? '✓ Completed'
                      : ticket.closureTypeLabel === 'Cancelled' ? '✕ Cancelled'
                      : '⏲ Auto-Closed'}
                  </span>
                )}
              </div>
            </div>
            <div className="text-xs font-mono text-white/50 shrink-0">{ticket.reference ?? `#${(id ?? '').slice(0, 8)}`}</div>
          </div>
          <div className="flex flex-wrap gap-x-10 gap-y-3 mt-5 pt-5 border-t border-white/10">
            <HeroFact label="Assignee" value={assigneeDisplay} />
            <HeroFact label="Source" value={sourceDisplay} />
            <HeroFact label="Response Due" value={ticket.responseDueAt ? fmtDate(ticket.responseDueAt) : '—'} warn={ticket.isResponseBreached} />
            <HeroFact label="Resolution Due" value={ticket.resolutionDueAt ? fmtDate(ticket.resolutionDueAt) : '—'} warn={ticket.isResolutionBreached} />
          </div>
        </div>

        {/* ── Status stepper ── */}
        <div className="bg-white rounded-xl border border-gray-200 px-6 py-5 mb-6">
          <div className="flex items-center">
            {STEPS.map((s, i) => {
              const done = i < currentStep, active = i === currentStep
              return (
                <Fragment key={s}>
                  <div className="flex flex-col items-center shrink-0">
                    <div className={`w-9 h-9 rounded-full flex items-center justify-center text-xs font-bold transition-colors ${active ? 'bg-gold text-white ring-4 ring-gold/20' : done ? 'bg-navy text-white' : 'bg-gray-100 text-gray-400 border border-gray-200'}`}>
                      {done ? '✓' : i + 1}
                    </div>
                    <span className={`text-[11px] mt-2 font-semibold whitespace-nowrap ${active ? 'text-gold' : done ? 'text-navy' : 'text-gray-400'}`}>{s}</span>
                  </div>
                  {i < STEPS.length - 1 && (
                    <div className={`flex-1 h-1 mx-2 rounded-full ${i < currentStep ? 'bg-navy' : 'bg-gray-100'}`} />
                  )}
                </Fragment>
              )
            })}
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Left — main content */}
          <div className="lg:col-span-2 space-y-5">
            {/* Description */}
            <div className="bg-white rounded-xl border border-gray-200 p-6">
              <p className="text-xs font-bold text-navy uppercase tracking-wider mb-3">Description</p>
              <TicketDescription description={ticket.description} />
            </div>

            {/* D5 — complaint 5-step workflow (only for complaint tickets, which auto-init steps) */}
            {complaintSteps.length > 0 && (
              <div className="bg-white rounded-xl border border-gray-200 p-6">
                <p className="text-xs font-bold text-navy uppercase tracking-wider mb-4">Complaint Workflow</p>
                <ol className="space-y-3">
                  {complaintSteps.map(step => {
                    const done = step.statusLabel === 'Completed'
                    const active = step.statusLabel === 'Active'
                    return (
                      <li key={step.id} className={`rounded-lg border p-4 ${active ? 'border-amber-300 bg-amber-50' : done ? 'border-green-200 bg-green-50' : 'border-gray-200 bg-gray-50 opacity-70'}`}>
                        <div className="flex items-center gap-3">
                          <span className={`flex items-center justify-center w-7 h-7 rounded-full text-xs font-bold ${done ? 'bg-green-500 text-white' : active ? 'bg-amber-500 text-white' : 'bg-gray-300 text-white'}`}>
                            {done ? '✓' : step.stepNumber}
                          </span>
                          <div className="flex-1">
                            <p className="text-sm font-semibold text-navy">
                              {['Acknowledge','Investigate','Respond','Satisfaction Check','Close'][step.stepNumber - 1] ?? step.stepTypeLabel}
                            </p>
                            {done && (
                              <p className="text-xs text-gray-500">
                                Completed {step.completedAt ? fmtDate(step.completedAt) : ''}
                                {step.satisfactionMet != null && ` · ${step.satisfactionMet ? 'Satisfied' : 'Dissatisfied'}`}
                              </p>
                            )}
                          </div>
                          <span className="text-xs font-medium text-gray-400">{done ? 'Done' : active ? 'Active' : 'Locked'}</span>
                        </div>

                        {done && (step.rootCause || step.preventiveAction) && (
                          <div className="mt-2 text-xs text-gray-600 pl-10 space-y-1">
                            {step.rootCause && <p><span className="font-semibold">Root cause:</span> {step.rootCause}</p>}
                            {step.preventiveAction && <p><span className="font-semibold">Preventive action:</span> {step.preventiveAction}</p>}
                          </div>
                        )}

                        {active && (
                          <div className="mt-3 pl-10 space-y-2">
                            {step.stepTypeLabel === 'SatisfactionCheck' && (
                              <div className="space-y-2">
                                <div className="flex gap-2">
                                  <button type="button" onClick={() => setStepForm(f => ({ ...f, satisfactionMet: true }))}
                                    className={`px-3 py-1.5 rounded-lg text-sm font-medium border ${stepForm.satisfactionMet === true ? 'bg-green-500 text-white border-green-500' : 'border-gray-200 text-gray-600'}`}>Satisfied</button>
                                  <button type="button" onClick={() => setStepForm(f => ({ ...f, satisfactionMet: false }))}
                                    className={`px-3 py-1.5 rounded-lg text-sm font-medium border ${stepForm.satisfactionMet === false ? 'bg-red-500 text-white border-red-500' : 'border-gray-200 text-gray-600'}`}>Dissatisfied</button>
                                </div>
                                {stepForm.satisfactionMet === false && (
                                  <input type="text" value={stepForm.signOffByUserId ?? ''} onChange={e => setStepForm(f => ({ ...f, signOffByUserId: e.target.value }))}
                                    placeholder="MD/Dept Head sign-off (user id) — required"
                                    className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400" />
                                )}
                              </div>
                            )}
                            {step.stepTypeLabel === 'Close' && (
                              <div className="space-y-2">
                                <textarea value={stepForm.rootCause ?? ''} onChange={e => setStepForm(f => ({ ...f, rootCause: e.target.value }))}
                                  placeholder="Root cause (required)" rows={2}
                                  className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 resize-none" />
                                <textarea value={stepForm.preventiveAction ?? ''} onChange={e => setStepForm(f => ({ ...f, preventiveAction: e.target.value }))}
                                  placeholder="Preventive action (required)" rows={2}
                                  className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 resize-none" />
                              </div>
                            )}
                            <textarea value={stepForm.notes ?? ''} onChange={e => setStepForm(f => ({ ...f, notes: e.target.value }))}
                              placeholder="Notes (optional)" rows={2}
                              className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 resize-none" />
                            {stepError && <p className="text-xs text-red-600">{stepError}</p>}
                            <button type="button" disabled={stepBusy} onClick={() => completeStep(step)}
                              className="px-4 py-2 bg-amber-500 hover:bg-amber-600 disabled:opacity-50 text-white text-sm font-semibold rounded-lg">
                              {stepBusy ? 'Saving…' : step.stepTypeLabel === 'Close' ? 'Complete & Close Complaint' : 'Complete Step'}
                            </button>
                          </div>
                        )}
                      </li>
                    )
                  })}
                </ol>
              </div>
            )}

            {/* Actions bar */}
            {transitions.length > 0 || statusLabel === 'Resolved' || statusLabel !== 'Closed' ? (
              <div className="bg-white rounded-xl border border-gray-200 p-4">
                <p className="text-xs font-bold text-navy uppercase tracking-wider mb-3">Actions</p>
                <div className="flex flex-wrap gap-2">
                  {transitions.includes('InProgress') && statusLabel !== 'InProgress' && (
                    <ActionBtn
                      label="Mark In Progress"
                      color="blue"
                      onClick={() => openModal('status', { newStatus: 'InProgress' })}
                    />
                  )}
                  {transitions.includes('Pending') && (
                    <ActionBtn
                      label="Mark Pending"
                      color="yellow"
                      onClick={() => openModal('status', { newStatus: 'Pending' })}
                    />
                  )}
                  {transitions.includes('Resolved') && (
                    <ActionBtn
                      label="Resolve"
                      color="green"
                      onClick={() => openModal('resolve')}
                    />
                  )}
                  {transitions.includes('Closed') && (
                    <ActionBtn
                      label="Close"
                      color="gray"
                      onClick={() => openModal('close')}
                    />
                  )}
                  {transitions.includes('Reopened') && (
                    <ActionBtn
                      label="Reopen"
                      color="orange"
                      onClick={() => openModal('reopen')}
                    />
                  )}
                  {['New','Assigned','InProgress','Pending'].includes(statusLabel) && !ticket.operationsAssignmentId && (
                    <ActionBtn
                      label="Assign to Technician"
                      color="purple"
                      onClick={() => openModal('assign')}
                    />
                  )}
                  {ticket.operationsAssignmentId && (
                    <ActionBtn
                      label="View Field Assignment"
                      color="yellow"
                      onClick={() => navigate(`/modules/operations/assignments/${ticket.operationsAssignmentId}`)}
                    />
                  )}
                  {['New','Assigned','InProgress','Pending','Reopened'].includes(statusLabel) && (
                    <ActionBtn
                      label="Assign Department"
                      color="indigo"
                      onClick={() => openModal('department', { departmentId: ticket.departmentId ?? '' })}
                    />
                  )}
                  {!ticket.isEscalated && ['New','Assigned','InProgress','Pending'].includes(statusLabel) && (
                    <ActionBtn
                      label="Escalate"
                      color="red"
                      onClick={() => openModal('escalate')}
                    />
                  )}
                  {!ticket.mergedIntoTicketId && ['New','Assigned','InProgress','Pending','Reopened','Escalated'].includes(statusLabel) && (
                    <ActionBtn
                      label="Find Duplicates"
                      color="orange"
                      onClick={findDuplicates}
                    />
                  )}
                </div>
              </div>
            ) : null}

            {/* Tabs */}
            <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
              <div className="flex border-b border-gray-100">
                {['comments','history','photos'].map(tab => (
                  <button
                    key={tab}
                    onClick={() => setActiveTab(tab)}
                    className={`px-5 py-3 text-sm font-semibold capitalize transition-colors ${
                      activeTab === tab
                        ? 'text-navy border-b-2 border-gold'
                        : 'text-gray-500 hover:text-gray-700'
                    }`}
                  >
                    {tab} {tab === 'comments' && comments.length > 0 && `(${comments.length})`}
                    {tab === 'history' && history.length > 0 && `(${history.length})`}
                    {tab === 'photos' && attachments.length > 0 && `(${attachments.length})`}
                  </button>
                ))}
              </div>

              <div className="p-5">
                {activeTab === 'comments' && (
                  <CommentsTab
                    comments={comments}
                    commentText={commentText}
                    setCommentText={setCommentText}
                    onSubmit={submitComment}
                    loading={commentLoading}
                    macros={macros}
                    showMacroPicker={showMacroPicker}
                    setShowMacroPicker={setShowMacroPicker}
                    onApplyMacro={applyMacro}
                    macroApplying={macroApplying}
                  />
                )}
                {activeTab === 'history' && <HistoryTab history={history} />}
                {activeTab === 'photos' && (
                  <PhotosTab
                    ticketId={id}
                    attachments={attachments}
                    onLightbox={setPhotoLightbox}
                    onUploaded={reloadAttachments}
                  />
                )}
              </div>
            </div>
          </div>

          {/* Right — metadata sidebar */}
          <div className="space-y-5">
            <div className="bg-white rounded-xl border border-gray-200 p-5 space-y-4">
              <p className="text-xs font-bold text-navy uppercase tracking-wider">Details</p>

              <MetaRow label="Category" value={ticket.categoryName} />
              {ticket.customerName && (
                <MetaRow
                  label="Client"
                  value={[ticket.customerName, ticket.customerCompany].filter(Boolean).join(' — ')}
                />
              )}
              <MetaRow
                label="Department"
                value={(departments.find(d => d.id === ticket.departmentId)?.name ?? ticket.departmentId) || '—'}
              />
              <MetaRow label="Assigned To" value={(() => { const u = users.find(u => u.id === ticket.assignedToUserId); return u ? `${u.firstName} ${u.lastName}` : ticket.assigneeName || 'Unassigned' })()} />
              {ticket.operationsAssignmentId && (
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '6px 0', borderBottom: '1px solid #f3f4f6' }}>
                  <span style={{ fontSize: 14, color: '#6b7280' }}>Field Assignment</span>
                  <a
                    href={`/modules/operations/assignments/${ticket.operationsAssignmentId}`}
                    onClick={e => { e.preventDefault(); navigate(`/modules/operations/assignments/${ticket.operationsAssignmentId}`) }}
                    style={{ fontSize: 14, fontWeight: 600, color: '#1B3A5C', textDecoration: 'none', background: '#DCE8F5', padding: '2px 8px', borderRadius: 6 }}
                  >
                    View Assignment →
                  </a>
                </div>
              )}
              <MetaRow label="Source" value={ticket.source != null ? (['Manual','System','CRM','Safety Report','Scheduled'][ticket.source] ?? '—') : '—'} />
              <MetaRow
                label="Response Due"
                value={ticket.responseDueAt ? fmtDate(ticket.responseDueAt) : '—'}
                warn={ticket.isResponseBreached}
              />
              <MetaRow
                label="First Response"
                value={ticket.firstResponseAt ? fmtDate(ticket.firstResponseAt) : (['Resolved','Closed'].includes(statusLabel) ? '—' : 'Awaiting')}
                warn={!ticket.firstResponseAt && ticket.isResponseBreached}
              />
              <MetaRow
                label="Resolution Due"
                value={ticket.resolutionDueAt ? fmtDate(ticket.resolutionDueAt) : '—'}
                warn={ticket.isResolutionBreached}
              />
              <MetaRow
                label="Resolved At"
                value={ticket.resolvedAt ? fmtDate(ticket.resolvedAt) : '—'}
              />
              <MetaRow
                label="Closed At"
                value={ticket.closedAt ? fmtDate(ticket.closedAt) : '—'}
              />
              <MetaRow label="Created" value={fmtDate(ticket.createdAt)} />
              <MetaRow label="Last Updated" value={fmtDate(ticket.updatedAt)} />
            </div>

            {ticket.resolutionNotes && (
              <div className="bg-green-50 border border-green-200 rounded-xl p-4">
                <p className="text-xs font-semibold text-green-700 uppercase tracking-wider mb-2">Resolution Notes</p>
                <p className="text-sm text-green-800 leading-relaxed">{ticket.resolutionNotes}</p>
              </div>
            )}

            {/* D3-3 — root-cause analysis (distinct from resolution notes) */}
            {ticket.rootCause && (
              <div className="bg-amber-50 border border-amber-200 rounded-xl p-4">
                <p className="text-xs font-semibold text-amber-700 uppercase tracking-wider mb-2">Root Cause</p>
                <p className="text-sm text-amber-900 leading-relaxed">{ticket.rootCause}</p>
              </div>
            )}

            {/* D7-1 — IT-helpdesk context */}
            {(ticket.employeeId || ticket.branchId || ticket.systemAffected) && (
              <div className="bg-white rounded-xl border border-gray-200 p-5 space-y-3">
                <p className="text-xs font-bold text-navy uppercase tracking-wider">IT Details</p>
                {ticket.employeeId && <MetaRow label="Employee" value={ticket.employeeId} />}
                {ticket.branchId && <MetaRow label="Branch" value={ticket.branchId} />}
                {ticket.systemAffected && <MetaRow label="System Affected" value={ticket.systemAffected} />}
              </div>
            )}

            {/* D3-1 / D3-2 — related records: parent, children, project / FSR context refs */}
            {(ticket.parentTicketId || ticket.projectId || ticket.fsrId || children.length > 0) && (
              <div className="bg-white rounded-xl border border-gray-200 p-5 space-y-3">
                <p className="text-xs font-bold text-navy uppercase tracking-wider">Related</p>
                {ticket.parentTicketId && (
                  <button
                    onClick={() => navigate(`/modules/ticketing/${ticket.parentTicketId}`)}
                    className="w-full text-left text-sm text-navy hover:underline"
                  >
                    ↑ Parent ticket
                  </button>
                )}
                {children.length > 0 && (
                  <div>
                    <p className="text-xs text-gray-500 mb-1">Child tickets ({children.length})</p>
                    <ul className="space-y-1">
                      {children.map(c => (
                        <li key={c.id}>
                          <button
                            onClick={() => navigate(`/modules/ticketing/${c.id}`)}
                            className="text-left text-sm text-navy hover:underline truncate w-full"
                          >
                            <span className="font-mono text-gray-400">{c.reference}</span> {c.title} · {c.statusLabel}
                          </button>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
                {ticket.projectId && <MetaRow label="Project" value={ticket.projectId} />}
                {ticket.fsrId && <MetaRow label="Field Service Report" value={ticket.fsrId} />}
              </div>
            )}

            {/* #5 — closure classification */}
            {ticket.closureTypeLabel && (
              <div className="bg-white rounded-xl border border-gray-200 p-4 space-y-2">
                <p className="text-xs font-bold text-navy uppercase tracking-wider">Closure</p>
                <div className="flex items-center gap-2">
                  <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                    ticket.closureTypeLabel === 'Completed'
                      ? 'bg-green-100 text-green-700'
                      : ticket.closureTypeLabel === 'Cancelled'
                        ? 'bg-red-100 text-red-700'
                        : 'bg-gray-100 text-gray-600'
                  }`}>
                    {ticket.closureTypeLabel === 'AutoClosed' ? 'Auto-Closed' : ticket.closureTypeLabel}
                  </span>
                  {ticket.closedAt && <span className="text-xs text-gray-400">{fmtDate(ticket.closedAt)}</span>}
                </div>
                {ticket.closureReason && (
                  <p className="text-sm text-gray-600 leading-relaxed">{ticket.closureReason}</p>
                )}
              </div>
            )}

            {/* Escalations */}
            {escalations.length > 0 && (
              <div className="bg-white rounded-xl border border-gray-200 p-4">
                <p className="text-xs font-bold text-navy uppercase tracking-wider mb-3">Escalations</p>
                <div className="space-y-3">
                  {escalations.map(esc => (
                    <div key={esc.id} className={`rounded-lg border p-3 ${esc.isAcknowledged ? 'border-gray-100 bg-gray-50' : 'border-red-100 bg-red-50'}`}>
                      <div className="flex items-start justify-between gap-2 mb-1">
                        <span className="text-xs font-semibold text-gray-700">
                          {['Supervisor','Department Head','MD'][esc.escalationLevel] ?? esc.escalationLevel}
                        </span>
                        {esc.isAcknowledged ? (
                          <span className="text-xs text-green-600 font-semibold">✓ Acknowledged</span>
                        ) : (
                          <button
                            onClick={() => acknowledgeEscalation(esc.id)}
                            disabled={ackLoading === esc.id}
                            className="text-xs font-semibold px-2 py-0.5 bg-navy hover:bg-navy-dark disabled:opacity-50 text-white rounded-md transition-colors"
                          >
                            {ackLoading === esc.id ? '…' : 'Acknowledge'}
                          </button>
                        )}
                      </div>
                      <p className="text-xs text-gray-500 italic leading-snug">{esc.reason}</p>
                      <p className="text-xs text-gray-400 mt-1">{fmtDate(esc.escalatedAt)}</p>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Watch toggle */}
            {currentUser?.id && (
              <button
                onClick={toggleWatch}
                disabled={watchLoading}
                className={`w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl border text-sm font-semibold transition-colors disabled:opacity-50 ${
                  isWatching
                    ? 'border-gold bg-gold/10 text-navy hover:bg-gold/20'
                    : 'border-gray-200 bg-white text-gray-600 hover:bg-gray-50'
                }`}
              >
                <svg className="w-4 h-4" fill={isWatching ? 'currentColor' : 'none'} viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                  <path strokeLinecap="round" strokeLinejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                </svg>
                {watchLoading ? '…' : isWatching ? 'Watching' : 'Watch ticket'}
              </button>
            )}

            {/* Tags */}
            <div className="bg-white rounded-xl border border-gray-200 p-4">
              <p className="text-xs font-bold text-navy uppercase tracking-wider mb-3">Tags</p>
              <div className="flex flex-wrap gap-1.5 mb-3">
                {ticketTags.length === 0
                  ? <span className="text-xs text-gray-300">No tags</span>
                  : ticketTags.map(tag => (
                    <span key={tag.id} className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold text-white"
                      style={{ backgroundColor: tag.color ?? '#6366f1' }}>
                      {tag.name}
                      <button onClick={() => removeTag(tag.id)} className="ml-0.5 opacity-70 hover:opacity-100">
                        <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                        </svg>
                      </button>
                    </span>
                  ))
                }
              </div>
              <select
                onChange={e => { if (e.target.value) { addTag(e.target.value); e.target.value = '' } }}
                className="w-full px-2.5 py-1.5 border border-gray-200 rounded-lg text-xs focus:outline-none focus:ring-2 focus:ring-gold">
                <option value="">+ Add tag…</option>
                {allTags.filter(t => !ticketTags.find(tt => tt.id === t.id)).map(t => (
                  <option key={t.id} value={t.id}>{t.name}</option>
                ))}
              </select>
            </div>

            {/* Satisfaction Rating */}
            {(ticket.statusLabel === 'Resolved' || ticket.statusLabel === 'Closed') && (
              <div className="bg-white rounded-xl border border-gray-200 p-4">
                <p className="text-xs font-bold text-navy uppercase tracking-wider mb-3">Satisfaction</p>
                {rating ? (
                  <div className="text-center">
                    <div className="flex justify-center gap-1 mb-2">
                      {[1,2,3,4,5].map(s => (
                        <svg key={s} className={`w-6 h-6 ${s <= rating.rating ? 'text-gold' : 'text-gray-200'}`}
                          fill="currentColor" viewBox="0 0 20 20">
                          <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" />
                        </svg>
                      ))}
                    </div>
                    <p className="text-xs font-semibold text-gray-600">{rating.rating} / 5</p>
                    {rating.comment && <p className="text-xs text-gray-500 mt-1 italic">&ldquo;{rating.comment}&rdquo;</p>}
                  </div>
                ) : (
                  <div>
                    <p className="text-xs text-gray-500 mb-2">How was your experience?</p>
                    <div className="flex justify-center gap-1 mb-3">
                      {[1,2,3,4,5].map(s => (
                        <button key={s} type="button"
                          onMouseEnter={() => setRatingHover(s)} onMouseLeave={() => setRatingHover(0)}
                          onClick={() => submitRating(s)} disabled={ratingSubmitting}
                          className="focus:outline-none disabled:opacity-50">
                          <svg className={`w-7 h-7 transition-colors ${s <= (ratingHover || 0) ? 'text-gold' : 'text-gray-200'}`}
                            fill="currentColor" viewBox="0 0 20 20">
                            <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" />
                          </svg>
                        </button>
                      ))}
                    </div>
                    <input type="text" value={ratingComment} onChange={e => setRatingComment(e.target.value)}
                      placeholder="Optional comment…"
                      className="w-full px-2.5 py-1.5 border border-gray-200 rounded-lg text-xs focus:outline-none focus:ring-2 focus:ring-gold" />
                    <p className="text-xs text-gray-400 mt-1.5 text-center">Click a star to submit</p>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      </main>

      {/* Action Modal */}
      {actionModal && (
        <ActionModal
          type={actionModal}
          data={actionData}
          setData={setActionData}
          onConfirm={handleAction}
          onCancel={() => { setActionModal(null); setActionData({}) }}
          loading={actionLoading}
          error={actionError}
          departments={departments}
          users={users}
        />
      )}

      {/* #19 — Duplicate detection & merge modal */}
      {dupModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4" onClick={() => setDupModalOpen(false)}>
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg p-6" onClick={e => e.stopPropagation()}>
            <div className="flex items-start justify-between gap-3 mb-1">
              <h2 className="text-lg font-bold text-navy">Possible Duplicates</h2>
              <button onClick={() => setDupModalOpen(false)} className="text-gray-400 hover:text-gray-600">
                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><path d="M6 18L18 6M6 6l12 12"/></svg>
              </button>
            </div>
            <p className="text-xs text-gray-500 mb-4">
              Open tickets from the same requester. Merging closes the duplicate and links it to <span className="font-semibold">this</span> ticket.
            </p>

            {dupLoading ? (
              <p className="text-sm text-gray-400 text-center py-8 animate-pulse">Searching…</p>
            ) : duplicates.length === 0 ? (
              <div className="text-center py-8">
                <div className="text-2xl mb-1">✓</div>
                <p className="text-sm text-gray-500">No likely duplicates found.</p>
              </div>
            ) : (
              <div className="space-y-2 max-h-80 overflow-y-auto">
                {duplicates.map(d => (
                  <div key={d.id} className="flex items-center justify-between gap-3 rounded-lg border border-gray-200 p-3">
                    <button
                      onClick={() => { setDupModalOpen(false); navigate(`/modules/ticketing/${d.id}`) }}
                      className="min-w-0 text-left"
                    >
                      <p className="text-sm font-semibold text-gray-800 truncate">{d.title}</p>
                      <p className="text-xs text-gray-400">
                        {d.reference ?? `#${(d.id ?? '').slice(0, 8)}`} · {d.statusLabel} · {fmtDate(d.createdAt)}
                      </p>
                    </button>
                    <button
                      onClick={() => mergeDuplicate(d.id)}
                      disabled={mergeLoading === d.id}
                      className="shrink-0 px-3 py-1.5 text-xs font-semibold text-white bg-navy hover:bg-navy-dark disabled:opacity-50 rounded-lg transition-colors"
                    >
                      {mergeLoading === d.id ? 'Merging…' : 'Merge into this'}
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {/* Photo lightbox */}
      {photoLightbox && (
        <div className="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-4" onClick={() => setPhotoLightbox(null)}>
          <img src={photoLightbox} alt="Attachment" className="max-w-full max-h-full rounded-xl shadow-2xl" onClick={e => e.stopPropagation()} />
          <button onClick={() => setPhotoLightbox(null)} className="absolute top-4 right-4 w-9 h-9 bg-white/20 hover:bg-white/30 rounded-full flex items-center justify-center transition-colors">
            <svg className="w-5 h-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><path d="M6 18L18 6M6 6l12 12"/></svg>
          </button>
        </div>
      )}
    </>
  )
}
