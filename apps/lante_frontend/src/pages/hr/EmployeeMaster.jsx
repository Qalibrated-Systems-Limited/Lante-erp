import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, Tabs, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import * as hr from '../../services/hr.js'

// ─────────────────────────────────────────────────────────────────────────────
// H1 — employee master, document vault, positions and org chart. REAL, wired to
// hr-service (/api/v1/hr/*). Per HR-DEC-8 these are the HR-admin screens;
// employee self-service (own leave, payslips, GPS clock-in) is a later pass.
// The rest of HrPage.jsx is still the mock shell for phases H2–H11.
// ─────────────────────────────────────────────────────────────────────────────

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')

const STATUS_VARIANT = {
  OnProbation: 'amber', Active: 'green', OnLeave: 'blue',
  Suspended: 'red', Resigned: 'default', Terminated: 'default',
}
const EMPLOYMENT_TYPES = ['Permanent', 'FixedTerm', 'Contract', 'Intern', 'Casual']
const DOC_TYPES = [
  'SignedContract', 'IdCopy', 'AcademicCertificate', 'ProfessionalCertificate',
  'MedicalRecord', 'KraPinCertificate', 'NssfCard', 'ShaCard', 'PassportPhoto', 'Other',
]

function Kpi({ label, value, sub, color }) {
  return (
    <Card style={{ padding: 14 }}>
      <p style={{ fontSize: 22, fontWeight: 800, color: color ?? T.navy, margin: 0 }}>{value}</p>
      <p style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .4, marginTop: 3 }}>{label}</p>
      {sub && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{sub}</p>}
    </Card>
  )
}

