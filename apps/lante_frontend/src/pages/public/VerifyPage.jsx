// src/pages/public/VerifyPage.jsx — manual certificate lookup form.
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { T } from '../../theme/tokens.js';
import { NavBar, Footer, PageEyebrow } from '../../components/public/shared.jsx';

export default function VerifyPage() {
  const navigate = useNavigate();
  const [certNo, setCertNo] = useState('');

  const submit = (e) => {
    e.preventDefault();
    if (!certNo.trim()) return;
    navigate(`/verify/${encodeURIComponent(certNo.trim())}`);
  };

  return (
    <div style={{ fontFamily: "'Inter', sans-serif", color: T.dgrey, background: T.offwt, minHeight: '100vh' }}>
      <NavBar active="/verify" />
      <div style={{ maxWidth: 560, margin: '0 auto', padding: '70px 32px 90px', textAlign: 'center' }}>
        <PageEyebrow>Certificate Verification</PageEyebrow>
        <h1 style={{ fontSize: 26, fontWeight: 800, color: T.navy, margin: '0 0 12px', letterSpacing: '-0.01em' }}>
          Verify a Lante calibration certificate
        </h1>
        <p style={{ fontSize: 14, color: T.mgrey, lineHeight: 1.6, marginBottom: 28 }}>
          Every certificate we issue carries a QR code linking here. Enter the certificate number printed
          on the document to confirm it's genuine and check its current status.
        </p>
        <form onSubmit={submit} style={{ display: 'flex', gap: 10 }}>
          <input
            value={certNo}
            onChange={e => setCertNo(e.target.value)}
            placeholder="e.g. QSL/QP/19/CERT/0001"
            style={{
              flex: 1, padding: '12px 16px', border: `1.5px solid ${T.lgrey}`, borderRadius: 8,
              fontSize: 14, color: T.dgrey, outline: 'none', fontFamily: 'inherit',
            }}
          />
          <button type="submit" style={{
            fontSize: 14, fontWeight: 700, color: T.navyD, background: T.gold, padding: '12px 24px',
            borderRadius: 8, border: 'none', cursor: 'pointer', whiteSpace: 'nowrap',
          }}>
            Verify →
          </button>
        </form>
      </div>
      <Footer />
    </div>
  );
}
