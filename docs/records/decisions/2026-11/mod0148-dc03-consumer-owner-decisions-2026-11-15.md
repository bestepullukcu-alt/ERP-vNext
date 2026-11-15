# MOD-0148 — DC-03 tüketici kararları (2026-11-15)

- Sahip: Natig (K1, MOD-0148 modül sahibi, MVP-6 CT). Kaynak: sahibin sohbet cevabı, 2026-11-15 (soru 1: Beste'nin önerisi kabul;
  soru 2: «Evet, denetlesin»; soru 3: varsayılandan sıkı seçenek; soru 4: MOD-0148 manifesti).
- Konu: MOD-0360-FU01 r2 (Supplier User Binding, DC-03) paketinin MOD-0148 sahibine sorduğu SQ-7, SQ-8, SQ-11, SQ-14.
  Paket: `execution/domains/platform-shared-services/module-packs/MOD-0360-FU01-supplier-user-binding.md`, sha256
  `8086bf9aefa3f8e7b3db664893149d372def69153d1552cb8149b3d05aaa2d4a` (ready-for-dev; `execution/approvals/approvals.tsv` CT-APR-01
  satırı, onaylayan Ali, 2026-11-13). Merkez Auth ekibi (Beste) geliştirmeye başladı ve seçimlerin MOD-0148 paketine yazılmasını istedi.
- Bağlama koşulu (Auth'un, değişmedi): `eligibleForTransactions = true`, `onboardingStatus = Approved`, `purchasingEnabled = true`
  (MOD-0360-FU01 D6, AC-27).

## Kararlar

- **K-1 — sabit izin kümesi (SQ-7):** `supplychain.supplier-portal.read` ve `.submit`, `supplier_user` token'ına sabit küme olarak
  girer; kiracı rolü yoktur.
- **K-2 — `amr` yeniden denetimi (SQ-8):** e-posta kodu adımını Auth zorlar; portal da token'ın `amr` claim'ini yeniden denetler.
  E-posta kodu adımı yoksa 403.
- **K-3 — `binding_rev` çağrısı (SQ-11):** portal `createOwnPortalSubmission` ve `submitOwnPortalSubmission` öncesinde Auth'un S2S
  `binding_rev` ucunu çağırır. Uyuşmazlık 403; Auth erişilemezse 503 (kapalı-başarısız). Servis adı `Diten.SupplyChain`, audience
  `TRUSTED_SUPPLIER_BINDING_CONSUMER` (değerler SQ-3, Beste 2026-11-13, `dc03-supplier-user-binding-owner-decisions-01.md`). Bu bir kod
  kapısıdır; her kabul satırının bir sabotajı vardır. Sonuç: bağın kaldırılması bir sonraki yazmada hemen etkili olur.
- **K-4 — katalog (SQ-14):** iki anahtar Auth kataloğuna MOD-0148 manifestinden girer, MOD-0360-FU01'in tedarikçi kapısı dilimi
  (DL-2) başlamadan önce.

## Pakette

`execution/domains/supply-chain-execution/module-packs/MOD-0148-supplier-portal.md` metin revizyonu 2026-11-15: §0.0 "DC-03 consumer
rules" P1–P4 (K-1…K-4), "Undeclared statuses" (get ve submit'te 403), test kimlikleri T-0148-V1-PERMSET-01 / AMR-01 / BREV-01 /
CAT-01, §5, §13, §14, §16, §18, §19, "Teslim dilimleri" notu.

## Açık kalanlar

- Bu revizyonun bağımsız incelemesi (CT-APR-02) ve yeni CT-APR-01 onay satırı: 2026-10-28 satırı önceki sürüme bağlıdır.
- MOD-0360-FU01 tarafı: SQ-7, SQ-8, SQ-11, SQ-14 cevaplarının o pakete yazılması Auth sahibinin işidir (XMC-001); bu kayıt yalnız
  MOD-0148 tarafını bağlar.
- Katalog adımında `module-self-registration-standard` §2d'ye kayıtlı istisna (henüz hiçbir denetleyicinin zorlamadığı anahtar):
  inceleyici ve sahip görmeli.
