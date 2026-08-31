import { exportToPdf, exportToExcel, DEPARTMENT_COLUMNS } from '../../utils/export.js'
import { T } from '../../theme/tokens.js'
import { Card, Badge, Btn, Alert, Input, Modal, DataTable, SectionHeader, Loading, Tabs, EmptyState } from '../../components/ui.jsx'
import { useDepartmentsPage } from './useDepartmentsPage.js'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const TABS = [
  { id: 'departments', label: 'Departments' },
  { id: 'branches', label: 'Branches' },
]

export default function DepartmentsPage() {
  const s = useDepartmentsPage()

  const departmentRows = s.departments.map(dept => {
    const canInteract = s.canInteractWithDepartment(dept.id)
    const dim = { opacity: canInteract ? 1 : 0.5 }
    return [
      <strong style={dim}>{dept.name}</strong>,
      <span style={dim}>{dept.description || '—'}</span>,
      dept.departmentGroupId
        ? <Badge variant="purple">{dept.departmentGroupId}</Badge>
        : <span style={{ color: T.mgrey, fontSize: 12 }}>—</span>,
      canInteract ? (
        <Btn size="sm" variant="ghost" onClick={() => s.openUsers(dept)} style={{ background: T.blueL, color: T.blue }}>
          {dept.userCount ?? 0} user{dept.userCount !== 1 ? 's' : ''}
        </Btn>
      ) : (
        <span style={{ fontSize: 12, color: T.mgrey }}>{dept.userCount ?? 0} user{dept.userCount !== 1 ? 's' : ''}</span>
      ),
      <Badge variant={dept.isActive ? 'green' : 'default'}>{dept.isActive ? 'Active' : 'Inactive'}</Badge>,
      canInteract ? (
        <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
          <Btn size="sm" variant="ghost" onClick={() => s.openEdit(dept)}>Edit</Btn>
          <Btn size="sm" variant="danger" onClick={() => s.openDelete(dept)}>Delete</Btn>
        </div>
      ) : null,
    ]
  })

  const branchRows = s.branches.map(b => [
    <span style={{ fontFamily: 'monospace', fontSize: 11, color: T.mgrey }}>{b.code || '—'}</span>,
    <strong>{b.name}</strong>,
    b.isHeadOffice ? <Badge variant="navy">Head Office</Badge> : <Badge variant="default">Branch</Badge>,
    <Badge variant={b.isActive ? 'green' : 'default'}>{b.isActive ? 'Active' : 'Inactive'}</Badge>,
  ])

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <SectionHeader
          title="Departments & Branches"
          sub={s.tab === 'departments'
            ? (s.loading ? 'Loading…' : `${s.departments.length} department${s.departments.length !== 1 ? 's' : ''}`)
            : (s.branchesLoading ? 'Loading…' : `${s.branches.length} branch${s.branches.length !== 1 ? 'es' : ''}`)}
          action={s.tab === 'departments' ? (
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <Btn variant="ghost" onClick={() => exportToPdf({ title: 'Departments Report', columns: DEPARTMENT_COLUMNS, rows: s.departments, filename: 'lante-departments' })}>PDF</Btn>
              <Btn variant="ghost" onClick={() => exportToExcel({ title: 'Departments', columns: DEPARTMENT_COLUMNS, rows: s.departments, filename: 'lante-departments', sheetName: 'Departments' })}>Excel</Btn>
              <Btn onClick={s.openCreate}>+ New Department</Btn>
            </div>
          ) : null}
        />

        <Tabs tabs={TABS} active={s.tab} setActive={s.setTab} />

        {s.tab === 'departments' && (
          <>
            {s.error && <Alert type="error">{s.error}</Alert>}
            {s.loading ? (
              <Loading />
            ) : s.departments.length === 0 ? (
              <EmptyState icon="🏢" title="No departments yet" sub="Create your first department to get started." />
            ) : (
              <Card style={{ padding: 0, overflow: 'hidden' }}>
                <DataTable
                  headers={['Department', 'Description', 'Visibility Group', 'Users', 'Status', 'Actions']}
                  rows={departmentRows}
                  empty="No departments found."
                />
              </Card>
            )}
          </>
        )}

        {s.tab === 'branches' && (
          <>
            {s.branchesError && <Alert type="error">{s.branchesError}</Alert>}
            {s.branchesLoading ? (
              <Loading />
            ) : s.branches.length === 0 ? (
              <EmptyState icon="🏬" title="No branches yet" sub="Branches are managed at the tenant/platform level." />
            ) : (
              <Card style={{ padding: 0, overflow: 'hidden' }}>
                <DataTable
                  headers={['Code', 'Branch Name', 'Type', 'Status']}
                  rows={branchRows}
                  empty="No branches found."
                />
              </Card>
            )}
          </>
        )}
      </div>

      {/* Create / Edit Modal */}
      {(s.modal === 'create' || s.modal === 'edit') && (
        <Modal title={s.modal === 'create' ? 'New Department' : 'Edit Department'} onClose={() => s.setModal(null)}>
          {s.formError && <Alert type="error">{s.formError}</Alert>}
          <Input
            label="Name"
            required
            value={s.form.name}
            onChange={v => s.setForm(f => ({ ...f, name: v }))}
            placeholder="e.g. Engineering"
          />
          <div style={{ marginBottom: 14 }}>
            <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>Description</label>
            <textarea
              value={s.form.description}
              onChange={e => s.setForm(f => ({ ...f, description: e.target.value }))}
              rows={3}
              placeholder="Optional description…"
              style={{ width: '100%', padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, color: T.dgrey, outline: 'none', boxSizing: 'border-box', resize: 'none', fontFamily: 'inherit' }}
            />
          </div>
          <Input
            label="Visibility Group"
            value={s.form.departmentGroupId}
            onChange={v => s.setForm(f => ({ ...f, departmentGroupId: v }))}
            placeholder="e.g. technical-group"
            note="Departments sharing the same group ID can see each other's data. Leave blank for no grouping."
          />
          <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', marginBottom: 4 }}>
            <input
              type="checkbox"
              checked={s.form.isActive}
              onChange={e => s.setForm(f => ({ ...f, isActive: e.target.checked }))}
              style={{ width: 16, height: 16, accentColor: T.gold }}
            />
            <span style={{ fontSize: 13, fontWeight: 600, color: T.dgrey }}>Active</span>
          </label>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
            <Btn variant="ghost" onClick={() => s.setModal(null)}>Cancel</Btn>
            <Btn disabled={s.submitting} onClick={s.handleSave}>{s.submitting ? 'Saving…' : 'Save'}</Btn>
          </div>
        </Modal>
      )}

      {/* Delete Modal */}
      {s.modal === 'delete' && (
        <Modal title="Delete Department" onClose={() => s.setModal(null)}>
          {s.formError && <Alert type="error">{s.formError}</Alert>}
          <p style={{ fontSize: 13, color: T.dgrey }}>
            Are you sure you want to delete <strong>{s.selected?.name}</strong>? This action cannot be undone.
          </p>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
            <Btn variant="ghost" onClick={() => s.setModal(null)}>Cancel</Btn>
            <Btn variant="danger" disabled={s.submitting} onClick={s.handleDelete}>{s.submitting ? 'Deleting…' : 'Delete'}</Btn>
          </div>
        </Modal>
      )}

      {/* Users in Department Modal */}
      {s.modal === 'users' && (
        <Modal title={`Users — ${s.selected?.name}`} onClose={() => s.setModal(null)} width={640}>
          {s.deptUsersLoading ? (
            <Loading />
          ) : s.deptUsers.length === 0 ? (
            <EmptyState icon="👥" title="No users in this department." />
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column' }}>
              {s.deptUsers.map(u => (
                <div key={u.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '10px 2px', borderBottom: `1px solid ${T.lgrey}` }}>
                  <div>
                    <p style={{ fontSize: 13, fontWeight: 700, color: T.dgrey, margin: 0 }}>{u.firstName} {u.lastName}</p>
                    <p style={{ fontSize: 12, color: T.mgrey, margin: 0 }}>{u.email}</p>
                  </div>
                  <Badge variant={u.isActive ? 'green' : 'default'}>{u.isActive ? 'Active' : 'Inactive'}</Badge>
                </div>
              ))}
            </div>
          )}
          <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
            <Btn variant="ghost" onClick={() => s.setModal(null)}>Close</Btn>
          </div>
        </Modal>
      )}
    </>
  )
}
