#!/usr/bin/env python3
"""
Fail when a money-bearing service calls `Math.Round` directly on a currency value instead of going
through its own `Money.Round` helper.

.NET's bare `Math.Round(x, 2)` defaults to banker's rounding (ToEven); each service's `Money.Round`
is pinned to commercial rounding (AwayFromZero) instead — the convention a Kenyan accountant expects
on a payslip, and what payroll (`PayrollRunService.Round`) already used before this landed. The two
modes only disagree at exact half-cents, which is exactly what percentage arithmetic (PAYE, NSSF,
SHA, VAT, discounts, landed-cost apportionment) produces, and finance's own `JournalService` refuses
to post a journal whose debits and credits differ by even one cent — so a service that rounds money
differently than finance does can have its figures rejected at the finance boundary. See #380.

This is deliberately scoped to money-bearing services only, NOT a repo-wide sweep: `operations`'
~72 `Math.Round` calls are mostly percentages/durations where the rounding mode is immaterial, and
plenty of `Math.Round` calls *within* the services below are legitimately non-money too (scores,
percentages, hours, days) — see KNOWN_GAPS below, which baselines those files rather than pretending
this check understands C# well enough to tell a currency value from a percentage on its own.

Deliberately regex-based, not a C# parser — matches the style of every other validator here.

Usage:  python3 scripts/ci/validate_money_rounding.py
Exit:   0 clean, 1 an un-baselined bare Math.Round found in a money-bearing service, 2 could not parse.
"""
import glob
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Services with real currency amounts running through them. Not operations (percentages/durations,
# no money math) and not the services with zero money `Math.Round` call sites (ticketing, hse,
# compliance — checked by hand for #380; nothing to fix there today).
MONEY_SERVICES = ["finance", "hr", "procurement", "reporting", "crm"]

MATH_ROUND_RE = re.compile(r"\bMath\.Round\(")

# file (repo-relative) -> reason every remaining bare Math.Round in it is legitimately not money
# (percentages, scores, hours, days) or is intentionally out of scope for #380. A file that stops
# needing its entry is a FAILURE here, same as every other KNOWN_GAPS in this repo — the baseline
# can only shrink.
KNOWN_GAPS = {
    "packages/microservices/finance/src/FinanceService.Infrastructure/Services/BudgetService.cs":
        "ConsumedPct/AchievedPct are percentages, not currency",
    "packages/microservices/finance/src/FinanceService.Core/Services/DepreciationRules.cs":
        "#360 (depreciation ToEven) is tracked and decided separately from #380 — not touched here",
    "packages/microservices/hr/src/HrService.Core/Services/AttendanceService.cs":
        "punctuality/attendance rates and worked-hours, not currency",
    "packages/microservices/hr/src/HrService.Core/Services/LearningService.cs":
        "PercentOfTarget/PercentUsed are percentages — the file's actual money calls go through Round() -> Money.Round",
    "packages/microservices/hr/src/HrService.Core/Services/SalaryIncrementService.cs":
        "IncreasePercent/the '+x%' formatter are percentages — IncreaseAmount goes through Round() -> Money.Round",
    "packages/microservices/hr/src/HrService.Core/Services/AppraisalService.cs":
        "appraisal weights/scores only — no currency in this file",
    "packages/microservices/hr/src/HrService.Core/Services/RecruitmentService.cs":
        "days-to-hire, shortlist/hire rates, interview scores — no currency",
    "packages/microservices/hr/src/HrService.Core/Services/LeaveService.cs":
        "days-allowed proration — not currency, and already explicit AwayFromZero",
    "packages/microservices/procurement/src/ProcurementService.Core/Services/PerformanceReviewService.cs":
        "vendor performance scores (RejectRatePct, QualityScore, etc.) — no currency; its own Round2 is separate from InternationalPoService's",
    "packages/microservices/procurement/src/ProcurementService.Core/Services/QuotationService.cs":
        "TotalScore is a weighted composite score, not currency",
    "packages/microservices/reporting/src/ReportingService.Infrastructure/Services/RevenueVsTargetReportService.cs":
        "AchievedPct is a percentage, not currency",
    "packages/microservices/reporting/src/ReportingService.Infrastructure/Services/ProjectProfitabilityReportService.cs":
        "UtilizationPercent is a percentage, not currency",
    "packages/microservices/crm/src/CrmService.Core/Services/MarketingService.cs":
        "remaining calls are ROI/spend-percent ratios — CostPerLead (actual currency) already goes through Money.Round",
    "packages/microservices/crm/src/CrmService.Core/Services/AfterSalesService.cs":
        "NPS/CSAT scores — no currency",
    "packages/microservices/crm/src/CrmService.Core/Services/DashboardService.cs":
        "win-rate/attainment percentages — no currency",
    "packages/microservices/crm/src/CrmService.Core/Services/QuotationService.cs":
        "remaining call is a price-variance percentage — the file's real money (discount/line total/VAT) goes through Money.Round",
    "packages/microservices/crm/src/CrmService.Infrastructure/Services/CrmBackgroundService.cs":
        "spend percentage, not currency",
}


def die(msg: str) -> None:
    print(f"::error::{msg}")
    sys.exit(2)


def main() -> int:
    errors = []
    baselined = []
    checked = 0

    for svc in MONEY_SERVICES:
        svc_dir = os.path.join(REPO, "packages/microservices", svc)
        if not os.path.isdir(svc_dir):
            die(f"{svc_dir}: no such service directory — MONEY_SERVICES out of date?")
        for path in sorted(glob.glob(os.path.join(svc_dir, "**", "*.cs"), recursive=True)):
            rel = os.path.relpath(path, REPO)
            if "/bin/" in rel or "/obj/" in rel or "Tests" in rel or rel.endswith("/Money.cs"):
                continue
            with open(path, encoding="utf-8") as fh:
                src = fh.read()
            if not MATH_ROUND_RE.search(src):
                continue
            checked += 1
            if rel in KNOWN_GAPS:
                baselined.append(rel)
            else:
                hits = len(MATH_ROUND_RE.findall(src))
                errors.append(
                    f"{rel}: {hits} bare Math.Round call(s) on what may be a currency value — "
                    f"use this service's Money.Round instead (commercial/AwayFromZero rounding, "
                    f"see #380), or if this is genuinely not money (a percentage/score/duration), "
                    f"add it to KNOWN_GAPS in this script with a reason."
                )

    stale = sorted(set(KNOWN_GAPS) - set(baselined))

    print(f"Files with a Math.Round call in money-bearing services: {checked}")
    if baselined:
        print(f"{len(baselined)} known non-money file(s), tracked deliberately (not a failure):")
        for rel in sorted(baselined):
            print(f"  {rel} — {KNOWN_GAPS[rel]}")

    for rel in stale:
        print(f"::error::{rel} is listed in KNOWN_GAPS but no longer contains a Math.Round call — "
              f"remove the entry. The baseline can only shrink.")

    if stale:
        print(f"\n{len(stale)} stale KNOWN_GAPS entr(y/ies). Remove them.")
        return 1

    if errors:
        for e in errors:
            print(f"::error::{e}")
        print(f"\n{len(errors)} un-baselined bare Math.Round call(s) found in money-bearing services.")
        return 1

    print("\nEvery Math.Round call in money-bearing services is either Money.Round or a baselined "
          "non-money use.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
