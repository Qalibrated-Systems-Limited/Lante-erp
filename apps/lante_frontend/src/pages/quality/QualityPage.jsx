import { useState, useEffect } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, Tabs, SectionHeader, DataTable, Modal, Input, Select } from '../../components/ui.jsx'
import { getQualityDashboard } from '../../services/quality.js'

// ─────────────────────────────────────────────────────────────────────────────
// Quality (QMS) — port of the deployed QSL Quality module. 5 tabs:
// Overview / Nonconformities & CAPA / Internal Audits / Management Review /
// Competency Matrix. UI-only shell — MOCK/local state, no QMS backend yet
// (see memory BACKEND GAPS). Each modal prepends to its local register.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'

const NC_SOURCES = ['Process / Internal', 'Internal Audit', 'External Audit', 'Customer Complaint', 'Supplier', 'Calibration / Equipment']
const NC_CATEGORIES = ['Minor NC', 'Major NC', 'Observation', 'Opportunity for Improvement']
const AUDIT_STANDARDS = ['ISO 17020', 'ISO 17025', 'ISO 9001', 'ISO 45001']
const AUTH_CATEGORIES = ['Calibration', 'Inspection', 'Testing', 'Sampling']
const AUTH_LEVELS = ['Trainee', 'Authorized', 'Approved Signatory']
const STAFF = ['James Otieno', 'Grace Wanjiru', 'Peter Kimani', 'Aisha Mohammed', 'David Mwangi', 'Faith Njeri']

const daysUntil = d => { if (!d) return null; const x = new Date(d); return isNaN(x) ? null : Math.ceil((x - new Date(new Date().toDateString())) / 86400000) }
const nextNo = (list, prefix) => `${prefix}-${String(list.length + 1).padStart(3, '0')}`

