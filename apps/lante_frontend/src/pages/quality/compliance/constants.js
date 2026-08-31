import { createElement } from 'react'
import { Badge } from '../../../components/ui.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// Shared constants, label maps, and small pure helpers for the Compliance &
// Governance module. Split out of the former monolithic CompliancePage.jsx so
// every tab file can import the same source of truth without duplicating it.
// Every *_LABELS array's index matches the corresponding C# enum's integer value exactly.
// ─────────────────────────────────────────────────────────────────────────────

export const GIFT_DIRECTIONS = ['Given', 'Received']
export const COI_STATUSES = ['Pending', 'Submitted', 'Reviewed']
export const WB_STATUSES = ['New', 'Under Investigation', 'Resolved', 'Closed']
export const DSR_TYPES = ['Access', 'Erasure', 'Correction']
export const DSR_STATUSES = ['Open', 'In Progress', 'Completed', 'Overdue']
export const BREACH_STATUSES = ['Open', 'Contained', 'Notified', 'Closed']
export const RELATIONSHIP_TYPES = ['Shareholder', 'Director', 'Affiliate', 'Other']
export const PARTY_RELATIONSHIPS = ['Subsidiary', 'SisterCompany', 'Affiliate', 'JointVenture', 'Other']
export const PARTY_RELATIONSHIP_LABELS = { Subsidiary: 'Subsidiary', SisterCompany: 'Sister Company', Affiliate: 'Affiliate', JointVenture: 'Joint Venture', Other: 'Other' }
export const LICENCE_TYPES = ['Nca', 'Nema', 'KebsNmk', 'Dosh', 'Other']
export const LICENCE_TYPE_LABELS = { Nca: 'NCA', Nema: 'NEMA', KebsNmk: 'KEBS/NMK', Dosh: 'DOSHS', Other: 'Other' }
export const OBLIGATION_FREQUENCIES = ['Monthly', 'Quarterly', 'Annually']
export const ANNUAL_RETURN_STATUSES = ['Pending', 'Filed', 'Overdue']
export const TCC_STATUSES = ['Valid', 'Expired', 'Pending']
export const COSEC_TASK_STATUSES = ['Open', 'InProgress', 'Done', 'Overdue']
export const COSEC_TASK_LABELS = { Open: 'Open', InProgress: 'In Progress', Done: 'Done', Overdue: 'Overdue' }
export const ragVariant = rag => rag === 'Red' ? 'red' : rag === 'Amber' ? 'amber' : 'green'

export const today = () => new Date().toISOString().slice(0, 10)
export const daysUntil = d => { if (!d) return null; const x = new Date(d); return isNaN(x) ? null : Math.ceil((x - new Date(new Date().toDateString())) / 86400000) }
export const dueBadge = (d, okLabel = 'OK') => {
  if (d === null) return createElement(Badge, { variant: 'default' }, '—')
  if (d < 0) return createElement(Badge, { variant: 'red' }, `Overdue ${Math.abs(d)}d`)
  if (d <= 30) return createElement(Badge, { variant: 'amber' }, `Due in ${d}d`)
  return createElement(Badge, { variant: 'green' }, okLabel)
}

export const EMPTY_GIFT = { employeeUserId: '', direction: 'Given', counterpartyName: '', isGovernmentOfficial: false, description: '', value: '', date: today() }
export const EMPTY_COI = { employeeUserId: '', year: new Date().getFullYear(), hasConflict: false, details: '' }
export const EMPTY_WB = { anonymous: false, summary: '' }
export const EMPTY_WB_UPDATE = { status: 'New', outcome: '' }
export const EMPTY_DSR = { type: 'Access', requestorName: '', requestorContact: '', receivedOn: today() }
export const EMPTY_DSR_UPDATE = { status: 'Open', notes: '' }
export const EMPTY_BREACH = { occurredAt: today(), discoveredAt: today(), description: '' }
export const EMPTY_BREACH_UPDATE = { status: 'Open', odpcNotifiedAt: '', remediationNotes: '' }
export const EMPTY_POLICY = { title: '', version: '', fileUrl: '' }
export const EMPTY_RESOLUTION = { referenceNo: '', title: '', resolutionDate: today(), summary: '', scannedCopyUrl: '' }
export const EMPTY_LICENCE = { type: 'Nca', authority: '', licenceNumber: '', issuedOn: '', expiryDate: '', alertDays: 90, renewalRequirements: '' }
export const EMPTY_RENEWAL = { licenceNumber: '', issuedOn: today(), newExpiryDate: '' }
export const EMPTY_TRAINING = { employeeUserId: '', completedOn: today(), certificateUrl: '' }
export const EMPTY_RELATED_PARTY = { partyName: '', relationshipType: 'Shareholder', transactionDate: today(), amount: '', description: '' }
export const EMPTY_ICM_PARTY = { companyName: '', regNo: '', relationship: 'SisterCompany', notes: '' }
export const EMPTY_ICSA = { relatedPartyId: '', scope: '', rechargeRate: '', startDate: today(), endDate: '' }
export const EMPTY_ICM_TXN = { icsaId: '', relatedPartyId: '', year: new Date().getFullYear(), amount: '', qslLedgerRef: '', sisterLedgerRef: '' }
export const EMPTY_OBLIGATION = { name: '', authority: '', frequency: 'Monthly', statutoryDay: 9, ownerUserId: '' }
export const EMPTY_ANNUAL_RETURN = { year: new Date().getFullYear(), dueDate: today() }
export const EMPTY_ANNUAL_RETURN_FILE = { filedDate: today() }
export const EMPTY_TCC = { expiryDate: '', itaxRef: '', alertDays: 60 }
export const EMPTY_TCC_RENEW = { newExpiryDate: '', itaxRef: '' }
export const EMPTY_COSEC_TASK = { obligationId: '', title: '', responsiblePersonUserId: '', dueDate: today() }
