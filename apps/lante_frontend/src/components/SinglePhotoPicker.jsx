import { useEffect, useMemo } from 'react'

// Single-photo picker with a thumbnail preview and a Remove button — plain
// <input type="file"> gives no way to review or deselect a wrong photo.
export default function SinglePhotoPicker({ file, onChange }) {
  const previewUrl = useMemo(() => file ? URL.createObjectURL(file) : null, [file])
  useEffect(() => () => { if (previewUrl) URL.revokeObjectURL(previewUrl) }, [previewUrl])

  if (file) {
    return (
      <div className="flex items-center gap-3 border border-gray-200 rounded-lg p-2">
        <img src={previewUrl} alt="Preview" className="w-12 h-12 object-cover rounded-lg flex-shrink-0" />
        <span className="text-sm text-gray-600 truncate flex-1">{file.name}</span>
        <button
          type="button"
          onClick={() => onChange(null)}
          className="text-xs font-semibold text-red-600 hover:text-red-700 px-2 py-1 rounded-lg hover:bg-red-50 flex-shrink-0"
        >
          ✕ Remove
        </button>
      </div>
    )
  }

  return (
    <input
      type="file"
      accept="image/*"
      onChange={e => onChange(e.target.files?.[0] ?? null)}
      className="w-full text-sm text-gray-600 file:mr-3 file:py-2 file:px-3 file:rounded-lg file:border-0 file:bg-gray-100 file:text-gray-700 file:text-sm file:font-medium"
    />
  )
}
