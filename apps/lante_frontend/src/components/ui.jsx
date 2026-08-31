// src/components/ui.jsx — shared UI primitives for the QSL-rebranded QaliCore app.
//
// Ported from the QSL Next.js dashboard's inline component set so every module
// (ported or newly built) draws from one kit. All inline-style driven, keyed
// off the design tokens in ../theme/tokens.js. Import what you need:
//
//   import { Card, Stat, Btn, Badge, Modal, DataTable, Tabs } from '../components/ui.jsx'
//
import { useState, useEffect, useRef } from 'react';
import { T } from '../theme/tokens.js';
import HelpTip from './HelpTip.jsx';

// ── Badge ─────────────────────────────────────────────────────────────────────
export function Badge({ children, variant = 'default', size = 'sm' }) {
  const map = {
    green:   { bg: T.greenL, color: T.green },
    red:     { bg: T.redL, color: T.red },
    amber:   { bg: T.amberL, color: T.amber },
    blue:    { bg: T.blueL, color: T.blue },
    navy:    { bg: '#DCE8F5', color: T.navy },
    purple:  { bg: T.purpleL, color: T.purple },
    default: { bg: T.lgrey, color: T.dgrey },
  };
  const s = map[variant] || map.default;
  return (
    <span style={{
      background: s.bg, color: s.color,
      padding: size === 'sm' ? '2px 8px' : '4px 12px',
      borderRadius: 20, fontSize: size === 'sm' ? 11 : 12, fontWeight: 600,
      whiteSpace: 'nowrap', display: 'inline-block',
    }}>{children}</span>
  );
}

// ── Card ──────────────────────────────────────────────────────────────────────
export function Card({ children, style = {}, onClick }) {
  return (
    <div onClick={onClick} style={{
      background: T.white, borderRadius: 10, padding: 20,
      boxShadow: '0 1px 3px rgba(27,58,92,.08)', border: `1px solid ${T.lgrey}`,
      cursor: onClick ? 'pointer' : 'default', ...style,
    }}>{children}</div>
  );
}

// ── Stat tile ───────────────────────────────────────────────────────────────
export function Stat({ label, value, sub, icon, variant }) {
  const cols = { green: T.green, red: T.red, amber: T.amber, blue: T.blue };
  const bgs  = { green: T.greenL, red: T.redL, amber: T.amberL, blue: T.blueL };
  return (
    <Card style={{ padding: '16px 18px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div style={{ flex: 1 }}>
          <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', letterSpacing: .8, marginBottom: 5 }}>{label}</p>
          <p style={{ fontSize: 20, fontWeight: 700, color: cols[variant] || T.navy, lineHeight: 1 }}>{value}</p>
          {sub && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 4 }}>{sub}</p>}
        </div>
        {icon && <div style={{ width: 36, height: 36, background: bgs[variant] || '#DCE8F5', borderRadius: 9, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 16 }}>{icon}</div>}
      </div>
    </Card>
  );
}

// ── Kpi (big stat card — matches the deployed dashboard/workspace KPI tiles) ──
export function Kpi({ label, value, sub, icon, variant, loading, onClick }) {
  const valueColors = { green: T.green, red: T.red, amber: T.amber, blue: T.blue };
  const iconBg = { green: T.greenL, red: T.redL, amber: T.amberL, blue: T.blueL };
  return (
    <div onClick={onClick} style={{
      background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 16,
      padding: '16px', boxShadow: '0 1px 3px rgba(27,58,92,.06)',
      display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 10, minHeight: 68,
      cursor: onClick ? 'pointer' : 'default',
    }}>
      <div style={{ minWidth: 0, flex: 1 }}>
        <div style={{ fontSize: 10, fontWeight: 700, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .6, marginBottom: 6 }}>{label}</div>
        {loading ? (
          <div style={{ height: 26, width: 64, background: T.lgrey, borderRadius: 6, opacity: .6 }} />
        ) : (
          <div style={{ fontSize: 22, fontWeight: 800, color: valueColors[variant] || T.navy, lineHeight: 1.1, wordBreak: 'break-word' }}>{value}</div>
        )}
        {sub && <div style={{ fontSize: 12, color: T.mgrey, marginTop: 6 }}>{sub}</div>}
      </div>
      {icon && <div style={{ width: 40, height: 40, borderRadius: 12, background: iconBg[variant] || '#DCE8F5', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18, flexShrink: 0 }}>{icon}</div>}
    </div>
  );
}

