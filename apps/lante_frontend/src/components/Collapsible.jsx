// src/components/Collapsible.jsx — dismissible, collapsible summary panel.
//
// Used for onboarding/explainer content (e.g. "how the Stores flow works")
// that a user should be able to permanently hide once they understand it.
import { useState } from 'react';
import { Info } from 'lucide-react';
import { T } from '../theme/tokens.js';
import { Card } from './ui.jsx';

export default function Collapsible({ title, defaultOpen = false, dismissKey, children }) {
  const [dismissed, setDismissed] = useState(() => !!dismissKey && localStorage.getItem(dismissKey) === '1');
  const [open, setOpen] = useState(defaultOpen);

  if (dismissed) return null;

  function dismiss(e) {
    e.stopPropagation();
    if (dismissKey) localStorage.setItem(dismissKey, '1');
    setDismissed(true);
  }

  if (!open) {
    return (
      <button
        onClick={() => setOpen(true)}
        style={{
          display: 'inline-flex', alignItems: 'center', gap: 6,
          padding: '8px 14px', marginBottom: 16,
          background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 8,
          fontSize: 13, fontWeight: 700, color: T.navy, cursor: 'pointer',
        }}
      >
        <Info size={14} />
        About
        <span style={{ fontSize: 10 }}>▼</span>
      </button>
    );
  }

  return (
    <Card style={{ marginBottom: 16, padding: 0, overflow: 'hidden' }}>
      <div
        onClick={() => setOpen(false)}
        style={{
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          padding: '12px 16px', cursor: 'pointer', background: T.offwt,
          borderBottom: `1px solid ${T.lgrey}`,
        }}
      >
        <span style={{ fontSize: 13, fontWeight: 700, color: T.navy }}>{title}</span>
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
          <span style={{ fontSize: 12, color: T.mgrey }}>▲ Hide</span>
          {dismissKey && (
            <button
              onClick={dismiss}
              title="Dismiss — won't show again"
              style={{ background: 'none', border: 'none', color: T.mgrey, fontSize: 18, cursor: 'pointer', lineHeight: 1, padding: 0 }}
            >×</button>
          )}
        </div>
      </div>
      <div style={{ padding: 18 }}>
        {children}
      </div>
    </Card>
  );
}
