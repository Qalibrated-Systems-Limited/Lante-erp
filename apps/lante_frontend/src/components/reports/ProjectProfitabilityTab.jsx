import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Alert, SectionHeader, DataTable, Loading, EmptyState, Select, Progress } from '../ui.jsx'
import { getProjectProfitability } from '../../services/reports.js'
import { exportToExcel, exportReportToPdf } from '../../utils/export.js'

export default function ProjectProfitabilityTab({ canExport }) {
  const [projectId, setProjectId] = useState('')
  const [portfolio, setPortfolio] = useState(null)   // { projects, totals } — no projectId selected
  const [detail, setDetail] = useState(null)         // single-project shape — projectId selected
  const [options, setOptions] = useState([{ value: '', label: 'All Projects (portfolio view)' }])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    setError(null)
    getProjectProfitability(projectId ? { projectId } : undefined)
      .then(res => {
        if (projectId) {
          setDetail(res)
        } else {
          setPortfolio(res)
          const projects = res?.projects ?? []
          setOptions([{ value: '', label: 'All Projects (portfolio view)' }, ...projects.map(p => ({ value: p.projectId, label: p.projectName }))])
        }
      })
      .catch(() => setError('Failed to load project profitability.'))
      .finally(() => setLoading(false))
  }, [projectId])

  useEffect(() => { load() }, [load])

  function exportPortfolio() {
    exportToExcel({
      title: 'Project Profitability — Portfolio',
      filename: 'project-profitability-portfolio',
      sheetName: 'Portfolio',
      columns: [
        { header: 'Project', accessor: r => r.projectName, width: 28 },
        { header: 'Planned Budget', accessor: r => r.plannedBudget, width: 18 },
        { header: 'Actual Cost', accessor: r => r.actualCost, width: 18 },
        { header: 'Remaining', accessor: r => r.remaining, width: 16 },
        { header: 'Utilisation %', accessor: r => r.utilizationPercent, width: 14 },
      ],
      rows: portfolio?.projects ?? [],
    })
  }
  function exportDetail() {
    exportToExcel({
      title: `Project Profitability — ${detail?.projectId ?? projectId}`,
      filename: `project-profitability-${projectId}`,
      sheetName: 'Lines',
      columns: [
        { header: 'Description', accessor: r => r.description ?? r.name ?? '—', width: 30 },
        { header: 'Budget', accessor: r => r.budget ?? r.plannedAmount ?? '', width: 16 },
        { header: 'Actual', accessor: r => r.actual ?? r.actualCost ?? '', width: 16 },
      ],
      rows: detail?.lines ?? [],
    })
  }
  function exportPortfolioPdf() {
    exportReportToPdf({
      title: 'Project Profitability — Portfolio',
      filename: 'project-profitability-portfolio',
      sections: [{
        heading: 'Portfolio',
        columns: [
          { header: 'Project', accessor: r => r.projectName },
          { header: 'Planned Budget', accessor: r => fmt.kes(r.plannedBudget) },
          { header: 'Actual Cost', accessor: r => fmt.kes(r.actualCost) },
          { header: 'Remaining', accessor: r => fmt.kes(r.remaining) },
          { header: 'Utilisation %', accessor: r => `${r.utilizationPercent ?? 0}%` },
        ],
        rows: portfolio?.projects ?? [],
      }],
    })
  }
  function exportDetailPdf() {
    exportReportToPdf({
      title: `Project Profitability — ${detail?.projectId ?? projectId}`,
      filename: `project-profitability-${projectId}`,
      summary: [
        { label: 'Planned Budget', value: fmt.kes(detail?.plannedBudget) },
        { label: 'Total Estimated', value: fmt.kes(detail?.totalEstimated) },
        { label: 'Actual Cost', value: fmt.kes(detail?.actualCost) },
        { label: 'Remaining', value: fmt.kes(detail?.remaining) },
      ],
      sections: [
        {
          heading: 'Budget Lines',
          columns: [
            { header: 'Description', accessor: r => r.description ?? r.name ?? '—' },
            { header: 'Budget', accessor: r => fmt.kes(r.budget ?? r.plannedAmount) },
            { header: 'Actual', accessor: r => fmt.kes(r.actual ?? r.actualCost) },
          ],
          rows: detail?.lines ?? [],
        },
        {
          heading: 'Milestone Breakdown',
          columns: [
            { header: 'Milestone', accessor: r => r.name ?? r.milestoneName ?? '—' },
            { header: 'Budget', accessor: r => fmt.kes(r.budget ?? r.plannedAmount) },
            { header: 'Actual', accessor: r => fmt.kes(r.actual ?? r.actualCost) },
            { header: 'Status', accessor: r => r.status ?? '—' },
          ],
          rows: detail?.milestoneBreakdown ?? [],
        },
      ],
    })
  }

  return (
    <>
      <div style={{ marginBottom: 16, display: 'flex', alignItems: 'flex-end', gap: 10, flexWrap: 'wrap' }}>
        <div style={{ minWidth: 280 }}>
          <Select label="Project" value={projectId} onChange={setProjectId} options={options} />
        </div>
        <Btn size="sm" onClick={load} style={{ marginBottom: 14 }}>Refresh</Btn>
      </div>

      {error && <Alert type="error">{error}</Alert>}

      {loading ? <Loading /> : projectId ? (
        !detail ? <EmptyState title="No data for this project" /> : (
          <>
            <SectionHeader title={`Project Profitability`} sub={detail.projectId}
              action={canExport && (detail.lines ?? []).length > 0 && <div style={{ display: 'flex', gap: 8 }}>
                <Btn size="sm" variant="ghost" onClick={exportDetail}>⬇ Export</Btn>
                <Btn size="sm" variant="ghost" onClick={exportDetailPdf}>⬇ PDF</Btn>
              </div>} />
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(180px,1fr))', gap: 14, marginBottom: 22 }}>
              <Card style={{ padding: '14px 16px' }}>
                <div style={{ fontSize: 11, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .6 }}>Planned Budget</div>
                <div style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{fmt.kes(detail.plannedBudget)}</div>
              </Card>
              <Card style={{ padding: '14px 16px' }}>
                <div style={{ fontSize: 11, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .6 }}>Total Estimated</div>
                <div style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{fmt.kes(detail.totalEstimated)}</div>
              </Card>
              <Card style={{ padding: '14px 16px' }}>
                <div style={{ fontSize: 11, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .6 }}>Actual Cost</div>
                <div style={{ fontSize: 18, fontWeight: 700, color: T.red }}>{fmt.kes(detail.actualCost)}</div>
              </Card>
              <Card style={{ padding: '14px 16px' }}>
                <div style={{ fontSize: 11, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .6 }}>Remaining</div>
                <div style={{ fontSize: 18, fontWeight: 700, color: (detail.remaining ?? 0) < 0 ? T.red : T.green }}>{fmt.kes(detail.remaining)}</div>
              </Card>
            </div>
            <div style={{ marginBottom: 22 }}>
              <div style={{ fontSize: 12, color: T.mgrey, marginBottom: 4 }}>Budget Utilisation — {(detail.utilizationPercent ?? 0)}%</div>
              <Progress value={(detail.utilizationPercent ?? 0) / 100} />
            </div>

            <SectionHeader title="Budget Lines" />
            <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 22 }}>
              <DataTable headers={['Description', 'Budget', 'Actual']} empty="No budget lines."
                rows={(detail.lines ?? []).map(l => [
                  l.description ?? l.name ?? '—', fmt.kes(l.budget ?? l.plannedAmount), fmt.kes(l.actual ?? l.actualCost),
                ])} />
            </Card>

            <SectionHeader title="Milestone Breakdown" />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable headers={['Milestone', 'Budget', 'Actual', 'Status']} empty="No milestone breakdown."
                rows={(detail.milestoneBreakdown ?? []).map(m => [
                  m.name ?? m.milestoneName ?? '—', fmt.kes(m.budget ?? m.plannedAmount), fmt.kes(m.actual ?? m.actualCost), m.status ?? '—',
                ])} />
            </Card>
          </>
        )
      ) : !portfolio || (portfolio.projects ?? []).length === 0 ? (
        <EmptyState title="No projects found" />
      ) : (
        <>
          <SectionHeader title="Project Profitability — Portfolio" sub="All projects: budget, actual cost and utilisation"
            action={canExport && <div style={{ display: 'flex', gap: 8 }}>
              <Btn size="sm" variant="ghost" onClick={exportPortfolio}>⬇ Export</Btn>
              <Btn size="sm" variant="ghost" onClick={exportPortfolioPdf}>⬇ PDF</Btn>
            </div>} />
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable headers={['Project', 'Planned Budget', 'Actual Cost', 'Remaining', 'Utilisation']}
              rows={(portfolio.projects ?? []).map(p => [
                <strong>{p.projectName}</strong>, fmt.kes(p.plannedBudget), fmt.kes(p.actualCost),
                <span style={{ color: (p.remaining ?? 0) < 0 ? T.red : T.dgrey }}>{fmt.kes(p.remaining)}</span>,
                <div style={{ minWidth: 120 }}>
                  <Progress value={(p.utilizationPercent ?? 0) / 100} />
                  <div style={{ fontSize: 11, color: T.mgrey, marginTop: 2 }}>{p.utilizationPercent ?? 0}%</div>
                </div>,
              ])} />
          </Card>
        </>
      )}
    </>
  )
}
