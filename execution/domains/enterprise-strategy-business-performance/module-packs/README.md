# ESBP Module Packs

Bu klasör Enterprise Strategy & Business Performance domain'inin gelecekteki tek-modül delivery
sözleşmelerini tutar.

Bu scaffold herhangi bir module pack oluşturmaz veya production implementation yetkisi vermez.

## Giriş Kapıları

1. [DCP-005](../../../portfolio/delivery-capability-packs/DCP-005-management-governance-core.md) ordered
   sequence ve ilgili dependency/gate'ler doğrulanır.
2. Kimlik DCP-002 fail-closed preflight ile doğrulanır. Candidate kimlikler runtime literal olamaz.
3. `/prepare-module-pack` ile pack `draft` olarak hazırlanır.
4. Pack insan incelemesi sonrası açıkça `approved` / `ready-for-dev` olmadan `@orchestrator` production kodu
   başlatmaz.
5. Gate 2 tehlikesine dokunan pack ayrıca yazılı Control Tower Gate 2 PASS gerektirir.

İlk beklenen authoring sırası:

1. `CAND-CAP-0007-FU01` — Security, Tenancy & Data Migration Foundation
2. `MOD-0352` — Enterprise Strategy Management (1.1; active DCP-006 scope dışı)
3. MOD-0117 altında Demand transition slice (kimlik DCP-003 sahibiyle birlikte kararlaştırılır)
4. `MOD-0354` — DWS Wave 1 structural mechanics
5. `MOD-0355` — BPM process model/version
