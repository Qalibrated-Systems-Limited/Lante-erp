import { useRef } from 'react'

export default function ImagePicker({ files, onChange, label = 'Images' }) {
  const ref = useRef(null)

  const add = (e) => {
    const picked = Array.from(e.target.files ?? [])
    if (picked.length) onChange([...files, ...picked])
    e.target.value = ''
  }

  const remove = (i) => onChange(files.filter((_, j) => j !== i))

  return (
    <div>
      <label style={{ fontSize: 14, fontWeight: 600, color: '#374151', display: 'block', marginBottom: 6 }}>
        {label}
      </label>
      {files.length > 0 && (
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 8 }}>
          {files.map((f, i) => (
            <div key={i} style={{ position: 'relative' }}>
              <img
                src={URL.createObjectURL(f)}
                alt=""
                style={{ width: 64, height: 64, objectFit: 'cover', borderRadius: 8, display: 'block' }}
              />
              <button
                type="button"
                onClick={() => remove(i)}
                style={{
                  position: 'absolute', top: -6, right: -6,
                  background: '#ef4444', color: '#fff', border: 'none',
                  borderRadius: '50%', width: 18, height: 18,
                  fontSize: 14, lineHeight: '18px', textAlign: 'center',
                  cursor: 'pointer', padding: 0,
                }}
              >×</button>
            </div>
          ))}
        </div>
      )}
      <button
        type="button"
        onClick={() => ref.current?.click()}
        style={{
          border: '2px dashed #e5e7eb', borderRadius: 8,
          padding: '7px 14px', fontSize: 14, color: '#9ca3af',
          cursor: 'pointer', background: 'none', width: '100%',
        }}
      >
        + Add images
      </button>
      <input ref={ref} type="file" accept="image/*" multiple style={{ display: 'none' }} onChange={add} />
    </div>
  )
}
