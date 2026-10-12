# Worked examples

🤖 Applying knowledge of @business-analyst. Every assumption is marked **[A…]**; source cells in brackets.

## 1. Automatic_Order — "Calculation" sheet (recomputed from the file)

Inputs as written in `inputs/Automatic_Order_v1_2.xlsx`, sheet *Calculation*:

| Item | Cell | Value |
|---|---|---|
| Shipment Forecast, Jan-2024 | M10 (column M = 2024-01-01, M6) | 24,932 |
| MAIN WH Stock Actual/Forecast, Dec-2023 | L11 (column L = 2023-12-01) | 14,932 |
| MIN PRODUCTION BATCH, units | G38 | 3,000 |
| Lead-time components, days | G22 negotiation 10 · G23 custom docs 10 · G24 lab 10 · G25 penetration 20 · G26 CWH→WS 20 · G27 factory→CWH 20 · G29 production 90 · G32 CWH obligatory 30 | Σ = 210 |
| Months offset | G34 `=SUM(G22:G33)/-30` | −7 |
| Formula in the file | E12 `=ROUNDUP((M10-L11)/G38,0)*G38` | 12,000 |

Step by step:

1. Net requirement = shipment forecast − main-warehouse stock = 24,932 − 14,932 = **10,000**.
2. Batches = 10,000 / 3,000 = 3.33 → ROUNDUP → **4**.
3. Order forecast = 4 × 3,000 = **12,000** (matches E12 and the order actual E13 = 12,000).
4. Timing: total lead time 210 days = **7 months** (G34). Jan-2024 − 7 months = **Jun-2023**, but the file places the order forecast in column E = **May-2023** (8 months earlier). **[OQ-AO-13]** — either an extra month of buffer or a mistake.
5. Production actual 12,000 appears in H15 = **Aug-2023**, i.e. 3 months after the order (production lead time 90 days, G29). Consistent.
6. Excess over need = 12,000 − 10,000 = 2,000 units carried as stock (lot-sizing effect).

Observations: the net requirement uses only one month of shipment forecast (Jan-2024) and one stock figure (Dec-2023); the unlabelled monthly series C7:X7 is not used by E12 **[OQ-AO-12]**. "PENETRATION" and "CWH OBLIGATORY" are counted as lead time **[OQ-AO-10, OQ-AO-11]**.

## 2. One S&OP chain (illustrative numbers — not from the files)

**[A1]** one product, one wholesaler, calendar months; **[A2]** CWH→WS lead time = 1 month; **[A3]** Min distributor stock (LEAD TIME page) = 0, so the 1 / 3 months thresholds apply; **[A4]** no shipment plan or actual exists for the forecast months; **[A5]** lead-time settings: averages negotiation 10, custom docs 10, GIT 20, lab 10 days; maxima 15, 15, 30, 15 days; **[A6]** last six months' sell-out actual = 1,000/month.

| Step | Rule | Calculation | Result |
|---|---|---|---|
| a. Sell-out forecast Feb–Jun | SOP-24 (manual) | input | 1,000 / month |
| b. Wholesaler stock actual, end Jan | SOP-23 | input | 3,000 |
| c. Shipment forecast, Feb | SOP-27 (manual) | input | 2,000 |
| d. Sell-in forecast, Mar | SOP-18 | = shipment forecast of (Mar − 1 month) | 2,000 |
| e. Special cases, Mar | SOP-20 | approved credit note (expired) | 50 |
| f. Wholesaler stock forecast, Feb | SOP-21 | 3,000 + 0 − 0 − 1,000 | 2,000 |
| g. Wholesaler stock forecast, Mar | SOP-21 | 2,000 + 2,000 − 50 − 1,000 | 2,950 |
| h. Months of supply, Mar | SOP-22 | 2,950 / 1,000 | 2.95 → **YELLOW** (>1 and <3) |
| i. Months of supply, Apr / May | SOP-21/22 | 1,950 / 1,000; 950 / 1,000 | 1.95 YELLOW; 0.95 **RED** (before the Apr shipment in step l) |
| j. Obligatory stock | SOP-37 | 6,000/6/30 × (10+10+20+10) | 1,666.7 |
| k. Safety stock | SOP-38 | 6,000/6/30 × (15+15+30+15) − 1,666.7 | 833.3 |
| l. Shipment need for a shipment arriving in May, covering May–Jun **[A7]** | SOP-29 | (1,000 + 1,000) + 1,666.7 + 833.3 − 1,950 | **2,550** (shipped Apr) **[A8: OBL+SS added once per period, not per month — OQ-SOP-29]** |
| m. CWH stock forecast | SOP-30 | end Mar 6,000 **[A9]**; Apr −2,550; May −3,000 other markets **[A10]**; Jun −2,000 | Apr 3,450; May 450; Jun **−1,550 RED** |
| n. Production / order need | SOP-40 / SOP-41 | shortage 1,550 in Jun; LT-P 3 months, LT-F-CW 1 month **[A11]** | order need shown in **Feb** (Jun − 4); production need in **Mar** (Jun − 3, tooltip) or Jul (TR note) — **OQ-SOP-40** |
| o. Lot-sized order | AO-01 logic applied **[A12]** | ROUNDUP(1,550 / 3,000) × 3,000 | 3,000 |

If the analysed month is January, the order month (Feb) is still open; if the analysis runs in March, the need is already late — the files do not say what happens to a need that falls in the past **[OQ-SOP-41]**.
