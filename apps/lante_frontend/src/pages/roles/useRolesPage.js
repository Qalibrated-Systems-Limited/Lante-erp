import { useState, useEffect } from 'react'
import api from '../../api/axios.js'

export function useRolesPage() {
  const [roles, setRoles] = useState([])
  const [allPermissions, setAllPermissions] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [modal, setModal] = useState(null) // 'create' | 'edit' | 'delete' | 'permissions' | 'users'
  const [selected, setSelected] = useState(null)

  // Permissions modal state
  const [rolePerms, setRolePerms] = useState([])
  const [rolePermsLoading, setRolePermsLoading] = useState(false)

  // Users modal state
  const [allUsers, setAllUsers] = useState([])
  const [usersLoading, setUsersLoading] = useState(false)
  const [userSearch, setUserSearch] = useState('')

  // Create/Edit form
  const [form, setForm] = useState({ name: '', description: '' })
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError] = useState('')

  useEffect(() => {
    load()
    api.get('/api/v1/permissions', { params: { page: 1, pageSize: 200 } })
      .then(res => setAllPermissions(res.data?.data?.items ?? []))
      .catch(() => {})
  }, [])

  async function load() {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/roles')
      setRoles(Array.isArray(res.data?.data) ? res.data.data : [])
    } catch {
      setError('Failed to load roles.')
    } finally {
      setLoading(false)
    }
  }

  // ── Permissions modal ───────────────────────────────────────────────────────
  async function openPermissions(role) {
    setSelected(role)
    setModal('permissions')
    setRolePermsLoading(true)
    try {
      const res = await api.get(`/api/v1/roles/${role.id}/permissions`)
      setRolePerms(Array.isArray(res.data?.data) ? res.data.data : [])
    } catch {
      setRolePerms([])
    } finally {
      setRolePermsLoading(false)
    }
  }

  async function addPermission(permId) {
    try {
      await api.post(`/api/v1/roles/${selected.id}/permissions/${permId}`)
      const res = await api.get(`/api/v1/roles/${selected.id}/permissions`)
      const updated = Array.isArray(res.data?.data) ? res.data.data : []
      setRolePerms(updated)
      setRoles(rs => rs.map(r => r.id === selected.id
        ? { ...r, permissions: updated.map(p => p.name) }
        : r
      ))
    } catch {}
  }

  async function removePermission(permId) {
    try {
      await api.delete(`/api/v1/roles/${selected.id}/permissions/${permId}`)
      const updated = rolePerms.filter(p => p.id !== permId)
      setRolePerms(updated)
      setRoles(rs => rs.map(r => r.id === selected.id
        ? { ...r, permissions: updated.map(p => p.name) }
        : r
      ))
    } catch {}
  }

  // ── Users modal ─────────────────────────────────────────────────────────────
  async function openUsers(role) {
    setSelected(role)
    setUserSearch('')
    setModal('users')
    setUsersLoading(true)
    try {
      const res = await api.get('/api/v1/users', { params: { page: 1, pageSize: 500 } })
      setAllUsers(res.data?.data?.items ?? [])
    } catch {
      setAllUsers([])
    } finally {
      setUsersLoading(false)
    }
  }

  async function assignUserToRole(userId) {
    try {
      await api.post(`/api/v1/users/${userId}/roles/${selected.id}`)
      setAllUsers(us => us.map(u =>
        u.id === userId ? { ...u, roles: [...(u.roles ?? []), selected.name] } : u
      ))
      setRoles(rs => rs.map(r =>
        r.id === selected.id ? { ...r, userCount: (r.userCount ?? 0) + 1 } : r
      ))
    } catch {}
  }

  async function removeUserFromRole(userId) {
    try {
      await api.delete(`/api/v1/users/${userId}/roles/${selected.id}`)
      setAllUsers(us => us.map(u =>
        u.id === userId ? { ...u, roles: (u.roles ?? []).filter(rn => rn !== selected.name) } : u
      ))
      setRoles(rs => rs.map(r =>
        r.id === selected.id ? { ...r, userCount: Math.max(0, (r.userCount ?? 0) - 1) } : r
      ))
    } catch {}
  }

  // ── Create / Edit / Delete ───────────────────────────────────────────────────
  async function handleSave() {
    setFormError('')
    if (!form.name.trim()) { setFormError('Name is required.'); return }
    setSubmitting(true)
    try {
      if (modal === 'create') {
        await api.post('/api/v1/roles', { name: form.name.trim(), description: form.description.trim() })
      } else {
        await api.put(`/api/v1/roles/${selected.id}`, { name: form.name.trim(), description: form.description.trim() })
      }
      setModal(null)
      load()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to save.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDelete() {
    setSubmitting(true)
    try {
      await api.delete(`/api/v1/roles/${selected.id}`)
      setModal(null)
      load()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to delete.')
    } finally {
      setSubmitting(false)
    }
  }

  // ── Derived lists ────────────────────────────────────────────────────────────
  const assignedPermIds = new Set(rolePerms.map(p => p.id))
  const unassigned = allPermissions.filter(p => !assignedPermIds.has(p.id))

  const assignedUsers   = allUsers.filter(u => (u.roles ?? []).includes(selected?.name))
  const unassignedUsers = allUsers.filter(u => !(u.roles ?? []).includes(selected?.name))

  const lc = userSearch.toLowerCase()
  const filteredAssigned   = lc ? assignedUsers.filter(u => `${u.firstName} ${u.lastName} ${u.email}`.toLowerCase().includes(lc)) : assignedUsers
  const filteredUnassigned = lc ? unassignedUsers.filter(u => `${u.firstName} ${u.lastName} ${u.email}`.toLowerCase().includes(lc)) : unassignedUsers

  return {
    roles,
    allPermissions,
    loading,
    error,

    modal, setModal,
    selected, setSelected,

    rolePerms,
    rolePermsLoading,

    allUsers,
    usersLoading,
    userSearch, setUserSearch,

    form, setForm,
    submitting,
    formError, setFormError,

    openPermissions,
    addPermission,
    removePermission,

    openUsers,
    assignUserToRole,
    removeUserFromRole,

    handleSave,
    handleDelete,

    unassigned,
    assignedUsers,
    unassignedUsers,
    filteredAssigned,
    filteredUnassigned,
  }
}
