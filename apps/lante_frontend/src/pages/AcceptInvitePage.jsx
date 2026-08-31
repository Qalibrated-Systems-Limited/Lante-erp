import { useState, useEffect } from 'react'
import { useNavigate, useSearchParams, Link } from 'react-router-dom'
import api from '../api/axios.js'
import { T } from '../theme/tokens.js'

// Accept-invite page for the single-login model. Opened from the emailed invite link
// (lante.africa/accept-invite?token=…). Validates the token, then lets the user set
// their own password. On success they're sent to /login.
export default function AcceptInvitePage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const token = params.get('token') || ''

  const [phase, setPhase] = useState('loading') // loading | ready | invalid | done
  const [info, setInfo] = useState(null)
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [showPw, setShowPw] = useState(false)
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    if (!token) { setPhase('invalid'); return }
    api.get(`/api/v1/auth/invite/${encodeURIComponent(token)}`)
      .then(res => { setInfo(res.data?.data ?? res.data); setPhase('ready') })
      .catch(() => setPhase('invalid'))
  }, [token])

  async function submit(e) {
    e.preventDefault()
    setError('')
    if (newPassword !== confirmPassword) { setError('Passwords do not match.'); return }
    setSubmitting(true)
    try {
      await api.post('/api/v1/auth/accept-invite', { token, newPassword, confirmPassword })
      setPhase('done')
    } catch (err) {
      setError(err.response?.data?.message || err.response?.data?.errors?.[0] || 'Could not set your password. The invite may have expired.')
    } finally {
      setSubmitting(false)
    }
  }

  const inputStyle = {
    width: '100%', padding: '11px 13px', border: `1.5px solid ${T.lgrey}`,
    borderRadius: 8, fontSize: 14, color: T.dgrey, outline: 'none', boxSizing: 'border-box',
    fontFamily: 'inherit', background: T.white,
  }
  const labelStyle = { display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 6 }

  return (
    <div style={{ minHeight: '100vh', background: T.navyD, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 20, fontFamily: "'Inter', sans-serif", position: 'relative', overflow: 'hidden' }}>
      <div style={{ position: 'absolute', inset: 0, pointerEvents: 'none', backgroundImage: 'linear-gradient(rgba(200,150,12,.05) 1px,transparent 1px),linear-gradient(90deg,rgba(200,150,12,.05) 1px,transparent 1px)', backgroundSize: '48px 48px' }} />
      <style>{`@keyframes spin{to{transform:rotate(360deg)}} .spin{animation:spin .7s linear infinite}`}</style>

      <div style={{ width: '100%', maxWidth: 420, position: 'relative', zIndex: 1 }}>
        <div style={{ textAlign: 'center', marginBottom: 32 }}>
          <div style={{ fontSize: 34, fontWeight: 800, color: T.gold, letterSpacing: -1 }}>Lante</div>
          <div style={{ fontSize: 12, color: 'rgba(255,255,255,.5)', letterSpacing: 2, marginTop: 4, textTransform: 'uppercase' }}>Enterprise Resource Planning</div>
        </div>

        <div style={{ background: T.white, borderRadius: 16, padding: 36, boxShadow: '0 24px 64px rgba(0,0,0,.4)' }}>
          {phase === 'loading' && (
            <div style={{ textAlign: 'center', padding: '20px 0', color: T.mgrey, fontSize: 14 }}>
              <div className="spin" style={{ width: 24, height: 24, border: `3px solid ${T.lgrey}`, borderTopColor: T.navy, borderRadius: '50%', margin: '0 auto 14px' }} />
              Checking your invite…
            </div>
          )}

          {phase === 'invalid' && (
            <div style={{ textAlign: 'center' }}>
              <div style={{ fontSize: 34, marginBottom: 10 }}>⌛</div>
              <h1 style={{ fontSize: 18, fontWeight: 700, color: T.navy, margin: '0 0 8px' }}>Invite link invalid or expired</h1>
              <p style={{ fontSize: 13, color: T.mgrey, margin: '0 0 18px' }}>Please ask your administrator to resend your invitation.</p>
              <Link to="/login" style={{ fontSize: 13, fontWeight: 700, color: T.navy }}>Go to sign in →</Link>
            </div>
          )}

          {phase === 'done' && (
            <div style={{ textAlign: 'center' }}>
              <div style={{ fontSize: 34, marginBottom: 10 }}>✅</div>
              <h1 style={{ fontSize: 18, fontWeight: 700, color: T.navy, margin: '0 0 8px' }}>You're all set</h1>
              <p style={{ fontSize: 13, color: T.mgrey, margin: '0 0 18px' }}>Your password has been set. You can now sign in.</p>
              <button onClick={() => navigate('/login')} style={{ padding: '11px 22px', background: T.navy, color: '#fff', border: 'none', borderRadius: 8, fontSize: 14, fontWeight: 700, cursor: 'pointer' }}>Sign in →</button>
            </div>
          )}

          {phase === 'ready' && (
            <>
              <h1 style={{ fontSize: 20, fontWeight: 700, color: T.navy, marginBottom: 6 }}>Set your password</h1>
              <p style={{ fontSize: 13, color: T.mgrey, marginBottom: 4 }}>
                Welcome{info?.firstName ? `, ${info.firstName}` : ''}{info?.companyName ? ` — ${info.companyName}` : ''}.
              </p>
              {info?.email && <p style={{ fontSize: 12, color: T.mgrey, marginBottom: 22 }}>Signing in as <strong style={{ color: T.dgrey }}>{info.email}</strong></p>}

              {error && (
                <div style={{ background: T.redL, border: '1px solid #FCA5A5', color: T.red, padding: '10px 14px', borderRadius: 8, fontSize: 13, marginBottom: 18 }}>{error}</div>
              )}

              <form onSubmit={submit}>
                <div style={{ marginBottom: 16 }}>
                  <label style={labelStyle}>New Password</label>
                  <div style={{ position: 'relative' }}>
                    <input type={showPw ? 'text' : 'password'} required autoFocus value={newPassword} onChange={e => setNewPassword(e.target.value)} placeholder="••••••••" disabled={submitting}
                      style={{ ...inputStyle, paddingRight: 56 }} onFocus={e => e.target.style.borderColor = T.gold} onBlur={e => e.target.style.borderColor = T.lgrey} />
                    <button type="button" onClick={() => setShowPw(s => !s)} style={{ position: 'absolute', right: 13, top: '50%', transform: 'translateY(-50%)', fontSize: 13, fontWeight: 600, color: T.mgrey, background: 'none', border: 'none', cursor: 'pointer' }}>{showPw ? 'Hide' : 'Show'}</button>
                  </div>
                </div>
                <div style={{ marginBottom: 24 }}>
                  <label style={labelStyle}>Confirm Password</label>
                  <input type="password" required value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} placeholder="••••••••" disabled={submitting}
                    style={inputStyle} onFocus={e => e.target.style.borderColor = T.gold} onBlur={e => e.target.style.borderColor = T.lgrey} />
                </div>
                <button type="submit" disabled={submitting || !newPassword} style={{ width: '100%', padding: 12, background: (submitting || !newPassword) ? T.mgrey : T.navy, color: '#fff', border: 'none', borderRadius: 8, fontSize: 14, fontWeight: 700, cursor: (submitting || !newPassword) ? 'not-allowed' : 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8 }}>
                  {submitting ? (<><span className="spin" style={{ width: 15, height: 15, border: '2px solid rgba(255,255,255,.4)', borderTopColor: '#fff', borderRadius: '50%' }} />Setting password…</>) : 'Set password & continue →'}
                </button>
              </form>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
