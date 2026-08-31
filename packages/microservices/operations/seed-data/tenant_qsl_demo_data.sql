-- =============================================================================
-- Operations domain — demo/seed data for the LIVE tenant_qsl schema
-- =============================================================================
-- Inserts realistic Projects -> Milestones -> ProjectTasks for a testing /
-- calibration / laboratory-services company (QSL), matching the entity model in
-- packages/microservices/operations/src/OperationsService.Core/Entities and the
-- tenant table shapes created by Migrations/Tenant/20260706121109_TenantInitialCreate.
--
-- HOW TO RUN: Users/Departments live in a SEPARATE database (lante_userservice),
-- not this one (lante_operations) — Postgres can't cross-database SELECT, so the
-- PM/department/assignee ids can't be resolved inline here. Fetch them first,
-- then pass as psql variables:
--   psql "$USERSERVICE_CONN" -t -c \
--     'SELECT "Id" FROM tenant_qsl."Users" WHERE "IsCompanyAdmin" AND NOT "IsDeleted" LIMIT 1'
--   psql "$USERSERVICE_CONN" -t -c \
--     'SELECT "Id" FROM tenant_qsl."Departments" WHERE "Name" = '"'"'Technical'"'"' LIMIT 1'
--   psql "$OPERATIONS_CONN" -v pm_id="'<pm-guid>'" -v dept_id="'<dept-guid>'" \
--     -v u1_id="'<user-guid-or-pm>'" -v u2_id="'<user-guid-or-pm>'" -v u3_id="'<user-guid-or-pm>'" \
--     -f tenant_qsl_demo_data.sql
-- (u1/u2/u3 default to the PM if fewer than 3 other active users exist — that was
-- the case in tenant_qsl as of 2026-07-09: exactly one active user, so every
-- assignee slot fell back to the company admin.)
--
-- IDEMPOTENT: every row has a fixed id (a hardcoded GUID literal, not a
-- human-readable slug — the original 'seed-ops-...' slugs 404'd against this
-- API's [HttpGet("{id:guid}")] route constraints, since the controller only
-- matches GUID-shaped route segments; fixed 2026-07-10). All inserts use
-- ON CONFLICT ("Id") DO NOTHING, so the script is safe to re-run; it never
-- updates existing rows.
--
-- REAL USERS: ProjectManagerId / AssignedToUserId / CreatedBy are plain text ids
-- referencing user-service users (no FK) — passed in via the psql variables
-- above, not resolved inline. Some tasks are deliberately left unassigned
-- (NULL), which the schema allows.
--
-- ENUMS ARE STORED AS integers (OperationsService.Core.Enums.ProjectEnums):
--   ProjectStatus:   0 Draft, 1 Planning, 2 PendingMdApproval, 3 PendingFinanceApproval,
--                    4 Active, 5 OnHold, 6 Completed, 7 Closed, 8 Cancelled
--   ProjectType:     0 Service, 1 Construction, 2 Calibration, 3 ICT, 4 CRM, 5 Sales, 6 General
--   RiskLevel:       0 Low, 1 Medium, 2 High
--   MilestoneStatus: 0 NotStarted, 1 InProgress, 2 Completed, 3 Delayed
--   TaskStatus:      0 NotStarted, 1 InProgress, 2 Done, 3 Blocked
--   ("overdue" is represented as DueDate in the past with Status <> Done)
--
-- Reference "today" for the date mix in this data set: 2026-07-09.
-- =============================================================================

DO $$
DECLARE
    v_now  timestamp := (now() AT TIME ZONE 'utc');
    v_pm   text;   -- project manager (company admin)
    v_dept text;   -- Technical department id
    v_u1   text;   -- task assignees (real users, round-robin)
    v_u2   text;
    v_u3   text;
