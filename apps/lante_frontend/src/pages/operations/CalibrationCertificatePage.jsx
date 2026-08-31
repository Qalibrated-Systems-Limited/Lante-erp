import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { QRCodeSVG } from 'qrcode.react'
import api from '../../api/axios.js'
import useCompanyBranding from '../../hooks/useCompanyBranding.js'

// This page is the on-screen twin of CertificatePdfService.cs — same clause numbering, same
// wording, same navy/gold treatment — so what a technician approves on screen is what the
// client receives as a PDF. When you change one, change the other.

// ── Issuing laboratory ─────────────────────────────────────────────────────────
// The name/address/contact block now comes from useCompanyBranding() (the tenant's own
// company.legal_name / company.address / company.phone / company.email settings) instead of
// being hardcoded here — see CertHeader below. ACCREDITED stays fixed: it's a regulatory claim,
// not a tenant identity field.
const ACCREDITED  = 'Issued under ISO/IEC 17025:2015 · Accredited by KENAS'

// The header shows the legal name as a bold line with a small gold "LIMITED"/"LTD"/"PLC" suffix
// badge underneath — a stylistic split, not two separate identity fields. Derive both from the
// one legalName setting so it still degrades gracefully (no dangling badge) for a tenant whose
// legal name doesn't end in a recognised company suffix.
function splitLegalName(legalName) {
  const m = /^(.*)\s+(LIMITED|LTD\.?|PLC|LLC|INC\.?)$/i.exec((legalName || '').trim())
  return m ? { name: m[1], suffix: m[2].toUpperCase() } : { name: legalName, suffix: '' }
}

// ── Density lookup (mirrors DataSheetCalculationService.GetClassDensity) ──────
function getClassDensity(weightClass) {
  const cls = (weightClass ?? '').replace(/class/gi, '').trim().toUpperCase()
  const map = {
    E1:[7950,140], E2:[7950,140], F1:[7950,140], F2:[7950,140],
    M1:[8400,170], M2:[7100,600], M3:[7100,600],
  }
  return map[cls] ?? [7950, 140]
}

// MPE tier text for accuracy class (OIML R 76-1 §3.6.1)
function getMpeTiers(accuracyClass) {
  const cls = (accuracyClass ?? '').replace(/class/gi, '').trim().toUpperCase()
  switch (cls) {
    case 'I':            return ['0 to 50 000e: ±0.5e']
    case 'II':           return ['0 to 5 000e: ±0.5e', '>5 000 to 20 000e: ±1e', '>20 000e: ±1.5e']
    case 'III':          return ['0 to 500e: ±1e', '>500 to 2 000e: ±2e', '>2 000 to 10 000e: ±3e']
    case 'IIII': case 'IV': return ['0 to 50e: ±1e', '>50 to 200e: ±2e', '>200 to 1 000e: ±3e']
    default:             return null
  }
}

// ── Dates ─────────────────────────────────────────────────────────────────────

