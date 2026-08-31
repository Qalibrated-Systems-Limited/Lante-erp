// src/pages/public/CartPage.jsx — Cart review page.
import { Link } from 'react-router-dom';
import { T } from '../../theme/tokens.js';
import { NavBar, Footer, PageEyebrow } from '../../components/public/shared.jsx';
import { useCart } from '../../components/public/CartContext.jsx';

export default function CartPage() {
  const { items, updateQty, removeItem, subtotal, loaded } = useCart();

  return (
    <div style={{ fontFamily: "'Inter', sans-serif", color: T.dgrey, background: T.offwt, minHeight: '100vh' }}>
      <NavBar active="/cart" />

      <div style={{ maxWidth: 760, margin: '0 auto', padding: '48px 32px 90px' }}>
        <PageEyebrow>Your cart</PageEyebrow>
        <h1 style={{ fontSize: 26, fontWeight: 800, color: T.navy, margin: '0 0 28px', letterSpacing: '-0.01em' }}>
          {loaded && items.length === 0 ? 'Your cart is empty' : 'Review your order'}
        </h1>

        {!loaded && <div style={{ color: T.mgrey, fontSize: 14 }}>Loading…</div>}

        {loaded && items.length === 0 && (
          <div style={{ background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 12, padding: '40px 28px', textAlign: 'center' }}>
            <div style={{ fontSize: 32, marginBottom: 12 }}>🛒</div>
            <p style={{ fontSize: 14, color: T.mgrey, marginBottom: 20 }}>Browse the shop and add a few items to get started.</p>
            <Link to="/shop" style={{ fontSize: 13.5, fontWeight: 700, color: T.navyD, background: T.gold, padding: '12px 24px', borderRadius: 8, textDecoration: 'none' }}>
              Browse Shop →
            </Link>
          </div>
        )}

        {loaded && items.length > 0 && (
          <>
            <div style={{ background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 12, overflow: 'hidden', marginBottom: 24 }}>
              {items.map((p, i) => (
                <div key={p.id} style={{
                  display: 'flex', alignItems: 'center', gap: 14, padding: '16px 20px',
                  borderBottom: i < items.length - 1 ? `1px solid ${T.lgrey}` : 'none',
                }}>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: 14, fontWeight: 700, color: T.navy }}>{p.name}</div>
                    <div style={{ fontSize: 11.5, color: T.mgrey, fontFamily: 'monospace', marginTop: 2 }}>{p.code}</div>
                  </div>
                  <div style={{ fontSize: 13.5, color: T.dgrey, width: 90, textAlign: 'right' }}>Kshs {p.price.toLocaleString('en-KE')}</div>
                  <div style={{ display: 'flex', alignItems: 'center', border: `1.5px solid ${T.lgrey}`, borderRadius: 7 }}>
                    <button onClick={() => updateQty(p.id, p.qty - 1)} style={{ width: 28, height: 30, border: 'none', background: 'none', fontSize: 14, cursor: 'pointer', color: T.navy }}>−</button>
                    <span style={{ width: 26, textAlign: 'center', fontSize: 13, fontWeight: 600 }}>{p.qty}</span>
                    <button onClick={() => updateQty(p.id, p.qty + 1)} style={{ width: 28, height: 30, border: 'none', background: 'none', fontSize: 14, cursor: 'pointer', color: T.navy }}>+</button>
                  </div>
                  <div style={{ fontSize: 13.5, fontWeight: 700, color: T.navy, width: 90, textAlign: 'right' }}>
                    Kshs {(p.price * p.qty).toLocaleString('en-KE')}
                  </div>
                  <button onClick={() => removeItem(p.id)} aria-label="Remove" style={{ border: 'none', background: 'none', color: T.mgrey, fontSize: 16, cursor: 'pointer', padding: 4 }}>✕</button>
                </div>
              ))}
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
              <Link to="/shop" style={{ fontSize: 13, color: T.mgrey, textDecoration: 'none' }}>← Continue shopping</Link>
              <div style={{ textAlign: 'right' }}>
                <div style={{ fontSize: 12, color: T.mgrey, marginBottom: 2 }}>Subtotal (VAT calculated at checkout)</div>
                <div style={{ fontSize: 20, fontWeight: 800, color: T.navy }}>Kshs {subtotal.toLocaleString('en-KE')}</div>
              </div>
            </div>

            <Link to="/checkout" style={{
              display: 'block', textAlign: 'center', fontSize: 14.5, fontWeight: 700, color: T.navyD,
              background: T.gold, padding: '14px 28px', borderRadius: 8, textDecoration: 'none',
            }}>
              Proceed to Checkout →
            </Link>
          </>
        )}
      </div>

      <Footer />
    </div>
  );
}
