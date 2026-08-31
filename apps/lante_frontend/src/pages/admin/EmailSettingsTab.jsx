import { useState, useEffect } from 'react'
import api from '../../api/axios.js'
import { Card, Btn, Alert, Loading, SectionHeader } from '../../components/ui.jsx'
import { T } from '../../theme/tokens.js'

// Per-tenant custom SMTP — separate endpoint from the generic system-settings registry (that one
// is readable by every authenticated user; a mail server password has no business in that payload).
// The password is never round-tripped: the backend only ever tells us whether one is set
// (hasPassword), and the field here starts empty — leaving it empty on save keeps whatever's
// already stored, typing a new value replaces it.
const emptyForm = {
  isEnabled: false, smtpHost: '', smtpPort: 587, smtpUsername: '', smtpPassword: '',
  fromEmail: '', fromName: '',
}

export default function EmailSettingsTab() {
  const [form, setForm] = useState(emptyForm)
  const [hasPassword, setHasPassword] = useState(false)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState(null)
  const [testEmail, setTestEmail] = useState('')
  const [testing, setTesting] = useState(false)
  const [testMsg, setTestMsg] = useState(null)

  useEffect(() => { load() }, [])

  async function load() {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/system-settings/email')
      const d = res.data?.data
      if (d) {
        setForm({
          isEnabled: d.isEnabled, smtpHost: d.smtpHost, smtpPort: d.smtpPort,
          smtpUsername: d.smtpUsername, smtpPassword: '', fromEmail: d.fromEmail, fromName: d.fromName,
        })
        setHasPassword(d.hasPassword)
      }
    } catch {
      setMsg({ type: 'error', text: 'Failed to load email settings.' })
    } finally {
      setLoading(false)
    }
  }

  const set = (key, value) => setForm(f => ({ ...f, [key]: value }))

  async function save() {
    setSaving(true)
    setMsg(null)
    try {
      const res = await api.put('/api/v1/system-settings/email', {
        ...form,
        smtpPort: Number(form.smtpPort) || 587,
        smtpPassword: form.smtpPassword || null, // empty = keep existing
      })
      setHasPassword(res.data?.data?.hasPassword ?? hasPassword)
      setForm(f => ({ ...f, smtpPassword: '' }))
      setMsg({ type: 'success', text: 'Email settings saved.' })
    } catch (err) {
      setMsg({ type: 'error', text: err.response?.data?.message ?? 'Failed to save email settings.' })
    } finally {
      setSaving(false)
    }
  }

  async function sendTest() {
    if (!testEmail.trim()) { setTestMsg({ type: 'error', text: 'Enter an email address to send the test to.' }); return }
    setTesting(true)
    setTestMsg(null)
    try {
      const res = await api.post('/api/v1/system-settings/email/test', { toEmail: testEmail.trim() })
      setTestMsg({ type: 'success', text: res.data?.message ?? 'Test email sent.' })
    } catch (err) {
      setTestMsg({ type: 'error', text: err.response?.data?.message ?? 'Test email failed to send.' })
    } finally {
      setTesting(false)
    }
  }

  if (loading) return <Loading />

  const inputStyle = { width: '100%', padding: '8px 10px', border: `1px solid ${T.lgrey}`, borderRadius: 6, fontSize: 13 }
  const labelStyle = { display: 'block', fontSize: 11, fontWeight: 600, color: T.dgrey, marginBottom: 4 }

  return (
    <>
      <Alert type="info">
        Route your outbound email (invites, notifications, portal confirmations) through your own mail
        server instead of Lante's shared one. Leave this off — or leave the host blank — to keep using
        the platform default.
      </Alert>
      {msg && <Alert type={msg.type}>{msg.text}</Alert>}

      <Card style={{ marginBottom: 14 }}>
        <SectionHeader title="Custom SMTP" sub="Only takes effect once enabled and a host is set." action={
          <Btn size="sm" disabled={saving} onClick={save}>{saving ? 'Saving…' : 'Save Changes'}</Btn>
        } />

        <label style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 14, cursor: 'pointer' }}>
          <input type="checkbox" checked={form.isEnabled} onChange={e => set('isEnabled', e.target.checked)} />
          <span style={{ fontSize: 13, fontWeight: 600, color: T.dgrey }}>Use our own SMTP server</span>
        </label>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px 18px' }}>
          <div>
            <label style={labelStyle}>SMTP Host</label>
            <input style={inputStyle} value={form.smtpHost} onChange={e => set('smtpHost', e.target.value)} placeholder="smtp.yourdomain.com" />
          </div>
          <div>
            <label style={labelStyle}>SMTP Port</label>
            <input type="number" style={inputStyle} value={form.smtpPort} onChange={e => set('smtpPort', e.target.value)} placeholder="587" />
          </div>
          <div>
            <label style={labelStyle}>Username</label>
            <input style={inputStyle} value={form.smtpUsername} onChange={e => set('smtpUsername', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Password{hasPassword && <span style={{ color: T.mgrey, fontWeight: 400 }}> (set — leave blank to keep)</span>}</label>
            <input type="password" style={inputStyle} value={form.smtpPassword} onChange={e => set('smtpPassword', e.target.value)}
              placeholder={hasPassword ? '••••••••' : ''} />
          </div>
          <div>
            <label style={labelStyle}>From Email</label>
            <input style={inputStyle} value={form.fromEmail} onChange={e => set('fromEmail', e.target.value)} placeholder="noreply@yourdomain.com" />
          </div>
          <div>
            <label style={labelStyle}>From Name</label>
            <input style={inputStyle} value={form.fromName} onChange={e => set('fromName', e.target.value)} placeholder="Your Company" />
          </div>
        </div>
      </Card>

      <Card>
        <SectionHeader title="Send a test email" sub="Confirms the saved settings actually work before you rely on them." />
        {testMsg && <Alert type={testMsg.type}>{testMsg.text}</Alert>}
        <div style={{ display: 'flex', gap: 10, alignItems: 'flex-end' }}>
          <div style={{ flex: 1 }}>
            <label style={labelStyle}>Send test to</label>
            <input style={inputStyle} value={testEmail} onChange={e => setTestEmail(e.target.value)} placeholder="you@yourdomain.com" />
          </div>
          <Btn size="sm" disabled={testing} onClick={sendTest}>{testing ? 'Sending…' : 'Send Test Email'}</Btn>
        </div>
      </Card>
    </>
  )
}
