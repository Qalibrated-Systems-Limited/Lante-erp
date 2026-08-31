import { createElement } from 'react'
import { Badge } from '../../components/ui.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// HSE constants & pure helpers — shared by HsePage.jsx and pages/hse/tabs/*.
// No JSX syntax here (this is a .js file); dueBadge builds its <Badge> via
// createElement so it still returns a real element without needing the JSX
// transform enabled for .js files.
// ─────────────────────────────────────────────────────────────────────────────

// Order matches the corresponding C# enum's integer values exactly (index sent as-is).
export const INCIDENT_TYPES = ['Near Miss', 'First Aid', 'Medical Treatment', 'Lost Time Injury', 'Positive Observation']
export const SEVERITIES = ['None', 'Low', 'Medium', 'High', 'Critical']
export const RAMS_STATUSES = ['Draft', 'Submitted', 'Under Review', 'Approved', 'Rejected', 'Needs Revision']
export const INCIDENT_STATUSES = ['Open', 'Under Investigation', 'CAPA Pending', 'Closed']
export const PPE_CONDITIONS = ['New', 'Good', 'Worn', 'Damaged']
export const INSPECTION_STATUSES = ['Scheduled', 'Passed', 'Failed', 'Overdue']
export const CORRECTIVE_STATUSES = ['Open', 'In Progress', 'Completed', 'Overdue']

export const today = () => new Date().toISOString().slice(0, 10)
export const daysUntil = d => { if (!d) return null; const x = new Date(d); return isNaN(x) ? null : Math.ceil((x - new Date(new Date().toDateString())) / 86400000) }
export const dueBadge = (d, passedLabel = 'OK') => {
  if (d === null) return createElement(Badge, { variant: 'default' }, '—')
  if (d < 0) return createElement(Badge, { variant: 'red' }, `Overdue ${Math.abs(d)}d`)
  if (d <= 30) return createElement(Badge, { variant: 'amber' }, `Due in ${d}d`)
  return createElement(Badge, { variant: 'green' }, passedLabel)
}

export const EMPTY_INCIDENT = {
  type: 'Near Miss', site: '', severity: 'Low', occurredAt: today(), description: '',
  isEnvironmental: false, nemaRef: '',
  capaDescription: '', capaOwnerUserId: '', capaDueDate: '',
}
export const EMPTY_RAMS = { site: '', title: '', subcontractorId: '', fileUrl: '', issueNotes: '' }
export const EMPTY_PPE = { employeeUserId: '', item: '', condition: 'New', replacementDueAt: '' }
export const EMPTY_PPE_UPDATE = { condition: 'New', returned: false, returnedAt: '', replacementDueAt: '' }
export const EMPTY_TALK = { site: '', supervisorUserId: '', topic: '', heldOn: today(), attendeeIds: [] }
export const EMPTY_TRAINING = { employeeUserId: '', course: '', completedOn: today(), expiresOn: '', certificateUrl: '' }
export const EMPTY_INSPECTION = { site: '', equipment: '', dueDate: '' }
export const EMPTY_RESULT = { status: 'Passed', inspectorName: '', certificateUrl: '', nextDueDate: '' }
export const EMPTY_CAPA = { description: '', ownerUserId: '', dueDate: '' }
export const EMPTY_SUBCONTRACTOR = { name: '', tradeCategory: '', safetyScore: '', ramsSubmitted: false, notes: '' }
export const EMPTY_PREQUAL = { safetyScore: '', ramsSubmitted: false, prequalified: false, notes: '' }
