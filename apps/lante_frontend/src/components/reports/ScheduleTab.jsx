import { useState, useEffect, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Badge, SectionHeader, DataTable, Modal, Input, Select, Loading, Alert } from '../ui.jsx'
import {
  getReportDefinitions, runReportNow,
  getReportSchedules, createReportSchedule, deleteReportSchedule,
  getReportRecipients, addReportRecipient, removeReportRecipient,
  getReportRuns,
} from '../../services/reports.js'

const RUN_STATUSES = ['Pending', 'Success', 'Failed']
const RUN_STATUS_VARIANT = { Pending: 'amber', Success: 'green', Failed: 'red' }
const EMPTY_SCHEDULE = { reportDefinitionId: '', cronExpression: '0 8 * * *', format: 0 }
const EMPTY_RECIPIENT = { email: '' }

export default function ScheduleTab() {
  const [definitions, setDefinitions] = useState([])
  const [schedules, setSchedules] = useState([])
  const [runs, setRuns] = useState([])
  const [loading, setLoading] = useState(true)
  const [msg, setMsg] = useState(null)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [scheduleForm, setScheduleForm] = useState(EMPTY_SCHEDULE)
  const [recipientForm, setRecipientForm] = useState(EMPTY_RECIPIENT)
  const [activeSchedule, setActiveSchedule] = useState(null)
  const [recipients, setRecipients] = useState([])
  const [runningId, setRunningId] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [d, s, r] = await Promise.all([
        getReportDefinitions().catch(() => []),
        getReportSchedules().catch(() => []),
        getReportRuns({ limit: 50 }).catch(() => []),
      ])
      setDefinitions(d ?? [])
      setSchedules(s ?? [])
      setRuns(r ?? [])
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  const definitionName = id => definitions.find(d => d.id === id)?.name ?? id

  async function handleRunNow(definitionId) {
    setRunningId(definitionId)
    try {
      await runReportNow(definitionId)
      setMsg({ type: 'success', text: 'Report generated — see Run History below.' })
      load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to generate report.' })
    } finally {
      setRunningId(null)
    }
  }

  async function submitSchedule() {
    if (!scheduleForm.reportDefinitionId || !scheduleForm.cronExpression) return
    setSaving(true)
    try {
      await createReportSchedule(scheduleForm)
      setMsg({ type: 'success', text: 'Schedule created.' })
      setScheduleForm(EMPTY_SCHEDULE); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to create schedule.' })
    } finally { setSaving(false) }
  }

  async function removeSchedule(id) {
    try {
      await deleteReportSchedule(id)
      load()
    } catch {
      setMsg({ type: 'error', text: 'Failed to delete schedule.' })
    }
  }

  async function openRecipients(schedule) {
    setActiveSchedule(schedule)
    setModal('recipients')
    try {
      setRecipients(await getReportRecipients(schedule.id))
    } catch {
      setRecipients([])
    }
  }

  async function submitRecipient() {
    if (!recipientForm.email || !activeSchedule) return
    setSaving(true)
    try {
      await addReportRecipient(activeSchedule.id, recipientForm)
      setRecipientForm(EMPTY_RECIPIENT)
      setRecipients(await getReportRecipients(activeSchedule.id))
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to add recipient.' })
    } finally { setSaving(false) }
  }

  async function removeRecipient(recipientId) {
    if (!activeSchedule) return
    try {
      await removeReportRecipient(activeSchedule.id, recipientId)
      setRecipients(await getReportRecipients(activeSchedule.id))
    } catch {
      setMsg({ type: 'error', text: 'Failed to remove recipient.' })
    }
  }

  if (loading) return <Loading />

  return (
    <>
      {msg && <Alert type={msg.type}>{msg.text}</Alert>}

      <SectionHeader title="Report Catalog" sub="Generate any report on demand — schedule one below for recurring delivery" />
      <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 22 }}>
        <DataTable
          headers={['Report', 'Category', 'Actions']}
          empty="No report definitions found."
          rows={definitions.map(d => [
            <strong style={{ fontSize: 12.5, color: T.dgrey }}>{d.name}</strong>,
            <Badge variant="navy">{d.category}</Badge>,
            <Btn size="sm" onClick={() => handleRunNow(d.id)} disabled={runningId === d.id}>{runningId === d.id ? 'Running…' : 'Run Now'}</Btn>,
          ])}
        />
      </Card>

      <SectionHeader title="Scheduled Deliveries" sub="Recurring report generation + email delivery — Excel only for now"
        action={<Btn onClick={() => setModal('schedule')}>+ New Schedule</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 22 }}>
        <DataTable
          headers={['Report', 'Cron', 'Next Run', 'Last Run', 'Status', 'Actions']}
          empty="No scheduled deliveries yet."
          rows={schedules.map(s => [
            <strong style={{ fontSize: 12.5, color: T.dgrey }}>{s.reportDefinitionName ?? definitionName(s.reportDefinitionId)}</strong>,
            <code style={{ fontSize: 12 }}>{s.cronExpression}</code>,
            s.nextRunAt ? fmt.date(s.nextRunAt) : '—',
            s.lastRunAt ? fmt.date(s.lastRunAt) : 'Never',
            s.isActive ? <Badge variant="green">Active</Badge> : <Badge variant="default">Paused</Badge>,
            <div style={{ display: 'flex', gap: 6 }}>
              <Btn size="sm" variant="ghost" onClick={() => openRecipients(s)}>Recipients</Btn>
              <Btn size="sm" variant="ghost" onClick={() => removeSchedule(s.id)}>Delete</Btn>
            </div>,
          ])}
        />
      </Card>

      <SectionHeader title="Run History" sub="Most recent 50 generations, scheduled and manual" />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Report', 'Generated', 'Triggered By', 'Status', 'File']}
          empty="No reports generated yet."
          rows={runs.map(r => [
            <strong style={{ fontSize: 12.5, color: T.dgrey }}>{r.reportDefinitionName ?? definitionName(r.reportDefinitionId)}</strong>,
            fmt.date(r.generatedAt),
            r.triggeredBy,
            <Badge variant={RUN_STATUS_VARIANT[RUN_STATUSES[r.status]] ?? 'default'}>{RUN_STATUSES[r.status] ?? r.status}</Badge>,
            r.fileUrl ? <a href={r.fileUrl} target="_blank" rel="noreferrer" style={{ color: T.blue }}>Download</a> : (r.errorMessage ?? '—'),
          ])}
        />
      </Card>

      {modal === 'schedule' && (
        <Modal title="New Scheduled Delivery" onClose={() => setModal(null)}>
          <Select label="Report" value={scheduleForm.reportDefinitionId} onChange={v => setScheduleForm({ ...scheduleForm, reportDefinitionId: v })}
            options={[{ value: '', label: '— Select —' }, ...definitions.map(d => ({ value: d.id, label: d.name }))]} />
          <Input label="Cron Expression" value={scheduleForm.cronExpression} onChange={v => setScheduleForm({ ...scheduleForm, cronExpression: v })}
            required placeholder="0 8 * * * (daily at 8am UTC)" note="Standard 5-field cron: minute hour day month weekday" />
          <Select label="Format" value={String(scheduleForm.format)} onChange={v => setScheduleForm({ ...scheduleForm, format: Number(v) })}
            options={[{ value: '0', label: 'Excel' }]} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitSchedule} disabled={saving || !scheduleForm.reportDefinitionId || !scheduleForm.cronExpression}>{saving ? 'Saving…' : 'Create Schedule'}</Btn>
          </div>
        </Modal>
      )}

      {modal === 'recipients' && activeSchedule && (
        <Modal title={`Recipients — ${activeSchedule.reportDefinitionName ?? definitionName(activeSchedule.reportDefinitionId)}`} onClose={() => setModal(null)}>
          <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 16 }}>
            <DataTable
              headers={['Email', 'Actions']}
              empty="No recipients yet."
              rows={recipients.map(r => [
                r.email,
                <Btn size="sm" variant="ghost" onClick={() => removeRecipient(r.id)}>Remove</Btn>,
              ])}
            />
          </Card>
          <Input label="Email" value={recipientForm.email} onChange={v => setRecipientForm({ ...recipientForm, email: v })} required placeholder="name@qalibrated.co.ke" />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Close</Btn>
            <Btn onClick={submitRecipient} disabled={saving || !recipientForm.email}>{saving ? 'Adding…' : 'Add Recipient'}</Btn>
          </div>
        </Modal>
      )}
    </>
  )
}
