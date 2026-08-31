import { NavLink, useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext.jsx'
import { T } from '../../theme/tokens.js'

const NAV = [
  {
    label: 'Overview',
    items: [
      { label: 'Dashboard',  to: '/platform/dashboard', icon: GridIcon },
    ],
  },
  {
    label: 'Management',
    items: [
      { label: 'Companies',  to: '/platform/companies', icon: BuildingIcon },
      { label: 'Plans',      to: '/platform/plans',     icon: CreditCardIcon },
    ],
  },
  {
    label: 'Communications',
    items: [
      { label: 'Broadcast',  to: '/platform/broadcast', icon: MegaphoneIcon },
    ],
  },
  {
    label: 'System',
    items: [
      { label: 'Backups',    to: '/platform/backups',   icon: DatabaseIcon },
    ],
  },
]

export default function PlatformLayout({ children }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  const initials = user
    ? `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase()
    : 'PA'

  function handleLogout() {
    logout()
    navigate('/platform/login')
  }

  return (
    <div style={{ display: 'flex', minHeight: '100vh', background: T.navyD, fontFamily: "'Inter','Helvetica Neue',sans-serif" }}>
      <style>{`
        .pnav-link {
          display:flex; align-items:center; gap:10px;
          padding:9px 12px; border-radius:10px;
          font-size:14px; font-weight:500; color:#6b7280;
          text-decoration:none; transition:color .15s,background .15s;
        }
        .pnav-link:hover { color:#f9fafb; background:rgba(255,255,255,.05); }
        .pnav-link.active { color:${T.goldL}; background:rgba(200,150,12,.1); font-weight:600; }
        .pnav-link.active svg { color:${T.goldL}; }
      `}</style>

      {/* ── Sidebar ── */}
      <aside style={{
        width: 240, flexShrink: 0,
        background: T.navyD,
        borderRight: '1px solid rgba(255,255,255,.06)',
        display: 'flex', flexDirection: 'column',
        padding: '20px 12px',
        position: 'sticky', top: 0, height: '100vh', overflowY: 'auto',
      }}>
        {/* Logo */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '4px 8px', marginBottom: 28 }}>
          <div style={{
            width: 32, height: 32, borderRadius: 9,
            background: `linear-gradient(135deg,${T.gold},${T.goldL})`,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 15, fontWeight: 800, color: T.navyD,
            flexShrink: 0,
          }}>L</div>
          <div>
            <div style={{ fontSize: 14, fontWeight: 700, color: '#f9fafb', lineHeight: 1.2 }}>
              Lante
            </div>
            <div style={{ fontSize: 10, fontWeight: 700, color: T.gold, letterSpacing: '1.5px', textTransform: 'uppercase' }}>
              Platform
            </div>
          </div>
        </div>

        {/* Nav groups */}
        <nav style={{ flex: 1 }}>
          {NAV.map(group => (
            <div key={group.label} style={{ marginBottom: 24 }}>
              <p style={{ fontSize: 10, fontWeight: 700, color: '#374151', letterSpacing: '1.5px', textTransform: 'uppercase', padding: '0 12px', marginBottom: 6 }}>
                {group.label}
              </p>
              {group.items.map(item => (
                <NavLink key={item.to} to={item.to} className="pnav-link">
                  <item.icon size={16} />
                  {item.label}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>

        {/* User footer */}
        <div style={{
          borderTop: '1px solid rgba(255,255,255,.06)',
          paddingTop: 16, display: 'flex', flexDirection: 'column', gap: 8,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '0 8px' }}>
            <div style={{
              width: 32, height: 32, borderRadius: '50%',
              background: `linear-gradient(135deg,${T.gold},${T.goldL})`,
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontSize: 12, fontWeight: 700, color: T.navyD, flexShrink: 0,
            }}>{initials}</div>
            <div style={{ overflow: 'hidden' }}>
              <p style={{ fontSize: 13, fontWeight: 600, color: '#f9fafb', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                {user?.firstName} {user?.lastName}
              </p>
              <p style={{ fontSize: 11, color: '#6b7280', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                {user?.email}
              </p>
            </div>
          </div>
          <button onClick={handleLogout} style={{
            display: 'flex', alignItems: 'center', gap: 8,
            padding: '9px 12px', borderRadius: 10,
            fontSize: 14, fontWeight: 500, color: '#6b7280',
            background: 'none', border: 'none', cursor: 'pointer', width: '100%', textAlign: 'left',
            fontFamily: "'Inter',sans-serif",
            transition: 'color .15s,background .15s',
          }}
          onMouseEnter={e => { e.currentTarget.style.color = '#ef4444'; e.currentTarget.style.background = 'rgba(239,68,68,.07)' }}
          onMouseLeave={e => { e.currentTarget.style.color = '#6b7280'; e.currentTarget.style.background = 'none' }}>
            <SignOutIcon size={16} />
            Sign out
          </button>
        </div>
      </aside>

      {/* ── Main content ── */}
      <main style={{ flex: 1, overflowY: 'auto', minWidth: 0, background: T.offwt }}>
        {/* Top bar */}
        <div style={{
          height: 56, borderBottom: `1px solid ${T.lgrey}`,
          display: 'flex', alignItems: 'center', padding: '0 28px',
          background: 'rgba(255,255,255,.85)', backdropFilter: 'blur(10px)',
          position: 'sticky', top: 0, zIndex: 10,
        }}>
          <div style={{
            display: 'inline-flex', alignItems: 'center', gap: 6,
            padding: '3px 10px', borderRadius: 100,
            background: '#DCE8F5', border: '1px solid rgba(27,58,92,.12)',
            fontSize: 11, fontWeight: 700, color: T.navy, letterSpacing: '1.5px', textTransform: 'uppercase',
          }}>
            <span style={{ width: 5, height: 5, borderRadius: '50%', background: '#34d399', display: 'inline-block' }}/>
            Platform Admin
          </div>
        </div>

        <div style={{ padding: '28px' }}>
          {children}
        </div>
      </main>
    </div>
  )
}

function GridIcon({ size = 16 }) {
  return <svg width={size} height={size} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></svg>
}
function BuildingIcon({ size = 16 }) {
  return <svg width={size} height={size} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0H5m14 0h2M5 21H3M9 7h1m-1 4h1m4-4h1m-1 4h1M9 21v-4a1 1 0 011-1h4a1 1 0 011 1v4"/></svg>
}
function CreditCardIcon({ size = 16 }) {
  return <svg width={size} height={size} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><rect x="1" y="4" width="22" height="16" rx="2"/><path strokeLinecap="round" d="M1 10h22"/></svg>
}
function MegaphoneIcon({ size = 16 }) {
  return <svg width={size} height={size} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M11 5.882V19.24a1.76 1.76 0 01-3.417.592l-2.147-6.15M18 13a3 3 0 100-6M5.436 13.683A4.001 4.001 0 017 6h1.832c4.1 0 7.625-1.234 9.168-3v14c-1.543-1.766-5.067-3-9.168-3H7a3.988 3.988 0 01-1.564-.317z"/></svg>
}
function DatabaseIcon({ size = 16 }) {
  return <svg width={size} height={size} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><ellipse cx="12" cy="5" rx="8" ry="3"/><path strokeLinecap="round" d="M4 5v14c0 1.657 3.582 3 8 3s8-1.343 8-3V5"/><path strokeLinecap="round" d="M4 12c0 1.657 3.582 3 8 3s8-1.343 8-3"/></svg>
}
function SignOutIcon({ size = 16 }) {
  return <svg width={size} height={size} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"/></svg>
}