// ── Employees ────────────────────────────────────────────────────────────────
export function EmployeesTab({ flash }) {
  const [rows, setRows] = useState([])
  const [sum, setSum] = useState(null)
  const [loading, setLoading] = useState(true)
  const [filter, setFilter] = useState({ search: '', status: '' })
  const [newOpen, setNewOpen] = useState(false)
  const [detailId, setDetailId] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    hr.listEmployees({ search: filter.search || undefined, status: filter.status || undefined, pageSize: 200 })
      .then(r => setRows(r.data ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
    hr.employeeSummary().then(setSum).catch(() => {})
  }, [filter])
  useEffect(() => { load() }, [load])

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 18 }}>
        <Kpi label="Employees" value={sum?.total ?? 0} />
        <Kpi label="Active" value={sum?.active ?? 0} color={T.green} />
        <Kpi label="On Probation" value={sum?.onProbation ?? 0} color={T.amber} />
        <Kpi label="Onboarding Incomplete" value={sum?.onboardingIncomplete ?? 0} color={T.amber} />
        <Kpi label="No Login Account" value={sum?.withoutUserAccount ?? 0} color={T.blue} />
        <Kpi label="Docs To Verify" value={sum?.documentsAwaitingVerification ?? 0} color={T.blue} />
        <Kpi label="Certs Expiring ≤30d" value={sum?.certificationsExpiringIn30Days ?? 0} color={T.amber} />
        <Kpi label="Certs Expired" value={sum?.expiredCertifications ?? 0} color={T.red} />
      </div>

      <SectionHeader
        title="Employee Master"
        sub="A new hire starts on probation. Onboarding needs a signed contract and ID copy on file; documents are verified by a second HR officer."
        action={<Btn size="sm" onClick={() => setNewOpen(true)}>+ Onboard Employee</Btn>}
      />

      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14 }}>
        <input placeholder="Search name / number / email…" value={filter.search}
          onChange={e => setFilter(f => ({ ...f, search: e.target.value }))}
          style={{ flex: '1 1 220px', height: 40, padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, boxSizing: 'border-box' }} />
        <select value={filter.status} onChange={e => setFilter(f => ({ ...f, status: e.target.value }))}
          style={{ height: 40, padding: '0 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}>
          <option value="">All statuses</option>
          {Object.keys(STATUS_VARIANT).map(s => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      {loading ? <Loading /> : (
        <DataTable
          headers={['Employee #', 'Name', 'Department', 'Position', 'Type', 'Hired', 'Onboarding', 'Status', 'Actions']}
          empty="No employees yet — onboard the first one."
          rows={rows.map(e => [
            <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{e.employeeNumber}</span>,
            <span>{e.fullName}{!e.hasUserAccount && <span style={{ fontSize: 10, color: T.blue }}> · no login</span>}</span>,
            e.departmentName ?? '—',
            e.jobTitle ?? '—',
            e.employmentType,
            fmtDate(e.hireDate),
            e.onboardingComplete
              ? <span style={{ color: T.green, fontSize: 12 }}>complete</span>
              : <span style={{ color: T.amber, fontSize: 12 }}>incomplete</span>,
            <Badge variant={STATUS_VARIANT[e.status] ?? 'default'}>{e.status}</Badge>,
            <Btn size="sm" variant="outline" onClick={() => setDetailId(e.id)}>Open</Btn>,
          ])}
        />
      )}

      {newOpen && <OnboardModal onClose={() => setNewOpen(false)} onSaved={() => { setNewOpen(false); load() }} flash={flash} />}
      {detailId && <EmployeeDetailModal id={detailId} onClose={() => setDetailId(null)} onChanged={load} flash={flash} />}
    </div>
  )
}

function OnboardModal({ onClose, onSaved, flash }) {
  const [departments, setDepartments] = useState([])
  const [branches, setBranches] = useState([])
  const [positions, setPositions] = useState([])
  const [managers, setManagers] = useState([])
  const [busy, setBusy] = useState(false)
  const [f, setF] = useState({
    firstName: '', lastName: '', otherNames: '', workEmail: '', workPhone: '',
    nationalId: '', kraPin: '', nssfNumber: '', shaNumber: '',
    hireDate: '', employmentType: 'Permanent', contractEndDate: '',
    departmentId: '', branchId: '', positionId: '', reportsToId: '', createUserAccount: true,
  })

  useEffect(() => {
    hr.listDepartments().then(d => setDepartments(d ?? [])).catch(() => {})
    hr.listBranches().then(b => setBranches(b ?? [])).catch(() => {})
    hr.listPositions().then(p => setPositions(p ?? [])).catch(() => {})
    hr.listEmployees({ pageSize: 200 }).then(r => setManagers(r.data ?? [])).catch(() => {})
  }, [])

  const needsEnd = f.employmentType === 'FixedTerm' || f.employmentType === 'Contract'

  const save = async () => {
    if (!f.firstName.trim() || !f.lastName.trim()) { flash('First and last name are required.'); return }
    if (!f.workEmail.trim()) { flash('A work email is required — it is the login identity.'); return }
    if (!f.hireDate) { flash('The hire date is required.'); return }
    if (needsEnd && !f.contractEndDate) { flash(`${f.employmentType} employment needs a contract end date.`); return }
    setBusy(true)
    try {
      const r = await hr.createEmployee({
        ...f,
        hireDate: new Date(f.hireDate).toISOString(),
        contractEndDate: f.contractEndDate ? new Date(f.contractEndDate).toISOString() : undefined,
        departmentId: f.departmentId || undefined,
        branchId: f.branchId || undefined,
        positionId: f.positionId || undefined,
        reportsToId: f.reportsToId || undefined,
      })
      flash(r?.message ?? 'Employee onboarded.')
      onSaved()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not onboard.') }
    finally { setBusy(false) }
  }

  return (
    <Modal title="Onboard Employee" onClose={onClose} width={720}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
        <Input label="First name" value={f.firstName} onChange={v => setF(x => ({ ...x, firstName: v }))} required />
        <Input label="Last name" value={f.lastName} onChange={v => setF(x => ({ ...x, lastName: v }))} required />
        <Input label="Other names" value={f.otherNames} onChange={v => setF(x => ({ ...x, otherNames: v }))} />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Input label="Work email (login identity)" value={f.workEmail} onChange={v => setF(x => ({ ...x, workEmail: v }))} required />
        <Input label="Work phone" value={f.workPhone} onChange={v => setF(x => ({ ...x, workPhone: v }))} />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 10 }}>
        <Input label="National ID" value={f.nationalId} onChange={v => setF(x => ({ ...x, nationalId: v }))} />
        <Input label="KRA PIN" value={f.kraPin} onChange={v => setF(x => ({ ...x, kraPin: v }))} />
        <Input label="NSSF no." value={f.nssfNumber} onChange={v => setF(x => ({ ...x, nssfNumber: v }))} />
        <Input label="SHA no." value={f.shaNumber} onChange={v => setF(x => ({ ...x, shaNumber: v }))} />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
        <Input label="Hire date" type="date" value={f.hireDate} onChange={v => setF(x => ({ ...x, hireDate: v }))} required />
        <Select label="Employment type" value={f.employmentType} onChange={v => setF(x => ({ ...x, employmentType: v }))} options={EMPLOYMENT_TYPES} />
        <Input label={`Contract end${needsEnd ? ' *' : ''}`} type="date" value={f.contractEndDate}
          onChange={v => setF(x => ({ ...x, contractEndDate: v }))} />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Select label="Department (user-service)" value={f.departmentId} onChange={v => setF(x => ({ ...x, departmentId: v }))}
          options={[{ value: '', label: 'Select…' }, ...departments.map(d => ({ value: d.id, label: d.name }))]} />
        <Select label="Branch (user-service)" value={f.branchId} onChange={v => setF(x => ({ ...x, branchId: v }))}
          options={[{ value: '', label: 'Select…' }, ...branches.map(b => ({ value: b.id, label: b.name }))]} />
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
        <Select label="Position" value={f.positionId} onChange={v => setF(x => ({ ...x, positionId: v }))}
          options={[{ value: '', label: 'Select…' }, ...positions.map(p => ({ value: p.id, label: p.title }))]} />
        <Select label="Reports to" value={f.reportsToId} onChange={v => setF(x => ({ ...x, reportsToId: v }))}
          options={[{ value: '', label: '(apex — no manager)' }, ...managers.map(m => ({ value: m.id, label: `${m.fullName} — ${m.jobTitle ?? ''}` }))]} />
      </div>

      <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, color: T.dgrey, marginTop: 6 }}>
        <input type="checkbox" checked={f.createUserAccount} onChange={e => setF(x => ({ ...x, createUserAccount: e.target.checked }))} />
        Create the login account and send the invitation now
      </label>
      <p style={{ fontSize: 11, color: T.mgrey, marginTop: 4 }}>
        The employee record is saved even if the account cannot be created — you can retry from the detail view.
      </p>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 }}>
        <Btn variant="outline" onClick={onClose}>Cancel</Btn>
        <Btn disabled={busy} onClick={save}>Onboard</Btn>
      </div>
    </Modal>
  )
}

