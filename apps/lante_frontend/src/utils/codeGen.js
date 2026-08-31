// Generates a short, human-readable code from a name (e.g. "Consumables" -> "CONS-482",
// "PPE & Safety Equipment" -> "PSE-217") for Category/Location Code fields. Never required to
// use — always paired with a manual-entry input the user can type over.
export function generateCodeFromName(name) {
  const words = (name || '').trim().split(/\s+/).map(w => w.replace(/[^a-zA-Z0-9]/g, '')).filter(Boolean)
  const base = words.length >= 2
    ? words.slice(0, 4).map(w => w[0]).join('').toUpperCase()
    : (words[0] || 'X').slice(0, 4).toUpperCase()
  const suffix = String(Math.floor(100 + Math.random() * 900))
  return `${base}-${suffix}`
}
