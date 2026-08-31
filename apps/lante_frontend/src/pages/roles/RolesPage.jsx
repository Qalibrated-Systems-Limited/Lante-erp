import { exportToPdf, exportToExcel, ROLE_COLUMNS } from '../../utils/export.js'
import { T } from '../../theme/tokens.js'
import { Card, Badge, Btn, Alert, Modal, Input, DataTable, SectionHeader, Loading, EmptyState } from '../../components/ui.jsx'
import { useRolesPage } from './useRolesPage.js'

const PERM_LABELS = {
  'system.admin': 'System Admin', 'departments.manage': 'Manage Departments',
  'users.read': 'View Users', 'users.write': 'Manage Users', 'users.delete': 'Delete Users',
  'roles.manage': 'Manage Roles', 'permissions.manage': 'Manage Permissions', 'settings.manage': 'Manage Settings',
  'tickets.read.all': 'View All Tickets', 'tickets.read.dept': 'View Dept Tickets', 'tickets.read.own': 'View Own Tickets',
  'tickets.write': 'Create & Edit Tickets', 'tickets.assign': 'Assign Tickets',
  'tickets.resolve': 'Resolve Tickets', 'tickets.delete': 'Delete Tickets',
  'projects.read.all': 'View All Projects', 'projects.read.dept': 'View Dept Projects', 'projects.read.own': 'View Own Projects',
  'projects.write': 'Create & Edit Projects', 'projects.approve': 'Approve Projects', 'projects.delete': 'Delete Projects',
  'finance.read': 'View Finance', 'finance.write': 'Create Finance Records',
  'finance.approve': 'Approve Financials', 'finance.reports': 'Finance Reports',
  'reports.view': 'View Reports', 'reports.export': 'Export Reports',
  'portal.manage': 'Manage Portal',
  'fleet.read': 'View Fleet', 'fleet.write': 'Manage Fleet', 'fleet.delete': 'Delete Fleet Records', 'fleet.expenses': 'Fleet Expenses',
  'technician.read': 'View Technicians', 'technician.write': 'Manage Technicians',
  'technician.delete': 'Delete Technician Records', 'technician.approve': 'Approve Technician Forms',
  'licensing.read': 'View Licenses', 'licensing.write': 'Manage Licenses', 'licensing.delete': 'Delete Licenses',
}

const PAD = 'clamp(16px, 2.4vw, 26px)'