// ── MiniStat (compact fact tile for detail-view headers — no card chrome) ────
export function MiniStat({ label, value }) {
  return (
    <div style={{ background: T.offwt, borderRadius: 8, padding: '12px 14px' }}>
      <div style={{ fontSize: 10, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .6, marginBottom: 4 }}>{label}</div>
      <div style={{ fontSize: 15, fontWeight: 700, color: T.navy }}>{value ?? '—'}</div>
    </div>
  );
}

// Responsive KPI grid helper — 1 col on mobile up to N per row on desktop.
export const KPI_GRID = { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 14 };

// ── Button ────────────────────────────────────────────────────────────────────
export function Btn({ children, variant = 'primary', onClick, size = 'md', disabled = false, type = 'button', style = {} }) {
  const styles = {
    primary: { bg: T.brandVar, color: T.white },
    gold:    { bg: T.accentVar, color: T.white },
    outline: { bg: 'transparent', color: T.navy, border: `1.5px solid ${T.navy}` },
    danger:  { bg: T.red, color: T.white },
    ghost:   { bg: T.offwt, color: T.dgrey, border: `1px solid ${T.lgrey}` },
    green:   { bg: T.green, color: T.white },
  };
  const s = styles[variant] || styles.primary;
  const pads = { sm: '5px 12px', md: '8px 18px', lg: '11px 24px' };
  return (
    <button type={type} onClick={onClick} disabled={disabled} style={{
      background: disabled ? T.lgrey : s.bg, color: disabled ? T.mgrey : s.color,
      border: s.border || 'none', padding: pads[size], borderRadius: 7,
      fontSize: size === 'sm' ? 12 : 13, fontWeight: 600,
      cursor: disabled ? 'not-allowed' : 'pointer', ...style,
    }}>{children}</button>
  );
}

// ── Alert ─────────────────────────────────────────────────────────────────────
export function Alert({ type = 'info', children }) {
  const t = {
    info:    { bg: '#EFF6FF', border: '#BFDBFE', color: '#1E40AF', icon: 'ℹ️' },
    warning: { bg: T.amberL, border: '#FCD34D', color: T.amber, icon: '⚠️' },
    error:   { bg: T.redL, border: '#FCA5A5', color: T.red, icon: '🔴' },
    success: { bg: T.greenL, border: '#86EFAC', color: T.green, icon: '✅' },
  }[type];
  return (
    <div style={{ background: t.bg, border: `1px solid ${t.border}`, color: t.color, padding: '10px 14px', borderRadius: 8, display: 'flex', gap: 8, fontSize: 13, marginBottom: 14 }}>
      <span>{t.icon}</span><span style={{ flex: 1 }}>{children}</span>
    </div>
  );
}

// ── HelpPanel ─────────────────────────────────────────────────────────────────
// Collapsed-by-default "About" toggle. Opens as a floating panel that overlays
// whatever comes after it in the page (e.g. the stat-card row) instead of
// pushing that content down. `variant` lets callers tint it to match the
// tab's current severity (e.g. red when a stat card on the page is critical).
const HELP_PANEL_STYLES = {
  blue:  { bg: T.blueL, border: '#BFDBFE', color: '#1E40AF' },
  green: { bg: T.greenL, border: '#86EFAC', color: T.green },
  amber: { bg: T.amberL, border: '#FCD34D', color: T.amber },
  red:   { bg: T.redL, border: '#FCA5A5', color: T.red },
};

