import { useEffect, useState } from 'react'
import api from '../../api/axios.js'
import PlatformLayout from './PlatformLayout.jsx'

const EMPTY_FORM = {
  name: '', description: '',
  priceMonthly: '', priceAnnual: '',
  maxBranches: '1', maxUsers: '10',
  isActive: true,
}

export default function PlatformPlansPage() {
  const [plans, setPlans] = useState([])
  const [loading, setLoading] = useState(true)
  const [modal, setModal] = useState(null)   // null | 'create' | {plan}
  const [form, setForm] = useState(EMPTY_FORM)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [deleteConfirm, setDeleteConfirm] = useState(null)

  function loadPlans() {
    return api.get('/api/v1/platform/plans')
      .then(r => setPlans(r.data?.data ?? r.data))
      .finally(() => setLoading(false))
  }

  useEffect(() => { loadPlans() }, [])

  function openCreate() {
    setForm(EMPTY_FORM)
    setError('')
    setModal('create')
  }

  function openEdit(plan) {
    setForm({
      name: plan.name,
      description: plan.description ?? '',
      priceMonthly: plan.priceMonthly,
      priceAnnual: plan.priceAnnual,
      maxBranches: plan.maxBranches,
      maxUsers: plan.maxUsers,
      isActive: plan.isActive,
    })
    setError('')
    setModal(plan)
  }

  async function handleSave() {
    setError('')
    setSaving(true)
    const payload = {
      ...form,
      priceMonthly: Number(form.priceMonthly),
      priceAnnual: Number(form.priceAnnual),
      maxBranches: Number(form.maxBranches),
      maxUsers: Number(form.maxUsers),
    }
    try {
      if (modal === 'create') {
        await api.post('/api/v1/platform/plans', payload)
      } else {
        await api.put(`/api/v1/platform/plans/${modal.id}`, payload)
      }
      setModal(null)
      setLoading(true)
      await loadPlans()
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to save plan.')
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete(planId) {
    setSaving(true)
    try {
      await api.delete(`/api/v1/platform/plans/${planId}`)
      setDeleteConfirm(null)
      setLoading(true)
      await loadPlans()
    } catch (err) {
      setError(err.response?.data?.message || 'Cannot delete plan — it may be in use.')
      setDeleteConfirm(null)
    } finally {
      setSaving(false)
    }
  }

  function set(field) {
    return e => setForm(f => ({ ...f, [field]: e.target.value }))
  }

  const inputStyle = {
    width: '100%', height: 40, padding: '0 12px',
    fontSize: 14, fontFamily: "'Inter',sans-serif",
    border: '1.5px solid #E8ECF0', borderRadius: 9,
    background: '#FFFFFF', color: '#1B3A5C', outline: 'none',
    boxSizing: 'border-box',
  }
  const labelStyle = { display: 'block', fontSize: 12, fontWeight: 600, color: '#9ca3af', marginBottom: 6, letterSpacing: '.3px' }

  return (
    <PlatformLayout>
      <div style={{ maxWidth: 900 }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: 28, flexWrap: 'wrap', gap: 16 }}>
          <div>
            <h1 style={{ fontFamily: "'Inter',sans-serif", fontSize: 26, fontWeight: 700, color: '#1B3A5C', marginBottom: 6 }}>Subscription Plans</h1>
            <p style={{ fontSize: 14, color: '#6b7280' }}>{plans.length} plan{plans.length !== 1 ? 's' : ''} · Payment integration coming soon</p>
          </div>
          <button onClick={openCreate} style={{
            padding: '10px 20px', borderRadius: 10,
            background: 'linear-gradient(135deg,#C8960C,#E8B84D)',
            color: '#000', fontSize: 14, fontWeight: 700,
            border: 'none', cursor: 'pointer',
            fontFamily: "'Inter',sans-serif",
            boxShadow: '0 4px 14px rgba(200,150,12,.25)',
          }}>+ New Plan</button>
        </div>

        {error && !modal && (
          <div style={{ background: 'rgba(239,68,68,.08)', border: '1px solid rgba(239,68,68,.2)', color: '#f87171', borderRadius: 10, padding: '12px 14px', fontSize: 14, marginBottom: 20 }}>
            {error}
          </div>
        )}

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill,minmax(260px,1fr))', gap: 14 }}>
          {loading ? (
            [1,2,3].map(i => <div key={i} style={{ height: 180, borderRadius: 14, background: '#FFFFFF', border: '1px solid #E8ECF0' }}/>)
          ) : plans.map(plan => (
            <div key={plan.id} style={{
              background: '#FFFFFF', border: '1px solid #E8ECF0',
              borderRadius: 14, padding: '20px 20px',
              opacity: plan.isActive ? 1 : .55,
              position: 'relative',
            }}>
              {!plan.isActive && (
                <span style={{ position: 'absolute', top: 12, right: 12, fontSize: 10, fontWeight: 700, padding: '2px 7px', borderRadius: 100, background: 'rgba(107,114,128,.15)', color: '#6b7280', letterSpacing: '.5px', textTransform: 'uppercase' }}>
                  Inactive
                </span>
              )}
              <p style={{ fontFamily: "'Inter',sans-serif", fontSize: 17, fontWeight: 700, color: '#1B3A5C', marginBottom: 4 }}>{plan.name}</p>
              {plan.description && <p style={{ fontSize: 12, color: '#6b7280', marginBottom: 14, lineHeight: 1.5 }}>{plan.description}</p>}

              <div style={{ borderTop: '1px solid #E8ECF0', paddingTop: 14, marginBottom: 14 }}>
                <div style={{ display: 'flex', gap: 16, marginBottom: 10 }}>
                  <div>
                    <p style={{ fontSize: 10, color: '#4b5563', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '.5px' }}>Monthly</p>
                    <p style={{ fontSize: 17, fontWeight: 700, color: '#C8960C', fontFamily: "'Inter',sans-serif" }}>
                      {plan.priceMonthly === 0 ? 'Free' : `KES ${plan.priceMonthly.toLocaleString()}`}
                    </p>
                  </div>
                  <div>
                    <p style={{ fontSize: 10, color: '#4b5563', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '.5px' }}>Annual</p>
                    <p style={{ fontSize: 17, fontWeight: 700, color: '#9ca3af', fontFamily: "'Inter',sans-serif" }}>
                      {plan.priceAnnual === 0 ? 'Free' : `KES ${plan.priceAnnual.toLocaleString()}`}
                    </p>
                  </div>
                </div>
                <div style={{ display: 'flex', gap: 14, fontSize: 12, color: '#6b7280' }}>
                  <span>{plan.maxBranches === -1 ? '∞' : plan.maxBranches} branches</span>
                  <span>·</span>
                  <span>{plan.maxUsers === -1 ? '∞' : plan.maxUsers} users</span>
                </div>
              </div>

              <div style={{ display: 'flex', gap: 8 }}>
                <button onClick={() => openEdit(plan)} style={{
                  flex: 1, padding: '7px 0', borderRadius: 8, fontSize: 13, fontWeight: 600,
                  background: '#F0F4F8', color: '#9ca3af',
                  border: '1px solid #E8ECF0', cursor: 'pointer',
                  fontFamily: "'Inter',sans-serif",
                }}>Edit</button>
                <button onClick={() => setDeleteConfirm(plan)} style={{
                  padding: '7px 12px', borderRadius: 8, fontSize: 13, fontWeight: 600,
                  background: 'rgba(239,68,68,.06)', color: '#f87171',
                  border: '1px solid rgba(239,68,68,.12)', cursor: 'pointer',
                  fontFamily: "'Inter',sans-serif",
                }}>Delete</button>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Create / Edit Modal */}
      {modal && (
        <Overlay onClick={() => setModal(null)}>
          <div onClick={e => e.stopPropagation()} style={{
            background: '#0d1117', border: '1px solid #E8ECF0',
            borderRadius: 18, padding: '28px', width: '100%', maxWidth: 480, maxHeight: '90vh', overflowY: 'auto',
          }}>
            <h2 style={{ fontFamily: "'Inter',sans-serif", fontSize: 19, fontWeight: 700, color: '#1B3A5C', marginBottom: 22 }}>
              {modal === 'create' ? 'Create Plan' : `Edit — ${modal.name}`}
            </h2>

            {error && (
              <div style={{ background: 'rgba(239,68,68,.08)', border: '1px solid rgba(239,68,68,.2)', color: '#f87171', borderRadius: 9, padding: '10px 12px', fontSize: 13, marginBottom: 18 }}>
                {error}
              </div>
            )}

            <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
              <div>
                <label style={labelStyle}>PLAN NAME</label>
                <input style={inputStyle} value={form.name} onChange={set('name')} placeholder="Professional" />
              </div>
              <div>
                <label style={labelStyle}>DESCRIPTION</label>
                <input style={inputStyle} value={form.description} onChange={set('description')} placeholder="For growing businesses" />
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <div>
                  <label style={labelStyle}>PRICE / MONTH (KES)</label>
                  <input type="number" style={inputStyle} value={form.priceMonthly} onChange={set('priceMonthly')} placeholder="0" min="0" />
                </div>
                <div>
                  <label style={labelStyle}>PRICE / YEAR (KES)</label>
                  <input type="number" style={inputStyle} value={form.priceAnnual} onChange={set('priceAnnual')} placeholder="0" min="0" />
                </div>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <div>
                  <label style={labelStyle}>MAX BRANCHES (-1 = unlimited)</label>
                  <input type="number" style={inputStyle} value={form.maxBranches} onChange={set('maxBranches')} placeholder="1" min="-1" />
                </div>
                <div>
                  <label style={labelStyle}>MAX USERS (-1 = unlimited)</label>
                  <input type="number" style={inputStyle} value={form.maxUsers} onChange={set('maxUsers')} placeholder="10" min="-1" />
                </div>
              </div>
              <label style={{ display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', fontSize: 14, color: '#9ca3af', fontWeight: 500 }}>
                <input type="checkbox" checked={form.isActive} onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))}
                  style={{ accentColor: '#C8960C', width: 16, height: 16 }} />
                Active (visible during company creation)
              </label>
            </div>

            <div style={{ display: 'flex', gap: 10, marginTop: 24 }}>
              <button onClick={() => setModal(null)} style={{
                flex: 1, padding: '10px 0', borderRadius: 9, fontSize: 14, fontWeight: 600,
                background: '#FFFFFF', color: '#6b7280',
                border: '1px solid #E8ECF0', cursor: 'pointer',
                fontFamily: "'Inter',sans-serif",
              }}>Cancel</button>
              <button onClick={handleSave} disabled={saving || !form.name} style={{
                flex: 1, padding: '10px 0', borderRadius: 9, fontSize: 14, fontWeight: 700,
                background: 'linear-gradient(135deg,#C8960C,#E8B84D)',
                color: '#000', border: 'none', cursor: saving ? 'not-allowed' : 'pointer',
                fontFamily: "'Inter',sans-serif",
                opacity: (saving || !form.name) ? .6 : 1,
              }}>{saving ? 'Saving…' : 'Save Plan'}</button>
            </div>
          </div>
        </Overlay>
      )}

      {/* Delete confirm modal */}
      {deleteConfirm && (
        <Overlay onClick={() => setDeleteConfirm(null)}>
          <div onClick={e => e.stopPropagation()} style={{
            background: '#0d1117', border: '1px solid #E8ECF0',
            borderRadius: 16, padding: '28px', width: '100%', maxWidth: 380, textAlign: 'center',
          }}>
            <div style={{ fontSize: 36, marginBottom: 12 }}>⚠️</div>
            <h2 style={{ fontSize: 17, fontWeight: 700, color: '#1B3A5C', marginBottom: 8 }}>Delete "{deleteConfirm.name}"?</h2>
            <p style={{ fontSize: 14, color: '#6b7280', marginBottom: 24 }}>This cannot be undone. Plans in use by companies cannot be deleted.</p>
            <div style={{ display: 'flex', gap: 10 }}>
              <button onClick={() => setDeleteConfirm(null)} style={{
                flex: 1, padding: '10px 0', borderRadius: 9, fontSize: 14, fontWeight: 600,
                background: '#FFFFFF', color: '#6b7280',
                border: '1px solid #E8ECF0', cursor: 'pointer',
                fontFamily: "'Inter',sans-serif",
              }}>Cancel</button>
              <button onClick={() => handleDelete(deleteConfirm.id)} disabled={saving} style={{
                flex: 1, padding: '10px 0', borderRadius: 9, fontSize: 14, fontWeight: 700,
                background: 'rgba(239,68,68,.15)', color: '#f87171',
                border: '1px solid rgba(239,68,68,.2)', cursor: saving ? 'not-allowed' : 'pointer',
                fontFamily: "'Inter',sans-serif",
              }}>{saving ? 'Deleting…' : 'Yes, Delete'}</button>
            </div>
          </div>
        </Overlay>
      )}
    </PlatformLayout>
  )
}

function Overlay({ children, onClick }) {
  return (
    <div onClick={onClick} style={{
      position: 'fixed', inset: 0, background: 'rgba(0,0,0,.7)', backdropFilter: 'blur(4px)',
      display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100, padding: 24,
    }}>
      {children}
    </div>
  )
}
