import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import { useAuth } from '../../context/AuthContext.jsx'

export function useUsersPage() {
  const navigate = useNavigate()
  const { tenantId, isCompanyAdmin } = useAuth()

  const [users, setUsers] = useState([])
  const [departments, setDepartments] = useState([])
  const [branches, setBranches] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [total, setTotal] = useState(0)
  const [search, setSearch] = useState('')

  const [showDeleted, setShowDeleted] = useState(false)
  const [roles, setRoles] = useState([])
  const [modal, setModal] = useState(null) // 'create' | 'delete' | 'resetpw' | 'restore'
  const [selected, setSelected] = useState(null)
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', mobileNumber: '', departmentId: '', roleIds: [], branchId: '' })
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError] = useState('')
  const [actionMsg, setActionMsg] = useState('')

  useEffect(() => {
    api.get('/api/v1/departments')
      .then(res => {
        const raw = res.data?.data
        setDepartments(Array.isArray(raw) ? raw : raw?.departments ?? [])
      })
      .catch(() => {})
    api.get('/api/v1/roles')
      .then(res => setRoles(res.data?.data ?? []))
      .catch(() => {})
    if (tenantId) {
      api.get(`/api/v1/tenants/${tenantId}/branches`)
        .then(res => setBranches(res.data?.data ?? []))
        .catch(() => {})
    }
  }, [tenantId])

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const params = { page, pageSize: 15 }
      if (search) params.search = search
      const endpoint = showDeleted ? '/api/v1/users/deleted' : '/api/v1/users'
      const res = await api.get(endpoint, { params })
      const data = res.data?.data
      setUsers(data?.items ?? [])
      setTotalPages(data?.totalPages ?? 1)
      setTotal(data?.totalCount ?? 0)
    } catch {
      setError('Failed to load users.')
    } finally {
      setLoading(false)
    }
  }, [page, search, showDeleted])

  useEffect(() => { load() }, [load])

  function openCreate() {
    setForm({ firstName: '', lastName: '', email: '', mobileNumber: '', departmentId: '', roleIds: [], branchId: '' })
    setFormError('')
    setModal('create')
  }

  function toggleRoleId(roleId) {
    setForm(f => ({
      ...f,
      roleIds: f.roleIds.includes(roleId)
        ? f.roleIds.filter(id => id !== roleId)
        : [...f.roleIds, roleId]
    }))
  }

  async function handleCreate() {
    setFormError('')
    if (!form.firstName.trim() || !form.lastName.trim() || !form.email.trim()) {
      setFormError('First name, last name and email are required.')
      return
    }
    setSubmitting(true)
    try {
      await api.post('/api/v1/users', {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        mobileNumber: form.mobileNumber.trim(),
        departmentId: form.departmentId || null,
        roleIds: form.roleIds.length > 0 ? form.roleIds : null,
        branchId: form.branchId || null,
      })
      setModal(null)
      setActionMsg(`Invite sent to ${form.email.trim()} — they'll set their own password via the emailed link.`)
      load()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to create user.')
    } finally {
      setSubmitting(false)
    }
  }

  async function toggleTwoFactor(user) {
    const endpoint = user.twoFactorEnabled ? 'disable-2fa' : 'enable-2fa'
    try {
      await api.put(`/api/v1/users/${user.id}/${endpoint}`)
      setActionMsg(`Two-factor authentication ${user.twoFactorEnabled ? 'disabled' : 'enabled'} for ${user.firstName} ${user.lastName}.`)
      setTimeout(() => setActionMsg(''), 3000)
      load()
    } catch {
      setActionMsg('Action failed.')
      setTimeout(() => setActionMsg(''), 3000)
    }
  }

  async function handleResendInvite(user) {
    try {
      await api.post(`/api/v1/users/${user.id}/resend-invite`)
      setActionMsg(`Invite resent to ${user.firstName} ${user.lastName}.`)
      setTimeout(() => setActionMsg(''), 3000)
      load()
    } catch (err) {
      setActionMsg(err.response?.data?.message ?? 'Failed to resend invite.')
      setTimeout(() => setActionMsg(''), 3000)
    }
  }

  async function handleDelete() {
    setSubmitting(true)
    try {
      await api.delete(`/api/v1/users/${selected.id}`)
      setModal(null)
      load()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to delete.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleResetPassword() {
    setSubmitting(true)
    try {
      await api.post(`/api/v1/users/${selected.id}/reset-password`)
      setModal(null)
      setActionMsg('Password reset email sent.')
      setTimeout(() => setActionMsg(''), 3000)
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to reset password.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleRestore() {
    setSubmitting(true)
    try {
      await api.patch(`/api/v1/users/${selected.id}/restore`)
      setModal(null)
      setActionMsg(`${selected.firstName} ${selected.lastName} restored successfully.`)
      setTimeout(() => setActionMsg(''), 3000)
      load()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to restore user.')
    } finally {
      setSubmitting(false)
    }
  }

  return {
    navigate,
    tenantId,
    isCompanyAdmin,

    users, setUsers,
    departments, setDepartments,
    branches, setBranches,
    loading, setLoading,
    error, setError,
    page, setPage,
    totalPages, setTotalPages,
    total, setTotal,
    search, setSearch,

    showDeleted, setShowDeleted,
    roles, setRoles,
    modal, setModal,
    selected, setSelected,
    form, setForm,
    submitting, setSubmitting,
    formError, setFormError,
    actionMsg, setActionMsg,

    load,
    openCreate,
    toggleRoleId,
    handleCreate,
    toggleTwoFactor,
    handleResendInvite,
    handleDelete,
    handleResetPassword,
    handleRestore,
  }
}