// Picks the most severe of the given Kpi variants — used to tint HelpPanel
// to match the worst stat card currently showing on the page.
export function worstVariant(...variants) {
  if (variants.includes('red')) return 'red';
  if (variants.includes('amber')) return 'amber';
  return 'blue';
}

export function HelpPanel({ title = 'About this page', children, variant = 'blue' }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);
  const s = HELP_PANEL_STYLES[variant] || HELP_PANEL_STYLES.blue;

  useEffect(() => {
    if (!open) return;
    function onClickOutside(e) {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    }
    document.addEventListener('mousedown', onClickOutside);
    return () => document.removeEventListener('mousedown', onClickOutside);
  }, [open]);

  return (
    <div ref={ref} style={{ position: 'relative', marginBottom: 14, zIndex: open ? 40 : 'auto' }}>
      <button onClick={() => setOpen(o => !o)} style={{
        display: 'flex', alignItems: 'center', gap: 6, background: 'none',
        border: `1px solid ${T.lgrey}`, borderRadius: 7, padding: '6px 12px',
        fontSize: 12, fontWeight: 600, color: T.navy, cursor: 'pointer',
      }}>
        <span>ℹ️</span> About
        <span style={{ fontSize: 9, transform: open ? 'rotate(180deg)' : 'none', transition: 'transform .15s' }}>▾</span>
      </button>
      {open && (
        <div style={{
          position: 'absolute', top: '100%', left: 0, right: 0, marginTop: 8,
          background: s.bg, border: `1px solid ${s.border}`, color: s.color,
          padding: '12px 14px', borderRadius: 8, fontSize: 13, lineHeight: 1.5,
          boxShadow: '0 12px 32px rgba(13,34,56,.18)',
        }}>
          <button onClick={() => setOpen(false)} aria-label="Close" style={{
            position: 'absolute', top: 8, right: 10, background: 'none', border: 'none',
            fontSize: 18, lineHeight: 1, color: s.color, cursor: 'pointer', padding: 0,
          }}>×</button>
          {title && <div style={{ fontWeight: 700, marginBottom: 4, paddingRight: 20 }}>{title}</div>}
          {children}
        </div>
      )}
    </div>
  );
}

// ── Progress bar ───────────────────────────────────────────────────────────
export function Progress({ value, height = 6 }) {
  return (
    <div style={{ background: T.lgrey, borderRadius: 99, overflow: 'hidden', height }}>
      <div style={{ width: `${Math.min((value || 0) * 100, 100)}%`, height: '100%', background: value >= .95 ? T.red : value >= .8 ? T.amber : T.green, borderRadius: 99, transition: 'width .3s' }} />
    </div>
  );
}

// ── Input ─────────────────────────────────────────────────────────────────────
export function Input({ label, value, onChange, type = 'text', placeholder = '', required, note, readOnly, help, helpKey }) {
  return (
    <div style={{ marginBottom: 14 }}>
      {label && (
        <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>
          {label}{required && <span style={{ color: T.red }}> *</span>}
          {help && helpKey && <HelpTip text={help} dismissKey={helpKey} />}
        </label>
      )}
      <input type={type} value={value ?? ''} onChange={e => onChange && onChange(e.target.value)} placeholder={placeholder} readOnly={readOnly}
        style={{ width: '100%', padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, color: T.dgrey, background: readOnly ? T.offwt : T.white, outline: 'none', boxSizing: 'border-box' }} />
      {note && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 3 }}>{note}</p>}
    </div>
  );
}

