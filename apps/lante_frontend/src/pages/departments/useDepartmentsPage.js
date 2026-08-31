import { useState, useEffect } from 'react'
import api from '../../api/axios.js'
import { useAuth } from '../../context/AuthContext.jsx'

export function useDepartmentsPage() {
  const { canInteractWithDepartment, isAdmin, tenantId } = useAuth()
  const [tab, setTab] = useState('departments')

  const [departments, setDepartments] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [modal, setModal] = useState(null) // null | 'create' | 'edit' | 'delete' | 'users'
  const [selected, setSelected] = useState(null)
  const [deptUsers, setDeptUsers] = useState([])
  const [deptUsersLoading, setDeptUsersLoading] = useState(false)

  const [form, setForm] = useState({ name: '', description: '', isActive: true, departmentGroupId: '' })
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError] = useState('')

  // Read-only branches list — shown alongside Departments in a sub-tab.
  const [branches, setBranches] = useState([])
  const [branchesLoading, setBranchesLoading] = useState(false)
  const [branchesError, setBranchesError] = useState('')

  useEffect(() => { load() }, [])

  useEffect(() => {
    if (!tenantId) return
    setBranchesLoading(true)
    setBranchesError('')
    api.get(`/api/v1/tenants/${tenantId}/branches`)
      .then(res => setBranches(res.data?.data ?? []))
      .catch(() => setBranchesError('Failed to load branches.'))
      .finally(() => setBranchesLoading(false))
  }, [tenantId])

  async function load() {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/departments')
      const raw = res.data?.data
      setDepartments(Array.isArray(raw) ? raw : raw?.departments ?? [])
    } catch {
      setError('Failed to load departments.')
    } finally {
      setLoading(false)
    }
  }

  function openCreate() {
    setForm({ name: '', description: '', isActive: true, departmentGroupId: '' })
    setFormError('')
    setModal('create')
  }

  function openEdit(dept) {
    setSelected(dept)
    setForm({ name: dept.name, description: dept.description ?? '', isActive: dept.isActive, departmentGroupId: dept.departmentGroupId ?? '' })
    setFormError('')
    setModal('edit')
  }

  function openDelete(dept) {
    setSelected(dept)
    setModal('delete')
  }

  async function openUsers(dept) {
    setSelected(dept)
    setModal('users')
    setDeptUsersLoading(true)
    try {
      const res = await api.get(`/api/v1/departments/${dept.id}/users`)
      const raw = res.data?.data
      setDeptUsers(Array.isArray(raw) ? raw : [])
    } catch {
      setDeptUsers([])
    } finally {
      setDeptUsersLoading(false)
    }
  }

  async function handleSave() {
    setFormError('')
    if (!form.name.trim()) { setFormError('Name is required.'); return }
    setSubmitting(true)
    try {
      const groupId = form.departmentGroupId.trim() || null
      if (modal === 'create') {
        await api.post('/api/v1/departments', { name: form.name.trim(), description: form.description.trim(), isActive: form.isActive, departmentGroupId: groupId })
      } else {
        await api.put(`/api/v1/departments/${selected.id}`, { name: form.name.trim(), description: form.description.trim(), isActive: form.isActive, departmentGroupId: groupId ?? '' })
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
      await api.delete(`/api/v1/departments/${selected.id}`)
      setModal(null)
      load()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to delete.')
    } finally {
      setSubmitting(false)
    }
  }

  return {
    canInteractWithDepartment,
    isAdmin,
    tenantId,
    tab,
    setTab,

    departments,
    loading,
    error,

    modal,
    setModal,
    selected,
    deptUsers,
    deptUsersLoading,

    form,
    setForm,
    submitting,
    formError,

    branches,
    branchesLoading,
    branchesError,

    openCreate,
    openEdit,
    openDelete,
    openUsers,
    handleSave,
    handleDelete,
  }
}
