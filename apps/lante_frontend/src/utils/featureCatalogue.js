/**
 * featureCatalogue.js — mirrors LicenseFeatures.cs
 *
 * Single source of truth for label resolution in the ERP frontend.
 * Must stay in sync with LicenseService.Core/Constants/LicenseFeatures.cs.
 */

export const FEATURE_CATALOGUE = [
  // Hardware
  { value: "anpr",            label: "ANPR / NPR Camera",               group: "Hardware" },
  { value: "ticket_printer",  label: "Ticket Printer",                  group: "Hardware" },
  { value: "rfid",            label: "RFID Reader",                     group: "Hardware" },
  { value: "nfc",             label: "NFC Reader",                      group: "Hardware" },
  // Modules
  { value: "kiosk",           label: "Unmanned Kiosk",                  group: "Modules"  },
  { value: "dual_lane",       label: "Dual Lane / Second Scale",        group: "Modules"  },
  { value: "reports",         label: "Advanced Reports",                group: "Modules"  },
  { value: "analytics",       label: "Analytics Dashboard",             group: "Modules"  },
  { value: "user_management", label: "User Management",                 group: "Modules"  },
  { value: "shifts",          label: "Shifts",                          group: "Modules"  },
  { value: "backup",          label: "Backup & Microservice Mgmt",      group: "Modules"  },
  // Future
  { value: "boom_barrier",    label: "Boom Barrier Controller",         group: "Modules"  },
  { value: "sms_alerts",      label: "SMS Alerts",                      group: "Modules"  },
]

const _map = Object.fromEntries(FEATURE_CATALOGUE.map(f => [f.value, f]))

/** Returns the human-readable label for a feature value, or the raw value if unknown. */
export function featureLabel(value) {
  return _map[value]?.label ?? value
}

/** Returns the group for a feature value. */
export function featureGroup(value) {
  return _map[value]?.group ?? 'Other'
}
