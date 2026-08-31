import { X } from 'lucide-react'

// Like SinglePhotoPicker, but aware of a photo that may already be uploaded
// (an existing URL) — shows that until the user picks a replacement file.
export default function EditPhotoSlot({ currentUrl, file, onChange }) {
  if (file) {
    const previewUrl = URL.createObjectURL(file)
    return (
      <div className="flex items-center gap-3 border border-gray-200 rounded-lg p-2">
        <img src={previewUrl} alt="New preview" className="w-12 h-12 object-cover rounded-lg flex-shrink-0" />
        <span className="text-sm text-gray-600 truncate flex-1">{file.name}</span>
        <button type="button" onClick={() => onChange(null)}
          className="inline-flex items-center gap-1 text-xs font-semibold text-red-600 hover:text-red-700 px-2 py-1 rounded-lg hover:bg-red-50 flex-shrink-0">
          <X size={14} /> Remove
        </button>
      </div>
    )
  }

  if (currentUrl) {
    return (
      <div className="flex items-center gap-3 border border-gray-200 rounded-lg p-2">
        <img src={currentUrl} alt="Current" className="w-12 h-12 object-cover rounded-lg flex-shrink-0" />
        <span className="text-sm text-gray-400 flex-1">Uploaded</span>
        <label className="text-xs font-semibold text-navy hover:underline px-2 py-1 cursor-pointer flex-shrink-0">
          Replace
          <input type="file" accept="image/*" className="hidden" onChange={e => onChange(e.target.files?.[0] ?? null)} />
        </label>
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
