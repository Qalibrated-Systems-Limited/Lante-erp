import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'

export default function UserDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [user, setUser] = useState(null)
  const [roles, setRoles] = useState([])
  const [permissions, setPermissions] = useState([])
  const [departments, setDepartments] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [allRoles, setAllRoles] = useState([])
  const [editModal, setEditModal] = useState(false)
  const [assignRoleModal, setAssignRoleModal] = useState(false)
  const [selectedRoleId, setSelectedRoleId] = useState('')
  const [form, setForm] = useState({})
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError] = useState('')
  const [successMsg, setSuccessMsg] = useState('')
  const [actionError, setActionError] = useState('')

  useEffect(() => {
    api.get('/api/v1/departments')
      .then(res => {
        const raw = res.data?.data
        setDepartments(Array.isArray(raw) ? raw : raw?.departments ?? [])
      })
      .catch(() => {})
    api.get('/api/v1/roles')
      .then(res => setAllRoles(res.data?.data ?? []))
      .catch(() => {})
    loadUser()
  }, [id])

  async function loadUser() {
    setLoading(true)
    setError('')
    try {
      const [userRes, rolesRes, permsRes] = await Promise.all([
        api.get(`/api/v1/users/${id}`),
        api.get(`/api/v1/users/${id}/roles`),
        api.get(`/api/v1/users/${id}/permissions`),
      ])
      setUser(userRes.data?.data)
      setRoles(rolesRes.data?.data?.roles ?? [])
      setPermissions(permsRes.data?.data?.permissions ?? [])
    } catch {
      setError('Failed to load user.')
    } finally {
      setLoading(false)
    }
  }

  function openEdit() {
    setForm({
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email ?? '',
      mobileNumber: user.mobileNumber ?? '',
      departmentId: user.departmentId ?? '',
    })
    setFormError('')
    setEditModal(true)
  }

  async function handleSave() {
    setFormError('')
    if (!form.firstName?.trim() || !form.lastName?.trim()) {
      setFormError('First and last name are required.')
      return
    }
    if (!form.email?.trim()) {
      setFormError('Email is required.')
      return
    }
    setSubmitting(true)
    try {
      await api.put(`/api/v1/users/${id}`, {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        mobileNumber: form.mobileNumber.trim(),
        departmentId: form.departmentId || null,
      })
      setEditModal(false)
      setSuccessMsg('User updated successfully.')
      setTimeout(() => setSuccessMsg(''), 3000)
      loadUser()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to update.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleResendInvite() {
    setSubmitting(true)
    setActionError('')
    try {
      await api.post(`/api/v1/users/${id}/resend-invite`)
      setSuccessMsg('Invite resent.')
      setTimeout(() => setSuccessMsg(''), 3000)
      loadUser()
    } catch (err) {
      setActionError(err.response?.data?.message ?? 'Failed to resend invite.')
      setTimeout(() => setActionError(''), 4000)
    } finally {
      setSubmitting(false)
    }
  }

  async function handleAssignRole() {
    if (!selectedRoleId) return
    setSubmitting(true)
    setFormError('')
    try {
      await api.post(`/api/v1/users/${id}/roles/${selectedRoleId}`)
      setAssignRoleModal(false)
      setSelectedRoleId('')
      setSuccessMsg('Role assigned.')
      setTimeout(() => setSuccessMsg(''), 3000)
      loadUser()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to assign role.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleRemoveRole(roleId) {
    try {
      await api.delete(`/api/v1/users/${id}/roles/${roleId}`)
      setSuccessMsg('Role removed.')
      setTimeout(() => setSuccessMsg(''), 3000)
      loadUser()
    } catch {
      setSuccessMsg('Failed to remove role.')
      setTimeout(() => setSuccessMsg(''), 3000)
    }
  }

  async function toggleTwoFactor() {
    const endpoint = user.twoFactorEnabled ? 'disable-2fa' : 'enable-2fa'
    try {
      await api.put(`/api/v1/users/${id}/${endpoint}`)
      setSuccessMsg(`Two-factor authentication ${user.twoFactorEnabled ? 'disabled' : 'enabled'}.`)
      setTimeout(() => setSuccessMsg(''), 3000)
      loadUser()
    } catch {
      setError('Action failed.')
    }
  }

  if (loading) return (
    <>
      <div className="flex-1 flex items-center justify-center">
        <p className="text-gray-400 text-sm animate-pulse">Loading user…</p>
      </div>
    </>
  )

  if (error || !user) return (
    <>
      <div className="flex-1 flex flex-col items-center justify-center gap-3">
        <p className="text-gray-500">{error || 'User not found.'}</p>
        <button onClick={() => navigate('/modules/users')} className="text-amber-600 text-sm font-medium">Back to users</button>
      </div>
    </>
  )

  const initials = `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase()

  return (
    <>

      <main className="flex-1 max-w-4xl mx-auto w-full px-4 sm:px-6 py-8">
        <button
          onClick={() => navigate('/modules/users')}
          className="flex items-center gap-1.5 text-sm text-gray-400 hover:text-gray-600 mb-5 transition-colors"
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          All Users
        </button>

        {successMsg && (
          <div className="bg-green-50 border border-green-200 text-green-700 rounded-xl px-5 py-3 text-sm mb-5">{successMsg}</div>
        )}
        {actionError && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-3 text-sm mb-5">{actionError}</div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Profile card */}
          <div className="lg:col-span-1">
            <div className="bg-white rounded-xl border border-gray-200 p-6 text-center">
              <div className="w-20 h-20 rounded-full bg-zinc-100 text-zinc-950 flex items-center justify-center text-2xl font-bold mx-auto mb-4">
                {initials}
              </div>
              <h2 className="text-lg font-bold text-gray-900">{user.firstName} {user.lastName}</h2>
              <p className="text-sm text-gray-500 mt-0.5">{user.email}</p>
              {user.mobileNumber && <p className="text-sm text-gray-500">{user.mobileNumber}</p>}

              <div className="flex items-center justify-center gap-2 mt-3">
                <span className={`px-2.5 py-0.5 rounded-full text-xs font-semibold ${user.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
                  {user.isActive ? 'Active' : 'Inactive'}
                </span>
                {user.twoFactorEnabled && (
                  <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-blue-100 text-blue-700">2FA On</span>
                )}
                {user.isFirstLogin && (
                  <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-700">First Login</span>
                )}
              </div>

              <div className="mt-4 pt-4 border-t border-gray-100 space-y-2">
                <button
                  onClick={openEdit}
                  className="w-full py-2 bg-amber-500 hover:bg-amber-600 text-white text-sm font-semibold rounded-lg transition-colors"
                >
                  Edit Profile
                </button>
                <button
                  onClick={toggleTwoFactor}
                  className={`w-full py-2 text-sm font-semibold rounded-lg border transition-colors ${
                    user.twoFactorEnabled
                      ? 'border-orange-200 text-orange-600 hover:bg-orange-50'
                      : 'border-blue-200 text-blue-600 hover:bg-blue-50'
                  }`}
                >
                  {user.twoFactorEnabled ? 'Disable 2FA' : 'Enable 2FA'}
                </button>
                {!user.isActive && user.isFirstLogin && (
                  <button
                    onClick={handleResendInvite}
                    disabled={submitting}
                    className="w-full py-2 text-sm font-semibold rounded-lg border border-amber-200 text-amber-600 hover:bg-amber-50 disabled:opacity-50 transition-colors"
                  >
                    {submitting ? 'Sending…' : 'Resend Invite'}
                  </button>
                )}
              </div>
            </div>

            {/* Meta */}
            <div className="bg-white rounded-xl border border-gray-200 p-5 mt-4 space-y-3">
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Info</p>
              <MetaRow label="Department" value={user.departmentName ?? '—'} />
              <MetaRow label="Created" value={fmtDate(user.createdAt)} />
              <MetaRow label="Updated" value={fmtDate(user.updatedAt)} />
            </div>
          </div>

          {/* Right — roles + permissions */}
          <div className="lg:col-span-2 space-y-5">
            <div className="bg-white rounded-xl border border-gray-200 p-6">
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-sm font-bold text-gray-700 uppercase tracking-wider">Roles ({roles.length})</h3>
                <button
                  onClick={() => { setSelectedRoleId(''); setFormError(''); setAssignRoleModal(true) }}
                  className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-amber-500 hover:bg-amber-600 text-white text-xs font-semibold rounded-lg transition-colors"
                >
                  <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                  </svg>
                  Assign Role
                </button>
              </div>
              {roles.length === 0 ? (
                <p className="text-sm text-gray-400">No roles assigned.</p>
              ) : (
                <div className="space-y-2">
                  {roles.map(r => (
                    <div key={r.id ?? r.name} className="flex items-center justify-between px-3 py-2 bg-zinc-50 rounded-lg">
                      <div>
                        <p className="text-sm font-medium text-zinc-950">{r.name ?? r}</p>
                        {r.description && <p className="text-xs text-zinc-600">{r.description}</p>}
                      </div>
                      <button
                        onClick={() => handleRemoveRole(r.id)}
                        className="p-1 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded transition-colors"
                        title="Remove role"
                      >
                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                        </svg>
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div className="bg-white rounded-xl border border-gray-200 p-6">
              <h3 className="text-sm font-bold text-gray-700 uppercase tracking-wider mb-4">
                Permissions ({permissions.length})
              </h3>
              {permissions.length === 0 ? (
                <p className="text-sm text-gray-400">No permissions via roles.</p>
              ) : (
                <div className="flex flex-wrap gap-2">
                  {permissions.map(p => (
                    <span key={p} className="px-2.5 py-1 bg-gray-100 text-gray-600 text-xs font-medium rounded-lg font-mono">
                      {p}
                    </span>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      </main>

      {/* Edit Modal */}
      {editModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6">
            <div className="flex items-center justify-between mb-5">
              <h2 className="text-lg font-bold text-zinc-950">Edit User</h2>
              <button onClick={() => setEditModal(false)} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 transition-colors">
                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
            {formError && <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm mb-4">{formError}</div>}
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-sm font-semibold text-gray-700 mb-1.5">First Name *</label>
                  <input type="text" value={form.firstName} onChange={e => setForm(f => ({ ...f, firstName: e.target.value }))} className="input" />
                </div>
                <div>
                  <label className="block text-sm font-semibold text-gray-700 mb-1.5">Last Name *</label>
                  <input type="text" value={form.lastName} onChange={e => setForm(f => ({ ...f, lastName: e.target.value }))} className="input" />
                </div>
              </div>
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1.5">Email *</label>
                <input type="email" value={form.email} onChange={e => setForm(f => ({ ...f, email: e.target.value }))} className="input" />
              </div>
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1.5">Mobile Number</label>
                <input type="text" value={form.mobileNumber} onChange={e => setForm(f => ({ ...f, mobileNumber: e.target.value }))} className="input" />
              </div>
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-1.5">Department</label>
                <select value={form.departmentId} onChange={e => setForm(f => ({ ...f, departmentId: e.target.value }))} className="input">
                  <option value="">None</option>
                  {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
                </select>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 mt-6">
              <button onClick={() => setEditModal(false)} className="px-4 py-2 text-sm font-medium border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">Cancel</button>
              <button onClick={handleSave} disabled={submitting} className="px-5 py-2 bg-amber-500 hover:bg-amber-600 disabled:opacity-50 text-white text-sm font-semibold rounded-lg transition-colors">
                {submitting ? 'Saving…' : 'Save'}
              </button>
            </div>
          </div>
        </div>
      )}
      {/* Assign Role Modal */}
      {assignRoleModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-sm p-6">
            <div className="flex items-center justify-between mb-5">
              <h2 className="text-lg font-bold text-zinc-950">Assign Role</h2>
              <button onClick={() => setAssignRoleModal(false)} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 transition-colors">
                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
            {formError && <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm mb-4">{formError}</div>}
            <div className="mb-4">
              <label className="block text-sm font-semibold text-gray-700 mb-1.5">Select Role</label>
              <div className="border border-gray-200 rounded-lg max-h-56 overflow-y-auto divide-y divide-gray-50">
                {allRoles
                  .filter(r => !roles.some(ur => (ur.id ?? ur) === r.id))
                  .map(role => (
                    <label key={role.id} className="flex items-center gap-3 px-3 py-2.5 cursor-pointer hover:bg-gray-50">
                      <input
                        type="radio"
                        name="roleSelect"
                        value={role.id}
                        checked={selectedRoleId === role.id}
                        onChange={() => setSelectedRoleId(role.id)}
                        className="accent-amber-500"
                      />
                      <div>
                        <p className="text-sm font-medium text-gray-800">{role.name}</p>
                        {role.description && <p className="text-xs text-gray-400">{role.description}</p>}
                      </div>
                    </label>
                  ))
                }
                {allRoles.filter(r => !roles.some(ur => (ur.id ?? ur) === r.id)).length === 0 && (
                  <p className="text-sm text-gray-400 px-3 py-4">All available roles are already assigned.</p>
                )}
              </div>
            </div>
            <div className="flex items-center justify-end gap-3">
              <button onClick={() => setAssignRoleModal(false)} className="px-4 py-2 text-sm font-medium border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">Cancel</button>
              <button
                onClick={handleAssignRole}
                disabled={submitting || !selectedRoleId}
                className="px-5 py-2 bg-amber-500 hover:bg-amber-600 disabled:opacity-50 text-white text-sm font-semibold rounded-lg transition-colors"
              >
                {submitting ? 'Assigning…' : 'Assign'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}

function MetaRow({ label, value }) {
  return (
    <div className="flex justify-between gap-2">
      <span className="text-xs text-gray-400">{label}</span>
      <span className="text-xs font-medium text-gray-700 text-right">{value}</span>
    </div>
  )
}

function fmtDate(dt) {
  if (!dt) return '—'
  return new Date(dt).toLocaleDateString(undefined, { dateStyle: 'medium' })
}