// ── FileInput ─────────────────────────────────────────────────────────────────
// Uploads immediately on selection via onFileSelected(file); the parent tracks
// uploading/uploaded state and stores the resulting URL (mirrors Input's
// controlled-value pattern but for a file rather than free text).
export function FileInput({ label, accept, uploading, fileUrl, onFileSelected, required, note }) {
  return (
    <div style={{ marginBottom: 14 }}>
      {label && <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>{label}{required && <span style={{ color: T.red }}> *</span>}</label>}
      <input type="file" accept={accept} disabled={uploading}
        onChange={e => onFileSelected(e.target.files?.[0] ?? null)}
        style={{ width: '100%', fontSize: 13, color: T.dgrey }} />
      {uploading && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 3 }}>Uploading…</p>}
      {!uploading && fileUrl && (
        <p style={{ fontSize: 11, color: T.green, marginTop: 3 }}>
          ✓ Uploaded — <a href={fileUrl} target="_blank" rel="noreferrer" style={{ color: T.green }}>view file</a>
        </p>
      )}
      {note && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 3 }}>{note}</p>}
    </div>
  );
}

// ── Select ────────────────────────────────────────────────────────────────────
export function Select({ label, value, onChange, options, required, help, helpKey, style }) {
  return (
    <div style={{ marginBottom: 14, ...style }}>
      {label && (
        <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>
          {label}{required && <span style={{ color: T.red }}> *</span>}
          {help && helpKey && <HelpTip text={help} dismissKey={helpKey} />}
        </label>
      )}
      <select value={value ?? ''} onChange={e => onChange(e.target.value)}
        style={{ width: '100%', height: 40, padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, color: T.dgrey, background: T.white, outline: 'none', boxSizing: 'border-box' }}>
        {options.map(o => {
          const isObj = o !== null && typeof o === 'object'
          const val = isObj ? o.value : o
          const text = isObj ? (o.label ?? '') : o
          return <option key={val} value={val}>{text}</option>
        })}
      </select>
    </div>
  );
}

// ── Modal ─────────────────────────────────────────────────────────────────────
export function Modal({ title, children, onClose, width = 540 }) {
  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(13,34,56,.65)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: 16 }} onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={{ background: T.white, borderRadius: 12, width: '100%', maxWidth: width, maxHeight: '90vh', overflow: 'auto', boxShadow: '0 24px 60px rgba(0,0,0,.3)' }}>
        <div style={{ padding: '16px 22px', borderBottom: `1px solid ${T.lgrey}`, display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: T.navy, borderRadius: '12px 12px 0 0' }}>
          <h3 style={{ color: T.white, fontSize: 15, fontWeight: 700, margin: 0 }}>{title}</h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', color: T.white, fontSize: 22, cursor: 'pointer', lineHeight: 1, padding: 0 }}>×</button>
        </div>
        <div style={{ padding: 22 }}>{children}</div>
      </div>
    </div>
  );
}

// ── DocumentPreview ───────────────────────────────────────────────────────────
// Inline preview for an uploaded document URL — images render directly, PDFs render in an
// embedded viewer; anything else (doc/docx) falls back to a note (pair with an "open in new
// tab" link in the caller, since there's no reliable inline renderer for those).
const DOCUMENT_PREVIEW_IMAGE_EXTENSIONS = ['jpg', 'jpeg', 'png', 'webp'];
export function DocumentPreview({ url }) {
  const ext = url.split('.').pop()?.toLowerCase().split('?')[0];
  if (DOCUMENT_PREVIEW_IMAGE_EXTENSIONS.includes(ext)) {
    return <img src={url} alt="Document preview" style={{ maxWidth: '100%', display: 'block', borderRadius: 8 }} />;
  }
  if (ext === 'pdf') {
    return <iframe src={url} title="Document preview" style={{ width: '100%', height: '70vh', border: 'none', borderRadius: 8 }} />;
  }
  return <p style={{ fontSize: 13, color: T.mgrey }}>Preview isn't available for this file type — use "Open in new tab" below.</p>;
}

