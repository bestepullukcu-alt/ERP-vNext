# Assumptions and measurement boundary

1. The fixed Update-03 rubric, 103 effort rows and category structure are retained.
2. One person-day equals eight active specialist hours. This is the existing comparison assumption, not measured elapsed time, agent runtime, payroll time or a calendar forecast.
3. O/M/P estimates are delivery estimates. File, route and test counts are evidence coverage only and are not effort multipliers.
4. The Shipment rework estimates exclude the already-completed disposable preparation. They cover the still-unapplied target work, validation, independent reproduction and bounded recovery.
5. Shipment rework is an allocation inside existing `0183-5-REMAINING` and `0183-6-REMAINING`; it is not added to the portfolio denominator.
6. Fresh Auth-chain completion consumes the existing `S2-REMAINING` reserve. The previously credited NumericDate implementation remains in `S1-DELIVERED` and is not credited again.
7. MFA, forced-password and broader tenant/actor tuples are retained as inherited/not-fresh evidence. This report does not convert them to fresh PASS or create an unapproved new verification reserve.
