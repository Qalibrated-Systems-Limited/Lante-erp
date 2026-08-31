import { useEffect, useState } from 'react'
import api from '../../api/axios.js'
import PlatformLayout from './PlatformLayout.jsx'

const JOB_LABELS = {
  'lante-backup-pgbackrest':       'Postgres Physical Backup (pgBackRest)',
  'lante-backup-logical-dumps':    'Postgres Logical Dumps',
  'lante-backup-verify':           'Restore Verification',
  'lante-backup-secrets-snapshot': 'Secrets Snapshot',
}

const STATUS_COLORS = {
  Running:    ['rgba(200,150,12,.1)',  '#C8960C'],
  Healthy:    ['rgba(52,211,153,.1)',  '#34d399'],
  Failed:     ['rgba(239,68,68,.1)',   '#f87171'],
  Suspended:  ['rgba(107,114,128,.1)', '#9ca3af'],
  'Never run':['rgba(107,114,128,.1)', '#9ca3af'],
}

// Same shape the reference provisioning page's status derives from, just for a CronJob instead of
// a per-service schema row: active > suspended > compare the two timestamps CronJobStatus already
// reports > never run at all.
function jobStatus(job) {
  if (job.active) return 'Running'
  if (job.suspended) return 'Suspended'
  if (!job.lastScheduleTime) return 'Never run'
  if (job.lastSuccessfulTime && new Date(job.lastSuccessfulTime) >= new Date(job.lastScheduleTime)) return 'Healthy'
  return 'Failed'
}

function fmt(ts) {
  return ts ? new Date(ts).toLocaleString() : '—'
}

export default function PlatformBackupsPage() {
  const [jobs, setJobs] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [triggering, setTriggering] = useState(null)   // job name currently being triggered, or null
  const [triggerMsg, setTriggerMsg] = useState(null)    // { name, text, isError }

  useEffect(() => { fetchStatus() }, [])

  function fetchStatus() {
    setError('')
    return api.get('/api/v1/platform/backups')
      .then(r => setJobs(r.data?.data ?? r.data ?? []))
      .catch(() => setError('Failed to load backup status.'))
      .finally(() => setLoading(false))
  }

  async function triggerJob(name) {
    setTriggerMsg(null)
    setTriggering(name)
    // Optimistic: the CronJob controller picks up the new Job's ownership within its own
    // reconcile loop, not instantly — mark it running locally rather than waiting on a refetch
    // to reflect something that hasn't propagated yet.
    setJobs(rows => rows.map(j => j.name === name ? { ...j, active: true } : j))
    try {
      const r = await api.post(`/api/v1/platform/backups/${name}/trigger`)
      const jobName = r.data?.data?.jobName
      setTriggerMsg({ name, text: jobName ? `Triggered — job ${jobName}` : 'Triggered.', isError: false })
    } catch (err) {
      const text = err.response?.data?.message || 'Trigger failed.'
      setTriggerMsg({ name, text, isError: true })
    } finally {
      setTriggering(null)
      await fetchStatus()
    }
  }

  return (
    <PlatformLayout>
      <div style={{ maxWidth: 900 }}>
        <div style={{ marginBottom: 24 }}>
          <h1 style={{ fontFamily: "'Inter',sans-serif", fontSize: 24, fontWeight: 700, color: '#1B3A5C' }}>Backups</h1>
          <p style={{ fontSize: 13, color: '#6b7280', marginTop: 4 }}>
            View status and manually trigger the platform's scheduled backup jobs. Restoring from a
            backup isn't done here — this only reports status and starts a run.
          </p>
        </div>

        <div style={{ background: '#FFFFFF', border: '1px solid #E8ECF0', borderRadius: 14, overflow: 'hidden' }}>
          <div style={{ padding: '18px 20px' }}>
            {loading ? (
              <p style={{ fontSize: 14, color: '#6b7280' }}>Loading backup status…</p>
            ) : error ? (
              <p style={{ fontSize: 14, color: '#f87171' }}>{error}</p>
            ) : jobs.length === 0 ? (
              <p style={{ fontSize: 14, color: '#4b5563' }}>No backup jobs found in this environment.</p>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {jobs.map(job => {
                  const status = jobStatus(job)
                  const [bg, color] = STATUS_COLORS[status] ?? STATUS_COLORS['Never run']
                  const isTriggering = triggering === job.name
                  const msg = triggerMsg?.name === job.name ? triggerMsg : null
                  return (
                    <div key={job.name} style={{ padding: '14px 16px', background: '#FFFFFF', borderRadius: 10, border: '1px solid #E8ECF0' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
                        <div style={{ flex: '1 1 220px', minWidth: 0 }}>
                          <p style={{ fontSize: 14, fontWeight: 600, color: '#1B3A5C' }}>{JOB_LABELS[job.name] ?? job.name}</p>
                          <p style={{ fontSize: 11, color: '#4b5563', fontFamily: 'monospace' }}>{job.name} · {job.schedule ?? '—'}</p>
                        </div>
                        <span style={{ fontSize: 11, fontWeight: 700, padding: '3px 9px', borderRadius: 100, background: bg, color, flexShrink: 0 }}>{status}</span>
                        <div style={{ flex: '1 1 260px', display: 'flex', gap: 20, fontSize: 12, color: '#4b5563' }}>
                          <span>Last run: {fmt(job.lastScheduleTime)}</span>
                          <span>Last success: {fmt(job.lastSuccessfulTime)}</span>
                        </div>
                        <button onClick={() => triggerJob(job.name)} disabled={isTriggering || job.active} style={{
                          padding: '8px 16px', borderRadius: 10,
                          background: isTriggering ? 'rgba(200,150,12,.08)' : 'rgba(200,150,12,.12)',
                          color: '#C8960C', border: '1px solid rgba(200,150,12,.25)',
                          fontSize: 13, fontWeight: 600, cursor: (isTriggering || job.active) ? 'not-allowed' : 'pointer',
                          fontFamily: "'Inter',sans-serif", opacity: (isTriggering || job.active) ? .6 : 1,
                          flexShrink: 0,
                        }}>
                          {isTriggering ? 'Triggering…' : 'Trigger Run'}
                        </button>
                      </div>
                      {msg && (
                        <p style={{ fontSize: 12, marginTop: 8, color: msg.isError ? '#f87171' : '#34d399' }}>{msg.text}</p>
                      )}
                    </div>
                  )
                })}
              </div>
            )}
          </div>
        </div>
      </div>
    </PlatformLayout>
  )
}