export default function QualityPage() {
  const [tab, setTab] = useState('overview')
  const [modal, setModal] = useState(null)
  const [msg, setMsg] = useState(null)

  const [ncs, setNcs] = useState([])
  const [audits, setAudits] = useState([])
  const [reviews, setReviews] = useState([])
  const [auths, setAuths] = useState([])

  // Customer Satisfaction — the ONE tab on this page backed by a real API
  // (compliance-service's public survey + QualityDashboardController). Everything else
  // above/below remains the pre-existing UI-only QMS mock.
  const [surveyDashboard, setSurveyDashboard] = useState(null)
  const [surveyLoading, setSurveyLoading] = useState(true)
  const [surveyError, setSurveyError] = useState('')

  useEffect(() => {
    let cancelled = false
    setSurveyLoading(true)
    getQualityDashboard()
      .then(data => { if (!cancelled) { setSurveyDashboard(data); setSurveyError('') } })
      .catch(err => { if (!cancelled) setSurveyError(err.response?.data?.message ?? 'Failed to load customer survey results.') })
      .finally(() => { if (!cancelled) setSurveyLoading(false) })
    return () => { cancelled = true }
  }, [])

  const [ncForm, setNcForm] = useState({ source: NC_SOURCES[0], category: NC_CATEGORIES[0], description: '', clause: '', containment: '' })
  const [auditForm, setAuditForm] = useState({ title: '', standard: AUDIT_STANDARDS[0], auditor: STAFF[0], dept: '', planned: '', scope: '' })
  const [reviewForm, setReviewForm] = useState({ date: '', chair: '', attendees: '', agenda: '' })
  const [authForm, setAuthForm] = useState({ employee: '', scope: '', category: AUTH_CATEGORIES[0], level: AUTH_LEVELS[1], evidence: '', expiry: '' })

  function raiseNc() {
    if (!ncForm.description) return
    setNcs([{ id: nextNo(ncs, 'NC'), ...ncForm, status: 'Open', raised: new Date().toISOString().slice(0, 10) }, ...ncs])
    setNcForm({ source: NC_SOURCES[0], category: NC_CATEGORIES[0], description: '', clause: '', containment: '' })
    setModal(null); setMsg({ type: 'success', text: 'Nonconformity raised. CAPA required per QMS procedure.' })
  }
  function planAudit() {
    if (!auditForm.title || !auditForm.planned) return
    setAudits([{ id: nextNo(audits, 'AUD'), ...auditForm, findings: 0, status: 'Planned' }, ...audits])
    setAuditForm({ title: '', standard: AUDIT_STANDARDS[0], auditor: STAFF[0], dept: '', planned: '', scope: '' })
    setModal(null); setMsg({ type: 'success', text: 'Internal audit added to the programme.' })
  }
  function scheduleReview() {
    if (!reviewForm.date) return
    setReviews([{ id: nextNo(reviews, 'MR'), ...reviewForm, status: 'Scheduled', decisions: '—' }, ...reviews])
    setReviewForm({ date: '', chair: '', attendees: '', agenda: '' })
    setModal(null); setMsg({ type: 'success', text: 'Management review scheduled.' })
  }
  function grantAuth() {
    if (!authForm.employee || !authForm.scope) return
    setAuths([{ id: Date.now().toString(), ...authForm }, ...auths])
    setAuthForm({ employee: '', scope: '', category: AUTH_CATEGORIES[0], level: AUTH_LEVELS[1], evidence: '', expiry: '' })
    setModal(null); setMsg({ type: 'success', text: 'Authorization granted and added to the matrix.' })
  }

  const openNcs = ncs.filter(n => n.status !== 'Closed').length
  const authExpiring = auths.filter(a => { const d = daysUntil(a.expiry); return d !== null && d <= 60 }).length

  const ncVariant = c => c === 'Major NC' ? 'red' : c === 'Minor NC' ? 'amber' : 'blue'
  const ratingVariant = r => (r === 'Outstanding' || r === 'Good') ? 'green' : r === 'Average' ? 'blue' : r === 'Poor' ? 'amber' : r === 'Very Poor' ? 'red' : 'default'
  const feedbackTypeVariant = t => t === 'Compliment' ? 'green' : t === 'Complaint' ? 'red' : 'blue'
  const authStatus = a => {
    const d = daysUntil(a.expiry)
    if (d === null) return { label: 'Active', variant: 'green' }
    if (d < 0) return { label: 'Expired', variant: 'red' }
    if (d <= 60) return { label: `Expires ${d}d`, variant: 'amber' }
    return { label: 'Active', variant: 'green' }
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {msg && <Alert type={msg.type}>{msg.text}</Alert>}

        <Tabs
          tabs={[
            { id: 'overview', label: 'Overview' },
            { id: 'nc', label: 'Nonconformities & CAPA' },
            { id: 'audits', label: 'Internal Audits' },
            { id: 'review', label: 'Management Review' },
            { id: 'competency', label: 'Competency Matrix' },
            { id: 'customer-satisfaction', label: 'Customer Satisfaction' },
          ]}
          active={tab}
          setActive={setTab}
        />

        {/* ── Overview ── */}
        {tab === 'overview' && (
          <>
            <div style={{ ...KPI_GRID, marginBottom: 18 }}>
              <Kpi label="Open Nonconformities" value={openNcs} icon="⚠️" variant={openNcs ? 'amber' : undefined} />
              <Kpi label="Audits Planned" value={audits.length} icon="📋" />
              <Kpi label="Management Reviews" value={reviews.length} icon="🗓️" />
              <Kpi label="Authorizations Expiring" value={authExpiring} icon="🎓" variant={authExpiring ? 'red' : undefined} />
            </div>
            <Alert type="info">
              QMS aligned to <strong>ISO/IEC 17020</strong> &amp; <strong>17025</strong>. Track nonconformities &amp; CAPA, the internal audit programme, management reviews and the competency/authorization matrix from the tabs above.
            </Alert>
            <Card>
              <SectionHeader title="Quality Management System" sub="Impartiality, competence and consistent operation of the inspection & calibration body." />
              <p style={{ fontSize: 13, color: T.dgrey, lineHeight: 1.6 }}>
                Use <strong>Nonconformities &amp; CAPA</strong> to log and close out issues, <strong>Internal Audits</strong> to run the audit programme,
                <strong> Management Review</strong> for periodic leadership review, and <strong>Competency Matrix</strong> to record who is authorized for which method until when.
              </p>
            </Card>
          </>
        )}

        {/* ── Nonconformities & CAPA ── */}
        {tab === 'nc' && (
          <>
            <SectionHeader title="Nonconformity Register" action={<Btn onClick={() => setModal('nc')}>+ Raise NC</Btn>} />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable
                headers={['NC No', 'Source', 'Category', 'Description', 'Clause', 'CAPA', 'Status', 'Raised']}
                empty="No nonconformities recorded."
                rows={ncs.map(n => [
                  <strong style={{ fontSize: 12 }}>{n.id}</strong>, n.source,
                  <Badge variant={ncVariant(n.category)}>{n.category}</Badge>,
                  <span style={{ fontSize: 12 }}>{n.description}</span>, n.clause || '—',
                  n.containment ? 'Containment logged' : <Badge variant="amber">Pending</Badge>,
                  <Badge variant={n.status === 'Closed' ? 'green' : 'amber'}>{n.status}</Badge>, fmt.date(n.raised),
                ])}
              />
            </Card>
          </>
        )}

        {/* ── Internal Audits ── */}
        {tab === 'audits' && (
          <>
            <SectionHeader title="Internal Audit Programme" action={<Btn onClick={() => setModal('audit')}>+ Plan Audit</Btn>} />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable
                headers={['Audit No', 'Title', 'Standard', 'Auditor', 'Dept', 'Planned', 'Findings', 'Status']}
                empty="No audits planned."
                rows={audits.map(a => [
                  <strong style={{ fontSize: 12 }}>{a.id}</strong>, a.title,
                  <Badge variant="blue">{a.standard}</Badge>, a.auditor, a.dept || '—', fmt.date(a.planned),
                  a.findings, <Badge variant={a.status === 'Completed' ? 'green' : 'amber'}>{a.status}</Badge>,
                ])}
              />
            </Card>
          </>
        )}

        {/* ── Management Review ── */}
        {tab === 'review' && (
          <>
            <SectionHeader title="Management Reviews" action={<Btn onClick={() => setModal('review')}>+ Schedule Review</Btn>} />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable
                headers={['Review No', 'Date', 'Chaired By', 'Attendees', 'Status', 'Decisions']}
                empty="No management reviews recorded."
                rows={reviews.map(r => [
                  <strong style={{ fontSize: 12 }}>{r.id}</strong>, fmt.date(r.date), r.chair || '—', r.attendees || '—',
                  <Badge variant={r.status === 'Completed' ? 'green' : 'amber'}>{r.status}</Badge>, r.decisions,
                ])}
              />
            </Card>
          </>
        )}

        {/* ── Competency Matrix ── */}
        {tab === 'competency' && (
          <>
            <Alert type="info">
              Authorization matrix (17020 §6.1 / 17025 §6.2): who is signed off for which method, at what level, by whom, until when.
              <strong> Amber</strong> = expires within 60 days, <strong>red</strong> = expired — requalify or revoke.
            </Alert>
            <SectionHeader title="Active Authorizations" action={<Btn onClick={() => setModal('auth')}>+ Grant Authorization</Btn>} />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable
                headers={['Employee', 'Skill / Method / Scope', 'Category', 'Level', 'Evidence', 'Expiry', 'Status']}
                empty="No authorizations recorded yet."
                rows={auths.map(a => {
                  const s = authStatus(a)
                  return [
                    <strong style={{ fontSize: 12 }}>{a.employee}</strong>, a.scope,
                    <Badge variant="blue">{a.category}</Badge>, a.level, a.evidence || '—',
                    a.expiry ? fmt.date(a.expiry) : 'No expiry', <Badge variant={s.variant}>{s.label}</Badge>,
                  ]
                })}
              />
            </Card>
          </>
        )}

        {/* ── Customer Satisfaction (real data — public survey results) ── */}
        {tab === 'customer-satisfaction' && (
          <>
            <Alert type="info">
              Results from the public Customer Satisfaction Survey (<code>/portal/survey</code>) — anonymous
              compliments, complaints and general feedback submitted by clients, rated on service quality.
            </Alert>

            {surveyError && <Alert type="error">{surveyError}</Alert>}

            <div style={{ ...KPI_GRID, marginBottom: 18 }}>
              <Kpi label="Total Responses" value={surveyDashboard?.totalResponses ?? 0} icon="🗳️" loading={surveyLoading} />
              <Kpi
                label="Average Rating"
                value={surveyDashboard?.averageRating != null ? surveyDashboard.averageRating.toFixed(2) : '—'}
                sub="out of 5"
                icon="⭐"
                variant={surveyDashboard?.averageRating >= 4 ? 'green' : surveyDashboard?.averageRating >= 3 ? 'amber' : surveyDashboard ? 'red' : undefined}
                loading={surveyLoading}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 14, marginBottom: 18 }}>
              <Card>
                <SectionHeader title="Rating Breakdown" />
                {(surveyDashboard?.ratingBreakdown ?? []).map(r => (
                  <div key={r.label} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '6px 0', borderBottom: `1px solid ${T.lgrey}` }}>
                    <Badge variant={ratingVariant(r.label)}>{r.label}</Badge>
                    <strong style={{ fontSize: 13, color: T.navy }}>{r.count}</strong>
                  </div>
                ))}
                {!surveyLoading && (surveyDashboard?.ratingBreakdown ?? []).length === 0 && (
                  <p style={{ fontSize: 13, color: T.mgrey, padding: '8px 0' }}>No responses yet.</p>
                )}
              </Card>

              <Card>
                <SectionHeader title="Feedback Type Breakdown" />
                {(surveyDashboard?.feedbackTypeBreakdown ?? []).map(f => (
                  <div key={f.label} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '6px 0', borderBottom: `1px solid ${T.lgrey}` }}>
                    <Badge variant={feedbackTypeVariant(f.label)}>{f.label}</Badge>
                    <strong style={{ fontSize: 13, color: T.navy }}>{f.count}</strong>
                  </div>
                ))}
                {!surveyLoading && (surveyDashboard?.feedbackTypeBreakdown ?? []).length === 0 && (
                  <p style={{ fontSize: 13, color: T.mgrey, padding: '8px 0' }}>No responses yet.</p>
                )}
              </Card>
            </div>

            <SectionHeader title="Recent Responses" sub="Last 20 submissions, most recent first" />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable
                headers={['Submitted', 'Respondent', 'Corporation', 'Counsellor', 'Type', 'Rating', 'Details']}
                empty={surveyLoading ? 'Loading…' : 'No survey responses yet.'}
                rows={(surveyDashboard?.recentResponses ?? []).map(r => [
                  fmt.date(r.submittedAt),
                  r.respondentName,
                  r.corporationName,
                  r.counsellorName,
                  <Badge variant={feedbackTypeVariant(r.feedbackType)}>{r.feedbackType}</Badge>,
                  <Badge variant={ratingVariant(r.rating)}>{r.rating}</Badge>,
                  <span style={{ fontSize: 12 }}>{r.details}</span>,
                ])}
              />
            </Card>
          </>
        )}

        {/* ── Modals ── */}
        {modal === 'nc' && (
          <Modal title="Raise Nonconformity" onClose={() => setModal(null)}>
            <Select label="Source" value={ncForm.source} onChange={v => setNcForm({ ...ncForm, source: v })} options={NC_SOURCES.map(s => ({ value: s, label: s }))} />
            <Select label="Category" value={ncForm.category} onChange={v => setNcForm({ ...ncForm, category: v })} options={NC_CATEGORIES.map(s => ({ value: s, label: s }))} />
            <Input label="Description of the nonconformity" value={ncForm.description} onChange={v => setNcForm({ ...ncForm, description: v })} required placeholder="What requirement was not met, where, and how it was found" />
            <Input label="Clause Reference (optional)" value={ncForm.clause} onChange={v => setNcForm({ ...ncForm, clause: v })} placeholder="e.g. ISO 17020 §7.1.3" />
            <Input label="Immediate containment taken (optional)" value={ncForm.containment} onChange={v => setNcForm({ ...ncForm, containment: v })} placeholder="What was done right away to limit impact" />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
              <Btn onClick={raiseNc} disabled={!ncForm.description}>Raise NC</Btn>
            </div>
          </Modal>
        )}

        {modal === 'audit' && (
          <Modal title="Plan Internal Audit" onClose={() => setModal(null)}>
            <Input label="Audit Title" value={auditForm.title} onChange={v => setAuditForm({ ...auditForm, title: v })} required placeholder="e.g. Calibration lab process audit" />
            <Select label="Standard" value={auditForm.standard} onChange={v => setAuditForm({ ...auditForm, standard: v })} options={AUDIT_STANDARDS.map(s => ({ value: s, label: s }))} />
            <Select label="Auditor" value={auditForm.auditor} onChange={v => setAuditForm({ ...auditForm, auditor: v })} options={STAFF.map(s => ({ value: s, label: s }))} />
            <Input label="Department / Area" value={auditForm.dept} onChange={v => setAuditForm({ ...auditForm, dept: v })} placeholder="e.g. Technical / Calibration" />
            <Input label="Planned Date" type="date" value={auditForm.planned} onChange={v => setAuditForm({ ...auditForm, planned: v })} required />
            <Input label="Scope (optional)" value={auditForm.scope} onChange={v => setAuditForm({ ...auditForm, scope: v })} placeholder="Clauses / processes covered" />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
              <Btn onClick={planAudit} disabled={!auditForm.title || !auditForm.planned}>Plan Audit</Btn>
            </div>
          </Modal>
        )}

        {modal === 'review' && (
          <Modal title="Schedule Management Review" onClose={() => setModal(null)}>
            <Input label="Review Date" type="date" value={reviewForm.date} onChange={v => setReviewForm({ ...reviewForm, date: v })} required />
            <Select label="Chaired By" value={reviewForm.chair} onChange={v => setReviewForm({ ...reviewForm, chair: v })} options={[{ value: '', label: 'Select…' }, ...STAFF.map(s => ({ value: s, label: s }))]} />
            <Input label="Attendees" value={reviewForm.attendees} onChange={v => setReviewForm({ ...reviewForm, attendees: v })} placeholder="Names / roles" />
            <Input label="Inputs / Agenda" value={reviewForm.agenda} onChange={v => setReviewForm({ ...reviewForm, agenda: v })} placeholder="Audit results, NC trends, customer feedback, resource needs…" />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
              <Btn onClick={scheduleReview} disabled={!reviewForm.date}>Schedule</Btn>
            </div>
          </Modal>
        )}

        {modal === 'auth' && (
          <Modal title="Grant Authorization" onClose={() => setModal(null)}>
            <Select label="Employee" value={authForm.employee} onChange={v => setAuthForm({ ...authForm, employee: v })} required options={[{ value: '', label: 'Select…' }, ...STAFF.map(s => ({ value: s, label: s }))]} />
            <Input label="Skill / Method / Scope" value={authForm.scope} onChange={v => setAuthForm({ ...authForm, scope: v })} required placeholder="e.g. Pressure calibration — dead-weight tester, 0–700 bar" />
            <Select label="Category" value={authForm.category} onChange={v => setAuthForm({ ...authForm, category: v })} options={AUTH_CATEGORIES.map(s => ({ value: s, label: s }))} />
            <Select label="Level" value={authForm.level} onChange={v => setAuthForm({ ...authForm, level: v })} options={AUTH_LEVELS.map(s => ({ value: s, label: s }))} />
            <Input label="Evidence" value={authForm.evidence} onChange={v => setAuthForm({ ...authForm, evidence: v })} placeholder="Training record / witnessed assessment reference" />
            <Input label="Expiry Date (optional)" type="date" value={authForm.expiry} onChange={v => setAuthForm({ ...authForm, expiry: v })} note="Leave blank for no expiry; set one to drive requalification alerts" />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
              <Btn onClick={grantAuth} disabled={!authForm.employee || !authForm.scope}>Grant</Btn>
            </div>
          </Modal>
        )}
      </div>
    </>
  )
}