function EmployeeDetailModal({ id, onClose, onChanged, flash }) {
  const [e, setE] = useState(null)
  const [tab, setTab] = useState('personal')
  const [busy, setBusy] = useState(false)
  const [doc, setDoc] = useState({ documentType: 'SignedContract', fileUrl: '', expiryDate: '' })
  const [cert, setCert] = useState({ certificationName: '', issuingBody: '', expiryDate: '' })
  const [contact, setContact] = useState({ name: '', relationship: '', phone: '', isPrimary: false })
  const [bank, setBank] = useState({ bankName: '', branchName: '', accountNumber: '', isPrimary: false })

  const load = useCallback(() => { hr.getEmployee(id).then(setE).catch(() => setE(null)) }, [id])
  useEffect(() => { load() }, [load])

  const run = async (fn, ok) => {
    setBusy(true)
    try { const r = await fn(); flash(r?.message ?? ok); load(); onChanged?.() }
    catch (err) { flash(err.response?.data?.message ?? 'Action failed.') }
    finally { setBusy(false) }
  }

  if (!e) return <Modal title="Employee" onClose={onClose}><Loading /></Modal>

  return (
    <Modal title={`${e.employeeNumber} — ${e.fullName}`} onClose={onClose} width={820}>
      <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap', marginBottom: 12 }}>
        <Badge variant={STATUS_VARIANT[e.status] ?? 'default'}>{e.status}</Badge>
        <span style={{ fontSize: 13, color: T.mgrey }}>{e.jobTitle ?? '—'} · {e.departmentName ?? '—'}</span>
        <span style={{ fontSize: 12, color: T.mgrey }}>hired {fmtDate(e.hireDate)}</span>
        {e.reportsToName && <span style={{ fontSize: 12, color: T.mgrey }}>reports to {e.reportsToName}</span>}
        {e.hasExpiredCertification && <Badge variant="red">expired certification</Badge>}
      </div>

      {!e.onboardingComplete && (
        <div style={{ padding: 10, background: T.amberL, borderRadius: 8, marginBottom: 12 }}>
          <p style={{ fontSize: 12, color: T.amber, margin: 0 }}>
            Onboarding incomplete — still missing: <b>{e.missingMandatoryDocuments.join(', ') || '—'}</b>
          </p>
        </div>
      )}
      {!e.hasUserAccount && (
        <div style={{ padding: 10, border: `1px solid ${T.blueL}`, borderRadius: 8, marginBottom: 12, display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <span style={{ fontSize: 12, color: T.blue, flex: '1 1 260px' }}>
            No login account.{e.accountCreationError ? ` Last attempt: ${e.accountCreationError}` : ''}
          </span>
          <Btn size="sm" variant="outline" disabled={busy}
            onClick={() => run(() => hr.createUserAccount(e.id), 'Login account created.')}>Create login account</Btn>
        </div>
      )}

      <Tabs tabs={[
        { id: 'personal', label: 'Personal' },
        { id: 'documents', label: `Documents (${e.documents.length})` },
        { id: 'certifications', label: `Certifications (${e.certifications.length})` },
        { id: 'contacts', label: `Contacts (${e.emergencyContacts.length})` },
        { id: 'history', label: 'Education & History' },
        { id: 'bank', label: `Bank (${e.bankDetails.length})` },
      ]} active={tab} setActive={setTab} />

      {tab === 'personal' && (
        <DataTable headers={['Field', 'Value']} rows={[
          ['Full name', e.fullName],
          ['Work email', e.workEmail],
          ['Work phone', e.workPhone ?? '—'],
          ['National ID', e.nationalId ?? '—'],
          ['Date of birth', fmtDate(e.dateOfBirth)],
          ['KRA PIN', e.kraPin ?? '—'],
          ['NSSF / SHA / HELB', `${e.nssfNumber ?? '—'} / ${e.shaNumber ?? '—'} / ${e.helbNumber ?? '—'}`],
          ['Employment type', e.employmentType],
          ['Contract', e.contractEndDate ? `${fmtDate(e.contractStartDate)} → ${fmtDate(e.contractEndDate)}` : 'open-ended'],
          ['Confirmation date', fmtDate(e.confirmationDate)],
          ['Branch', e.branchName ?? '—'],
        ]} />
      )}

      {tab === 'documents' && (
        <div>
          <DataTable
            headers={['Type', 'Uploaded', 'Expiry', 'Verified', 'Actions']}
            empty="No documents on file."
            rows={e.documents.map(d => [
              <span>{d.documentType}{d.isExpired && <span style={{ color: T.red, fontSize: 10 }}> · expired</span>}</span>,
              <span style={{ fontSize: 12, color: T.mgrey }}>{fmtDate(d.uploadedAt)}</span>,
              d.expiryDate ? fmtDate(d.expiryDate) : '—',
              d.isVerified
                ? <span style={{ color: T.green, fontSize: 12 }}>✓ {fmtDate(d.verifiedAt)}</span>
                : <span style={{ color: T.amber, fontSize: 12 }}>pending</span>,
              d.isVerified
                ? <a href={d.fileUrl} target="_blank" rel="noreferrer" style={{ color: T.blue, fontSize: 12 }}>view</a>
                : <Btn size="sm" variant="outline" disabled={busy}
                    onClick={() => run(() => hr.verifyDocument(d.id, {}), 'Document verified.')}>Verify</Btn>,
            ])}
          />
          <p style={{ fontSize: 11, color: T.mgrey, marginTop: 6 }}>
            A document must be verified by a different HR officer from the one who uploaded it.
          </p>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end', marginTop: 10 }}>
            <select value={doc.documentType} onChange={ev => setDoc(d => ({ ...d, documentType: ev.target.value }))}
              style={{ height: 36, padding: '0 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }}>
              {DOC_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
            <input placeholder="File URL" value={doc.fileUrl} onChange={ev => setDoc(d => ({ ...d, fileUrl: ev.target.value }))}
              style={{ height: 36, flex: '1 1 200px', padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <input type="date" value={doc.expiryDate} onChange={ev => setDoc(d => ({ ...d, expiryDate: ev.target.value }))}
              style={{ height: 36, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <Btn size="sm" variant="outline" disabled={busy || !doc.fileUrl.trim()}
              onClick={() => { run(() => hr.uploadDocument(e.id, {
                documentType: doc.documentType, fileUrl: doc.fileUrl,
                expiryDate: doc.expiryDate ? new Date(doc.expiryDate).toISOString() : undefined,
              }), 'Document uploaded.'); setDoc(d => ({ ...d, fileUrl: '', expiryDate: '' })) }}>Upload</Btn>
          </div>
        </div>
      )}

      {tab === 'certifications' && (
        <div>
          <DataTable
            headers={['Certification', 'Body', 'Issued', 'Expires', 'State']}
            empty="No professional certifications recorded."
            rows={e.certifications.map(c => [
              c.certificationName,
              c.issuingBody ?? '—',
              fmtDate(c.issueDate),
              fmtDate(c.expiryDate),
              c.isExpired ? <Badge variant="red">expired</Badge>
                : c.isExpiringSoon ? <Badge variant="amber">expiring ≤30d</Badge>
                  : <Badge variant="green">valid</Badge>,
            ])}
          />
          <p style={{ fontSize: 11, color: T.mgrey, marginTop: 6 }}>
            An expired certification blocks the salary increment workflow (HR-035).
          </p>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end', marginTop: 10 }}>
            <input placeholder="Certification name" value={cert.certificationName}
              onChange={ev => setCert(c => ({ ...c, certificationName: ev.target.value }))}
              style={{ height: 36, flex: '1 1 180px', padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <input placeholder="Issuing body" value={cert.issuingBody}
              onChange={ev => setCert(c => ({ ...c, issuingBody: ev.target.value }))}
              style={{ height: 36, width: 140, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <input type="date" value={cert.expiryDate} onChange={ev => setCert(c => ({ ...c, expiryDate: ev.target.value }))}
              style={{ height: 36, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <Btn size="sm" variant="outline" disabled={busy || !cert.certificationName.trim()}
              onClick={() => { run(() => hr.addCertification(e.id, {
                ...cert, expiryDate: cert.expiryDate ? new Date(cert.expiryDate).toISOString() : undefined,
              }), 'Certification registered.'); setCert({ certificationName: '', issuingBody: '', expiryDate: '' }) }}>Add</Btn>
          </div>
        </div>
      )}

      {tab === 'contacts' && (
        <div>
          <DataTable
            headers={['Name', 'Relationship', 'Phone', 'Primary', 'Actions']}
            empty="No emergency contacts."
            rows={e.emergencyContacts.map(c => [
              c.name, c.relationship, c.phone,
              c.isPrimary ? <Badge variant="green">primary</Badge> : '—',
              <Btn size="sm" variant="danger" disabled={busy}
                onClick={() => run(() => hr.removeSubRecord('emergency-contact', c.id), 'Contact removed.')}>Remove</Btn>,
            ])}
          />
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end', marginTop: 10 }}>
            <input placeholder="Name" value={contact.name} onChange={ev => setContact(c => ({ ...c, name: ev.target.value }))}
              style={{ height: 36, flex: '1 1 140px', padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <input placeholder="Relationship" value={contact.relationship} onChange={ev => setContact(c => ({ ...c, relationship: ev.target.value }))}
              style={{ height: 36, width: 130, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <input placeholder="Phone" value={contact.phone} onChange={ev => setContact(c => ({ ...c, phone: ev.target.value }))}
              style={{ height: 36, width: 140, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <label style={{ fontSize: 12, display: 'flex', alignItems: 'center', gap: 5 }}>
              <input type="checkbox" checked={contact.isPrimary} onChange={ev => setContact(c => ({ ...c, isPrimary: ev.target.checked }))} /> primary
            </label>
            <Btn size="sm" variant="outline" disabled={busy || !contact.name.trim() || !contact.phone.trim()}
              onClick={() => { run(() => hr.addEmergencyContact(e.id, contact), 'Contact added.'); setContact({ name: '', relationship: '', phone: '', isPrimary: false }) }}>Add</Btn>
          </div>
        </div>
      )}

      {tab === 'history' && (
        <div>
          <h4 style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, margin: '4px 0 8px' }}>Education</h4>
          <DataTable headers={['Institution', 'Qualification', 'Field', 'Year']} empty="No education records."
            rows={e.education.map(x => [x.institution, x.qualification, x.fieldOfStudy ?? '—', x.yearCompleted ?? '—'])} />
          <h4 style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, margin: '14px 0 8px' }}>Prior employment</h4>
          <DataTable headers={['Employer', 'Job title', 'From', 'To', 'Reason for leaving']} empty="No prior employment recorded."
            rows={e.employmentHistory.map(x => [x.employerName, x.jobTitle ?? '—', fmtDate(x.startDate), fmtDate(x.endDate), x.reasonForLeaving ?? '—'])} />
        </div>
      )}

      {tab === 'bank' && (
        <div>
          <DataTable
            headers={['Bank', 'Branch', 'Account', 'Primary', 'Actions']}
            empty="No bank details — payroll cannot pay this employee."
            rows={e.bankDetails.map(b => [
              b.bankName, b.branchName ?? '—',
              <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{b.accountNumber}</span>,
              b.isPrimary ? <Badge variant="green">primary</Badge> : '—',
              <Btn size="sm" variant="danger" disabled={busy}
                onClick={() => run(() => hr.removeSubRecord('bank-detail', b.id), 'Bank detail removed.')}>Remove</Btn>,
            ])}
          />
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end', marginTop: 10 }}>
            <input placeholder="Bank" value={bank.bankName} onChange={ev => setBank(b => ({ ...b, bankName: ev.target.value }))}
              style={{ height: 36, width: 140, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <input placeholder="Branch" value={bank.branchName} onChange={ev => setBank(b => ({ ...b, branchName: ev.target.value }))}
              style={{ height: 36, width: 120, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <input placeholder="Account number" value={bank.accountNumber} onChange={ev => setBank(b => ({ ...b, accountNumber: ev.target.value }))}
              style={{ height: 36, flex: '1 1 160px', padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
            <label style={{ fontSize: 12, display: 'flex', alignItems: 'center', gap: 5 }}>
              <input type="checkbox" checked={bank.isPrimary} onChange={ev => setBank(b => ({ ...b, isPrimary: ev.target.checked }))} /> primary
            </label>
            <Btn size="sm" variant="outline" disabled={busy || !bank.bankName.trim() || !bank.accountNumber.trim()}
              onClick={() => { run(() => hr.addBankDetail(e.id, bank), 'Bank detail added.'); setBank({ bankName: '', branchName: '', accountNumber: '', isPrimary: false }) }}>Add</Btn>
          </div>
        </div>
      )}
    </Modal>
  )
}

// ── Positions ────────────────────────────────────────────────────────────────
export function PositionsTab({ flash }) {
  const [rows, setRows] = useState([])
  const [departments, setDepartments] = useState([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [f, setF] = useState({ title: '', jobGrade: '', departmentId: '', approvedHeadcount: '' })

  const load = useCallback(() => {
    setLoading(true)
    hr.listPositions(true).then(p => setRows(p ?? [])).catch(() => setRows([])).finally(() => setLoading(false))
  }, [])
  useEffect(() => { load(); hr.listDepartments().then(d => setDepartments(d ?? [])).catch(() => {}) }, [load])

  const add = async () => {
    if (!f.title.trim()) { flash('The position title is required.'); return }
    setBusy(true)
    try {
      const r = await hr.createPosition({
        title: f.title, jobGrade: f.jobGrade || undefined,
        departmentId: f.departmentId || undefined,
        approvedHeadcount: f.approvedHeadcount ? Number(f.approvedHeadcount) : undefined,
      })
      flash(r?.message ?? 'Position created.')
      setF({ title: '', jobGrade: '', departmentId: '', approvedHeadcount: '' })
      load()
    } catch (e) { flash(e.response?.data?.message ?? 'Could not create.') }
    finally { setBusy(false) }
  }

  return (
    <div>
      <SectionHeader
        title="Positions"
        sub="Job positions are HR's own — deliberately separate from the permission roles user-service manages. KPI scorecards attach to these in a later phase."
      />
      {loading ? <Loading /> : (
        <DataTable
          headers={['Title', 'Grade', 'Department', 'Headcount', 'Filled', 'Vacancies', 'Active']}
          empty="No positions defined yet."
          rows={rows.map(p => [
            p.title, p.jobGrade ?? '—', p.departmentName ?? '—',
            p.approvedHeadcount ?? '—', p.filledCount,
            p.vacancies == null ? '—' : <span style={{ color: p.vacancies > 0 ? T.amber : T.green }}>{p.vacancies}</span>,
            p.isActive ? <Badge variant="green">active</Badge> : <Badge variant="default">inactive</Badge>,
          ])}
        />
      )}
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end', marginTop: 12 }}>
        <input placeholder="Position title" value={f.title} onChange={e => setF(x => ({ ...x, title: e.target.value }))}
          style={{ height: 36, flex: '1 1 180px', padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
        <input placeholder="Grade" value={f.jobGrade} onChange={e => setF(x => ({ ...x, jobGrade: e.target.value }))}
          style={{ height: 36, width: 90, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
        <select value={f.departmentId} onChange={e => setF(x => ({ ...x, departmentId: e.target.value }))}
          style={{ height: 36, padding: '0 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }}>
          <option value="">Department…</option>
          {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
        <input placeholder="Headcount" type="number" value={f.approvedHeadcount}
          onChange={e => setF(x => ({ ...x, approvedHeadcount: e.target.value }))}
          style={{ height: 36, width: 110, padding: '4px 10px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 12 }} />
        <Btn size="sm" variant="outline" disabled={busy} onClick={add}>+ Add position</Btn>
      </div>
    </div>
  )
}

// ── Org chart ────────────────────────────────────────────────────────────────
export function OrgChartTab({ flash }) {
  const [chart, setChart] = useState(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)

  const load = useCallback(() => {
    setLoading(true)
    hr.getOrgChart().then(setChart).catch(() => setChart(null)).finally(() => setLoading(false))
  }, [])
  useEffect(() => { load() }, [load])

  const rebuild = async () => {
    setBusy(true)
    try { const r = await hr.rebuildOrgChart(); flash(r?.message ?? 'Rebuilt.'); load() }
    catch (e) { flash(e.response?.data?.message ?? 'Could not rebuild.') }
    finally { setBusy(false) }
  }

  const Node = ({ n }) => (
    <li style={{ margin: '4px 0' }}>
      <div style={{ display: 'inline-flex', alignItems: 'center', gap: 8, padding: '6px 10px', background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 8 }}>
        <span style={{ fontWeight: 700, fontSize: 13, color: T.navy }}>{n.fullName}</span>
        <span style={{ fontSize: 11, color: T.mgrey }}>{n.jobTitle ?? '—'}</span>
        {n.departmentName && <Badge variant="blue">{n.departmentName}</Badge>}
        {!n.isActive && <Badge variant="default">{n.status}</Badge>}
      </div>
      {n.reports?.length > 0 && (
        <ul style={{ listStyle: 'none', margin: '2px 0 2px 22px', paddingLeft: 14, borderLeft: `2px solid ${T.lgrey}` }}>
          {n.reports.map(r => <Node key={r.employeeId} n={r} />)}
        </ul>
      )}
    </li>
  )

  return (
    <div>
      <SectionHeader
        title="Organisational Chart"
        sub="Built from each employee's reporting line, with the MD at the apex. New hires and transfers update it automatically; rebuild re-derives every node."
        action={<Btn size="sm" variant="outline" disabled={busy} onClick={rebuild}>Rebuild</Btn>}
      />
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: 12, marginBottom: 16 }}>
        <Kpi label="People On Chart" value={chart?.nodeCount ?? 0} />
        <Kpi label="Depth" value={chart?.maxDepth ?? 0} />
        <Kpi label="Unplaced" value={chart?.unplacedEmployees ?? 0} color={(chart?.unplacedEmployees ?? 0) > 0 ? T.amber : T.green} />
        <Kpi label="Missing Reporting Line" value={chart?.missingReportingLine?.length ?? 0}
          color={(chart?.missingReportingLine?.length ?? 0) > 0 ? T.amber : T.green} />
      </div>

      {(chart?.missingReportingLine?.length ?? 0) > 0 && (
        <div style={{ padding: 10, background: T.amberL, borderRadius: 8, marginBottom: 12 }}>
          <p style={{ fontSize: 12, color: T.amber, margin: 0 }}>
            More than one apex — these have no manager set: <b>{chart.missingReportingLine.join(', ')}</b>
          </p>
        </div>
      )}

      {loading ? <Loading /> : (
        <Card style={{ padding: 16, overflowX: 'auto' }}>
          {(chart?.roots?.length ?? 0) === 0
            ? <p style={{ fontSize: 13, color: T.mgrey, margin: 0 }}>No employees on the chart yet.</p>
            : <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
                {chart.roots.map(r => <Node key={r.employeeId} n={r} />)}
              </ul>}
        </Card>
      )}
    </div>
  )
}
