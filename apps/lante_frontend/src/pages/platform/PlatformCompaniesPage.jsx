import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import PlatformLayout from './PlatformLayout.jsx'

export default function PlatformCompaniesPage() {
  const navigate = useNavigate()
  const [companies, setCompanies] = useState([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')

  useEffect(() => {
    api.get('/api/v1/platform/companies')
      .then(r => setCompanies(r.data?.data ?? r.data))
      .catch(console.error)
      .finally(() => setLoading(false))
  }, [])

  const filtered = companies.filter(c =>
    c.name.toLowerCase().includes(search.toLowerCase()) ||
    c.slug.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <PlatformLayout>
      <div style={{ maxWidth: 1100 }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: 28, flexWrap: 'wrap', gap: 16 }}>
          <div>
            <h1 style={{ fontFamily: "'Inter',sans-serif", fontSize: 26, fontWeight: 700, color: '#1B3A5C', marginBottom: 6 }}>Companies</h1>
            <p style={{ fontSize: 14, color: '#6b7280' }}>{companies.length} registered {companies.length === 1 ? 'company' : 'companies'}</p>
          </div>
          <button onClick={() => navigate('/platform/companies/new')} style={{
            display: 'flex', alignItems: 'center', gap: 8,
            padding: '10px 20px', borderRadius: 10,
            background: 'linear-gradient(135deg,#C8960C,#E8B84D)',
            color: '#000', fontSize: 14, fontWeight: 700,
            border: 'none', cursor: 'pointer',
            fontFamily: "'Inter',sans-serif",
            boxShadow: '0 4px 14px rgba(200,150,12,.25)',
            transition: 'opacity .2s',
          }}
          onMouseEnter={e => (e.currentTarget.style.opacity = '.88')}
          onMouseLeave={e => (e.currentTarget.style.opacity = '1')}>
            + Add Company
          </button>
        </div>

        {/* Search */}
        <div style={{ marginBottom: 20 }}>
          <input
            type="text" placeholder="Search companies…" value={search}
            onChange={e => setSearch(e.target.value)}
            style={{
              width: '100%', maxWidth: 360, height: 40, padding: '0 14px',
              fontSize: 14, fontFamily: "'Inter',sans-serif",
              border: '1.5px solid #E8ECF0', borderRadius: 10,
              background: '#FFFFFF', color: '#1B3A5C', outline: 'none',
            }}
          />
        </div>

        <div style={{
          background: '#FFFFFF', border: '1px solid #E8ECF0',
          borderRadius: 16, overflow: 'hidden',
        }}>
          {loading ? (
            <div style={{ padding: '40px', textAlign: 'center', color: '#6b7280', fontSize: 14 }}>Loading companies…</div>
          ) : filtered.length === 0 ? (
            <div style={{ padding: '60px', textAlign: 'center' }}>
              <p style={{ fontSize: 15, fontWeight: 600, color: '#4b5563', marginBottom: 8 }}>No companies found</p>
              <button onClick={() => navigate('/platform/companies/new')} style={{
                fontSize: 14, fontWeight: 600, color: '#C8960C', background: 'none', border: 'none', cursor: 'pointer', fontFamily: "'Inter',sans-serif",
              }}>Create your first company →</button>
            </div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ borderBottom: '1px solid #E8ECF0' }}>
                  {['Company', 'Slug', 'Branches', 'Users', 'Plan', 'Subscription', 'Status', ''].map(h => (
                    <th key={h} style={{ padding: '12px 20px', textAlign: 'left', fontSize: 11, fontWeight: 700, color: '#4b5563', textTransform: 'uppercase', letterSpacing: '1px', whiteSpace: 'nowrap' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filtered.map((c, i) => (
                  <tr key={c.id} style={{ borderTop: i > 0 ? '1px solid #FFFFFF' : 'none', cursor: 'pointer', transition: 'background .15s' }}
                    onMouseEnter={e => (e.currentTarget.style.background = '#FFFFFF')}
                    onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}>
                    <td style={{ padding: '15px 20px', fontSize: 14, fontWeight: 600, color: '#1B3A5C' }}>{c.name}</td>
                    <td style={{ padding: '15px 20px', fontSize: 13, color: '#6b7280', fontFamily: 'monospace' }}>{c.slug}</td>
                    <td style={{ padding: '15px 20px', fontSize: 14, color: '#9ca3af', textAlign: 'center' }}>{c.branchCount}</td>
                    <td style={{ padding: '15px 20px', fontSize: 14, color: '#9ca3af', textAlign: 'center' }}>{c.userCount}</td>
                    <td style={{ padding: '15px 20px', fontSize: 13, color: '#9ca3af' }}>{c.subscription?.name ?? <span style={{ color: '#4b5563' }}>—</span>}</td>
                    <td style={{ padding: '15px 20px' }}><SubBadge status={c.subscription?.status} /></td>
                    <td style={{ padding: '15px 20px' }}><ActiveBadge active={c.isActive} /></td>
                    <td style={{ padding: '15px 20px' }}>
                      <button onClick={() => navigate(`/platform/companies/${c.id}`)} style={{
                        fontSize: 13, fontWeight: 600, color: '#C8960C', background: 'none', border: 'none', cursor: 'pointer', fontFamily: "'Inter',sans-serif",
                      }}>View →</button>
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

function SubBadge({ status }) {
  const map = { Active: ['rgba(52,211,153,.1)', '#34d399'], Trial: ['rgba(200,150,12,.1)', '#C8960C'], Suspended: ['rgba(239,68,68,.1)', '#f87171'], Cancelled: ['rgba(107,114,128,.1)', '#9ca3af'] }
  const [bg, color] = map[status] ?? ['rgba(107,114,128,.1)', '#9ca3af']
  return <span style={{ fontSize: 11, fontWeight: 700, padding: '3px 9px', borderRadius: 100, background: bg, color }}>{status ?? '—'}</span>
}

function ActiveBadge({ active }) {
  return <span style={{ fontSize: 11, fontWeight: 700, padding: '3px 9px', borderRadius: 100, background: active ? 'rgba(52,211,153,.1)' : 'rgba(239,68,68,.08)', color: active ? '#34d399' : '#f87171' }}>{active ? 'Active' : 'Inactive'}</span>
}
