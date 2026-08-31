// src/components/SelectWithAdd.jsx — a Select with an inline "+" quick-create button.
//
// Lets a form (e.g. New Item, Record Sale, Create Trip) create a missing
// reference-data record (Supplier, Item, Material, Vehicle Class, …) in a
// nested modal without losing the parent form's in-progress state. The
// caller supplies the quick-add form body and submit logic — this component
// only owns the "+" button, the nested modal shell, and loading/error state.
import { useState } from 'react';
import { Select, Btn, Modal, Alert } from './ui.jsx';
import { T } from '../theme/tokens.js';

export default function SelectWithAdd({
  label, value, onChange, options, required, style,
  addTitle,
  initialQuickAddForm = {},
  renderQuickAddForm,
  onQuickAddSubmit,
  onQuickAddCreated,
  getOptionFromRecord,
}) {
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState(initialQuickAddForm);
  const [saving, setSaving] = useState(false);
  const [err, setErr] = useState('');
  const [hover, setHover] = useState(false);

  function openModal() {
    setForm(initialQuickAddForm);
    setErr('');
    setOpen(true);
  }

  async function handleCreate() {
    setErr('');
    setSaving(true);
    try {
      const record = await onQuickAddSubmit(form);
      const opt = getOptionFromRecord(record);
      onQuickAddCreated?.(record);
      onChange(opt.value);
      setOpen(false);
    } catch (e) {
      setErr(e.response?.data?.message ?? e.message ?? `Failed to create ${addTitle}.`);
    } finally {
      setSaving(false);
    }
  }

  return (
    <div style={{ marginBottom: 14, ...style }}>
      <div style={{ display: 'flex', alignItems: 'flex-end', gap: 8, flexWrap: 'wrap' }}>
        <div style={{ flex: 1, minWidth: 160 }}>
          <Select label={label} value={value} onChange={onChange} required={required}
            options={options} style={{ marginBottom: 0 }} />
        </div>
        <button
          type="button"
          onClick={openModal}
          onMouseEnter={() => setHover(true)}
          onMouseLeave={() => setHover(false)}
          title={`Add a new ${addTitle}`}
          style={{
            height: 40, padding: '0 14px', borderRadius: 7, border: `1.5px solid ${T.lgrey}`,
            background: hover ? T.offwt : T.white, color: T.dgrey, boxSizing: 'border-box',
            fontSize: 13, fontWeight: 700, cursor: 'pointer', whiteSpace: 'nowrap',
            transition: 'background .15s',
          }}
        >
          + Add {addTitle}
        </button>
      </div>

      {open && (
        <Modal title={`New ${addTitle}`} onClose={() => setOpen(false)} width={440}>
          {err && <Alert type="error">{err}</Alert>}
          {renderQuickAddForm({ form, setForm })}
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setOpen(false)}>Cancel</Btn>
            <Btn disabled={saving} onClick={handleCreate}>{saving ? 'Creating…' : `Create ${addTitle}`}</Btn>
          </div>
        </Modal>
      )}
    </div>
  );
}
