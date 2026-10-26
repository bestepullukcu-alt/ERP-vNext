---
decision_id: TREASURY-INTAKE-OWNER-DECISIONS-01
status: approved — D1–D8 2026-10-23 itibarıyla yürürlükte; "Karar olmayanlar" bölümü karar değildir
decided_at_local: 2026-10-23
decided_by: depo sahibi (rol, K1); bu kayıt ad yazmaz
decision_source: Treasury Intake Control Tower oturumu (Claude Code); Control Tower 1–9 numaralı önerileri sundu, sahip "oneri ile devam" yanıtıyla hepsini kabul etti (öneri 1 = worktree açılışı, eylemdir, aşağıda D0)
scope: Treasury intake — 11 TP-001 v1.5 paketi (MOD-0289, MOD-0220, MOD-0119, MOD-0140, MOD-0120, MOD-0149, MOD-0126, MOD-0217, MOD-0124, MOD-0269, MOD-0057); başka domain'in belgesi bu kayıtla değişmez (XMC-001)
bound_to: dal `chore/ct/treasury-intake-ct-01`, taban origin/main 96bedc89374dd8fd06550c49ae2fc65ca07e6298
recorded_by: Treasury Intake Control Tower; bu kayıt kendi adına karar vermez, onay, paket ya da dağıtım izni değildir
---

# Treasury intake — sahip kararları (2026-10-23)

Girdi: handoff klasörü `/Users/natig/Projects/treasury-transfer-handoff/` (depo dışı). Sekiz SHA256SUMS dosyası
(271 dosya) ve `source-build-evidence-v1.0` (82 dosya) Treasury Intake CT tarafından `shasum -a 256 -c` ile doğrulandı;
hepsi OK.

## D0 — Yer (eylem)

Control Tower worktree'si: `/Users/natig/Projects/ERP-vNext-recovery-treasury`, dal `chore/ct/treasury-intake-ct-01`,
taban origin/main 96bedc893 (fetch yapılmadı; yerel origin/main kullanıldı). Kart §0'daki `.claude/worktrees/` yerine
kardeş klasör kullanıldı, çünkü `.claude/worktrees/` ana klasörde git tarafından yok sayılmıyor ve ana klasöre izlenmeyen
bir dizin eklerdi.

## D1 — Yöntem: doğrula, sonra uyarlayarak al

Kaynak kod, bağlanan commit'te derlendiği ve testleri geçtiği gösterildikten ve modülün Recovery pack'i `ready-for-dev`
olduktan sonra Recovery standartlarına uyarlanarak alınır (TP-001 §7 adım 3; her dosya `path@commit` + sha256 ile).
**Kod alma izni modül modül verilir; ilk aday MOD-0126.** Bu kayıt hiçbir modül için kod alma izni değildir.

## D2 — Treasury servis adı

`Diten.FinanceTreasuryService` (domain `finance-treasury` ile eşleşir; AP/AR'ı da kapsar).

## D3 — Dal kısa kodu (AGENTS §9)

`fin`. AGENTS.md'ye yazılması ayrı bir onaydır.

## D4 — Port

Port numarası önerilmez. EA'dan bir port ve dolu bant için bant kararı istenir (AGENTS §3).

## D5 — Karar 0.1: MOD-0269 sağlayıcı / arayüz

**Seçenek A — önce sözleşme, dağıtım kapalı.** Sağlayıcıdan bağımsız bir arayüz sözleşmesi (dağıtım isteği, sonuç,
durum, ekstre) yazılır; bağdaştırıcı "yapılandırılmamış" döner; bankaya mesaj gitmez. Kaynakta kapalı olan dört akış
(gerçek dağıtım, banka kimlikli geri çağrı, güvenilir non-HTTP çalışan, MOD-0121'e ekstre yayımı) kapalı kalır ve
varsayılmaz. Sağlayıcı seçimi ve dosya ile değişim sonraki ayrı kararlardır. Bankacılık sahibi henüz adlandırılmadı.

## D6 — Karar 0.0: TP-001

Level-1 paket kabulü şimdilik "TP-001 v1.5 taslağına göre" etiketiyle kaydedilir. TP-001'in `.antigravity/rules/`'a
alınması (kural haritası satırı ve onay kaydıyla) ayrı bir iştir.

## D7 — MOD-0289 yerleşimi (sahip önerisi)

Sahip, `master-data-management` / `Diten.MdmService` yerleşimini destekler
(`docs/roadmap/plans/erp-finance-legal-module-intake-01/MOD-0289-PLACEMENT-NOTE-v0.1.md`). **MDM domain sahibinin
onayı beklenir**; o onay gelmeden kayıt satırı ve pack açılmaz.

## D8 — Kuyruk kaydı

Kuyruk `treasury-intake` hazırlanır (strict `yes`, vocabulary `sop7`, `owner_confirmation` başlangıçta `pending`).
Kayıt dosyasında satırı olmayan modüller için `module` sütunu `ortak:<intake yolu>` kullanır. Kayıt satırı `main`'e
ayrı PR ile girer.

## Karar olmayanlar

Karar 0.3 (MOD-0003), 0.5 (AR fatura sınırı), 0.6 (ERP-FIN-BUNDLE), 0.7 (GL), MOD-0217 ve MOD-0057 yerleşimi,
MOD-0146, MOD-0289 ve MOD-0217 kırık testlerinin nedeni — öneri olarak sunuldu, karar verilmedi.

## Bu kayıt ne değildir

Kod alma izni, servis iskeleti, port tahsisi, AGENTS.md / `.antigravity` değişikliği, pack onayı ya da push / PR
izni değildir.