// "26 Mar 2026" — the compact form used in the identity strip and sign-off
function fmtShort(iso) {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

function nextDue(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  d.setFullYear(d.getFullYear() + 1)
  d.setDate(d.getDate() - 1)          // expires the day before, same as the PDF
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

// ── Number formatting (mirrors the Nfi/Num/Signed helpers in the PDF service) ─
// Digits are grouped with a non-breaking space (30 000.2) and each column carries a fixed
// number of decimals, so a result column reads as a column.

function num(v, dec) {
  if (v == null || Number.isNaN(Number(v))) return '—'
  return Number(v)
    .toLocaleString('en-GB', { minimumFractionDigits: dec, maximumFractionDigits: dec })
    .replace(/,/g, ' ')
}

function numN(v, dec) { return v == null ? '' : num(v, dec) }

// A value that rounds to zero at the displayed precision is written "0.0" with no sign —
// "+ 0.0" would assert a positive error the measurement does not support.
function signed(v, dec) {
  if (v == null) return ''
  if (Math.abs(v) < 0.5 * Math.pow(10, -dec)) return num(0, dec)
  return v > 0 ? `+ ${num(v, dec)}` : `− ${num(Math.abs(v), dec)}`
}

// Uncertainties span several decades, so show 4 significant figures rather than a fixed
// decimal count that would print 0.00 for the small ones.
function sig(v) {
  if (v == null || Number.isNaN(Number(v))) return ''
  return String(Number(Number(v).toPrecision(4)))
}

/**
 * Stable document control ID.
 *
 * This is a deliberate re-implementation of DocumentControlId() in CertificatePdfService.cs —
 * FNV-1a over the certificate number, so the preview and the downloaded PDF always show the
 * same ID for the same certificate. If you change the algorithm on either side, change both.
 */
function docControlId({ certificateNumber, jobNumber, labNo, issuedAt }, kind, docPrefix = 'LT') {
  const basis = certificateNumber || jobNumber || labNo || docPrefix
  let hash = 2166136261 >>> 0
  for (let i = 0; i < basis.length; i++) {
    hash = (hash ^ basis.charCodeAt(i)) >>> 0
    hash = Math.imul(hash, 16777619) >>> 0
  }
  const d = issuedAt ? new Date(issuedAt) : new Date()
  const date = `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`
  return `${docPrefix}-${kind}-${date}-${String(hash % 1000000).padStart(6, '0')}`
}

// Plain-text QR payload — identical to QrPayload() in the PDF service. No verification URL is
// encoded: this deployment has no verification endpoint, and a QR implying one would be an
// authenticity claim the system cannot honour.
function qrPayload({ certificateNumber, issuedAt }, docId, equipment, serial, client, labName) {
  const lines = [
    labName,
    `Certificate No: ${certificateNumber || '—'}`,
    `Document Control ID: ${docId}`,
    `Date of calibration: ${fmtShort(issuedAt)}`,
    `Next calibration due: ${nextDue(issuedAt)}`,
    `Item: ${equipment}`,
  ]
  if (serial) lines.push(`Serial No: ${serial}`)
  if (client) lines.push(`Client: ${client}`)
  return lines.join('\n')
}

// ── Eccentricity position labels ──────────────────────────────────────────────

const RADIAL_POS      = ['1 — Centre', '2 — Front left', '3 — Back left', '4 — Back right', '5 — Front right']
const END_TO_END_POS  = ['1 — First end', '2 — Second end']
const EME_POS         = ['1 — Front end', '2 — Middle', '3 — Back end']

function eccPositionLabels(eccTestType, errorCount) {
  if (eccTestType === 'EndToEnd')     return END_TO_END_POS
  if (eccTestType === 'EndMiddleEnd') return EME_POS
  if (errorCount <= 2)                return END_TO_END_POS
  if (errorCount === 3)               return EME_POS
  return RADIAL_POS
}

// Formats the conventional mass as "10 kg + 500 mg" or "10 kg − 200 mg"
function fmtConvMass(nominal, errorMg) {
  if (errorMg == null || nominal == null) return nominal ?? '—'
  const rounded = Math.round(errorMg * 1000) / 1000
  if (rounded === 0) return nominal
  return `${nominal} ${rounded > 0 ? '+' : '−'} ${num(Math.abs(rounded), 0)} mg`
}

// ── Shared chrome ─────────────────────────────────────────────────────────────

function Frame({ children }) {
  const { logoUrl } = useCompanyBranding()
  return (
    <div className="border-[1.5px] border-[#B8901F] p-[3px]">
      <div className="border-[0.5px] border-[#1B3A63] px-6 py-5 relative overflow-hidden">
        {/* House mark, matching the PDF's watermark. An <img> rather than a CSS background so it
            survives printing, and aria-hidden because it carries no information. The browser can
            apply opacity here; the PDF's copy is pre-faded because QuestPDF cannot. */}
        <div aria-hidden className="pointer-events-none absolute inset-0 flex items-center justify-center">
          <img src={logoUrl || "/qc-logo.png"} alt="" className="w-[340px] max-w-[55%] opacity-[0.07]" />
        </div>
        <div className="relative">{children}</div>
      </div>
    </div>
  )
}

function CertHeader({ scope, payload, legalName, address, phone, email }) {
  const { name, suffix } = splitLegalName(legalName)
  return (
    <>
      <div className="flex items-start gap-4">
        <div className="flex-[3.1]">
          <p className="text-xl font-bold text-[#0E2340] leading-none">{name}</p>
          {suffix && <p className="text-sm font-bold text-[#B8901F] leading-tight">{suffix}</p>}
          <p className="text-[10px] text-[#5A6675] mt-1 leading-snug">{address}</p>
          <p className="text-[10px] text-[#5A6675] leading-snug">{[email, phone].filter(Boolean).join(' · ')}</p>
        </div>
        <div className="flex-[3.4] text-center self-center">
          <h1 className="text-2xl font-bold text-[#0E2340] leading-tight">CALIBRATION</h1>
          <h1 className="text-2xl font-bold text-[#0E2340] leading-tight">CERTIFICATE</h1>
          <p className="text-[10px] text-[#5A6675] mt-1">{ACCREDITED}</p>
          <p className="text-[10px] italic text-[#1B3A63]">{scope}</p>
        </div>
        <div className="flex-[1.3] flex flex-col items-end shrink-0">
          <QRCodeSVG value={payload} size={74} level="M" fgColor="#0E2340" bgColor="#FFFFFF" />
          <p className="text-[8px] text-[#5A6675] mt-1">Scan for certificate details</p>
        </div>
      </div>
      <div className="border-b-2 border-[#B8901F] mt-3" />
    </>
  )
}

function IdentityStrip({ certNo, docId, calDate, dueDate }) {
  const Label = ({ children }) => (
    <td className="bg-[#0E2340] text-white text-[10px] font-bold px-2 py-1 border border-[#B8901F] whitespace-nowrap">
      {children}
    </td>
  )
  const Value = ({ children }) => (
    <td className="bg-[#FBF6E8] text-[#0E2340] text-[11px] font-bold px-2 py-1 border border-[#B8901F]">
      {children ?? '—'}
    </td>
  )
  return (
    <table className="w-full table-fixed mt-4 border-collapse">
      <tbody>
        <tr>
          <Label>CERTIFICATE No.</Label>      <Value>{certNo}</Value>
          <Label>DOCUMENT CONTROL ID</Label>  <Value>{docId}</Value>
        </tr>
        <tr>
          <Label>DATE OF CALIBRATION</Label>  <Value>{calDate}</Value>
          <Label>NEXT CALIBRATION DUE</Label> <Value>{dueDate}</Value>
        </tr>
      </tbody>
    </table>
  )
}

function Section({ n, title, children }) {
  return (
    <div className="mt-4">
      <div className="flex gap-2 items-baseline">
        <span className="text-[13px] font-bold text-[#B8901F]">{n}.</span>
        <span className="text-[13px] font-bold text-[#0E2340] uppercase">{title}</span>
      </div>
      <div className="border-b border-[#B8901F] mt-[2px]" />
      <div className="mt-1.5">{children}</div>
    </div>
  )
}

function SubTitle({ n, children }) {
  return (
    <p className="mt-3 text-[11px] font-bold">
      <span className="text-[#B8901F]">{n}</span>
      <span className="text-[#1B3A63] ml-1.5">{children}</span>
    </p>
  )
}

function Clause({ n, children }) {
  return (
    <div className="flex gap-2 mt-1 text-[11px] leading-snug text-[#1A1A1A]">
      <span className="shrink-0 w-6 font-bold text-[#1B3A63] tabular-nums">{n}</span>
      <div className="text-justify">{children}</div>
    </div>
  )
}

function GroupLabel({ children }) {
  return <p className="text-[11px] font-bold text-[#B8901F]">{children}</p>
}

function KeyVal({ label, value }) {
  return (
    <div className="flex gap-2 mt-0.5 text-[11px]">
      <span className="flex-[2] font-bold text-[#1B3A63]">{label}</span>
      <span className="flex-[3] text-[#1A1A1A]">{value || '—'}</span>
    </div>
  )
}

function EnvBand({ env, third }) {
  const range = (a, b, unit) => {
    if (a == null && b == null) return '—'
    if (a == null) return `${b} ${unit}`
    if (b == null || b === a) return `${a} ${unit}`
    return `${a} ${unit} – ${b} ${unit}`
  }
  return (
    <div className="mt-1.5 bg-[#FBF6E8] border border-[#E0C572] px-3 py-2 flex gap-4">
      <div className="flex-1">
        <p className="text-[10px] font-bold text-[#1B3A63]">Ambient temperature:</p>
        <p className="text-[11px]">{range(env?.startTemperature, env?.endTemperature, '°C')}</p>
      </div>
      <div className="flex-1">
        <p className="text-[10px] font-bold text-[#1B3A63]">Relative humidity:</p>
        <p className="text-[11px]">{range(env?.startHumidity, env?.endHumidity, '% rh')}</p>
      </div>
      <div className="flex-1">
        <p className="text-[10px] font-bold text-[#1B3A63]">{third.label}</p>
        <p className="text-[11px]">{third.value}</p>
      </div>
    </div>
  )
}

function Th({ children, className = '' }) {
  return (
    <th className={`bg-[#0E2340] text-white text-[10px] font-bold px-1.5 py-1 border border-[#E0C572] text-center leading-tight ${className}`}>
      {children}
    </th>
  )
}

function Td({ children, zebra, left = false, bold = false }) {
  return (
    <td className={[
      'text-[11px] px-1.5 py-[3px] border border-[#C9D2DE]',
      zebra ? 'bg-[#FBF6E8]' : 'bg-white',
      left ? 'text-left' : 'text-center',
      bold ? 'font-bold text-[#0E2340]' : '',
    ].join(' ')}>
      {children}
    </td>
  )
}

function SignBlock({ heading, name, role, date }) {
  return (
    <div className="flex-1 border border-[#C9D2DE] p-2">
      <p className="text-[10px] font-bold text-[#0E2340]">{heading}</p>
      <div className="h-9" />
      <div className="border-b border-[#1B3A63]" />
      <p className="text-[11px] font-bold mt-1">{name || '—'}</p>
      <p className="text-[9px] text-[#5A6675]">{role}</p>
      <p className="text-[9px] text-[#5A6675]">Date: {date}</p>
    </div>
  )
}

function Authorisation({ calBy, appBy, calDate, appDate }) {
  return (
    <div className="flex gap-2 mt-2">
      <SignBlock heading="CALIBRATED BY" name={calBy} role="Calibration Technician" date={calDate} />
      <SignBlock heading="APPROVED BY"   name={appBy}
                 role="Technical Manager · Authorised Signatory" date={appDate} />
      <div className="flex-1 border border-[#C9D2DE] p-2">
        <p className="text-[10px] font-bold text-[#5A6675] text-center">OFFICIAL LABORATORY STAMP</p>
        <div className="h-[52px]" />
      </div>
    </div>
  )
}

function FooterLine({ docId }) {
  return (
    <>
      <div className="border-b border-[#B8901F] mt-4" />
      <p className="text-[9px] italic text-[#5A6675] text-center mt-1.5">
        System-generated document · Document Control ID{' '}
        <span className="font-bold not-italic text-[#1B3A63]">{docId}</span>
        {' '}· Page 1 of 1 · — End of Certificate —
      </p>
    </>
  )
}

function Spine({ children }) {
  return (
    <div className="hidden md:flex items-center justify-center w-5 shrink-0 print:flex">
      <span className="text-[8px] font-bold tracking-wider text-[#8C99AB] whitespace-nowrap"
            style={{ writingMode: 'vertical-rl', transform: 'rotate(180deg)' }}>
        {children}
      </span>
    </div>
  )
}

// ── NAWI certificate ──────────────────────────────────────────────────────────

function NawiCertificate({ rawData, calc, lwo, intakeForm, customerName, customerAddress, branding }) {
  const ecc  = calc?.eccentricity
  const rep  = calc?.repeatability
  const lin  = calc?.linearity ?? []
  const tol  = calc?.tolerance
  const dis  = calc?.discrimination
  const inst = rawData?.instrumentDetails ?? {}
  const tw   = rawData?.testWeights ?? {}
  const env  = rawData?.environmentalConditions

  const isWeighbridge = lwo?.dataSheet?.sheetType === 'NawiWeighbridge'
  const equipment = inst.equipmentType || (isWeighbridge ? 'Weighbridge' : 'Electronic balance')

  const calDate = fmtShort(lwo?.certificateIssuedAt)
  const appDate = lwo?.tmReviewedAt ? fmtShort(lwo.tmReviewedAt) : calDate
  const dueDate = nextDue(lwo?.certificateIssuedAt)

  const [rhoNom, rhoU] = getClassDensity(tw.class)
  const mpeTiers       = getMpeTiers(inst.accuracyClass)

  const failedLin = lin.filter(r => r.definitivePass === false)
  const overallPass = tol?.overallPass

  const eccLabels = eccPositionLabels(rawData?.eccentricityTestType, (ecc?.errors ?? []).length)
  // Read indications directly from raw input — calc.eccentricity.indications may be absent in
  // results stored before that field was added; rawData always has the technician's entries.
  const rawEcc  = rawData?.eccentricity
  const eccInds = rawEcc
    ? [rawEcc.ind1, rawEcc.ind2, rawEcc.ind3, rawEcc.ind4, rawEcc.ind5]
    : (ecc?.indications ?? [])
  const eccErrs = ecc?.errors ?? []

  // Repeatability stores errors (indication − test load); rebuild the indications for display.
  const repInds = (rep?.errors ?? []).map(e => (e == null ? null : rep.testLoad + e))
  const repHalf = Math.ceil(repInds.length / 2)

  const docId = docControlId({
    certificateNumber: lwo?.certificateNumber, jobNumber: lwo?.jobNumber,
    labNo: rawData?.labNo, issuedAt: lwo?.certificateIssuedAt,
  }, 'NAWI', branding.docPrefix)

  let clause = 0
  const c5 = () => `5.${++clause}`

  return (
    <div className="flex">
      <Spine>NON-AUTOMATIC WEIGHING INSTRUMENT · EURAMET cg-18</Spine>
      <div className="flex-1">
        <Frame>
          <CertHeader
            scope="Scope: Non-Automatic Weighing Instruments (NAWI)"
            legalName={branding.legalName} address={branding.address} phone={branding.phone} email={branding.email}
            payload={qrPayload(
              { certificateNumber: lwo?.certificateNumber, issuedAt: lwo?.certificateIssuedAt },
              docId, equipment, inst.serialNo, customerName, branding.legalName)}
          />
          <IdentityStrip certNo={lwo?.certificateNumber} docId={docId}
                         calDate={calDate} dueDate={dueDate} />

          <Section n="1" title="Client and item identification">
            <div className="flex gap-6">
              <div className="flex-1">
                <GroupLabel>Requested by</GroupLabel>
                <KeyVal label="Client:"               value={customerName} />
                <KeyVal label="Address:"              value={customerAddress} />
                <KeyVal label="Place of calibration:" value={intakeForm?.location} />
                <KeyVal label="Lab No.:"              value={rawData?.labNo} />
                <KeyVal label="Sticker No.:"          value={intakeForm?.stickerNumber} />
              </div>
              <div className="flex-1">
                <GroupLabel>Item calibrated</GroupLabel>
                <KeyVal label="Equipment:"            value={equipment} />
                <KeyVal label="Manufacturer / Model:" value={[inst.manufacturer, inst.model].filter(Boolean).join(' · ')} />
                <KeyVal label="Serial No.:"           value={inst.serialNo} />
                <KeyVal label="Max capacity / interval d:" value={[inst.maximumCapacity, inst.division].filter(Boolean).join(' · ')} />
                {inst.rangeType     && <KeyVal label="Range type:"     value={inst.rangeType} />}
                {inst.accuracyClass && <KeyVal label="Accuracy class:" value={inst.accuracyClass} />}
              </div>
            </div>
          </Section>

          <Section n="2" title="Reference standards, method and metrological traceability">
            <Clause n="2.1">
              The weighing instrument was calibrated in accordance with EURAMET Calibration Guide No. 18,
              Version 4.0 (11/2015) — Guidelines on the Calibration of Non-Automatic Weighing Instruments.
            </Clause>
            <Clause n="2.2">
              Calibration procedure: repeatability, eccentricity and linearity (weighing) tests were
              performed in accordance with EURAMET cg-18 v4.0 §4 (Determination of indication errors).
            </Clause>
            <Clause n="2.3">
              Reference standards used: standard masses of Class <strong>{tw.class || '—'}</strong>,
              serial No. <strong>{tw.serialNumber || '—'}</strong>.
            </Clause>
            <Clause n="2.4">
              {tw.traceabilityCertificateNo
                ? `Traceability of reference standards: certificate No. ${tw.traceabilityCertificateNo}, issued by the Kenya Bureau of Standards (KEBS).`
                : 'Traceability of reference standards: the standards are traceable to the national measurement standards.'}
            </Clause>
            <Clause n="2.5">
              This certificate documents traceability to the national measurement standards and to the
              units of measurement realised at KEBS, or at other recognised national metrology institutes,
              in accordance with the International System of Units (SI).
            </Clause>
            <Clause n="2.6">
              Measurement uncertainty evaluated in accordance with JCGM 100:2008 (GUM) and EA-4/02 M:2022.
            </Clause>
          </Section>

          <Section n="3" title="Environmental conditions during calibration">
            <EnvBand env={env} third={{
              label: 'Disturbing influences:',
              value: 'Vibration, air draughts and magnetic influence negligible',
            }} />
          </Section>

          <Section n="4" title="Measurement results">
            {lin.length > 0 && (
              <>
                <SubTitle n="4.1">Weighing (linearity) test — indications before and after adjustment</SubTitle>
                <table className="w-full mt-1.5 border-collapse">
                  <thead>
                    <tr>
                      <Th>Nominal mass<br />(g)</Th>
                      <Th>Indication before<br />adjustment (g)</Th>
                      <Th>Indication after<br />adjustment (g)</Th>
                      <Th>Error of<br />indication (g)</Th>
                      <Th>Coverage<br />factor k</Th>
                      <Th>Expanded uncertainty<br />U of indication (g)</Th>
                    </tr>
                  </thead>
                  <tbody>
                    {lin.map((row, i) => (
                      <tr key={i}>
                        <Td zebra={i % 2 === 1}>{num(row.testLoad, 0)}</Td>
                        <Td zebra={i % 2 === 1}>{numN(row.asFoundIndication ?? row.definitiveIndication, 1)}</Td>
                        <Td zebra={i % 2 === 1}>{numN(row.definitiveIndication, 1)}</Td>
                        <Td zebra={i % 2 === 1}>{signed(row.definitiveError, 1)}</Td>
                        <Td zebra={i % 2 === 1}>{row.coverageFactor != null ? num(row.coverageFactor, 1) : ''}</Td>
                        <Td zebra={i % 2 === 1}>{sig(row.uExpanded)}</Td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </>
            )}

            <div className="flex gap-3 mt-1">
              {ecc && (
                <div className="flex-1">
                  <SubTitle n="4.2">
                    {isWeighbridge ? 'End-to-end loading test' : 'Eccentricity test'} — test load {num(ecc.testLoad, 0)} g
                  </SubTitle>
                  <table className="w-full mt-1.5 border-collapse">
                    <thead>
                      <tr><Th>Load position</Th><Th>Indication (g)</Th><Th>Deviation from centre (g)</Th></tr>
                    </thead>
                    <tbody>
                      {eccLabels.map((pos, i) => (
                        <tr key={i}>
                          <Td zebra={i % 2 === 1} left>{pos}</Td>
                          <Td zebra={i % 2 === 1}>{eccInds[i] != null ? num(eccInds[i], 1) : '—'}</Td>
                          <Td zebra={i % 2 === 1}>
                            {eccErrs[i] == null ? 'reference' : signed(eccErrs[i], 1)}
                          </Td>
                        </tr>
                      ))}
                      <tr>
                        <Td zebra left bold>Maximum deviation</Td>
                        <Td zebra>—</Td>
                        <Td zebra bold>{num(ecc.maximumDeviation, 1)}</Td>
                      </tr>
                    </tbody>
                  </table>
                  {ecc.mpe != null && (
                    <p className="text-[9px] italic text-[#5A6675] mt-0.5">
                      MPE ± {num(ecc.mpe, 1)} g · {ecc.pass === true ? 'PASS' : ecc.pass === false ? 'FAIL' : ''}
                    </p>
                  )}
                </div>
              )}

              {rep && (
                <div className="flex-1">
                  <SubTitle n="4.3">Repeatability test — test load {num(rep.testLoad, 0)} g</SubTitle>
                  <table className="w-full mt-1.5 border-collapse">
                    <thead>
                      <tr><Th>Run</Th><Th>Indication (g)</Th><Th>Run</Th><Th>Indication (g)</Th></tr>
                    </thead>
                    <tbody>
                      {Array.from({ length: repHalf }, (_, i) => {
                        const j = i + repHalf
                        return (
                          <tr key={i}>
                            <Td zebra={i % 2 === 1}>{i + 1}</Td>
                            <Td zebra={i % 2 === 1}>{repInds[i] != null ? num(repInds[i], 1) : '—'}</Td>
                            <Td zebra={i % 2 === 1}>{j < repInds.length ? j + 1 : ''}</Td>
                            <Td zebra={i % 2 === 1}>{j < repInds.length && repInds[j] != null ? num(repInds[j], 1) : ''}</Td>
                          </tr>
                        )
                      })}
                      <tr>
                        <Td zebra left bold>Standard deviation s</Td>
                        <Td zebra bold>{num(rep.standardDeviation, 2)} g</Td>
                        <Td zebra bold>MPE</Td>
                        <Td zebra bold>{rep.mpe != null ? `± ${num(rep.mpe, 1)} g` : '—'}</Td>
                      </tr>
                    </tbody>
                  </table>
                  <p className="text-[9px] italic text-[#5A6675] mt-0.5">
                    {rep.pass === true ? 'PASS' : rep.pass === false ? 'FAIL' : ''}
                    {rep.insufficientReadings ? ' (insufficient readings — advisory only)' : ''}
                  </p>
                </div>
              )}
            </div>

            {dis && (
              <>
                <SubTitle n="4.4">Discrimination test — test load {num(dis.testLoad, 0)} g</SubTitle>
                <p className="text-[11px] mt-0.5">
                  Indication before {numN(dis.indication1, 1)} g · after {numN(dis.indication2, 1)} g ·
                  change {numN(dis.indicationChange, 1)} g (minimum required {numN(dis.minRequiredChange, 1)} g) ·
                  {' '}{dis.pass === true ? 'PASS' : dis.pass === false ? 'FAIL' : ''}
                </p>
              </>
            )}
          </Section>

          <Section n="5" title="Statements and comments">
            <Clause n={c5()}>
              The results reported in clause 4 relate only to the instrument identified in clause 1 of
              this certificate, at the location and under the conditions stated.
            </Clause>
            <Clause n={c5()}>
              <span className={overallPass === false ? 'font-semibold text-red-700' : ''}>
                {overallPass === false
                  ? 'Some of the calculated errors of indication are outside the allowable tolerance for the instrument.'
                  : 'The calculated errors of indication are within the allowable tolerance for the instrument.'}
              </span>
              {' '}Conformity is stated using simple acceptance (guard band w = 0), ISO/IEC 17025:2015 cl. 7.8.6.
              {failedLin.length > 0 && ` Errors outside MPE at loads: ${failedLin.map(r => r.testLoad).join(', ')}.`}
            </Clause>
            {mpeTiers && inst.accuracyClass && (
              <Clause n={c5()}>
                Unless otherwise agreed, tolerances are those permitted for Class{' '}
                <strong>{inst.accuracyClass}</strong> weighing instruments per OIML R 76-1:{' '}
                {mpeTiers.join('; ')}.
              </Clause>
            )}
            <Clause n={c5()}>
              {`The mass standards used have assumed densities of ${num(rhoNom, 0)} kg/m³, with accompanying density uncertainties of ${num(rhoU, 0)} kg/m³.`}
            </Clause>
            <Clause n={c5()}>
              The reported expanded uncertainty of measurement is stated as the standard uncertainty of
              measurement multiplied by the coverage factor k = 2, which, unless otherwise stated,
              corresponds to a coverage probability of approximately 95 %.
            </Clause>
            <Clause n={c5()}>
              This certificate shall not be reproduced except in full, without the written approval of
              the issuing laboratory. It does not of itself imply any product certification or approval by KENAS.
            </Clause>
            <Clause n={c5()}>
              Validity: recalibration is recommended by {dueDate}. The recalibration interval is the
              responsibility of the user.
            </Clause>
            {lwo?.certificateNotes && <Clause n={c5()}>{lwo.certificateNotes}</Clause>}
          </Section>

          <Section n="6" title="Authorisation">
            <Authorisation
              calBy={rawData?.calibrationDoneBy ?? lwo?.benchTechnicianName}
              appBy={lwo?.tmReviewedByName ?? rawData?.checkedBy}
              calDate={calDate} appDate={appDate} />
          </Section>

          <FooterLine docId={docId} />
        </Frame>
      </div>
    </div>
  )
}

// ── Mass certificate ──────────────────────────────────────────────────────────

function MassCertificate({ rawData, calc, lwo, intakeForm, customerName, customerAddress, branding }) {
  const blocks    = calc?.blockResults ?? []
  const rawBlocks = rawData?.blocks ?? []
  const unc       = calc?.uncertainty
  const env       = rawData?.environmentalConditions
  const comp      = rawData?.comparatorDetails

  const calDate = fmtShort(lwo?.certificateIssuedAt)
  const appDate = lwo?.tmReviewedAt ? fmtShort(lwo.tmReviewedAt) : calDate
  const dueDate = nextDue(lwo?.certificateIssuedAt)

  const refStds = rawBlocks.reduce((acc, b) => {
    const key = b.referenceStdSerialNo ?? ''
    if (key && !acc.find(r => r.serialNo === key))
      acc.push({ class: b.referenceStdClass, serialNo: b.referenceStdSerialNo, certNo: b.referenceStdCertificateNo })
    return acc
  }, [])

  const massClass = rawBlocks.find(b => b.class)?.class ?? blocks.find(b => b.class)?.class ?? '—'
  const serials   = [...new Set(rawBlocks.map(b => b.serialNo).filter(Boolean))].join(', ')
  const first     = blocks[0] ?? {}

  const docId = docControlId({
    certificateNumber: lwo?.certificateNumber, jobNumber: lwo?.jobNumber,
    labNo: rawData?.labNo, issuedAt: lwo?.certificateIssuedAt,
  }, 'MASS', branding.docPrefix)

  const allPass = blocks.length > 0 && blocks.every(b => b.withinTolerance === true)
  const k = unc?.coverageFactor ?? 2

  let clause = 0
  const c5 = () => `5.${++clause}`

  const budgetRows = unc ? [
    ['Repeatability of the weighing process',        'u(w)',  'normal',         '1',   unc.uBalance],
    ['Resolution / readability of the comparator',   'u(d)',  'rectangular',    '2√3', unc.uResolution],
    ['Convection effects',                           'u(c)',  'rectangular',    '2√3', unc.uConvection],
    ['Uncertainty of the reference standard',        'u(mᵣ)', 'normal (k = 2)', '2',   unc.uReferenceWeight],
    [`Air buoyancy correction (ρ = ${num(unc.uAsssumedDensityKgM3 ?? 7950, 0)} kg/m³)`,
                                                     'u(B)',  'rectangular',    '√3',  unc.uAirBuoyancy],
  ] : []

  const mg = v => (v == null ? '—' : sig(v * 1000))

  return (
    <div className="flex">
      <Spine>MASS CALIBRATION · OIML R111-1:2004</Spine>
      <div className="flex-1">
        <Frame>
          <CertHeader
            scope="Scope: Mass — OIML Classes E₂, F₁, F₂, M₁, M₂, M₃"
            legalName={branding.legalName} address={branding.address} phone={branding.phone} email={branding.email}
            payload={qrPayload(
              { certificateNumber: lwo?.certificateNumber, issuedAt: lwo?.certificateIssuedAt },
              docId, 'Mass (weight)', serials, customerName, branding.legalName)}
          />
          <IdentityStrip certNo={lwo?.certificateNumber} docId={docId}
                         calDate={calDate} dueDate={dueDate} />

          <Section n="1" title="Client and item identification">
            <div className="flex gap-6">
              <div className="flex-1">
                <GroupLabel>Requested by</GroupLabel>
                <KeyVal label="Client:"               value={customerName} />
                <KeyVal label="Address:"              value={customerAddress} />
                <KeyVal label="Place of calibration:" value={intakeForm?.location} />
                <KeyVal label="Lab No.:"              value={rawData?.labNo} />
                <KeyVal label="Sticker No.:"          value={intakeForm?.stickerNumber} />
              </div>
              <div className="flex-1">
                <GroupLabel>Item calibrated</GroupLabel>
                <KeyVal label="Equipment:"      value="Mass (weight)" />
                <KeyVal label="Type / Class:"   value={massClass === '—' ? '—' : `Class ${massClass}`} />
                <KeyVal label="Nominal value:"  value={first.nominalValue} />
                <KeyVal label="Serial / ID No.:" value={serials} />
                {comp && <KeyVal label="Comparator:" value={[comp.model, comp.serialNo].filter(Boolean).join(' · ')} />}
              </div>
            </div>
          </Section>

          <Section n="2" title="Reference standards, method and metrological traceability">
            <Clause n="2.1">
              The mass was calibrated in accordance with OIML R111-1:2004 — Weights of classes E₁, E₂,
              F₁, F₂, M₁, M₁₋₂, M₂, M₂₋₃ and M₃, Part 1: Metrological and technical requirements.
            </Clause>
            <Clause n="2.2">
              Calibration procedure: procedure for the calibration of Class {massClass} masses
              (direct comparison method).
            </Clause>
            <Clause n="2.3">
              Reference standard used: standard mass of Class{' '}
              <strong>{refStds.map(r => r.class ?? '—').join(', ') || '—'}</strong>, serial No.{' '}
              <strong>{refStds.map(r => r.serialNo).join(', ') || '—'}</strong>.
            </Clause>
            <Clause n="2.4">
              {refStds.some(r => r.certNo)
                ? `Traceability of reference standard: certificate No. ${refStds.filter(r => r.certNo).map(r => r.certNo).join(', ')}, issued by the Kenya Bureau of Standards (KEBS).`
                : 'Traceability of reference standard: the standard is traceable to the national measurement standards.'}
            </Clause>
            <Clause n="2.5">
              This certificate documents traceability to the national measurement standards and to the
              units of measurement realised at KEBS, or at other recognised national metrology institutes,
              in accordance with the International System of Units (SI).
            </Clause>
            <Clause n="2.6">
              Measurement uncertainty evaluated in accordance with JCGM 100:2008 (GUM) and EA-4/02 M:2022.
            </Clause>
          </Section>

          <Section n="3" title="Environmental conditions during calibration">
            <EnvBand env={env} third={{ label: 'Air density (assumed):', value: '1.2 kg/m³' }} />
          </Section>

          <Section n="4" title="Measurement results">
            <table className="w-full mt-1.5 border-collapse">
              <thead>
                <tr>
                  <Th>Nominal<br />mass</Th>
                  <Th>Marking</Th>
                  <Th>Conventional mass<br />value</Th>
                  <Th>Correction<br />from nominal</Th>
                  <Th>Maximum permissible<br />error (± mg)</Th>
                  <Th>Expanded uncertainty<br />U (k = 2) (± mg)</Th>
                </tr>
              </thead>
              <tbody>
                {blocks.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="border border-[#C9D2DE] px-3 py-3 text-center text-[11px] text-[#5A6675] italic">
                      No measurement results available
                    </td>
                  </tr>
                ) : blocks.map((br, i) => {
                  const rawB  = rawBlocks[i] ?? {}
                  const errMg = br.finalCorrectionMg
                    ?? br.correctedDifferenceMg
                    ?? (br.massDifference != null ? br.massDifference * 1000 : null)
                  const uMg = br.uExpanded != null
                    ? Math.round(br.uExpanded * 1000 * 100) / 100
                    : unc?.uExpanded != null
                      ? Math.round(unc.uExpanded * 1000 * 100) / 100
                      : null
                  return (
                    <tr key={i}>
                      <Td zebra={i % 2 === 1}>{br.nominalValue ?? '—'}</Td>
                      <Td zebra={i % 2 === 1}>{rawB.serialNo ?? '—'}</Td>
                      <Td zebra={i % 2 === 1}>{fmtConvMass(br.nominalValue, errMg)}</Td>
                      <Td zebra={i % 2 === 1}>{errMg != null ? `${signed(errMg, 0)} mg` : '—'}</Td>
                      <Td zebra={i % 2 === 1}>{num(br.mpeMilligrams, 0)}</Td>
                      <Td zebra={i % 2 === 1}>{num(uMg, 0)}</Td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
            <p className="text-[9px] italic text-[#5A6675] mt-1">
              Conventional mass value reported at a reference air density of 1.2 kg/m³ and a reference
              density of the weight of 8 000 kg/m³ at 20 °C, per OIML D 28 and OIML R111-1:2004.
            </p>
          </Section>

          <Section n="5" title="Statements and comments">
            <Clause n={c5()}>
              The results reported in clause 4 relate only to the mass identified in clause 1 of this
              certificate, in the condition in which it was received.
            </Clause>
            <Clause n={c5()}>
              <span className={!allPass && blocks.length > 0 ? 'font-semibold text-red-700' : ''}>
                {allPass
                  ? `The calibrated mass has errors within the maximum permissible error for Class ${massClass} specified in OIML R111-1:2004.`
                  : `Some calibrated masses have errors outside the maximum permissible error for Class ${massClass} specified in OIML R111-1:2004.`}
              </span>
              {' '}Conformity is stated using simple acceptance (guard band w = 0), ISO/IEC 17025:2015 cl. 7.8.6.
            </Clause>
            <Clause n={c5()}>
              The reported expanded uncertainty of measurement is stated as the standard uncertainty of
              measurement multiplied by the coverage factor k = {k}, which, unless otherwise stated,
              corresponds to a coverage probability of approximately 95 %.
            </Clause>
            <Clause n={c5()}>
              This certificate shall not be reproduced except in full, without the written approval of
              the issuing laboratory. It does not of itself imply any product certification or approval by KENAS.
            </Clause>
            <Clause n={c5()}>
              Validity: recalibration is recommended by {dueDate}. The recalibration interval is the
              responsibility of the user and depends on usage, handling and environment.
            </Clause>
            {lwo?.certificateNotes && <Clause n={c5()}>{lwo.certificateNotes}</Clause>}
          </Section>

          {unc && (
            <Section n="6" title="Uncertainty of measurement — budget summary">
              <table className="w-full mt-1.5 border-collapse">
                <thead>
                  <tr>
                    <Th>Contribution to uncertainty</Th>
                    <Th>Symbol</Th>
                    <Th>Distribution</Th>
                    <Th>Divisor</Th>
                    <Th>Standard uncertainty<br />uᵢ (mg)</Th>
                  </tr>
                </thead>
                <tbody>
                  {budgetRows.map(([label, sym, dist, div, val], i) => (
                    <tr key={sym}>
                      <Td zebra={i % 2 === 1} left>{label}</Td>
                      <Td zebra={i % 2 === 1}>{sym}</Td>
                      <Td zebra={i % 2 === 1}>{dist}</Td>
                      <Td zebra={i % 2 === 1}>{div}</Td>
                      <Td zebra={i % 2 === 1}>{mg(val)}</Td>
                    </tr>
                  ))}
                  <tr>
                    <Td zebra left bold>Combined standard uncertainty uᴄ = √Σuᵢ²</Td>
                    <Td zebra bold>uᴄ</Td><Td zebra>—</Td><Td zebra>—</Td>
                    <Td zebra bold>{mg(unc.uCombined)}</Td>
                  </tr>
                  <tr>
                    <Td left bold>{`Expanded uncertainty U = k · uᴄ (k = ${k}, ≈ ${unc.confidenceLevel ?? '95 %'})`}</Td>
                    <Td bold>U</Td><Td>normal</Td><Td>—</Td>
                    <Td bold>{mg(unc.uExpanded)}</Td>
                  </tr>
                </tbody>
              </table>
              <p className="text-[9px] italic text-[#5A6675] mt-1">
                Budget prepared per JCGM 100:2008 (GUM) and EA-4/02 M:2022. Individual contributions are
                recorded on the laboratory worksheet retained on file and available to the client on request.
              </p>
            </Section>
          )}

          <Section n={unc ? '7' : '6'} title="Authorisation">
            <Authorisation
              calBy={rawData?.calibrationDoneBy ?? lwo?.benchTechnicianName}
              appBy={lwo?.tmReviewedByName ?? rawData?.checkedBy}
              calDate={calDate} appDate={appDate} />
          </Section>

          <FooterLine docId={docId} />
        </Frame>
      </div>
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function CalibrationCertificatePage() {
  const { id }   = useParams()
  const navigate = useNavigate()
  const printRef = useRef(null)
  const branding = useCompanyBranding()

  const [lwo,           setLwo]           = useState(null)
  const [assignment,    setAssignment]    = useState(null)
  const [loading,       setLoading]       = useState(true)
  const [error,         setError]         = useState(null)
  const [pdfLoading,    setPdfLoading]    = useState(false)
  const [recalcLoading, setRecalcLoading] = useState(false)
  const [recalcError,   setRecalcError]   = useState(null)

  const recalculate = async () => {
    setRecalcLoading(true)
    setRecalcError(null)
    try {
      await api.post(`/api/v1/assignments/${id}/lab-work-order/data-sheet/recalculate`)
      const res = await api.get(`/api/v1/assignments/${id}/lab-work-order`)
      setLwo(res.data?.data ?? res.data)
    } catch (err) {
      const msg = err?.response?.data?.message ?? err?.response?.data ?? err?.message ?? 'Unknown error'
      setRecalcError(`Recalculate failed (${err?.response?.status ?? 'network'}): ${typeof msg === 'string' ? msg : JSON.stringify(msg)}`)
      console.error('[Recalculate]', err)
    } finally { setRecalcLoading(false) }
  }

  const downloadPdf = async () => {
    setPdfLoading(true)
    try {
      const res = await api.get(`/api/v1/assignments/${id}/lab-work-order/certificate-pdf`, { responseType: 'blob' })
      const url = URL.createObjectURL(res.data)
      const a = document.createElement('a')
      a.href = url
      a.download = `${lwo?.certificateNumber ?? id}.pdf`
      a.click()
      URL.revokeObjectURL(url)
    } catch { /* silently fail */ } finally { setPdfLoading(false) }
  }

  useEffect(() => {
    setLoading(true)
    Promise.all([
      api.get(`/api/v1/assignments/${id}`),
      api.get(`/api/v1/assignments/${id}/lab-work-order`),
    ])
      .then(([aRes, lRes]) => {
        setAssignment(aRes.data?.data ?? aRes.data)
        setLwo(lRes.data?.data ?? lRes.data)
      })
      .catch(e => setError(e?.response?.data?.message ?? 'Failed to load certificate data'))
      .finally(() => setLoading(false))
  }, [id])

  if (loading) return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <p className="text-sm text-gray-500">Loading certificate…</p>
    </div>
  )

  if (error || !lwo) return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <div className="text-center">
        <p className="text-sm text-red-500 mb-3">{error ?? 'Certificate not found'}</p>
        <button onClick={() => navigate(-1)} className="text-xs text-indigo-600 underline">Go back</button>
      </div>
    </div>
  )

  const rawData = (() => { try { return JSON.parse(lwo.dataSheet?.rawDataJson ?? 'null') } catch { return null } })()
  const calc    = (() => { try { return JSON.parse(lwo.dataSheet?.calculatedResultsJson ?? 'null') } catch { return null } })()

  const sheetType     = lwo.dataSheet?.sheetType ?? ''
  const isWeighbridge = sheetType === 'NawiWeighbridge'
  // Detect mass via subType, sheetType, or service-request instrument fields (handles stale sheetType in DB)
  const isMassFromSR  = (() => {
    try {
      const sr = JSON.parse(assignment?.serviceRequestDataJson ?? 'null')
      return sr?.instruments?.some(i => i.massNominalValue || i.massAccuracyClass) ?? false
    } catch { return false }
  })()
  const isMass = sheetType === 'Mass'
    || lwo.calibrationSubType === 'Mass'
    || (!isWeighbridge && lwo.calibrationSubType !== 'BalanceAndPlatform' && isMassFromSR)

  const intakeForm      = (() => { try { return JSON.parse(lwo.intakeFormJson ?? 'null') } catch { return null } })()
  const customerName    = intakeForm?.customerName    ?? assignment?.serviceRequest?.customerName ?? '—'
  const customerAddress = intakeForm?.customerAddress ?? '—'

  const Certificate = isMass ? MassCertificate : NawiCertificate

  return (
    <div className="min-h-screen bg-gray-100 py-8 px-4 print:bg-white print:p-0">

      {/* Toolbar */}
      <div className="max-w-5xl mx-auto mb-4 flex items-center justify-between print:hidden">
        <button onClick={() => navigate(-1)} className="flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900">
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
          </svg>
          Back
        </button>
        <div className="flex items-center gap-2">
          <button onClick={recalculate} disabled={recalcLoading}
            className="flex items-center gap-2 px-4 py-2 bg-[#B8901F] hover:bg-[#a07d19] text-white text-sm font-semibold rounded-lg transition-colors disabled:opacity-60">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {recalcLoading ? 'Recalculating…' : 'Recalculate'}
          </button>
          <button onClick={downloadPdf} disabled={pdfLoading}
            className="flex items-center gap-2 px-4 py-2 bg-[#0E2340] hover:bg-[#1B3A63] text-white text-sm font-semibold rounded-lg transition-colors disabled:opacity-60">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 10v6m0 0l-3-3m3 3l3-3M3 17v3a1 1 0 001 1h16a1 1 0 001-1v-3" />
            </svg>
            {pdfLoading ? 'Generating…' : 'Download PDF'}
          </button>
          <button onClick={() => window.print()}
            className="flex items-center gap-2 px-4 py-2 bg-white border border-[#0E2340] text-[#0E2340] hover:bg-gray-50 text-sm font-semibold rounded-lg transition-colors">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17 17h2a2 2 0 002-2v-4a2 2 0 00-2-2H5a2 2 0 00-2 2v4a2 2 0 002 2h2m2 4h6a2 2 0 002-2v-4a2 2 0 00-2-2H9a2 2 0 00-2 2v4a2 2 0 002 2zm8-12V5a2 2 0 00-2-2H9a2 2 0 00-2 2v4h10z" />
            </svg>
            Print
          </button>
        </div>
      </div>

      {recalcError && (
        <div className="max-w-5xl mx-auto mb-4 px-4 py-3 bg-red-50 border border-red-200 rounded-lg text-red-700 text-sm print:hidden">
          {recalcError}
        </div>
      )}

      {/* Certificate body */}
      <div ref={printRef} className="max-w-5xl mx-auto bg-white shadow-lg p-4 print:shadow-none print:max-w-none print:p-0">
        <Certificate
          rawData={rawData}
          calc={calc}
          lwo={lwo}
          intakeForm={intakeForm}
          customerName={customerName}
          customerAddress={customerAddress}
          branding={branding}
        />
      </div>

      <style>{`
        @media print {
          body { background: white; }
          .print\\:hidden { display: none !important; }
          .print\\:shadow-none { box-shadow: none !important; }
          @page { size: A4; margin: 8mm; }
        }
      `}</style>
    </div>
  )
}
