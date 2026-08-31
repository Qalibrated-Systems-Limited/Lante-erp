import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import OperationsAlertsPanel from '../../components/operations/overview/OperationsAlertsPanel.jsx'

const STATUS_BADGE = {
  Active: 'bg-green-100 text-green-700',
  Planning: 'bg-amber-100 text-amber-700',
  Draft: 'bg-gray-100 text-gray-600',
  PendingMdApproval: 'bg-orange-100 text-orange-700',
  PendingFinanceApproval: 'bg-orange-100 text-orange-700',
  OnHold: 'bg-red-100 text-red-600',
  Completed: 'bg-indigo-100 text-indigo-700',
  Closed: 'bg-gray-200 text-gray-600',
  Pending: 'bg-amber-100 text-amber-700',
  InProgress: 'bg-blue-100 text-blue-700',
  Accepted: 'bg-green-100 text-green-700',
  Cancelled: 'bg-red-100 text-red-600',
}

export default function OperationsOverviewPage() {
  const navigate = useNavigate()
  const [stats, setStats] = useState({
    totalProjects: 0, activeProjects: 0,
    totalAssignments: 0, pendingAssignments: 0,
    inProgressAssignments: 0, completedAssignments: 0,
  })
  const [recentAssignments, setRecentAssignments] = useState([])
  const [recentProjects, setRecentProjects] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [projectsRes, assignmentsRes] = await Promise.all([
          api.get('/api/v1/projects?pageSize=5').catch(() => ({ data: { data: { items: [], totalCount: 0 } } })),
          api.get('/api/v1/assignments?pageSize=5').catch(() => ({ data: { data: { items: [], totalCount: 0 } } })),
        ])
        const projects = projectsRes.data?.data?.items ?? []
        const assignments = assignmentsRes.data?.data?.items ?? []
        setRecentProjects(projects)
        setRecentAssignments(assignments)
        setStats({
          totalProjects: projectsRes.data?.data?.totalCount ?? 0,
          activeProjects: projects.filter(p => p.status === 'Active').length,
          totalAssignments: assignmentsRes.data?.data?.totalCount ?? 0,
          pendingAssignments: assignments.filter(a => a.status === 'Pending').length,
          inProgressAssignments: assignments.filter(a => a.status === 'InProgress').length,
          completedAssignments: assignments.filter(a => a.status === 'Completed').length,
        })
      } finally {
        setLoading(false)
      }
    }
    fetchData()
  }, [])

  const KPI_CARDS = [
    { label: 'Total Projects',      value: stats.totalProjects,          icon: '📁', color: 'text-amber-600' },
    { label: 'Active Projects',     value: stats.activeProjects,         icon: '✅', color: 'text-green-600' },
    { label: 'Total Assignments',   value: stats.totalAssignments,       icon: '📋', color: 'text-blue-600' },
    { label: 'Pending',             value: stats.pendingAssignments,     icon: '⏳', color: 'text-orange-500' },
    { label: 'In Progress',         value: stats.inProgressAssignments,  icon: '🔄', color: 'text-indigo-600' },
    { label: 'Completed',           value: stats.completedAssignments,   icon: '🎯', color: 'text-emerald-600' },
  ]

  return (
    <>
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        {/* Header */}
        <div className="mb-6">
          <h1 className="text-2xl font-extrabold text-zinc-950">Operations Overview</h1>
          <p className="text-sm text-gray-500 mt-0.5">Monitor projects, assignments, and field activity across departments.</p>
        </div>

        {/* KPI Cards */}
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-4 mb-6">
          {KPI_CARDS.map(card => (
            <div key={card.label} className="bg-white rounded-xl border border-gray-200 p-4">
              <div className="flex items-start justify-between mb-2">
                <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider leading-tight">{card.label}</p>
                <span className="text-xl">{card.icon}</span>
              </div>
              <p className={`text-2xl font-extrabold ${card.color}`}>{loading ? '—' : card.value}</p>
            </div>
          ))}
        </div>

        {/* O11.7 — worker-engine alert strip */}
        <OperationsAlertsPanel />

        {/* Two-column layout */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Recent Projects */}
          <div className="bg-white rounded-xl border border-gray-200 p-5">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-sm font-bold text-gray-700 uppercase tracking-wider">Recent Projects</h2>
              <button
                onClick={() => navigate('/modules/operations/projects')}
                className="text-xs font-medium text-amber-600 hover:text-amber-800 transition-colors"
              >
                View all →
              </button>
            </div>
            {loading ? (
              <div className="space-y-3">{[1,2,3].map(i => <div key={i} className="h-10 bg-gray-100 rounded-lg animate-pulse" />)}</div>
            ) : recentProjects.length === 0 ? (
              <p className="text-sm text-gray-400 py-4 text-center">No projects yet.</p>
            ) : (
              <div className="divide-y divide-gray-50">
                {recentProjects.map(p => (
                  <div
                    key={p.id}
                    onClick={() => navigate(`/modules/operations/projects/${p.id}`)}
                    className="flex items-center justify-between py-3 cursor-pointer hover:bg-amber-50 -mx-2 px-2 rounded-lg transition-colors"
                  >
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-gray-900 truncate">{p.name}</p>
                      <p className="text-xs text-gray-400">{p.clientName ?? 'No client'}</p>
                    </div>
                    <span className={`ml-3 shrink-0 px-2 py-0.5 rounded-full text-xs font-semibold ${STATUS_BADGE[p.status] ?? 'bg-gray-100 text-gray-600'}`}>
                      {p.status}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Recent Assignments */}
          <div className="bg-white rounded-xl border border-gray-200 p-5">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-sm font-bold text-gray-700 uppercase tracking-wider">Recent Assignments</h2>
              <button
                onClick={() => navigate('/modules/operations/assignments')}
                className="text-xs font-medium text-amber-600 hover:text-amber-800 transition-colors"
              >
                View all →
              </button>
            </div>
            {loading ? (
              <div className="space-y-3">{[1,2,3].map(i => <div key={i} className="h-10 bg-gray-100 rounded-lg animate-pulse" />)}</div>
            ) : recentAssignments.length === 0 ? (
              <p className="text-sm text-gray-400 py-4 text-center">No assignments yet.</p>
            ) : (
              <div className="divide-y divide-gray-50">
                {recentAssignments.map(a => (
                  <div
                    key={a.id}
                    onClick={() => navigate(`/modules/operations/assignments/${a.id}`)}
                    className="flex items-center justify-between py-3 cursor-pointer hover:bg-amber-50 -mx-2 px-2 rounded-lg transition-colors"
                  >
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-gray-900 truncate">{a.title}</p>
                      <p className="text-xs text-gray-400">{a.departmentType} · {a.sourceType}</p>
                    </div>
                    <span className={`ml-3 shrink-0 px-2 py-0.5 rounded-full text-xs font-semibold ${STATUS_BADGE[a.status] ?? 'bg-gray-100 text-gray-600'}`}>
                      {a.status}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </main>
    </>
  )
}
