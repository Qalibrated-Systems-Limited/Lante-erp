import { useState } from 'react'
import { useNavigate, useLocation, Link } from 'react-router-dom'
import { GoogleLogin } from '@react-oauth/google'
import api from '../api/axios.js'
import { useAuth } from '../context/AuthContext.jsx'
import { T } from '../theme/tokens.js'

export default function LoginPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const { login } = useAuth()
  const redirectTo = location.state?.from || '/dashboard'

  const [step, setStep] = useState('credentials')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [firstLoginUserId, setFirstLoginUserId] = useState('')
  // Short-lived, password-change-only token issued by login once the temporary password checked out. The
  // update-password endpoint requires it — the user id alone is no longer enough (#263).
  const [passwordChangeToken, setPasswordChangeToken] = useState('')
  const [code, setCode] = useState('')
  const [sessionToken, setSessionToken] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const [showNewPassword, setShowNewPassword] = useState(false)

  async function handleCredentials(e) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      const res = await api.post('/api/v1/auth/login', { email, password })
      const data = res.data?.data ?? res.data
      if (data.token) {
        login(data.token, { id: data.id, email: data.email, firstName: data.firstName, lastName: data.lastName, departmentId: data.departmentId ?? null, departmentIds: data.departmentIds ?? [], userRoles: data.userRoles, permissions: data.permissions ?? [], tenantId: data.tenantId ?? null, branchId: data.branchId ?? null, branchName: data.branchName ?? null, hqBranchId: data.hqBranchId ?? null, isCompanyAdmin: data.isCompanyAdmin ?? false })
        navigate(redirectTo)
      } else if (data.userId && !data.sessionId) {
        setFirstLoginUserId(data.userId)
        setPasswordChangeToken(data.passwordChangeToken ?? '')
        setStep('first-login')
      } else {
        setSessionToken(data.sessionId)
        setStep('2fa')
      }
    } catch (err) {
      setError(
          err.response?.data?.message ||
          err.response?.data?.errors?.[0] ||
          'Invalid email or password. Please try again.'
      )
    } finally {
      setLoading(false)
    }
  }

  async function handleGoogleSuccess(credentialResponse) {
    setError('')
    setLoading(true)
    try {
      const res = await api.post('/api/v1/auth/google-login', {
        idToken: credentialResponse.credential
      })
      const data = res.data?.data ?? res.data
      login(data.token, {
        id: data.id, email: data.email,
        firstName: data.firstName, lastName: data.lastName,
        departmentId: data.departmentId ?? null,
        departmentIds: data.departmentIds ?? [],
        userRoles: data.userRoles,
        permissions: data.permissions ?? [],
        tenantId: data.tenantId ?? null,
        branchId: data.branchId ?? null,
        branchName: data.branchName ?? null,
        hqBranchId: data.hqBranchId ?? null,
        isCompanyAdmin: data.isCompanyAdmin ?? false
      })
      navigate(redirectTo)
    } catch (err) {
      setError(err.response?.data?.message || 'Google sign-in failed. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  async function handle2FA(e) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      const res = await api.post('/api/v1/auth/verify-2fa', { sessionId: sessionToken, code })
      const data = res.data?.data ?? res.data
      login(data.token, { id: data.id, email: data.email, firstName: data.firstName, lastName: data.lastName, departmentId: data.departmentId ?? null, departmentIds: data.departmentIds ?? [], userRoles: data.userRoles, permissions: data.permissions ?? [], tenantId: data.tenantId ?? null, branchId: data.branchId ?? null, branchName: data.branchName ?? null, hqBranchId: data.hqBranchId ?? null, isCompanyAdmin: data.isCompanyAdmin ?? false })
      navigate(redirectTo)
    } catch (err) {
      setError(err.response?.data?.message || 'Invalid verification code. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  async function handleFirstLogin(e) {
    e.preventDefault()
    setError('')
    if (newPassword !== confirmPassword) { setError('Passwords do not match.'); return }
    setLoading(true)
    try {
      await api.put(`/api/v1/auth/update-password/${firstLoginUserId}`, {
        currentPassword: password, newPassword, confirmNewPassword: newPassword,
      }, { headers: { Authorization: `Bearer ${passwordChangeToken}` } })
      const res = await api.post('/api/v1/auth/login', { email, password: newPassword })
      const data = res.data?.data ?? res.data
      if (data.token) {
        login(data.token, { id: data.id, email: data.email, firstName: data.firstName, lastName: data.lastName, departmentId: data.departmentId ?? null, departmentIds: data.departmentIds ?? [], userRoles: data.userRoles, permissions: data.permissions ?? [], tenantId: data.tenantId ?? null, branchId: data.branchId ?? null, branchName: data.branchName ?? null, hqBranchId: data.hqBranchId ?? null, isCompanyAdmin: data.isCompanyAdmin ?? false })
        navigate(redirectTo)
      } else {
        setSessionToken(data.sessionId)
        setStep('2fa')
      }
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to set password. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  function handleBack() {
    setStep('credentials')
    setCode('')
    setError('')
    setSessionToken('')
  }

  const stepTitle = step === 'credentials' ? 'Sign in to ERP'
      : step === 'first-login' ? 'Set your password'
          : 'Verify your identity'

  const stepSubtitle = step === 'credentials' ? 'Use your Lante email and ERP password'
      : step === 'first-login' ? 'Choose a secure password to continue'
          : `Enter the 6-digit code sent to ${email}`

  const inputStyle = {
    width: '100%', padding: '11px 13px', border: `1.5px solid ${T.lgrey}`,
    borderRadius: 8, fontSize: 14, color: T.dgrey, outline: 'none', boxSizing: 'border-box',
    fontFamily: 'inherit', background: T.white,
  }
  const labelStyle = { display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 6 }

  return (
    <div style={{ minHeight: '100vh', background: T.navyD, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 20, fontFamily: "'Inter', sans-serif", position: 'relative', overflow: 'hidden' }}>
      {/* Subtle grid motif */}
      <div style={{
        position: 'absolute', inset: 0, pointerEvents: 'none',
        backgroundImage: 'linear-gradient(rgba(200,150,12,.05) 1px,transparent 1px),linear-gradient(90deg,rgba(200,150,12,.05) 1px,transparent 1px)',
        backgroundSize: '48px 48px',
      }} />
      <style>{`
        @keyframes spin { to { transform: rotate(360deg); } }
        .spin { animation: spin .7s linear infinite; }
        @keyframes fadeup { from{opacity:0;transform:translateY(12px)} to{opacity:1;transform:none} }
        .fadeup { animation: fadeup .5s ease both; }
      `}</style>

      <div className="fadeup" style={{ width: '100%', maxWidth: 420, position: 'relative', zIndex: 1 }}>
        {/* Wordmark */}
        <div style={{ textAlign: 'center', marginBottom: 36 }}>
          <div style={{ fontSize: 38, fontWeight: 800, color: T.gold, letterSpacing: -1 }}>Lante</div>
          <div style={{ fontSize: 12, color: 'rgba(255,255,255,.5)', letterSpacing: 2, marginTop: 4, textTransform: 'uppercase' }}>Enterprise Resource Planning</div>
        </div>

        {/* Card */}
        <div style={{ background: T.white, borderRadius: 16, padding: 36, boxShadow: '0 24px 64px rgba(0,0,0,.4)' }}>
          {(step === '2fa' || step === 'first-login') && (
            <button type="button" onClick={handleBack} style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, fontWeight: 600, color: T.mgrey, background: 'none', border: 'none', cursor: 'pointer', padding: 0, marginBottom: 14 }}
              onMouseEnter={e => (e.currentTarget.style.color = T.gold)}
              onMouseLeave={e => (e.currentTarget.style.color = T.mgrey)}>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" /></svg>
              Back
            </button>
          )}

          <h1 style={{ fontSize: 20, fontWeight: 700, color: T.navy, marginBottom: 6 }}>{stepTitle}</h1>
          <p style={{ fontSize: 13, color: T.mgrey, marginBottom: 26 }}>{stepSubtitle}</p>

          {error && (
            <div style={{ background: T.redL, border: `1px solid #FCA5A5`, color: T.red, padding: '10px 14px', borderRadius: 8, fontSize: 13, marginBottom: 20, display: 'flex', gap: 8, alignItems: 'flex-start' }}>
              <svg style={{ flexShrink: 0, marginTop: 1 }} width="14" height="14" viewBox="0 0 20 20" fill="currentColor"><path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" /></svg>
              {error}
            </div>
          )}

          {/* ── CREDENTIALS ── */}
          {step === 'credentials' && (
            <form onSubmit={handleCredentials}>
              <div style={{ marginBottom: 16 }}>
                <label style={labelStyle}>Email Address</label>
                <input type="email" required autoFocus autoComplete="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="you@organisation.co.ke" disabled={loading}
                  style={inputStyle} onFocus={e => e.target.style.borderColor = T.gold} onBlur={e => e.target.style.borderColor = T.lgrey} />
              </div>
              <div style={{ marginBottom: 24 }}>
                <label style={labelStyle}>Password</label>
                <div style={{ position: 'relative' }}>
                  <input type={showPassword ? 'text' : 'password'} required autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} placeholder="••••••••" disabled={loading}
                    style={{ ...inputStyle, paddingRight: 56 }} onFocus={e => e.target.style.borderColor = T.gold} onBlur={e => e.target.style.borderColor = T.lgrey} />
                  <button type="button" onClick={() => setShowPassword(s => !s)} style={{ position: 'absolute', right: 13, top: '50%', transform: 'translateY(-50%)', fontSize: 13, fontWeight: 600, color: T.mgrey, background: 'none', border: 'none', cursor: 'pointer' }}
                    onMouseEnter={e => e.currentTarget.style.color = T.gold} onMouseLeave={e => e.currentTarget.style.color = T.mgrey}>
                    {showPassword ? 'Hide' : 'Show'}
                  </button>
                </div>
              </div>
              <SubmitBtn loading={loading} label="Sign In" loadingLabel="Signing in…" />

              <div style={{ display: 'flex', alignItems: 'center', gap: 12, margin: '20px 0' }}>
                <div style={{ flex: 1, height: 1, background: T.lgrey }} />
                <span style={{ fontSize: 12, color: T.mgrey, fontWeight: 500 }}>or</span>
                <div style={{ flex: 1, height: 1, background: T.lgrey }} />
              </div>
              <div style={{ display: 'flex', justifyContent: 'center' }}>
                <GoogleLogin onSuccess={handleGoogleSuccess} onError={() => setError('Google sign-in was cancelled or failed.')} width="348" shape="rectangular" theme="outline" text="continue_with" logo_alignment="left" />
              </div>
            </form>
          )}

          {/* ── FIRST LOGIN ── */}
          {step === 'first-login' && (
            <form onSubmit={handleFirstLogin}>
              <div style={{ marginBottom: 16 }}>
                <label style={labelStyle}>New Password</label>
                <div style={{ position: 'relative' }}>
                  <input type={showNewPassword ? 'text' : 'password'} required autoFocus value={newPassword} onChange={e => setNewPassword(e.target.value)} placeholder="••••••••" disabled={loading}
                    style={{ ...inputStyle, paddingRight: 56 }} onFocus={e => e.target.style.borderColor = T.gold} onBlur={e => e.target.style.borderColor = T.lgrey} />
                  <button type="button" onClick={() => setShowNewPassword(s => !s)} style={{ position: 'absolute', right: 13, top: '50%', transform: 'translateY(-50%)', fontSize: 13, fontWeight: 600, color: T.mgrey, background: 'none', border: 'none', cursor: 'pointer' }}>
                    {showNewPassword ? 'Hide' : 'Show'}
                  </button>
                </div>
              </div>
              <div style={{ marginBottom: 24 }}>
                <label style={labelStyle}>Confirm Password</label>
                <input type="password" required value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} placeholder="••••••••" disabled={loading}
                  style={inputStyle} onFocus={e => e.target.style.borderColor = T.gold} onBlur={e => e.target.style.borderColor = T.lgrey} />
              </div>
              <SubmitBtn loading={loading} label="Set Password & Continue" loadingLabel="Setting password…" />
            </form>
          )}

          {/* ── 2FA ── */}
          {step === '2fa' && (
            <form onSubmit={handle2FA}>
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, marginBottom: 24 }}>
                <div style={{ width: 52, height: 52, borderRadius: '50%', background: 'rgba(200,150,12,.1)', border: `1px solid rgba(200,150,12,.25)`, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <svg width="22" height="22" fill="none" viewBox="0 0 24 24" stroke={T.gold}><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" /></svg>
                </div>
              </div>
              <div style={{ marginBottom: 24 }}>
                <label style={{ ...labelStyle, textAlign: 'center' }}>6-digit code</label>
                <input type="text" inputMode="numeric" pattern="[0-9]{6}" maxLength={6} required autoFocus value={code} onChange={e => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))} placeholder="000000" disabled={loading}
                  style={{ ...inputStyle, fontSize: 22, fontFamily: "'JetBrains Mono', monospace", letterSpacing: '.5em', textAlign: 'center', padding: '13px' }} onFocus={e => e.target.style.borderColor = T.gold} onBlur={e => e.target.style.borderColor = T.lgrey} />
              </div>
              <SubmitBtn loading={loading} disabled={code.length < 6} label="Verify & Sign In" loadingLabel="Verifying…" />
            </form>
          )}
        </div>

        <div style={{ textAlign: 'center', marginTop: 20, fontSize: 11, color: 'rgba(255,255,255,.3)' }}>
          Secure access · Contact your administrator for account issues
        </div>
        <div style={{ textAlign: 'center', marginTop: 10 }}>
          <Link to="/" style={{ fontSize: 12, color: 'rgba(255,255,255,.45)', textDecoration: 'none' }}
            onMouseEnter={e => e.currentTarget.style.color = T.gold}
            onMouseLeave={e => e.currentTarget.style.color = 'rgba(255,255,255,.45)'}>
            ← Back to home
          </Link>
        </div>
      </div>
    </div>
  )
}

function SubmitBtn({ loading, disabled = false, label, loadingLabel }) {
  return (
    <button type="submit" disabled={loading || disabled} style={{
      width: '100%', padding: 12, background: (loading || disabled) ? T.mgrey : T.navy, color: T.white,
      border: 'none', borderRadius: 8, fontSize: 14, fontWeight: 700,
      cursor: (loading || disabled) ? 'not-allowed' : 'pointer',
      display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
    }}>
      {loading ? (
        <>
          <svg className="spin" width="16" height="16" viewBox="0 0 24 24" fill="none">
            <circle cx="12" cy="12" r="10" stroke="rgba(255,255,255,.3)" strokeWidth="3" />
            <path d="M12 2a10 10 0 0 1 10 10" stroke="#fff" strokeWidth="3" strokeLinecap="round" />
          </svg>
          {loadingLabel}
        </>
      ) : `${label} →`}
    </button>
  )
}