export default function RolesPage() {
  const {
    roles,
    loading,
    error,

    modal, setModal,
    selected, setSelected,

    rolePerms,
    rolePermsLoading,

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
  } = useRolesPage()

  const tableRows = roles.map(role => [
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
      <strong>{role.name}</strong>
      {role.isSystem ? <Badge variant="default">System</Badge> : <Badge variant="amber">Custom</Badge>}
    </span>,
    role.description || '—',
    <Btn size="sm" variant="ghost" onClick={() => openPermissions(role)}>
      {(role.permissions ?? []).length} permission{(role.permissions ?? []).length !== 1 ? 's' : ''}
    </Btn>,
    <Btn size="sm" variant="ghost" onClick={() => openUsers(role)} style={{ background: T.blueL, color: T.blue }}>
      {role.userCount ?? 0} user{role.userCount !== 1 ? 's' : ''}
    </Btn>,
    role.isSystem ? (
      <span style={{ fontSize: 12, color: T.mgrey }} title="System roles are locked">🔒 Locked</span>
    ) : (
      <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
        <Btn size="sm" variant="ghost" onClick={() => { setSelected(role); setForm({ name: role.name, description: role.description ?? '' }); setFormError(''); setModal('edit') }}>Edit</Btn>
        <Btn size="sm" variant="danger" onClick={() => { setSelected(role); setFormError(''); setModal('delete') }}>Delete</Btn>
      </div>
    ),
  ])

  return (
    <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
      <SectionHeader
        title="Roles"
        sub={loading ? 'Loading…' : `${roles.length} role${roles.length !== 1 ? 's' : ''}`}
        action={
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <Btn variant="ghost" onClick={() => exportToPdf({ title: 'Roles Report', columns: ROLE_COLUMNS, rows: roles, filename: 'lante-roles' })}>PDF</Btn>
            <Btn variant="ghost" onClick={() => exportToExcel({ title: 'Roles', columns: ROLE_COLUMNS, rows: roles, filename: 'lante-roles', sheetName: 'Roles' })}>Excel</Btn>
            <Btn onClick={() => { setForm({ name: '', description: '' }); setFormError(''); setModal('create') }}>+ New Role</Btn>
          </div>
        }
      />

      {error && <Alert type="error">{error}</Alert>}

      {loading ? (
        <Loading />
      ) : roles.length === 0 ? (
        <EmptyState icon="🛡️" title="No roles yet" />
      ) : (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <DataTable headers={['Role', 'Description', 'Permissions', 'Users', 'Actions']} rows={tableRows} empty="No roles found." />
        </Card>
      )}

      {/* ── Create / Edit Modal ─────────────────────────────────────────────── */}
      {(modal === 'create' || modal === 'edit') && (
        <Modal title={modal === 'create' ? 'New Role' : 'Edit Role'} onClose={() => setModal(null)}>
          {formError && <Alert type="error">{formError}</Alert>}
          <Input label="Name" required value={form.name} onChange={v => setForm(f => ({ ...f, name: v }))} placeholder="e.g. Finance Manager" />
          <div style={{ marginBottom: 14 }}>
            <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>Description</label>
            <textarea
              value={form.description}
              onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
              rows={3}
              placeholder="What can this role do?"
              style={{ width: '100%', padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, color: T.dgrey, outline: 'none', boxSizing: 'border-box', resize: 'none', fontFamily: 'inherit' }}
            />
          </div>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn disabled={submitting} onClick={handleSave}>{submitting ? 'Saving…' : 'Save'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── Delete Modal ────────────────────────────────────────────────────── */}
      {modal === 'delete' && (
        <Modal title="Delete Role" onClose={() => setModal(null)}>
          {formError && <Alert type="error">{formError}</Alert>}
          <p style={{ fontSize: 13, color: T.dgrey }}>
            Delete role <strong>{selected?.name}</strong>? This will affect all users assigned to this role.
          </p>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn variant="danger" disabled={submitting} onClick={handleDelete}>{submitting ? 'Deleting…' : 'Delete'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── Permissions Modal ───────────────────────────────────────────────── */}
      {modal === 'permissions' && (
        <Modal title={`Permissions — ${selected?.name}`} onClose={() => setModal(null)} width={700}>
          <p style={{ fontSize: 12, color: T.mgrey, marginTop: -8, marginBottom: 16 }}>
            {selected?.isSystem
              ? `${rolePerms.length} assigned · read-only (system role)`
              : `${rolePerms.length} assigned · ${unassigned.length} available`}
          </p>

          {rolePermsLoading ? (
            <Loading />
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              {/* Assigned */}
              <div>
                <div style={{ fontSize: 11, fontWeight: 700, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>
                  Assigned ({rolePerms.length})
                </div>
                {rolePerms.length === 0 ? (
                  <p style={{ fontSize: 12, color: T.mgrey }}>None assigned.</p>
                ) : (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, maxHeight: 280, overflowY: 'auto' }}>
                    {rolePerms.map(p => (
                      <div key={p.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: T.greenL, borderRadius: 7, padding: '8px 10px', gap: 8 }}>
                        <div style={{ minWidth: 0 }}>
                          <p style={{ fontSize: 12, fontWeight: 700, color: T.green, margin: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{PERM_LABELS[p.name] ?? p.name}</p>
                          <p style={{ fontSize: 11, fontFamily: T.mono, color: T.green, opacity: .7, margin: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{p.name}</p>
                        </div>
                        {!selected?.isSystem && (
                          <button onClick={() => removePermission(p.id)} title="Remove" style={{ background: 'none', border: 'none', color: T.red, cursor: 'pointer', fontSize: 16, lineHeight: 1, padding: 0, flexShrink: 0 }}>×</button>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Available */}
              {selected?.isSystem ? (
                <div>
                  <div style={{ fontSize: 11, fontWeight: 700, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Available</div>
                  <Alert type="info">This is a system role. Its permissions are fixed and can't be changed. Create a custom role to define your own permission set.</Alert>
                </div>
              ) : (
                <div>
                  <div style={{ fontSize: 11, fontWeight: 700, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>
                    Available ({unassigned.length})
                  </div>
                  {unassigned.length === 0 ? (
                    <p style={{ fontSize: 12, color: T.mgrey }}>All assigned.</p>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4, maxHeight: 280, overflowY: 'auto' }}>
                      {unassigned.map(p => (
                        <div key={p.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: T.offwt, borderRadius: 7, padding: '8px 10px', gap: 8 }}>
                          <div style={{ minWidth: 0 }}>
                            <p style={{ fontSize: 12, fontWeight: 700, color: T.dgrey, margin: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{PERM_LABELS[p.name] ?? p.name}</p>
                            <p style={{ fontSize: 11, fontFamily: T.mono, color: T.mgrey, margin: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{p.name}</p>
                          </div>
                          <button onClick={() => addPermission(p.id)} title="Add" style={{ background: 'none', border: 'none', color: T.green, cursor: 'pointer', fontSize: 16, lineHeight: 1, padding: 0, flexShrink: 0 }}>+</button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}

          <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Done</Btn>
          </div>
        </Modal>
      )}

      {/* ── Users Modal ─────────────────────────────────────────────────────── */}
      {modal === 'users' && (
        <Modal title={`Users — ${selected?.name}`} onClose={() => setModal(null)} width={700}>
          <p style={{ fontSize: 12, color: T.mgrey, marginTop: -8, marginBottom: 12 }}>
            {usersLoading ? 'Loading…' : `${assignedUsers.length} assigned · ${unassignedUsers.length} available`}
          </p>

          <Input value={userSearch} onChange={setUserSearch} placeholder="Search by name or email…" />

          {usersLoading ? (
            <Loading />
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              {/* Assigned users */}
              <div>
                <div style={{ fontSize: 11, fontWeight: 700, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>
                  Assigned ({filteredAssigned.length})
                </div>
                {filteredAssigned.length === 0 ? (
                  <p style={{ fontSize: 12, color: T.mgrey }}>{userSearch ? 'No matches.' : 'No users assigned.'}</p>
                ) : (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, maxHeight: 280, overflowY: 'auto' }}>
                    {filteredAssigned.map(u => (
                      <div key={u.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: T.blueL, borderRadius: 7, padding: '8px 10px', gap: 8 }}>
                        <div style={{ minWidth: 0 }}>
                          <p style={{ fontSize: 12, fontWeight: 700, color: T.blue, margin: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{u.firstName} {u.lastName}</p>
                          <p style={{ fontSize: 11, color: T.blue, opacity: .7, margin: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{u.email}</p>
                        </div>
                        <button onClick={() => removeUserFromRole(u.id)} title="Remove from role" style={{ background: 'none', border: 'none', color: T.red, cursor: 'pointer', fontSize: 16, lineHeight: 1, padding: 0, flexShrink: 0 }}>×</button>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Available users */}
              <div>
                <div style={{ fontSize: 11, fontWeight: 700, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>
                  Available ({filteredUnassigned.length})
                </div>
                {filteredUnassigned.length === 0 ? (
                  <p style={{ fontSize: 12, color: T.mgrey }}>{userSearch ? 'No matches.' : 'All users assigned.'}</p>
                ) : (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, maxHeight: 280, overflowY: 'auto' }}>
                    {filteredUnassigned.map(u => (
                      <div key={u.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: T.offwt, borderRadius: 7, padding: '8px 10px', gap: 8 }}>
                        <div style={{ minWidth: 0 }}>
                          <p style={{ fontSize: 12, fontWeight: 700, color: T.dgrey, margin: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{u.firstName} {u.lastName}</p>
                          <p style={{ fontSize: 11, color: T.mgrey, margin: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{u.email}</p>
                        </div>
                        <button onClick={() => assignUserToRole(u.id)} title="Assign to role" style={{ background: 'none', border: 'none', color: T.green, cursor: 'pointer', fontSize: 16, lineHeight: 1, padding: 0, flexShrink: 0 }}>+</button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Done</Btn>
          </div>
        </Modal>
      )}
    </div>
  )
}
