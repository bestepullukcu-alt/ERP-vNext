# Loads UI DEV v1.0 — HELD / çalıştırma yetkisi yok

Rol: CT tarafından atanacak tek UI writer. Başlangıç: explicit UI scope/Phase1.5 ve exact pack amendment onayı, seçilmiş target+HEAD+dirty baseline, aktif writer çakışması yok, shared integration manifest/preimage ve gerçek Auth test planı. Backend pack ready-for-dev tek başına yeterli değildir. Gates yoksa product write başlatma.

Oku: AGENTS/orchestrator, MOD0185 pack effective§28 ve onaylı UI eki; bu paket SCOPE/OWNED-PATHS/SHARED-HANDOFF/ACCEPTANCE; source hashes ve kabul snapshot'ı; ilgili UI/UAS/permission/L10n kuralları; gerçek GoldenReferenceSlim. Hash drift varsa ilgili scope'u durdur, başka lane değişikliğini geri alma.

NE: SCOPE'daki tenant list+create-only Slim dilimini exact20 UI path içinde uygula. Same-origin proxy yalnız mevcut Gateway GET/POST Loads; data thin AJAX;7 dil/RTL, typed repeatable stops/Shipment IDs; DataTablev2/SaveView; published exact key/root retry/error semantics. Backend by-ID/transition UI/lookup/root davranışı ekleme. Reference fixture gerekiyorsa yalnız açık test authority kapsamında SIMULATED işaretle.

YAPMA: Carrier/Shipment source, backend, pack/contract/guard/canonical, gateway, nav/catalog/sharedresources/layout/config/Program.cs veya Git değiştirme. Shared ihtiyaçları tek integration owner'a exact diff isteği olarak teslim et. İzin tanımı/rol grant'ı kendin üretme; scope'u sessiz genişletme.

DOĞRULA: U01–U19'un uygulanabilir UI-owned kontrolleri, meaningful controller/form/JS tests, Slim verifier, immutable source→binary→process→browser manifesti. Auth/live/PNG kapıları yoksa OPEN; test veya fixture sonuçlarını gerçek E4/E5 diye sunma. Yalnız lane-owned process/DB restart/cleanup; secretless/redacted kanıt. Teslim SOP22, exact changed hashes, actual test sonuçları ve independent VER handoff. DEV PASS ≠ CT acceptance.
