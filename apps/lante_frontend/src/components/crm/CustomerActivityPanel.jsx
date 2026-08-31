import { useState, useEffect, useCallback } from 'react'
import { Btn, Modal, Badge } from '../ui.jsx'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

const fmtDate = (d) => d ? new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '—'
const INTERACTION_TYPES = ['Call', 'Email', 'Meeting', 'SiteVisit', 'Proposal', 'Other']

// C7 — interactions timeline + follow-up tasks for a single customer (used on the Customer detail page).
export default function CustomerActivityPanel({ customerId }) {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission?.('crm.write')
  const [interactions, setInteractions] = useState([])
  const [tasks, setTasks] = useState([])
  const [modal, setModal] = useState(null)   // 'interaction' | 'task'
  const [it, setIt] = useState({ interactionType: 'Call', subject: '', description: '', outcome: '', nextActionDate: '' })
  const [task, setTask] = useState({ subject: '', taskType: 'FollowUp', dueDate: '' })
  const [toast, setToast] = useState('')
  const [busy, setBusy] = useState(false)

  const flash = (m) => { setToast(m); setTimeout(() => setToast(''), 3000) }
  const load = useCallback(async () => {
    try {
      const [ints, tks] = await Promise.all([crm.getInteractions(customerId), crm.listTasks({ customerId })])
      setInteractions(ints ?? []); setTasks(tks.data ?? [])
    } catch { /* silent */ }
  }, [customerId])
  useEffect(() => { load() }, [load])

  const saveInteraction = async () => {
    if (!it.subject.trim()) return flash('Subject required.')
    setBusy(true)
    try { await crm.logInteraction(customerId, { ...it, nextActionDate: it.nextActionDate || undefined }); setModal(null); setIt({ interactionType: 'Call', subject: '', description: '', outcome: '', nextActionDate: '' }); await load(); flash('Interaction logged.') }
    catch (e) { flash(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  const saveTask = async () => {
    if (!task.subject.trim() || !task.dueDate) return flash('Subject and due date required.')
    setBusy(true)
    try { await crm.createTask({ ...task, customerId }); setModal(null); setTask({ subject: '', taskType: 'FollowUp', dueDate: '' }); await load(); flash('Task created.') }
    catch (e) { flash(e.response?.data?.message ?? 'Failed.') } finally { setBusy(false) }
  }
  const doComplete = async (id) => { try { await crm.completeTask(id); await load() } catch { flash('Failed.') } }

  return (
    <>
      {toast && <div className="fixed bottom-6 right-6 z-[1100] bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg">{toast}</div>}

      <div className="grid md:grid-cols-2 gap-6 mt-6">
        {/* Interactions */}
        <section className="bg-white border border-gray-200 rounded-xl p-5">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-sm font-bold text-gray-700">Interactions</h2>
            {canWrite && <button onClick={() => setModal('interaction')} className="text-sm font-semibold text-navy hover:underline">+ Log</button>}
          </div>
          {interactions.length === 0 ? <p className="text-sm text-gray-400">No interactions logged.</p> : (
            <div className="space-y-2 max-h-72 overflow-y-auto">
              {interactions.map(i => (
                <div key={i.id} className="border border-gray-100 rounded-lg px-3 py-2">
                  <p className="text-sm font-semibold text-navy">{i.interactionType} · {i.subject}</p>
                  {i.description && <p className="text-xs text-gray-500 mt-0.5">{i.description}</p>}
                  {i.outcome && <p className="text-xs text-gray-600 mt-0.5">Outcome: {i.outcome}</p>}
                  <p className="text-[11px] text-gray-400 mt-0.5">{fmtDate(i.interactionDate)} · {i.performedBy}{i.nextActionDate ? ` · next ${fmtDate(i.nextActionDate)}` : ''}</p>
                </div>
              ))}
            </div>
          )}
        </section>

        {/* Tasks */}
        <section className="bg-white border border-gray-200 rounded-xl p-5">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-sm font-bold text-gray-700">Follow-up Tasks</h2>
            {canWrite && <button onClick={() => setModal('task')} className="text-sm font-semibold text-navy hover:underline">+ Add</button>}
          </div>
          {tasks.length === 0 ? <p className="text-sm text-gray-400">No tasks.</p> : (
            <div className="space-y-2 max-h-72 overflow-y-auto">
              {tasks.map(t => (
                <div key={t.id} className="flex items-center justify-between border border-gray-100 rounded-lg px-3 py-2">
                  <div>
                    <p className="text-sm font-semibold text-navy">{t.subject}{t.isAutoCreated && <span className="ml-1 text-[10px] text-amber-600">auto</span>}</p>
                    <p className="text-[11px] text-gray-400">{t.taskType} · due {fmtDate(t.dueDate)}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    {t.status === 'Open' ? (t.isOverdue ? <Badge variant="red">Overdue</Badge> : <Badge variant="blue">Open</Badge>) : <Badge variant={t.status === 'Done' ? 'green' : 'default'}>{t.status}</Badge>}
                    {canWrite && t.status === 'Open' && <button onClick={() => doComplete(t.id)} className="text-xs text-green-600 hover:underline">Done</button>}
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>

      {modal === 'interaction' && (
        <Modal title="Log Interaction" onClose={() => setModal(null)} width={440}>
          <Fld label="Type"><select value={it.interactionType} onChange={e => setIt(s => ({ ...s, interactionType: e.target.value }))} className="input">{INTERACTION_TYPES.map(t => <option key={t}>{t}</option>)}</select></Fld>
          <Fld label="Subject *"><input value={it.subject} onChange={e => setIt(s => ({ ...s, subject: e.target.value }))} className="input" /></Fld>
          <Fld label="Notes"><textarea rows={2} value={it.description} onChange={e => setIt(s => ({ ...s, description: e.target.value }))} className="input" /></Fld>
          <Fld label="Outcome"><input value={it.outcome} onChange={e => setIt(s => ({ ...s, outcome: e.target.value }))} className="input" /></Fld>
          <Fld label="Next action date"><input type="date" value={it.nextActionDate} onChange={e => setIt(s => ({ ...s, nextActionDate: e.target.value }))} className="input" /></Fld>
          <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn><Btn onClick={saveInteraction} disabled={busy}>Log</Btn></div>
        </Modal>
      )}
      {modal === 'task' && (
        <Modal title="New Task" onClose={() => setModal(null)} width={420}>
          <Fld label="Subject *"><input value={task.subject} onChange={e => setTask(s => ({ ...s, subject: e.target.value }))} className="input" /></Fld>
          <Fld label="Type"><select value={task.taskType} onChange={e => setTask(s => ({ ...s, taskType: e.target.value }))} className="input">{['FollowUp','Call','Visit','Email','Other'].map(t => <option key={t}>{t}</option>)}</select></Fld>
          <Fld label="Due date *"><input type="date" value={task.dueDate} onChange={e => setTask(s => ({ ...s, dueDate: e.target.value }))} className="input" /></Fld>
          <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn><Btn onClick={saveTask} disabled={busy}>Create</Btn></div>
        </Modal>
      )}
    </>
  )
}

function Fld({ label, children }) {
  return <label className="block mb-3"><span className="block text-xs font-medium text-gray-500 mb-1">{label}</span>{children}</label>
}
