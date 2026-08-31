// src/pages/public/ProductPage.jsx — Product detail page (mock data, see ShopPage.jsx).
import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { T } from '../../theme/tokens.js';
import { NavBar, Footer, PageEyebrow, PRODUCTS } from '../../components/public/shared.jsx';
import { useCart } from '../../components/public/CartContext.jsx';

export default function ProductPage() {
  const { id } = useParams();
  const { addItem } = useCart();
  const product = PRODUCTS.find(p => String(p.id) === id);
  const [qty, setQty] = useState(1);
  const [added, setAdded] = useState(false);

  if (!product) {
    return (
      <div style={{ fontFamily: "'Inter', sans-serif", background: T.offwt, minHeight: '100vh' }}>
        <NavBar active="/shop" />
        <div style={{ textAlign: 'center', padding: '90px 32px' }}>
          <p style={{ color: T.mgrey, fontSize: 14, marginBottom: 16 }}>That product isn't available right now.</p>
          <Link to="/shop" style={{ fontSize: 13.5, fontWeight: 700, color: T.navy, textDecoration: 'none' }}>← Back to shop</Link>
        </div>
        <Footer />
      </div>
    );
  }

  return (
    <div style={{ fontFamily: "'Inter', sans-serif", color: T.dgrey, background: T.offwt, minHeight: '100vh' }}>
      <NavBar active="/shop" />

      <div style={{ maxWidth: 980, margin: '0 auto', padding: '40px 32px 80px' }}>
        <Link to="/shop" style={{ fontSize: 13, color: T.mgrey, textDecoration: 'none', display: 'inline-block', marginBottom: 24 }}>← Back to shop</Link>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 40, alignItems: 'start' }}>
          <div style={{
            height: 320, borderRadius: 14, background: product.image_url ? `url(${product.image_url}) center/cover` : T.white,
            border: `1px solid ${T.lgrey}`, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 64,
          }}>
            {!product.image_url && '📦'}
          </div>

          <div>
            {product.category && <PageEyebrow>{product.category}</PageEyebrow>}
            <h1 style={{ fontSize: 26, fontWeight: 800, color: T.navy, margin: '0 0 6px', letterSpacing: '-0.01em' }}>{product.name}</h1>
            <div style={{ fontSize: 12, color: T.mgrey, marginBottom: 16, fontFamily: 'monospace' }}>{product.code}</div>
            <div style={{ fontSize: 28, fontWeight: 800, color: T.navy, marginBottom: 6 }}>
              Kshs {Number(product.price).toLocaleString('en-KE')}
              <span style={{ fontSize: 13, color: T.mgrey, fontWeight: 500 }}> / {product.unit}</span>
            </div>
            <div style={{ fontSize: 12.5, fontWeight: 600, color: product.stock_available > 5 ? T.green : T.gold, marginBottom: 20 }}>
              {product.stock_available > 5 ? `In stock (${product.stock_available} available)` : `Only ${product.stock_available} left in stock`}
            </div>
            {product.description && (
              <p style={{ fontSize: 14, color: T.dgrey, lineHeight: 1.65, marginBottom: 24 }}>{product.description}</p>
            )}

            <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 18 }}>
              <div style={{ display: 'flex', alignItems: 'center', border: `1.5px solid ${T.lgrey}`, borderRadius: 8 }}>
                <button onClick={() => setQty(q => Math.max(1, q - 1))} style={{ width: 36, height: 38, border: 'none', background: 'none', fontSize: 16, cursor: 'pointer', color: T.navy }}>−</button>
                <span style={{ width: 36, textAlign: 'center', fontSize: 14, fontWeight: 600 }}>{qty}</span>
                <button onClick={() => setQty(q => Math.min(product.stock_available, q + 1))} style={{ width: 36, height: 38, border: 'none', background: 'none', fontSize: 16, cursor: 'pointer', color: T.navy }}>+</button>
              </div>
              <button
                onClick={() => { addItem(product, qty); setAdded(true); setTimeout(() => setAdded(false), 1800); }}
                disabled={product.stock_available === 0}
                style={{
                  flex: 1, fontSize: 14, fontWeight: 700, color: T.navyD, background: product.stock_available === 0 ? T.lgrey : T.gold,
                  padding: '12px 22px', borderRadius: 8, border: 'none', cursor: product.stock_available === 0 ? 'default' : 'pointer',
                }}
              >
                {product.stock_available === 0 ? 'Out of stock' : added ? 'Added ✓' : 'Add to Cart'}
              </button>
            </div>
            {added && (
              <Link to="/cart" style={{ fontSize: 13, color: T.navy, fontWeight: 600, textDecoration: 'none' }}>View cart →</Link>
            )}
          </div>
        </div>
      </div>

      <Footer />
    </div>
  );
}
