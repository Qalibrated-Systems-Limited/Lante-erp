import { useState, useEffect, useCallback } from 'react'
import api from '../../api/axios.js'
import { Card, DataTable, Badge, Btn, Alert, Loading } from '../../components/ui.jsx'

// Real per-tenant registry (user-service SystemModule) — lets an admin hide a whole sidebar
// module for everyone, independent of per-user RBAC permissions.
export default function ModulesTab() {
  const [modules, setModules] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [msg, setMsg] = useState(null)
  const [toggling, setToggling] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/system-modules')
      setModules(res.data?.data ?? [])
    } catch {
      setError('Failed to load modules.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  async function toggle(m) {
    setToggling(m.moduleId)
    setMsg(null)
    try {
      await api.put(`/api/v1/system-modules/${m.moduleId}/toggle`, { enabled: !m.isEnabled })
      setMsg({ type: 'success', text: `${m.displayName} ${m.isEnabled ? 'disabled' : 'enabled'}.` })
      load()
    } catch (err) {
      setMsg({ type: 'error', text: err.response?.data?.message ?? 'Failed to update module.' })
    } finally {
      setToggling(null)
    }
  }

  return (
    <>
      <Alert type="info">
        Turn a whole sidebar module off for every user in this tenant — separate from per-user permissions.
        Core modules (Dashboard, My Workspace, Administration) can't be disabled.
      </Alert>
      {error && <Alert type="error">{error}</Alert>}
      {msg && <Alert type={msg.type}>{msg.text}</Alert>}

      {loading ? <Loading /> : (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <DataTable
            headers={['Module', 'Status', 'Core', 'Action']}
            rows={modules.map(m => [
              <strong>{m.displayName}</strong>,
              <Badge variant={m.isEnabled ? 'green' : 'default'}>{m.isEnabled ? 'Enabled' : 'Disabled'}</Badge>,
              m.isCore ? <Badge variant="navy">Core</Badge> : '—',
              m.isCore
                ? <span style={{ fontSize: 11, color: '#94A3B8' }}>Cannot disable</span>
                : <Btn size="sm" variant={m.isEnabled ? 'ghost' : 'gold'} disabled={toggling === m.moduleId} onClick={() => toggle(m)}>
                    {toggling === m.moduleId ? '…' : m.isEnabled ? 'Disable' : 'Enable'}
                  </Btn>,
            ])}
          />
        </Card>
      )}
    </>
  )
}