BEGIN
    -- ── Real principals, passed in via psql -v (see HOW TO RUN above) — cannot
    -- be resolved inline since Users/Departments live in a different database.
    v_pm   := COALESCE(:'pm_id', 'seed-admin');   -- company admin (ProjectManagerId/CreatedBy)
    v_dept := COALESCE(:'dept_id', '');           -- DepartmentId is NOT NULL; '' matches OperationsDbSeeder
    v_u1   := COALESCE(:'u1_id', v_pm);
    v_u2   := COALESCE(:'u2_id', v_pm);
    v_u3   := COALESCE(:'u3_id', v_pm);

    RAISE NOTICE 'Seeding tenant_qsl operations demo data (PM=%, dept=%)', v_pm, v_dept;

    -- ═════════════════════════════════════════════════════════════════════════
    -- PROJECTS (5) — varied status: 3 Active, 1 Planning, 1 Completed
    -- ═════════════════════════════════════════════════════════════════════════
    INSERT INTO tenant_qsl."Projects"
        ("Id","Name","ClientName","ClientReference","TenderReference","ScopeSummary","Notes",
         "Type","Status","RiskLevel","DepartmentId","ProjectManagerId","CrmLeadId",
         "ContractValue","PlannedBudget","ActualCost",
         "StartDate","ExpectedEndDate","ActualEndDate",
         "TenantId","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsDeleted")
    VALUES
    -- P1: internal accreditation programme — Active, mid-flight
    ('2f2c4cf7-29a2-4859-bca0-732c064d64e6',
     'ISO/IEC 17025 Accreditation Readiness Programme',
     'QSL Internal – Quality Assurance', 'QSL-QA-2026-01', NULL,
     'Prepare the metrology and chemistry laboratories for KENAS ISO/IEC 17025 accreditation: gap assessment, QMS documentation overhaul, internal audit and corrective actions, then the external assessment.',
     'Board-sponsored initiative. Accreditation window booked with KENAS for October 2026.',
     6, 4, 1, v_dept, v_pm, NULL,          -- General, Active, Medium
     0, 2400000, 910000,
     TIMESTAMP '2026-02-02 08:00:00', TIMESTAMP '2026-10-30 17:00:00', NULL,
     NULL, v_now, v_now, v_pm, NULL, FALSE),

    -- P2: large client calibration programme — Active, phase 2 slipping
    ('68cf197a-fdae-43fe-a40e-044fcdc1ca46',
     'KPA Mombasa Port – Annual Instrument Calibration Programme',
     'Kenya Ports Authority', 'KPA-INST-2026-114', 'KPA/T/2026/041',
     'Three-phase on-site calibration of ~420 instruments across the Mombasa port estate: pressure & temperature, mass/balances/weighbridges, then certificate issue and close-out.',
     'Port access requires KPA escort passes; weighbridge work needs crane support booked through the client.',
     2, 4, 2, v_dept, v_pm, NULL,          -- Calibration, Active, High
     4200000, 3600000, 1750000,
     TIMESTAMP '2026-04-06 08:00:00', TIMESTAMP '2026-08-31 17:00:00', NULL,
     NULL, v_now, v_now, v_pm, NULL, FALSE),

    -- P3: recurring sampling & lab analysis — Active, on track
    ('89e49186-0483-44ec-aca6-44f301727f79',
     'Tatu City – Quarterly Water Quality Monitoring',
     'Tatu City Ltd', 'TATU-ENV-2026-07', NULL,
     'Quarterly sampling of 18 monitoring points (boreholes, reticulation, effluent) with full physico-chemical and microbiological analysis and NEMA-format reporting.',
     NULL,
     0, 4, 0, v_dept, v_pm, NULL,          -- Service, Active, Low
     1560000, 1180000, 540000,
     TIMESTAMP '2026-01-12 08:00:00', TIMESTAMP '2026-12-18 17:00:00', NULL,
     NULL, v_now, v_now, v_pm, NULL, FALSE),

    -- P4: LIMS rollout — still in Planning
    ('50fc33d2-67eb-48a8-9ae1-4f74f92f5161',
     'LIMS Rollout – Sample Registration to Certificate Issue',
     'QSL Internal – ICT', 'QSL-ICT-2026-03', NULL,
     'Select and deploy a Laboratory Information Management System covering sample intake, worksheets, QC, and automated certificate generation; pilot in the chemistry lab first.',
     'Budget provisionally approved; vendor shortlist due before finance sign-off.',
     3, 1, 1, v_dept, v_pm, NULL,          -- ICT, Planning, Medium
     0, 5200000, 180000,
     TIMESTAMP '2026-06-15 08:00:00', TIMESTAMP '2026-12-18 17:00:00', NULL,
     NULL, v_now, v_now, v_pm, NULL, FALSE),

    -- P5: finished job — Completed, everything done, dates fully in the past
    ('6a629624-93e2-4215-916e-45bca3fcb453',
     'EPRA Depot Flow Meter Proving – Kisumu',
     'Energy & Petroleum Regulatory Authority', 'EPRA-MET-2026-009', 'EPRA/RFQ/2025/188',
     'On-site proving and adjustment of 12 custody-transfer flow meters at the Kisumu fuel depot, with calibration certificates and a close-out report.',
     'Completed ahead of schedule; client feedback positive.',
     2, 6, 0, v_dept, v_pm, NULL,          -- Calibration, Completed, Low
     980000, 760000, 712000,
     TIMESTAMP '2026-01-05 08:00:00', TIMESTAMP '2026-03-27 17:00:00', TIMESTAMP '2026-03-25 16:30:00',
     NULL, v_now, v_now, v_pm, NULL, FALSE)
    ON CONFLICT ("Id") DO NOTHING;

    -- ═════════════════════════════════════════════════════════════════════════
    -- MILESTONES (14) — chronologically ordered per project, mixed statuses
    -- ═════════════════════════════════════════════════════════════════════════
    INSERT INTO tenant_qsl."Milestones"
        ("Id","ProjectId","Title","Description","Order","DueDate","Status","PlannedAmount",
         "TenantId","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsDeleted")
    VALUES
    -- P1 ISO 17025 (4 milestones: done, done, in-progress, not started)
    ('049dc708-591d-4163-911f-94eef18b5462','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Gap Assessment & Baseline Audit',
     'Assess current QMS, equipment traceability and staff competence against ISO/IEC 17025:2017 clauses.',
     1, TIMESTAMP '2026-03-31 17:00:00', 2, 350000, NULL, v_now, v_now, v_pm, NULL, FALSE),
    ('d4d20bc4-4b77-4d21-ad9a-448fe4bd1a3d','2f2c4cf7-29a2-4859-bca0-732c064d64e6','QMS Documentation Overhaul',
     'Rewrite the quality manual, SOPs and forms; establish document control in SharePoint.',
     2, TIMESTAMP '2026-05-15 17:00:00', 2, 520000, NULL, v_now, v_now, v_pm, NULL, FALSE),
    ('a60be783-1fbd-4ac7-88ea-d4669fba67bf','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Internal Audit & Corrective Actions',
     'Full internal audit cycle, NC close-out and management review ahead of the external assessment.',
     3, TIMESTAMP '2026-07-31 17:00:00', 1, 480000, NULL, v_now, v_now, v_pm, NULL, FALSE),
    ('be44cabc-c658-4814-9176-4262054a0e8d','2f2c4cf7-29a2-4859-bca0-732c064d64e6','KENAS Accreditation Assessment',
     'Host the KENAS assessment team; respond to findings and obtain the accreditation decision.',
     4, TIMESTAMP '2026-10-15 17:00:00', 0, 1050000, NULL, v_now, v_now, v_pm, NULL, FALSE),

    -- P2 KPA (3 milestones: done, DELAYED (past due), not started)
    ('bfe83438-159d-4f02-bb2b-835b911961a1','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Phase 1 – Pressure & Temperature Instruments',
     'Calibrate all pressure gauges, transmitters and temperature sensors across berths 1–14.',
     1, TIMESTAMP '2026-05-30 17:00:00', 2, 1400000, NULL, v_now, v_now, v_pm, NULL, FALSE),
    ('928ec79e-4936-434d-8d0d-825f44e95ca8','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Phase 2 – Mass, Balances & Weighbridges',
     'Calibrate lab balances, platform scales and the 4 vehicle weighbridges (crane support required).',
     2, TIMESTAMP '2026-07-05 17:00:00', 3, 1500000, NULL, v_now, v_now, v_pm, NULL, FALSE),
    ('b0ead99e-7e95-41f9-bcc4-b808634b21ca','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Phase 3 – Certificates & Close-out Report',
     'Issue calibration certificates, compile the programme close-out report and hand over to KPA QA.',
     3, TIMESTAMP '2026-08-25 17:00:00', 0, 700000, NULL, v_now, v_now, v_pm, NULL, FALSE),

    -- P3 Tatu City (3 milestones: Q1 done, Q2 done, Q3 in progress)
    ('989e8be3-96d2-4165-b07b-030b6928d922','89e49186-0483-44ec-aca6-44f301727f79','Q1 Sampling Round & Analysis',
     'Q1 field sampling of all 18 points, lab analysis and NEMA-format quarterly report.',
     1, TIMESTAMP '2026-03-20 17:00:00', 2, 290000, NULL, v_now, v_now, v_pm, NULL, FALSE),
    ('72c99cf9-eb13-4a08-aa00-9902b3d51274','89e49186-0483-44ec-aca6-44f301727f79','Q2 Sampling Round & Analysis',
     'Q2 field sampling, analysis and reporting; includes two new borehole points added by the client.',
     2, TIMESTAMP '2026-06-19 17:00:00', 2, 310000, NULL, v_now, v_now, v_pm, NULL, FALSE),
    ('5a4a7b18-2f16-429e-8db4-e3384f224b7a','89e49186-0483-44ec-aca6-44f301727f79','Q3 Sampling Round & Analysis',
     'Q3 field sampling, analysis and reporting.',
     3, TIMESTAMP '2026-09-18 17:00:00', 1, 310000, NULL, v_now, v_now, v_pm, NULL, FALSE),

    -- P4 LIMS (2 milestones: in progress, not started)
    ('7fce0e3f-1d2d-4b86-8002-843416359d9e','50fc33d2-67eb-48a8-9ae1-4f74f92f5161','Requirements & Vendor Selection',
     'Document lab workflows, issue the RFP, score vendor demos and sign the contract.',
     1, TIMESTAMP '2026-08-14 17:00:00', 1, 400000, NULL, v_now, v_now, v_pm, NULL, FALSE),
    ('5d2506d4-edb7-4a05-b0cc-5fd74e796617','50fc33d2-67eb-48a8-9ae1-4f74f92f5161','Pilot Deployment – Chemistry Lab',
     'Install, configure and validate the LIMS for the chemistry lab; train analysts; go/no-go review.',
     2, TIMESTAMP '2026-11-13 17:00:00', 0, 1800000, NULL, v_now, v_now, v_pm, NULL, FALSE),

    -- P5 EPRA (2 milestones: both completed, in the past)
    ('d92450c0-ddf2-4a9a-a2e2-861113b4bb81','6a629624-93e2-4215-916e-45bca3fcb453','On-site Meter Proving',
     'Prove all 12 custody-transfer meters against the master meter; adjust and re-prove out-of-tolerance units.',
     1, TIMESTAMP '2026-02-27 17:00:00', 2, 520000, NULL, v_now, v_now, v_pm, NULL, FALSE),
    ('3d8398d2-8e75-4ae9-8c7c-d8de7c289a15','6a629624-93e2-4215-916e-45bca3fcb453','Certification & Handover',
     'Issue calibration certificates, deliver the close-out report and obtain client sign-off.',
     2, TIMESTAMP '2026-03-25 17:00:00', 2, 240000, NULL, v_now, v_now, v_pm, NULL, FALSE)
    ON CONFLICT ("Id") DO NOTHING;

    -- ═════════════════════════════════════════════════════════════════════════
    -- TASKS (66) — 4–6 per milestone; status/dates coherent with the milestone
    -- ═════════════════════════════════════════════════════════════════════════
    INSERT INTO tenant_qsl."ProjectTasks"
        ("Id","MilestoneId","ProjectId","Title","Description","DueDate","AssignedToUserId",
         "Status","LinkedAssignmentId","TenantId","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsDeleted")
    VALUES
    -- ── P1 / MS iso-1 (Completed) — all Done, Feb–Mar ────────────────────────
    ('efaf29a3-1750-4944-af60-861dcddffee8','049dc708-591d-4163-911f-94eef18b5462','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Clause-by-clause gap checklist (ISO/IEC 17025:2017)',NULL,TIMESTAMP '2026-02-20 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('cd9be12b-8ffd-48da-891e-85fd67379b83','049dc708-591d-4163-911f-94eef18b5462','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Equipment traceability register review','Verify every reference standard has an unbroken traceability chain to SI.',TIMESTAMP '2026-03-06 17:00:00',v_u2,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('f1d7f079-133f-4944-bd32-95a84394ac5d','049dc708-591d-4163-911f-94eef18b5462','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Staff competence matrix & training-gap analysis',NULL,TIMESTAMP '2026-03-13 17:00:00',v_u3,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('0d179261-9ea2-4063-986e-4e52cf44c3f1','049dc708-591d-4163-911f-94eef18b5462','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Environmental conditions audit (temp/humidity logs)',NULL,TIMESTAMP '2026-03-20 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('fd1330ee-ba34-4442-844c-4521c44b0d40','049dc708-591d-4163-911f-94eef18b5462','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Gap assessment report & board presentation',NULL,TIMESTAMP '2026-03-31 17:00:00',v_pm,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),

    -- ── P1 / MS iso-2 (Completed) — all Done, Apr–May ────────────────────────
    ('b5f010e2-9d15-4fce-9737-72a50b33e08d','d4d20bc4-4b77-4d21-ad9a-448fe4bd1a3d','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Rewrite quality manual to 2017 structure',NULL,TIMESTAMP '2026-04-17 17:00:00',v_u2,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('0a8a3bfb-573f-4fc5-b8f3-39babc9894f0','d4d20bc4-4b77-4d21-ad9a-448fe4bd1a3d','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Update 24 calibration SOPs & worksheets',NULL,TIMESTAMP '2026-04-30 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('4fa03a65-dc5a-4957-939d-6194a5f3329c','d4d20bc4-4b77-4d21-ad9a-448fe4bd1a3d','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Measurement uncertainty budgets for all disciplines','GUM-method budgets reviewed by the technical manager.',TIMESTAMP '2026-05-08 17:00:00',v_u3,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('9e957895-e3bb-4090-860f-3bdf4c03592d','d4d20bc4-4b77-4d21-ad9a-448fe4bd1a3d','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Set up document control library & retention rules',NULL,TIMESTAMP '2026-05-12 17:00:00',v_u2,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('29ac4417-733e-49b6-b63a-3790b6d678ff','d4d20bc4-4b77-4d21-ad9a-448fe4bd1a3d','2f2c4cf7-29a2-4859-bca0-732c064d64e6','All-staff QMS awareness training',NULL,TIMESTAMP '2026-05-15 17:00:00',v_pm,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),

    -- ── P1 / MS iso-3 (InProgress, due 31 Jul) — realistic mix incl. overdue ─
    ('2cc5df61-94d6-4e70-a30b-dd8908f277c6','a60be783-1fbd-4ac7-88ea-d4669fba67bf','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Internal audit – metrology lab',NULL,TIMESTAMP '2026-06-19 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('8ab7dd30-ddba-4d80-b217-5ac8798b859e','a60be783-1fbd-4ac7-88ea-d4669fba67bf','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Internal audit – chemistry lab',NULL,TIMESTAMP '2026-06-26 17:00:00',v_u2,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('76b45687-c577-4747-b37b-a66622981b85','a60be783-1fbd-4ac7-88ea-d4669fba67bf','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Close out NC-2026-011: balance calibration interval exceeded','Corrective action: shorten interval to 6 months and recalibrate affected balance.',TIMESTAMP '2026-07-03 17:00:00',v_u3,1,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),  -- OVERDUE (due 3 Jul, still in progress)
    ('f776e55f-653a-480b-911c-ccd80e2437fc','a60be783-1fbd-4ac7-88ea-d4669fba67bf','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Proficiency testing round – submit PT results to KENAS scheme',NULL,TIMESTAMP '2026-07-17 17:00:00',v_u1,1,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('a145c629-9565-4e14-9cc4-dcaceeec71b5','a60be783-1fbd-4ac7-88ea-d4669fba67bf','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Replace hygrometer in mass lab (blocking humidity NC)','Blocked: replacement unit stuck at customs clearance.',TIMESTAMP '2026-07-10 17:00:00',v_u2,3,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),  -- BLOCKED
    ('e6ddc2a2-3c82-4b8c-b23e-405c647fb991','a60be783-1fbd-4ac7-88ea-d4669fba67bf','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Management review meeting & minutes',NULL,TIMESTAMP '2026-07-31 17:00:00',v_pm,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),

    -- ── P1 / MS iso-4 (NotStarted, Oct) — all NotStarted, future dates ───────
    ('f950d881-8118-41ea-a7ff-a9a0839080af','be44cabc-c658-4814-9176-4262054a0e8d','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Submit application pack to KENAS',NULL,TIMESTAMP '2026-08-21 17:00:00',v_pm,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('b9dee284-a1ab-4f8a-b680-44b175d2f729','be44cabc-c658-4814-9176-4262054a0e8d','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Pre-assessment dry run with external consultant',NULL,TIMESTAMP '2026-09-18 17:00:00',v_u1,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('3c3bcf4e-53e8-4b4a-92fb-0390af1ec4e9','be44cabc-c658-4814-9176-4262054a0e8d','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Host KENAS on-site assessment (3 days)',NULL,TIMESTAMP '2026-10-09 17:00:00',NULL,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),  -- unassigned
    ('d6621b82-d264-4257-846a-62d10f8f7d20','be44cabc-c658-4814-9176-4262054a0e8d','2f2c4cf7-29a2-4859-bca0-732c064d64e6','Respond to assessment findings within 14 days',NULL,TIMESTAMP '2026-10-15 17:00:00',NULL,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),  -- unassigned

    -- ── P2 / MS kpa-1 (Completed) — all Done, Apr–May ────────────────────────
    ('ebc2c68e-b49a-4385-8e3a-5580b42a5db6','bfe83438-159d-4f02-bb2b-835b911961a1','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Mobilise team & obtain KPA escort passes',NULL,TIMESTAMP '2026-04-10 17:00:00',v_pm,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('fe5c1cd7-0933-4336-9113-0a3455ee82f0','bfe83438-159d-4f02-bb2b-835b911961a1','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Calibrate pressure gauges & transmitters – berths 1–7',NULL,TIMESTAMP '2026-04-24 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('f20caad6-3fe5-4f7c-a94a-68f0449a44ff','bfe83438-159d-4f02-bb2b-835b911961a1','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Calibrate pressure gauges & transmitters – berths 8–14',NULL,TIMESTAMP '2026-05-08 17:00:00',v_u2,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('0123e993-0000-4ebf-9ef7-00874c90c329','bfe83438-159d-4f02-bb2b-835b911961a1','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Calibrate temperature sensors – cold stores & reefer yard',NULL,TIMESTAMP '2026-05-22 17:00:00',v_u3,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('c0417a9d-c195-446c-86a6-c7dd23c552cf','bfe83438-159d-4f02-bb2b-835b911961a1','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Phase 1 QA review of raw calibration data',NULL,TIMESTAMP '2026-05-30 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),

    -- ── P2 / MS kpa-2 (DELAYED, was due 5 Jul) — done/overdue/blocked mix ────
    ('a20ed4a5-aad7-40a2-8c3a-c8109be18ebf','928ec79e-4936-434d-8d0d-825f44e95ca8','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Calibrate analytical & platform balances – port QA lab',NULL,TIMESTAMP '2026-06-12 17:00:00',v_u2,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('6d3509b8-21b4-4947-9929-c6637f7889ba','928ec79e-4936-434d-8d0d-825f44e95ca8','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Calibrate platform scales – container terminal',NULL,TIMESTAMP '2026-06-19 17:00:00',v_u3,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('9674d75b-abb7-404f-9a59-8b8f0031a609','928ec79e-4936-434d-8d0d-825f44e95ca8','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Weighbridge 1 & 2 calibration (50t test weights)',NULL,TIMESTAMP '2026-06-27 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('f364859e-f260-4785-860f-9d5cdcfc29af','928ec79e-4936-434d-8d0d-825f44e95ca8','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Weighbridge 3 & 4 calibration','Blocked: client crane for test-weight handling reallocated to vessel operations; awaiting new slot from KPA.',TIMESTAMP '2026-07-04 17:00:00',v_u1,3,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),  -- BLOCKED + OVERDUE (why the milestone is Delayed)
    ('63f1cc63-99bf-4607-b7ba-d0c13c364484','928ec79e-4936-434d-8d0d-825f44e95ca8','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Re-test out-of-tolerance balance (QA lab, asset KPA-BAL-07)',NULL,TIMESTAMP '2026-07-05 17:00:00',v_u2,1,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),  -- OVERDUE in progress
    ('a8f9aa44-e805-409c-8ae4-4d69ea05a664','928ec79e-4936-434d-8d0d-825f44e95ca8','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Phase 2 QA review of raw calibration data',NULL,TIMESTAMP '2026-07-12 17:00:00',NULL,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),  -- unassigned

    -- ── P2 / MS kpa-3 (NotStarted, Aug) — all NotStarted ─────────────────────
    ('ee618f74-5efc-46b4-90fd-310cf780cd53','b0ead99e-7e95-41f9-bcc4-b808634b21ca','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Generate & peer-review calibration certificates (batch 1)',NULL,TIMESTAMP '2026-08-07 17:00:00',v_u2,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('98de6e07-6ccb-4b1b-9ede-91a4c2c5d9da','b0ead99e-7e95-41f9-bcc4-b808634b21ca','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Generate & peer-review calibration certificates (batch 2)',NULL,TIMESTAMP '2026-08-14 17:00:00',v_u3,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('59a78c8d-ce7c-43e0-9634-4768afd00f22','b0ead99e-7e95-41f9-bcc4-b808634b21ca','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Compile programme close-out report',NULL,TIMESTAMP '2026-08-21 17:00:00',v_pm,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('a0c6f7b0-13ec-4b0a-b43e-bd2ccbe7ecc3','b0ead99e-7e95-41f9-bcc4-b808634b21ca','68cf197a-fdae-43fe-a40e-044fcdc1ca46','Client handover meeting with KPA QA',NULL,TIMESTAMP '2026-08-25 17:00:00',NULL,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),  -- unassigned

    -- ── P3 / MS tatu-1 (Completed, Q1) — all Done ────────────────────────────
    ('05e83ab2-a087-4cc0-9e90-8c999a93b117','989e8be3-96d2-4165-b07b-030b6928d922','89e49186-0483-44ec-aca6-44f301727f79','Q1 field sampling – 18 monitoring points','Includes chain-of-custody forms and cooler-box temperature logs.',TIMESTAMP '2026-02-13 17:00:00',v_u3,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('e30bfe80-d0d4-4463-97ca-e6a9e294a514','989e8be3-96d2-4165-b07b-030b6928d922','89e49186-0483-44ec-aca6-44f301727f79','Q1 physico-chemical analysis',NULL,TIMESTAMP '2026-02-27 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('e8cdcf0c-6b5b-42df-ad74-cb477122b3db','989e8be3-96d2-4165-b07b-030b6928d922','89e49186-0483-44ec-aca6-44f301727f79','Q1 microbiological analysis',NULL,TIMESTAMP '2026-02-27 17:00:00',v_u2,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('f28e88c3-247e-4981-b78c-155f693550d7','989e8be3-96d2-4165-b07b-030b6928d922','89e49186-0483-44ec-aca6-44f301727f79','Q1 NEMA-format report & client submission',NULL,TIMESTAMP '2026-03-20 17:00:00',v_pm,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),

    -- ── P3 / MS tatu-2 (Completed, Q2) — all Done ────────────────────────────
    ('ebec05a4-d9e8-4287-b1e1-8b263cf41f0d','72c99cf9-eb13-4a08-aa00-9902b3d51274','89e49186-0483-44ec-aca6-44f301727f79','Q2 field sampling – 20 monitoring points (2 new boreholes)',NULL,TIMESTAMP '2026-05-15 17:00:00',v_u3,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('ed2eb740-0a84-4ef2-956a-c97c9521bb95','72c99cf9-eb13-4a08-aa00-9902b3d51274','89e49186-0483-44ec-aca6-44f301727f79','Q2 physico-chemical analysis',NULL,TIMESTAMP '2026-05-29 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('9d972070-dee3-47ed-bde1-d0bb43cc61b9','72c99cf9-eb13-4a08-aa00-9902b3d51274','89e49186-0483-44ec-aca6-44f301727f79','Q2 microbiological analysis',NULL,TIMESTAMP '2026-05-29 17:00:00',v_u2,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('b68c57d2-dd2d-4fa6-a492-333905c34c75','72c99cf9-eb13-4a08-aa00-9902b3d51274','89e49186-0483-44ec-aca6-44f301727f79','Investigate elevated turbidity at point TC-11','Re-sampled and confirmed within limits; construction runoff identified as cause.',TIMESTAMP '2026-06-12 17:00:00',v_u3,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('7ad14c3c-65a4-46a7-920b-8948b9527486','72c99cf9-eb13-4a08-aa00-9902b3d51274','89e49186-0483-44ec-aca6-44f301727f79','Q2 NEMA-format report & client submission',NULL,TIMESTAMP '2026-06-19 17:00:00',v_pm,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),

    -- ── P3 / MS tatu-3 (InProgress, Q3) — early tasks done/in progress ───────
    ('c398ee85-de79-4089-87fc-8a27d77862df','5a4a7b18-2f16-429e-8db4-e3384f224b7a','89e49186-0483-44ec-aca6-44f301727f79','Prepare Q3 sampling plan & bottle order',NULL,TIMESTAMP '2026-07-03 17:00:00',v_u3,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('e1e5f692-5c71-476b-b70d-2612039d4a64','5a4a7b18-2f16-429e-8db4-e3384f224b7a','89e49186-0483-44ec-aca6-44f301727f79','Q3 field sampling – 20 monitoring points',NULL,TIMESTAMP '2026-08-14 17:00:00',v_u3,1,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('7e967599-d920-4328-93bd-99154e232cf9','5a4a7b18-2f16-429e-8db4-e3384f224b7a','89e49186-0483-44ec-aca6-44f301727f79','Q3 physico-chemical analysis',NULL,TIMESTAMP '2026-08-28 17:00:00',v_u1,1,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('bd7ec395-2c21-4019-a056-9370ac0dc6ff','5a4a7b18-2f16-429e-8db4-e3384f224b7a','89e49186-0483-44ec-aca6-44f301727f79','Q3 microbiological analysis',NULL,TIMESTAMP '2026-08-28 17:00:00',v_u2,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('3127d1c7-3ad3-4d56-90b9-8c8c935988e1','5a4a7b18-2f16-429e-8db4-e3384f224b7a','89e49186-0483-44ec-aca6-44f301727f79','Q3 NEMA-format report & client submission',NULL,TIMESTAMP '2026-09-18 17:00:00',NULL,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),  -- unassigned

    -- ── P4 / MS lims-1 (InProgress) — mixed, incl. blocked procurement ───────
    ('4044f683-2e86-49d3-bb17-cdeed263fdff','7fce0e3f-1d2d-4b86-8002-843416359d9e','50fc33d2-67eb-48a8-9ae1-4f74f92f5161','Map current sample-to-certificate workflow',NULL,TIMESTAMP '2026-06-26 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('2380bcbe-282d-4df1-a13a-b92c4c167c4e','7fce0e3f-1d2d-4b86-8002-843416359d9e','50fc33d2-67eb-48a8-9ae1-4f74f92f5161','Draft functional requirements specification',NULL,TIMESTAMP '2026-07-10 17:00:00',v_u2,1,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('92cad2d1-b4b6-409e-a988-e8e06529029f','7fce0e3f-1d2d-4b86-8002-843416359d9e','50fc33d2-67eb-48a8-9ae1-4f74f92f5161','Issue RFP to shortlisted LIMS vendors','Blocked: awaiting finance sign-off on the procurement budget line.',TIMESTAMP '2026-07-24 17:00:00',v_pm,3,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),  -- BLOCKED
    ('367ee5d5-9af1-43c9-a032-383fd02a630f','7fce0e3f-1d2d-4b86-8002-843416359d9e','50fc33d2-67eb-48a8-9ae1-4f74f92f5161','Score vendor demos with lab leads',NULL,TIMESTAMP '2026-08-07 17:00:00',v_u3,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('5f7472dd-ad97-4709-a8bf-4920c5eb818d','7fce0e3f-1d2d-4b86-8002-843416359d9e','50fc33d2-67eb-48a8-9ae1-4f74f92f5161','Contract negotiation & award',NULL,TIMESTAMP '2026-08-14 17:00:00',NULL,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),  -- unassigned

    -- ── P4 / MS lims-2 (NotStarted, Nov) — all NotStarted ────────────────────
    ('0e341958-94f5-449c-bed3-5d57b3bddd0d','5d2506d4-edb7-4a05-b0cc-5fd74e796617','50fc33d2-67eb-48a8-9ae1-4f74f92f5161','Install & configure LIMS test environment',NULL,TIMESTAMP '2026-09-25 17:00:00',NULL,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('96df1230-6a77-4a6e-b63a-ff25cfa0c550','5d2506d4-edb7-4a05-b0cc-5fd74e796617','50fc33d2-67eb-48a8-9ae1-4f74f92f5161','Migrate chemistry test methods & report templates',NULL,TIMESTAMP '2026-10-16 17:00:00',NULL,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('6ee8ca31-b1cc-422a-913b-2ed66073b212','5d2506d4-edb7-4a05-b0cc-5fd74e796617','50fc33d2-67eb-48a8-9ae1-4f74f92f5161','Analyst training & parallel-run validation',NULL,TIMESTAMP '2026-11-06 17:00:00',NULL,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('c5bdd3f8-3b99-4029-9b8d-600175987ec6','5d2506d4-edb7-4a05-b0cc-5fd74e796617','50fc33d2-67eb-48a8-9ae1-4f74f92f5161','Pilot go/no-go review with QA and ICT',NULL,TIMESTAMP '2026-11-13 17:00:00',v_pm,0,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),

    -- ── P5 / MS epra-1 (Completed) — all Done, Jan–Feb ───────────────────────
    ('a43bf585-5220-4b59-9a7d-9ce898baeed2','d92450c0-ddf2-4a9a-a2e2-861113b4bb81','6a629624-93e2-4215-916e-45bca3fcb453','Mobilise master meter & prover to Kisumu depot',NULL,TIMESTAMP '2026-01-16 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('10d58872-cafb-4d23-96c4-d87bee4a2a10','d92450c0-ddf2-4a9a-a2e2-861113b4bb81','6a629624-93e2-4215-916e-45bca3fcb453','Prove meters 1–6 (loading gantry A)',NULL,TIMESTAMP '2026-02-06 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('ba323b8d-1c83-4549-b5d7-b4240d5b6bfa','d92450c0-ddf2-4a9a-a2e2-861113b4bb81','6a629624-93e2-4215-916e-45bca3fcb453','Prove meters 7–12 (loading gantry B)',NULL,TIMESTAMP '2026-02-20 17:00:00',v_u2,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('e0653272-0027-4384-b34b-2da2f1d44990','d92450c0-ddf2-4a9a-a2e2-861113b4bb81','6a629624-93e2-4215-916e-45bca3fcb453','Adjust & re-prove out-of-tolerance meters (3 units)',NULL,TIMESTAMP '2026-02-27 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),

    -- ── P5 / MS epra-2 (Completed) — all Done, Mar ───────────────────────────
    ('0ca64b86-7d56-4aaf-809f-62aa91119719','3d8398d2-8e75-4ae9-8c7c-d8de7c289a15','6a629624-93e2-4215-916e-45bca3fcb453','Issue calibration certificates (12 meters)',NULL,TIMESTAMP '2026-03-13 17:00:00',v_u2,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('dac6e632-d113-437c-827c-a0bd30df5c85','3d8398d2-8e75-4ae9-8c7c-d8de7c289a15','6a629624-93e2-4215-916e-45bca3fcb453','Compile close-out report with as-found/as-left data',NULL,TIMESTAMP '2026-03-20 17:00:00',v_u3,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('d5ff4e66-8a25-4f20-8876-9829b66db72d','3d8398d2-8e75-4ae9-8c7c-d8de7c289a15','6a629624-93e2-4215-916e-45bca3fcb453','Client sign-off meeting at EPRA offices',NULL,TIMESTAMP '2026-03-25 17:00:00',v_pm,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE),
    ('48a8e139-48ab-49a9-97e5-e673fd557d8b','3d8398d2-8e75-4ae9-8c7c-d8de7c289a15','6a629624-93e2-4215-916e-45bca3fcb453','Demobilise equipment & archive job file',NULL,TIMESTAMP '2026-03-25 17:00:00',v_u1,2,NULL,NULL,v_now,v_now,v_pm,NULL,FALSE)
    ON CONFLICT ("Id") DO NOTHING;

    RAISE NOTICE 'Done. Seeded (or skipped, if already present): 5 projects, 14 milestones, 66 tasks.';
END
$$;

-- Quick verification queries (read-only, optional) — ids are fixed GUID literals now
-- (see the INSERT statements above), so match on ClientReference instead of a slug prefix:
--   SELECT "Id", "Name", "Status", "StartDate", "ExpectedEndDate" FROM tenant_qsl."Projects"
--    WHERE "ClientReference" IN ('QSL-QA-2026-01','KPA-INST-2026-114','TATU-ENV-2026-07','QSL-ICT-2026-03','EPRA-MET-2026-009');
--   SELECT p."Name", m."Order", m."Title", m."Status", m."DueDate", count(t."Id") AS tasks
--     FROM tenant_qsl."Milestones" m
--     JOIN tenant_qsl."Projects" p ON p."Id" = m."ProjectId"
--     LEFT JOIN tenant_qsl."ProjectTasks" t ON t."MilestoneId" = m."Id"
--    WHERE p."ClientReference" IN ('QSL-QA-2026-01','KPA-INST-2026-114','TATU-ENV-2026-07','QSL-ICT-2026-03','EPRA-MET-2026-009')
--    GROUP BY p."Name", m."Order", m."Title", m."Status", m."DueDate"
--    ORDER BY p."Name", m."Order";
--
-- To remove this demo data later — Milestones/ProjectTasks/BudgetLines/ProjectResources/
-- ProjectApprovals/ProjectHistories/CostEntries all FK to Projects with ON DELETE CASCADE,
-- so deleting the 5 project rows (by the fixed GUIDs used in the INSERT above) is enough:
--   DELETE FROM tenant_qsl."Projects" WHERE "ClientReference" IN
--     ('QSL-QA-2026-01','KPA-INST-2026-114','TATU-ENV-2026-07','QSL-ICT-2026-03','EPRA-MET-2026-009');
