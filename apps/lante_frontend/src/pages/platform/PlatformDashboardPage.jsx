import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import PlatformLayout from './PlatformLayout.jsx'

export default function PlatformDashboardPage() {
  const navigate = useNavigate()
  const [stats, setStats] = useState(null)
  const [companies, setCompanies] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    Promise.all([
      api.get('/api/v1/platform/dashboard'),
      api.get('/api/v1/platform/companies'),
    ]).then(([statsRes, companiesRes]) => {
      setStats(statsRes.data?.data ?? statsRes.data)
      setCompanies((companiesRes.data?.data ?? companiesRes.data).slice(0, 5))
    }).catch(console.error).finally(() => setLoading(false))
  }, [])

  const statCards = stats ? [
    { label: 'Total Companies',       value: stats.totalTenants,        color: '#C8960C' },
    { label: 'Total Branches',        value: stats.totalBranches,       color: '#60a5fa' },
    { label: 'Total Users',           value: stats.totalUsers,          color: '#34d399' },
    { label: 'Active Subscriptions',  value: stats.activeSubscriptions, color: '#f472b6' },
  ] : []

  return (
    <PlatformLayout>
      <div style={{ maxWidth: 1100 }}>
        <div style={{ marginBottom: 32 }}>
          <h1 style={{ fontFamily: "'Inter',sans-serif", fontSize: 26, fontWeight: 700, color: '#1B3A5C', marginBottom: 6 }}>
            Platform Overview
          </h1>
          <p style={{ fontSize: 14, color: '#6b7280' }}>Monitor all tenants, subscriptions and platform health.</p>
        </div>

        {loading ? (
          <div style={{ display: 'flex', gap: 16, marginBottom: 32 }}>
            {[1,2,3,4].map(i => (
              <div key={i} style={{ flex: 1, height: 100, borderRadius: 14, background: '#FFFFFF', border: '1px solid #E8ECF0', animation: 'pulse 1.5s ease-in-out infinite' }}/>
            ))}
          </div>
        ) : (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(200px,1fr))', gap: 16, marginBottom: 32 }}>
            {statCards.map(card => (
              <div key={card.label} style={{
                background: '#FFFFFF', border: '1px solid #E8ECF0',
                borderRadius: 14, padding: '20px 22px',
              }}>
                <p style={{ fontSize: 12, fontWeight: 700, color: '#4b5563', textTransform: 'uppercase', letterSpacing: '1px', marginBottom: 10 }}>
                  {card.label}
                </p>
                <p style={{ fontFamily: "'Inter',sans-serif", fontSize: 32, fontWeight: 700, color: card.color, lineHeight: 1 }}>
                  {card.value ?? '—'}
                </p>
              </div>
            ))}
          </div>
        )}

        {/* Recent companies */}
        <div style={{
          background: '#FFFFFF', border: '1px solid #E8ECF0',
          borderRadius: 16, overflow: 'hidden',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '18px 22px', borderBottom: '1px solid #E8ECF0' }}>
            <h2 style={{ fontSize: 15, fontWeight: 700, color: '#1B3A5C' }}>Recent Companies</h2>
            <button onClick={() => navigate('/platform/companies')} style={{
              fontSize: 13, fontWeight: 600, color: '#C8960C', background: 'none', border: 'none', cursor: 'pointer',
              fontFamily: "'Inter',sans-serif",
            }}>View all →</button>
          </div>
          {loading ? (
            <div style={{ padding: '24px 22px', color: '#6b7280', fontSize: 14 }}>Loading…</div>
          ) : companies.length === 0 ? (
            <div style={{ padding: '40px 22px', textAlign: 'center', color: '#4b5563', fontSize: 14 }}>
              No companies yet.{' '}
              <button onClick={() => navigate('/platform/companies/new')} style={{ color: '#C8960C', background: 'none', border: 'none', cursor: 'pointer', fontSize: 14, fontWeight: 600, fontFamily: "'Inter',sans-serif" }}>
                Create one →
              </button>
            </div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr>
                  {['Company', 'Slug', 'Branches', 'Users', 'Plan', 'Status'].map(h => (
                    <th key={h} style={{ padding: '10px 22px', textAlign: 'left', fontSize: 11, fontWeight: 700, color: '#4b5563', textTransform: 'uppercase', letterSpacing: '1px' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {companies.map((c, i) => (
                  <tr key={c.id}
                    onClick={() => navigate(`/platform/companies/${c.id}`)}
                    style={{ borderTop: i > 0 ? '1px solid #FFFFFF' : 'none', cursor: 'pointer', transition: 'background .15s' }}
                    onMouseEnter={e => (e.currentTarget.style.background = '#FFFFFF')}
                    onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}>
                    <td style={{ padding: '14px 22px', fontSize: 14, fontWeight: 600, color: '#1B3A5C' }}>{c.name}</td>
                    <td style={{ padding: '14px 22px', fontSize: 13, color: '#6b7280', fontFamily: 'monospace' }}>{c.slug}</td>
                    <td style={{ padding: '14px 22px', fontSize: 14, color: '#9ca3af' }}>{c.branchCount}</td>
                    <td style={{ padding: '14px 22px', fontSize: 14, color: '#9ca3af' }}>{c.userCount}</td>
                    <td style={{ padding: '14px 22px', fontSize: 13, color: '#9ca3af' }}>{c.subscription?.name ?? '—'}</td>
                    <td style={{ padding: '14px 22px' }}>
                      <StatusBadge status={c.subscription?.status} active={c.isActive} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </PlatformLayout>
  )
}

function StatusBadge({ status, active }) {
  if (!active) return <span style={{ fontSize: 11, fontWeight: 700, padding: '3px 9px', borderRadius: 100, background: 'rgba(239,68,68,.1)', color: '#f87171' }}>Suspended</span>
  const map = { Active: ['rgba(52,211,153,.1)', '#34d399'], Trial: ['rgba(200,150,12,.1)', '#C8960C'], Suspended: ['rgba(239,68,68,.1)', '#f87171'], Cancelled: ['rgba(107,114,128,.1)', '#9ca3af'] }
  const [bg, color] = map[status] ?? ['rgba(107,114,128,.1)', '#9ca3af']
  return <span style={{ fontSize: 11, fontWeight: 700, padding: '3px 9px', borderRadius: 100, background: bg, color }}>{status ?? 'No plan'}</span>
}
