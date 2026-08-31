import { useEffect, useState } from 'react'
import api from '../../api/axios.js'
import PlatformLayout from './PlatformLayout.jsx'

export default function PlatformBroadcastPage() {
  const [subject, setSubject] = useState('')
  const [body, setBody] = useState('')
  const [sending, setSending] = useState(false)
  const [result, setResult] = useState(null)
  const [error, setError] = useState('')

  const [audience, setAudience] = useState('all')     // 'all' | 'admins'
  const [admins, setAdmins] = useState([])
  const [adminsLoading, setAdminsLoading] = useState(false)
  const [selectedAdminIds, setSelectedAdminIds] = useState([])
  const [adminSearch, setAdminSearch] = useState('')

  const [history, setHistory] = useState([])
  const [historyLoading, setHistoryLoading] = useState(true)
  const [selected, setSelected] = useState(null)      // broadcast detail (with recipients)
  const [selectedLoading, setSelectedLoading] = useState(false)
  const [recipientFilter, setRecipientFilter] = useState('all') // 'all' | 'read' | 'unread' | 'not_delivered'

  useEffect(() => { loadHistory() }, [])

  useEffect(() => {
    if (audience === 'admins' && admins.length === 0) loadAdmins()
  }, [audience])

  async function loadAdmins() {
    setAdminsLoading(true)
    try {
      const res = await api.get('/api/v1/platform/admins')
      setAdmins(res.data?.data ?? [])
    } catch {
      setAdmins([])
    } finally {
      setAdminsLoading(false)
    }
  }

  function toggleAdmin(id) {
    setSelectedAdminIds(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id])
  }

  const filteredAdmins = admins.filter(a => {
    if (!adminSearch.trim()) return true
    const q = adminSearch.toLowerCase()
    return `${a.firstName} ${a.lastName} ${a.email} ${a.tenantName}`.toLowerCase().includes(q)
  })

  const sendDisabled = sending || !subject.trim() || !body.trim() || (audience === 'admins' && selectedAdminIds.length === 0)

  async function loadHistory() {
    setHistoryLoading(true)
    try {
      const res = await api.get('/api/v1/platform/broadcasts')
      setHistory(res.data?.data ?? [])
    } catch {
      // non-critical — history just won't show
    } finally {
      setHistoryLoading(false)
    }
  }

  async function openBroadcast(id) {
    setSelected(null)
    setRecipientFilter('all')
    setSelectedLoading(true)
    try {
      const res = await api.get(`/api/v1/platform/broadcasts/${id}`)
      setSelected(res.data?.data ?? null)
    } catch {
      setSelected(null)
    } finally {
      setSelectedLoading(false)
    }
  }

  async function handleSend(e) {
    e.preventDefault()
    if (!subject.trim() || !body.trim()) return
    if (audience === 'admins' && selectedAdminIds.length === 0) {
      setError('Select at least one admin to send to.')
      return
    }
    setError('')
    setResult(null)
    setSending(true)
    try {
      const payload = { subject, body }
      if (audience === 'admins') payload.userIds = selectedAdminIds
      const res = await api.post('/api/v1/platform/broadcast', payload)
      const data = res.data?.data ?? res.data
      setResult(data)
      setSubject('')
      setBody('')
      setSelectedAdminIds([])
      loadHistory()
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to send broadcast.')
    } finally {
      setSending(false)
    }
  }

  return (
    <PlatformLayout>
      <div style={{
        display: 'grid', gridTemplateColumns: 'minmax(0, 680px) minmax(300px, 380px)',
        gap: 32, alignItems: 'start', maxWidth: 1120,
      }}>
      <div>
        <h1 style={{ fontFamily: "'Inter',sans-serif", fontSize: 26, fontWeight: 700, color: '#1B3A5C', marginBottom: 6 }}>
          Broadcast Message
        </h1>
        <p style={{ fontSize: 14, color: '#6b7280', marginBottom: 32 }}>
          Send an email and in-app notification to all company administrators.
        </p>

        {result !== null && (
          <div style={{ marginBottom: 24 }}>
            <div style={{
              display: 'flex', alignItems: 'center', gap: 12,
              background: 'rgba(52,211,153,.07)', border: '1px solid rgba(52,211,153,.2)',
              color: '#34d399', borderRadius: 12, padding: '14px 18px',
              fontSize: 14, fontWeight: 600,
            }}>
              <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
              </svg>
              Email sent to {result.sent}/{result.total} admins · In-app notification created for {result.notified}/{result.total}. ✓
            </div>
            {result.notifyErrors?.length > 0 && (
              <div style={{
                marginTop: 10, background: 'rgba(239,68,68,.08)', border: '1px solid rgba(239,68,68,.2)',
                color: '#f87171', borderRadius: 10, padding: '12px 14px', fontSize: 13,
              }}>
                <strong>In-app notification failed for {result.notifyErrors.length} recipient(s):</strong>
                <ul style={{ margin: '6px 0 0', paddingLeft: 18 }}>
                  {result.notifyErrors.map((e, i) => <li key={i}>{e}</li>)}
                </ul>
              </div>
            )}
          </div>
        )}

        {error && (
          <div style={{
            background: 'rgba(239,68,68,.08)', border: '1px solid rgba(239,68,68,.2)',
            color: '#f87171', borderRadius: 10, padding: '12px 14px',
            fontSize: 14, marginBottom: 20,
          }}>
            {error}
          </div>
        )}

        <form onSubmit={handleSend}>
          <div style={{
            background: '#FFFFFF', border: '1px solid #E8ECF0',
            borderRadius: 16, padding: '24px', display: 'flex', flexDirection: 'column', gap: 18,
          }}>
            {/* Audience selector */}
            <div>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#9ca3af', marginBottom: 8, letterSpacing: '.3px' }}>
                AUDIENCE
              </label>
              <div style={{ display: 'flex', gap: 8 }}>
                {[['all', 'All Company Admins'], ['admins', 'Specific Admins']].map(([id, label]) => (
                  <button
                    key={id} type="button"
                    onClick={() => setAudience(id)}
                    style={{
                      padding: '7px 16px', borderRadius: 100, cursor: 'pointer',
                      fontSize: 12, fontWeight: 700, letterSpacing: '.5px', fontFamily: "'Inter',sans-serif",
                      border: audience === id ? '1px solid rgba(200,150,12,.35)' : '1px solid #E8ECF0',
                      background: audience === id ? 'rgba(200,150,12,.07)' : '#FFFFFF',
                      color: audience === id ? '#C8960C' : '#6b7280',
                    }}
                  >
                    {label}
                  </button>
                ))}
              </div>
            </div>

            {audience === 'admins' && (
              <div>
                <input
                  value={adminSearch} onChange={e => setAdminSearch(e.target.value)}
                  placeholder="Search by name, email, or company…"
                  style={{
                    width: '100%', height: 40, padding: '0 12px', boxSizing: 'border-box',
                    fontSize: 13, fontFamily: "'Inter',sans-serif",
                    border: '1.5px solid #E8ECF0', borderRadius: 10,
                    background: '#FFFFFF', color: '#1B3A5C', outline: 'none', marginBottom: 10,
                  }}
                />
                <div style={{ maxHeight: 220, overflowY: 'auto', border: '1px solid #E8ECF0', borderRadius: 10 }}>
                  {adminsLoading ? (
                    <p style={{ fontSize: 13, color: '#6b7280', padding: 14 }}>Loading admins…</p>
                  ) : filteredAdmins.length === 0 ? (
                    <p style={{ fontSize: 13, color: '#6b7280', padding: 14 }}>No admins match.</p>
                  ) : (
                    filteredAdmins.map((a, i) => {
                      const checked = selectedAdminIds.includes(a.id)
                      return (
                        <label key={a.id} style={{
                          display: 'flex', alignItems: 'center', gap: 10, padding: '9px 14px', cursor: 'pointer',
                          borderTop: i > 0 ? '1px solid #E8ECF0' : 'none',
                          background: checked ? '#F0F4F8' : 'none',
                        }}>
                          <input type="checkbox" checked={checked} onChange={() => toggleAdmin(a.id)} />
                          <div style={{ minWidth: 0 }}>
                            <div style={{ fontSize: 13, fontWeight: 600, color: '#1B3A5C' }}>{a.firstName} {a.lastName}</div>
                            <div style={{ fontSize: 11, color: '#9ca3af' }}>{a.email} · {a.tenantName}</div>
                          </div>
                        </label>
                      )
                    })
                  )}
                </div>
                <p style={{ fontSize: 12, color: '#6b7280', marginTop: 8 }}>
                  {selectedAdminIds.length} admin{selectedAdminIds.length === 1 ? '' : 's'} selected
                </p>
              </div>
            )}

            <div>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#9ca3af', marginBottom: 8, letterSpacing: '.3px' }}>
                SUBJECT
              </label>
              <input
                value={subject} onChange={e => setSubject(e.target.value)}
                placeholder="Platform maintenance scheduled for…"
                required
                style={{
                  width: '100%', height: 44, padding: '0 14px', boxSizing: 'border-box',
                  fontSize: 14, fontFamily: "'Inter',sans-serif",
                  border: '1.5px solid #E8ECF0', borderRadius: 10,
                  background: '#FFFFFF', color: '#1B3A5C', outline: 'none',
                }}
              />
            </div>

            <div>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#9ca3af', marginBottom: 8, letterSpacing: '.3px' }}>
                MESSAGE
              </label>
              <textarea
                value={body} onChange={e => setBody(e.target.value)}
                placeholder="Write your message here…"
                required rows={10}
                style={{
                  width: '100%', padding: '12px 14px', boxSizing: 'border-box',
                  fontSize: 14, fontFamily: "'Inter',sans-serif", lineHeight: 1.7,
                  border: '1.5px solid #E8ECF0', borderRadius: 10,
                  background: '#FFFFFF', color: '#1B3A5C', outline: 'none',
                  resize: 'vertical', minHeight: 180,
                }}
              />
              <p style={{ fontSize: 12, color: '#4b5563', marginTop: 6 }}>
                {body.length} characters · supports plain text
              </p>
            </div>

            {/* Preview box */}
            {(subject || body) && (
              <div style={{
                background: '#FFFFFF', border: '1px solid #E8ECF0',
                borderRadius: 10, padding: '16px 18px',
              }}>
                <p style={{ fontSize: 11, fontWeight: 700, color: '#4b5563', textTransform: 'uppercase', letterSpacing: '1px', marginBottom: 12 }}>Preview</p>
                <p style={{ fontSize: 13, fontWeight: 700, color: '#1B3A5C', marginBottom: 8 }}>{subject || '(no subject)'}</p>
                <p style={{ fontSize: 13, color: '#9ca3af', whiteSpace: 'pre-wrap', lineHeight: 1.7 }}>{body || '(no body)'}</p>
              </div>
            )}

            <div style={{ display: 'flex', justifyContent: 'flex-end', paddingTop: 4 }}>
              <button type="submit" disabled={sendDisabled} style={{
                padding: '11px 28px', borderRadius: 10,
                background: 'linear-gradient(135deg,#C8960C,#E8B84D)',
                color: '#000', fontSize: 14, fontWeight: 700,
                border: 'none', cursor: sendDisabled ? 'not-allowed' : 'pointer',
                fontFamily: "'Inter',sans-serif",
                opacity: sendDisabled ? .55 : 1,
                boxShadow: '0 4px 14px rgba(200,150,12,.25)',
                display: 'flex', alignItems: 'center', gap: 8,
              }}>
                {sending ? (
                  <>
                    <svg style={{ animation: 'spin .7s linear infinite' }} width="14" height="14" viewBox="0 0 24 24" fill="none">
                      <circle cx="12" cy="12" r="10" stroke="rgba(0,0,0,.3)" strokeWidth="3"/>
                      <path d="M12 2a10 10 0 0 1 10 10" stroke="#000" strokeWidth="3" strokeLinecap="round"/>
                    </svg>
                    Sending…
                  </>
                ) : (
                  <>
                    <svg width="15" height="15" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8"/>
                    </svg>
                    Send Broadcast
                  </>
                )}
              </button>
            </div>
          </div>
        </form>

        <div style={{
          marginTop: 20, padding: '14px 18px', borderRadius: 10,
          background: 'rgba(200,150,12,.05)', border: '1px solid rgba(200,150,12,.12)',
          fontSize: 13, color: '#92400e',
        }}>
          <strong style={{ color: '#d97706' }}>Note:</strong> This message will be sent as both an email and an in-app notification to {audience === 'admins' ? 'the selected admins' : 'all active company administrators'}. Use this for platform-wide announcements only.
        </div>
      </div>

      {/* ── Recent Broadcasts (sidebar) ── */}
      <div>
          <h2 style={{ fontSize: 16, fontWeight: 700, color: '#1B3A5C', marginBottom: 14 }}>Recent Broadcasts</h2>

          {historyLoading ? (
            <p style={{ fontSize: 13, color: '#6b7280' }}>Loading…</p>
          ) : history.length === 0 ? (
            <p style={{ fontSize: 13, color: '#6b7280' }}>No broadcasts sent yet.</p>
          ) : (
            <div style={{ background: '#FFFFFF', border: '1px solid #E8ECF0', borderRadius: 12, overflow: 'hidden' }}>
              {history.map((b, i) => (
                <button
                  key={b.id}
                  onClick={() => openBroadcast(b.id)}
                  style={{
                    width: '100%', textAlign: 'left', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12,
                    padding: '14px 18px', background: selected?.id === b.id ? '#F0F4F8' : 'none', border: 'none',
                    borderTop: i > 0 ? '1px solid #E8ECF0' : 'none', cursor: 'pointer', fontFamily: "'Inter',sans-serif",
                  }}
                >
                  <div style={{ minWidth: 0 }}>
                    <div style={{ fontSize: 14, fontWeight: 600, color: '#1B3A5C', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{b.subject}</div>
                    <div style={{ fontSize: 12, color: '#9ca3af', marginTop: 2 }}>
                      {b.sentByName} · {new Date(b.sentAt).toLocaleString()}
                    </div>
                  </div>
                  <div style={{ fontSize: 12, color: '#6b7280', flexShrink: 0 }}>
                    {b.emailsSent}/{b.totalRecipients} emailed · {b.notificationsSent}/{b.totalRecipients} notified
                  </div>
                </button>
              ))}
            </div>
          )}

          {/* Detail: per-recipient read status */}
          {(selectedLoading || selected) && (
            <div style={{ marginTop: 16, background: '#FFFFFF', border: '1px solid #E8ECF0', borderRadius: 12, padding: '18px 20px' }}>
              {selectedLoading ? (
                <p style={{ fontSize: 13, color: '#6b7280' }}>Loading recipients…</p>
              ) : (
                <>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 14 }}>
                    <div style={{ fontSize: 14, fontWeight: 700, color: '#1B3A5C' }}>{selected.subject}</div>
                    <button onClick={() => setSelected(null)} style={{ background: 'none', border: 'none', color: '#9ca3af', cursor: 'pointer', fontSize: 13 }}>Close</button>
                  </div>

                  {(() => {
                    const readCount = selected.recipients.filter(r => r.isRead).length
                    const unreadCount = selected.recipients.filter(r => !r.isRead && r.notificationSent).length
                    const notDeliveredCount = selected.recipients.filter(r => !r.notificationSent).length
                    const TABS = [
                      ['all', `All (${selected.recipients.length})`],
                      ['read', `Read (${readCount})`],
                      ['unread', `Unread (${unreadCount})`],
                      ['not_delivered', `Not delivered (${notDeliveredCount})`],
                    ]
                    const visible = selected.recipients.filter(r => {
                      if (recipientFilter === 'read') return r.isRead
                      if (recipientFilter === 'unread') return !r.isRead && r.notificationSent
                      if (recipientFilter === 'not_delivered') return !r.notificationSent
                      return true
                    })
                    return (
                      <>
                        <div style={{ display: 'flex', gap: 6, marginBottom: 14, flexWrap: 'wrap' }}>
                          {TABS.map(([id, label]) => (
                            <button
                              key={id} type="button"
                              onClick={() => setRecipientFilter(id)}
                              style={{
                                padding: '5px 12px', borderRadius: 100, cursor: 'pointer',
                                fontSize: 11, fontWeight: 700, fontFamily: "'Inter',sans-serif",
                                border: recipientFilter === id ? '1px solid rgba(200,150,12,.35)' : '1px solid #E8ECF0',
                                background: recipientFilter === id ? 'rgba(200,150,12,.07)' : '#FFFFFF',
                                color: recipientFilter === id ? '#C8960C' : '#6b7280',
                              }}
                            >
                              {label}
                            </button>
                          ))}
                        </div>

                        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                          {visible.length === 0 ? (
                            <p style={{ fontSize: 13, color: '#6b7280' }}>No recipients in this view.</p>
                          ) : visible.map((r, i) => (
                            <div key={i} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, padding: '10px 12px', background: '#F0F4F8', borderRadius: 8 }}>
                              <div style={{ minWidth: 0 }}>
                                <div style={{ fontSize: 13, fontWeight: 600, color: '#1B3A5C' }}>{r.userName || r.email}</div>
                                <div style={{ fontSize: 12, color: '#9ca3af' }}>{r.tenantName} · {r.email}</div>
                              </div>
                              <span style={{
                                fontSize: 11, fontWeight: 700, padding: '3px 9px', borderRadius: 100, flexShrink: 0,
                                background: r.isRead ? 'rgba(52,211,153,.1)' : r.notificationSent ? 'rgba(200,150,12,.1)' : 'rgba(239,68,68,.08)',
                                color: r.isRead ? '#34d399' : r.notificationSent ? '#C8960C' : '#f87171',
                              }}>
                                {r.isRead ? 'Read' : r.notificationSent ? 'Unread' : 'Not delivered'}
                              </span>
                            </div>
                          ))}
                        </div>
                      </>
                    )
                  })()}
                </>
              )}
            </div>
          )}
        </div>
      </div>
      <style>{`@keyframes spin{to{transform:rotate(360deg)}}`}</style>
    </PlatformLayout>
  )
}
