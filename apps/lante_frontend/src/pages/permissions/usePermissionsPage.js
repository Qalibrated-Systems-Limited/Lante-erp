import { useState, useEffect, useCallback } from 'react'
import api from '../../api/axios.js'

export function usePermissionsPage() {
  const [permissions, setPermissions] = useState([])
  const [loading, setLoading]         = useState(true)
  const [error, setError]             = useState('')
  const [search, setSearch]           = useState('')

  const [modal, setModal]           = useState(null)
  const [selected, setSelected]     = useState(null)
  const [form, setForm]             = useState({ name: '', description: '' })
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError]   = useState('')
  const [toast, setToast]           = useState('')

  const showToast = (msg) => { setToast(msg); setTimeout(() => setToast(''), 3000) }

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/permissions', { params: { page: 1, pageSize: 500 } })
      const data = res.data?.data
      setPermissions(data?.items ?? [])
    } catch {
      setError('Failed to load permissions.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  async function handleCreate() {
    setFormError('')
    if (!form.name.trim()) { setFormError('Permission key is required.'); return }
    setSubmitting(true)
    try {
      await api.post('/api/v1/permissions', { name: form.name.trim(), description: form.description.trim() || null })
      setModal(null)
      showToast('Permission created.')
      load()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to create permission.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleUpdate() {
    setFormError('')
    if (!form.name.trim()) { setFormError('Permission key is required.'); return }
    setSubmitting(true)
    try {
      await api.put(`/api/v1/permissions/${selected.id}`, { name: form.name.trim(), description: form.description.trim() || null })
      setModal(null)
      showToast('Permission updated.')
      load()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to update permission.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDelete() {
    setSubmitting(true)
    try {
      await api.delete(`/api/v1/permissions/${selected.id}`)
      setModal(null)
      setPermissions(prev => prev.filter(p => p.id !== selected.id))
      showToast('Permission deleted.')
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to delete.')
    } finally {
      setSubmitting(false)
    }
  }

  return {
    permissions,
    loading,
    error,
    search, setSearch,

    modal, setModal,
    selected, setSelected,
    form, setForm,
    submitting,
    formError, setFormError,
    toast,

    handleCreate,
    handleUpdate,
    handleDelete,
  }
}