// ── DataTable ─────────────────────────────────────────────────────────────────
export function DataTable({ headers, rows, empty = 'No records found.', onRowClick }) {
  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
        <thead>
          <tr>{headers.map((h, i) => <th key={i} style={{ background: T.brandVar, color: T.white, padding: '9px 13px', textAlign: h === 'Actions' ? 'right' : 'left', fontWeight: 600, fontSize: 11, textTransform: 'uppercase', letterSpacing: .5, whiteSpace: 'nowrap' }}>{h}</th>)}</tr>
        </thead>
        <tbody>
          {rows.length === 0
            ? <tr><td colSpan={headers.length} style={{ padding: 40, textAlign: 'center', color: T.mgrey }}>{empty}</td></tr>
            : rows.map((row, i) => (
              <tr key={i} onClick={onRowClick ? () => onRowClick(row, i) : undefined} style={{ background: i % 2 === 0 ? T.white : T.offwt, cursor: onRowClick ? 'pointer' : 'default' }}>
                {row.map((cell, j) => (
                  <td key={j} style={{ padding: '9px 13px', borderBottom: `1px solid ${T.lgrey}`, verticalAlign: 'middle', textAlign: headers[j] === 'Actions' ? 'right' : 'left' }}>
                    {headers[j] === 'Actions' ? <div style={{ display: 'flex', justifyContent: 'flex-end' }}>{cell}</div> : cell}
                  </td>
                ))}
              </tr>
            ))
          }
        </tbody>
      </table>
    </div>
  );
}

// ── SectionHeader ─────────────────────────────────────────────────────────────
export function SectionHeader({ title, sub, action }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 18, flexWrap: 'wrap', gap: 12 }}>
      <div><h2 style={{ fontSize: 16, fontWeight: 700, color: T.navy, margin: 0 }}>{title}</h2>{sub && <p style={{ fontSize: 12, color: T.mgrey, marginTop: 3, margin: 0 }}>{sub}</p>}</div>
      {action}
    </div>
  );
}

// ── Loading spinner ───────────────────────────────────────────────────────────
export function Loading() {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 60, color: T.mgrey, fontSize: 13 }}>
      <div style={{ width: 24, height: 24, border: `3px solid ${T.lgrey}`, borderTopColor: T.navy, borderRadius: '50%', animation: 'spin 1s linear infinite', marginRight: 12 }} />Loading…
      <style>{`@keyframes spin{to{transform:rotate(360deg)}}`}</style>
    </div>
  );
}

// ── Tabs ──────────────────────────────────────────────────────────────────────
export function Tabs({ tabs, active, setActive }) {
  return (
    <div style={{ display: 'flex', gap: 0, marginBottom: 22, borderBottom: `1px solid ${T.lgrey}`, overflowX: 'auto', overflowY: 'hidden' }}>
      {tabs.map(t => (
        <button key={t.id} onClick={() => setActive(t.id)} style={{ padding: '9px 18px', background: 'none', border: 'none', cursor: 'pointer', fontSize: 13, fontWeight: active === t.id ? 700 : 400, color: active === t.id ? T.navy : T.mgrey, borderBottom: active === t.id ? `2px solid ${T.gold}` : '2px solid transparent', marginBottom: -1, whiteSpace: 'nowrap' }}>
          {t.label}
        </button>
      ))}
    </div>
  );
}

// ── EmptyState ──────────────────────────────────────────────────────────────
// Not in the QSL source but useful for the polish pass — a consistent empty view.
export function EmptyState({ icon = '📭', title = 'Nothing here yet', sub, action }) {
  return (
    <div style={{ textAlign: 'center', padding: '56px 24px', color: T.mgrey }}>
      <div style={{ display: 'flex', justifyContent: 'center', fontSize: 40, marginBottom: 12 }}>{icon}</div>
      <div style={{ fontSize: 15, fontWeight: 700, color: T.dgrey, marginBottom: 6 }}>{title}</div>
      {sub && <div style={{ fontSize: 13, maxWidth: 380, margin: '0 auto 16px' }}>{sub}</div>}
      {action}
    </div>
  );
}
