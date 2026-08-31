import { exportToPdf, exportToExcel, USER_COLUMNS } from '../../utils/export.js'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, Input, Select, Modal, DataTable, SectionHeader, Loading, EmptyState } from '../../components/ui.jsx'
import { useUsersPage } from './useUsersPage.js'

const PAD = 'clamp(16px, 2.4vw, 26px)'

export default function UsersPage() {
  const s = useUsersPage()
  const {
    navigate,
    users, loading, error, page, totalPages, total, search, setSearch, setPage,
    showDeleted, setShowDeleted,
    roles, departments, branches,
    modal, setModal,
    selected, setSelected,
    form, setForm,
    submitting,
    formError, setFormError,
    actionMsg,
    load,
    openCreate,
    toggleRoleId,
    handleCreate,
    toggleTwoFactor,
    handleResendInvite,
    handleDelete,
    handleResetPassword,
    handleRestore,
  } = s

  const headers = ['Name', 'Email', 'Department', 'Branch', 'Roles', 'Status', 'Actions']

  const rows = users.map(user => [
    <div key="name" style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
      <div style={{
        width: 30, height: 30, borderRadius: '50%', flexShrink: 0,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 11, fontWeight: 700,
        background: showDeleted ? T.redL : T.offwt, color: showDeleted ? T.red : T.navy,
      }}>
        {user.firstName?.[0]}{user.lastName?.[0]}
      </div>
      <div>
        <p style={{ margin: 0, fontWeight: 600, color: showDeleted ? T.mgrey : T.dgrey, textDecoration: showDeleted ? 'line-through' : 'none' }}>
          {user.firstName} {user.lastName}
        </p>
        {showDeleted && <p style={{ margin: 0, fontSize: 11, color: T.red, fontWeight: 600 }}>Deleted</p>}
      </div>
    </div>,
    user.email,
    user.departmentName ?? <span style={{ color: T.mgrey }}>—</span>,
    user.branchName
      ? <Badge variant="amber">{user.branchName}</Badge>
      : user.isCompanyAdmin
        ? <Badge variant="default">Company Admin</Badge>
        : <span style={{ color: T.mgrey }}>—</span>,
    user.roles?.length > 0
      ? <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>{user.roles.map(r => <Badge key={r} variant="default">{r}</Badge>)}</div>
      : <span style={{ color: T.mgrey, fontSize: 12 }}>No roles</span>,
    showDeleted
      ? <Badge variant="red">Deleted</Badge>
      : (!user.isActive && user.isFirstLogin)
        ? <Badge variant="amber">Pending Invite</Badge>
        : <Badge variant={user.isActive ? 'green' : 'default'}>{user.isActive ? 'Active' : 'Inactive'}</Badge>,
    showDeleted
      ? <Btn variant="green" size="sm" onClick={() => { setSelected(user); setFormError(''); setModal('restore') }}>Restore</Btn>
      : (
        <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end', flexWrap: 'wrap' }}>
          <Btn variant="ghost" size="sm" onClick={() => navigate(`/modules/users/${user.id}`)}>View</Btn>
          <Btn variant="ghost" size="sm" onClick={() => toggleTwoFactor(user)}>{user.twoFactorEnabled ? 'Disable 2FA' : 'Enable 2FA'}</Btn>
          <Btn variant="ghost" size="sm" onClick={() => { setSelected(user); setFormError(''); setModal('resetpw') }}>Reset PW</Btn>
          {!user.isActive && user.isFirstLogin && (
            <Btn variant="ghost" size="sm" onClick={() => handleResendInvite(user)}>Resend Invite</Btn>
          )}
          <Btn variant="danger" size="sm" onClick={() => { setSelected(user); setFormError(''); setModal('delete') }}>Delete</Btn>
        </div>
      ),
  ])

  return (
    <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
      <SectionHeader
        title={showDeleted ? 'Deleted Users' : 'Users'}
        sub={loading ? 'Loading…' : `${total} user${total !== 1 ? 's' : ''}${showDeleted ? ' deleted' : ''}`}
        action={
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <Btn variant={showDeleted ? 'danger' : 'ghost'} size="sm" onClick={() => { setShowDeleted(v => !v); setPage(1); setSearch('') }}>
              {showDeleted ? 'View Active' : 'Deleted Users'}
            </Btn>
            <Btn variant="ghost" size="sm" onClick={() => navigate('/modules/admin?tab=roles')}>Roles</Btn>
            <Btn variant="ghost" size="sm" onClick={() => navigate('/modules/admin?tab=roles')}>Permissions</Btn>
            <Btn variant="ghost" size="sm" onClick={() => exportToPdf({ title: 'Users Report', columns: USER_COLUMNS, rows: users, filename: 'lante-users' })}>PDF</Btn>
            <Btn variant="ghost" size="sm" onClick={() => exportToExcel({ title: 'Users', columns: USER_COLUMNS, rows: users, filename: 'lante-users', sheetName: 'Users' })}>Excel</Btn>
            {!showDeleted && <Btn variant="gold" size="sm" onClick={openCreate}>+ New User</Btn>}
          </div>
        }
      />

      {actionMsg && <Alert type="success">{actionMsg}</Alert>}
      {error && <Alert type="error">{error}</Alert>}

      <Card style={{ marginBottom: 16 }}>
        <form onSubmit={e => { e.preventDefault(); setPage(1); load() }} style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
          <div style={{ flex: 1 }}>
            <Input value={search} onChange={setSearch} placeholder="Search by name or email…" />
          </div>
          <Btn type="submit">Search</Btn>
          {search && <Btn type="button" variant="ghost" onClick={() => { setSearch(''); setPage(1) }}>Clear</Btn>}
        </form>
      </Card>

      {loading ? (
        <Loading />
      ) : (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <DataTable
            headers={headers}
            rows={rows}
            empty={
              <EmptyState
                icon={showDeleted ? '🗑️' : '👥'}
                title={showDeleted ? 'No deleted users' : 'No users found'}
                sub={showDeleted ? 'Deleted users will appear here.' : 'Create a user or adjust your search.'}
              />
            }
          />
        </Card>
      )}

      {totalPages > 1 && (
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 16 }}>
          <p style={{ fontSize: 13, color: T.mgrey, margin: 0 }}>Page {page} of {totalPages}</p>
          <div style={{ display: 'flex', gap: 8 }}>
            <Btn variant="ghost" size="sm" disabled={page === 1} onClick={() => setPage(p => p - 1)}>Previous</Btn>
            <Btn variant="ghost" size="sm" disabled={page === totalPages} onClick={() => setPage(p => p + 1)}>Next</Btn>
          </div>
        </div>
      )}

      {/* Create Modal */}
      {modal === 'create' && (
        <Modal title="New User" onClose={() => setModal(null)}>
          {formError && <Alert type="error">{formError}</Alert>}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <Input label="First Name" required value={form.firstName} onChange={v => setForm(f => ({ ...f, firstName: v }))} placeholder="Jane" />
            <Input label="Last Name" required value={form.lastName} onChange={v => setForm(f => ({ ...f, lastName: v }))} placeholder="Doe" />
          </div>
          <Input label="Email" type="email" required value={form.email} onChange={v => setForm(f => ({ ...f, email: v }))} placeholder="jane@company.com" />
          <Input label="Mobile Number" value={form.mobileNumber} onChange={v => setForm(f => ({ ...f, mobileNumber: v }))} placeholder="+254700000000" />
          <Select
            label="Department"
            value={form.departmentId}
            onChange={v => setForm(f => ({ ...f, departmentId: v }))}
            options={[{ value: '', label: 'None' }, ...departments.map(d => ({ value: d.id, label: d.name }))]}
          />
          {branches.length > 0 && (
            <Select
              label="Branch"
              value={form.branchId}
              onChange={v => setForm(f => ({ ...f, branchId: v }))}
              options={[
                { value: '', label: '— Select Branch —' },
                ...branches.filter(b => b.isActive).map(b => ({ value: b.id, label: `${b.name}${b.isHeadOffice ? ' (HQ)' : ''}` })),
              ]}
            />
          )}
          {roles.length > 0 && (
            <div style={{ marginBottom: 14 }}>
              <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>Assign Roles</label>
              <div style={{ border: `1px solid ${T.lgrey}`, borderRadius: 7, padding: 12, maxHeight: 192, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 8 }}>
                {roles.map(role => (
                  <label key={role.id} style={{ display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer' }}>
                    <input
                      type="checkbox"
                      checked={form.roleIds.includes(role.id)}
                      onChange={() => toggleRoleId(role.id)}
                      style={{ width: 16, height: 16, accentColor: T.gold }}
                    />
                    <div>
                      <p style={{ margin: 0, fontSize: 13, fontWeight: 500, color: T.dgrey }}>{role.name}</p>
                      {role.description && <p style={{ margin: 0, fontSize: 11, color: T.mgrey }}>{role.description}</p>}
                    </div>
                  </label>
                ))}
              </div>
              {form.roleIds.length > 0 && (
                <p style={{ fontSize: 11, color: T.gold, marginTop: 6 }}>{form.roleIds.length} role{form.roleIds.length !== 1 ? 's' : ''} selected</p>
              )}
            </div>
          )}
          <ModalActions onCancel={() => setModal(null)} onConfirm={handleCreate} loading={submitting} confirmLabel="Create User" />
        </Modal>
      )}

      {/* Delete Modal */}
      {modal === 'delete' && (
        <Modal title="Delete User" onClose={() => setModal(null)}>
          {formError && <Alert type="error">{formError}</Alert>}
          <p style={{ fontSize: 13, color: T.dgrey }}>Delete <strong>{selected?.firstName} {selected?.lastName}</strong>? They will be soft-deleted and can be restored.</p>
          <ModalActions onCancel={() => setModal(null)} onConfirm={handleDelete} loading={submitting} confirmLabel="Delete" danger />
        </Modal>
      )}

      {/* Reset Password Modal */}
      {modal === 'resetpw' && (
        <Modal title="Reset Password" onClose={() => setModal(null)}>
          {formError && <Alert type="error">{formError}</Alert>}
          <p style={{ fontSize: 13, color: T.dgrey }}>Send a password reset to <strong>{selected?.email}</strong>?</p>
          <ModalActions onCancel={() => setModal(null)} onConfirm={handleResetPassword} loading={submitting} confirmLabel="Send Reset" />
        </Modal>
      )}

      {/* Restore Modal */}
      {modal === 'restore' && (
        <Modal title="Restore User" onClose={() => setModal(null)}>
          {formError && <Alert type="error">{formError}</Alert>}
          <p style={{ fontSize: 13, color: T.dgrey }}>
            Restore <strong>{selected?.firstName} {selected?.lastName}</strong>? They will be reactivated and can log in again.
          </p>
          <ModalActions onCancel={() => setModal(null)} onConfirm={handleRestore} loading={submitting} confirmLabel="Restore User" />
        </Modal>
      )}
    </div>
  )
}

function ModalActions({ onCancel, onConfirm, loading, confirmLabel, danger }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 20 }}>
      <Btn variant="ghost" onClick={onCancel}>Cancel</Btn>
      <Btn variant={danger ? 'danger' : 'gold'} onClick={onConfirm} disabled={loading}>{loading ? 'Saving…' : confirmLabel}</Btn>
    </div>
  )
}
