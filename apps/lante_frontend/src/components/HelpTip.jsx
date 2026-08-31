// src/components/HelpTip.jsx — small dismissible "what does this mean?" popover.
//
// Attach next to a label (e.g. a Select) to explain a menu/field a first-time
// user may not understand. "Got it" permanently hides it via localStorage;
// closing the popover otherwise (click-outside) just closes it for now.
import { useState, useEffect, useRef } from 'react';
import { T } from '../theme/tokens.js';

export default function HelpTip({ text, dismissKey }) {
  const [dismissed, setDismissed] = useState(() => !!dismissKey && localStorage.getItem(dismissKey) === '1');
  const [show, setShow] = useState(false);
  const ref = useRef(null);

  useEffect(() => {
    if (!show) return;
    function onClickOutside(e) {
      if (ref.current && !ref.current.contains(e.target)) setShow(false);
    }
    document.addEventListener('mousedown', onClickOutside);
    return () => document.removeEventListener('mousedown', onClickOutside);
  }, [show]);

  if (dismissed) return null;

  function gotIt() {
    if (dismissKey) localStorage.setItem(dismissKey, '1');
    setDismissed(true);
  }

  return (
    <span ref={ref} style={{ position: 'relative', display: 'inline-block', marginLeft: 6 }}>
      <button
        type="button"
        onClick={() => setShow(s => !s)}
        title="What does this mean?"
        style={{
          width: 16, height: 16, borderRadius: '50%', border: `1px solid ${T.mgrey}`,
          background: T.white, color: T.mgrey, fontSize: 10, fontWeight: 700,
          lineHeight: 1, cursor: 'pointer', padding: 0, display: 'inline-flex',
          alignItems: 'center', justifyContent: 'center',
        }}
      >?</button>
      {show && (
        <div style={{
          position: 'absolute', top: 22, left: 0, zIndex: 20, width: 240,
          background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 8,
          boxShadow: '0 8px 24px rgba(0,0,0,.15)', padding: 12,
        }}>
          <p style={{ fontSize: 12, color: T.dgrey, margin: 0, lineHeight: 1.5 }}>{text}</p>
          <button
            type="button"
            onClick={gotIt}
            style={{ background: 'none', border: 'none', color: T.navy, fontSize: 11, fontWeight: 700, cursor: 'pointer', padding: '8px 0 0', textDecoration: 'underline' }}
          >Got it, don't show again</button>
        </div>
      )}
    </span>
  );
}
