---
decision_id: PSS-MOD0286-OPEN-DECISIONS-01
status: decided — sahip Control Tower'ın önerilen dört cevabını kabul etti (2026-10-23, sohbette "karar-öneri"); diğer açık kararlar bu kayıtta açık kalır
prepared_at_local: 2026-10-23
prepared_by: PSS-UI Control Tower (Claude Code oturumu); bu kayıt kendi adına karar vermez
decided_by: depo sahibi (rol); kişi adı ve e-posta yazılmaz
trigger: bağımsız inceleme 05 UYGUN (`docs/records/reviews/MOD-0286/2026-10-22-d66d59cf.md`, reviews.tsv satır 5); paket onayı öncesi D-1 / D-2 / owner / target boşlukları
scope: yalnız MOD-0286 P1 (DL-1 şartname + DL-2 uyum kontrol listesi); paket gövdesi bu kayıtla DEĞİŞMEZ (CT-APR-02: inceleme paket sürümüne bağlıdır; yalnız `owner` / `target` TBD doldurması muaf)
pack: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md (revizyon 05, sha256 d66d59cf0a82524af8c480fcdbe82fcd75ad7244853916e7111d19d9717a2a6f @ 5e46ef8b6)
---

# MOD-0286 — P1 onayı öncesi açık kararlar (D-1, D-2, owner, target)

> Paket revizyon 05 bağımsız incelemede UYGUN aldı. Onay satırı açılmadan önce paketin "Open Decisions" tablosundaki
> D-1 ve D-2 ile frontmatter'daki `owner` / `target` alanlarının cevabı gerekiyordu. Cevaplar pakete yazılmaz (inceleme
> bayatlar); bu kayıtta tutulur. Paket D-1/D-2 satırlarını "karar bekliyor" olarak göstermeye devam eder; geçerli cevap
> bu kayıttır (Module Pack > Domain Config > AGENTS.md sırasında paket üstündür ama paket bu kararı açıkça sahibe bırakır).

## Karar

| # | Konu | Karar (sahip, 2026-10-23) | Dayanak |
|---|---|---|---|
| D-1 | DL-1 şartname ve DL-2 kontrol listesinin yeri | `docs/reference/modules/platform/ui-design-system-page-pattern-governance/` altında `spec.md` ve `compliance-checklist.md` (ikincisi iki bölümlü: kontrol listesi `UIC-n`, koşu sözleşmesi `RC-n`) | `.antigravity/rules/docs-organization.md:110` "Modül belgesi (spec, karar notu) → `docs/reference/modules/{platform\|tenant}/<modül>/`"; §1 soru 5 (bugünün gerçeği); `docs/` köküne dosya konmaz (K1, AC-10) |
| D-2 | On bir UI kural dosyası `.antigravity/rules/` altında kalır mı | Kalır. Şartname kuralları indeksler, değiştirmez; kural ile şartname çelişirse kural kazanır, çelişki şartnamenin Çatışma kaydına yazılır | Yetki sırası (AGENTS.md §1); `.antigravity/**` korunan yol; 62 paketin `verify_datatable_page.py` atfı ve AGENTS.md §6.1 haritası kırılmaz |
| owner | Paketin `owner` alanı | `K3` (geliştirici hesabı GMG-ERP; `execution/approvals/approvers.tsv`) | POLICY-REVIEW-START §İnceleme istisnası: `owner` alanının TBD'den doldurulması incelemeyi bayatlatmaz |
| target | Paketin `target` alanı | `2026-11-07` (DL-1 + DL-2 teslimi) | Aynı istisna (tarih alanı) |

## Açık kalanlar (bu kayıt karar vermez)

| # | Konu | Kimin | Not |
|---|---|---|---|
| D-3 | BL-655: MOD-0046 → MOD-0009-FU03 (depo) ↔ "Platform Administration workspace + MOD-0286" (Master 8.8 ADR-8.8-001) | Enterprise Architect / sahip | P1 iki eşlemede de yürür |
| D-4 | P2 (konsol, DL-3) tetikleyicisi | Sahip | Öneri: ilk tüketici (MOD-0295 / MOD-0297) `approved` ya da sahip kararı |
| D-5 | "MOD-0013" etiketlerinin MOD-0286'ya çevrilmesi (3 kural dosyası + AGENTS.md:139) | Sahip (`.antigravity/**`) | Ayrı commit, 🟢 |
| D-6 | MOD-0297 kayıt çakışması, MOD-0295 kayıt satırı yok | Enterprise Architect | FU-5 |
| D-7 | Mobil (Android) tasarım kiti MOD-0286 tüketicisi mi | Sahip | Öneri: ayrı; P2 revizyonunda yeniden sorulur |
| D-8a | `premium-modal-standard.md:27-52` inline `style=` ↔ FG-003 | Kural sahibi (sahip) | Öneri: FG-003 uygulanır |
| D-8b | `frontend-js-standard.md:53` "createCrudTable tercih edilen" ↔ `:981` + BL-440 (createList) | Kural sahibi (sahip / BL-440 CT) | Öneri: createList; 7.1 güncellenir |
| D-8c | `frontend-standards.md:208` "layout opsiyoneldir" ↔ `module-pack-standard.md` §2 açık layout | Kural sahibi (sahip) | Öneri: açık layout |

## Pakete etkisi

- Paket gövdesi değişmez. Frontmatter'da yalnız `owner: TBD` → `owner: K3` ve `target: TBD` → `target: 2026-11-07` yazılır (istisna kapsamı; `pack_approval.py check` bunu ölçer, sonuç bu kaydın commit'inden sonraki kontrol çıktısında).
- Onay satırı: `scripts/pack_approval.py row <paket> --author GMG-ERP --approver bestepullukcu-alt --risk HIGH --reason "<bu kayıt>"`, `execution/approvals/approvals.tsv`'ye yalnız ekleme; onaylayan PR'ı merge eder (CT-APR-01).
- DL-1 ve DL-2 iş paketleri `Kapsanan dilim: DL-1` / `DL-2` ile ayrı WP'lere verilir (CT-APR-02 §6); kuyruk kaydı (`ct-queue-registry.tsv`, `pss-ui`) sahip onayıyla `main`'e girmeden dağıtım yok (`--prepare`).
- İş takip sayfası (CT-TRK-01, S-39 ile zorunlu) `sayfa-ac` ile açılır; bu kaydın yolu sayfaya yazılır.

## Seçilmeyen seçenekler (kayıt için)

- D-1 için `docs/reference/architecture/`: README'si geliştirme sonrası mimari belgeler içindir, bu belge "bugünün gerçeği" bir modül belgesidir.
- D-2 için kuralları şartnameye taşımak: korunan yol değişikliği, 62 paket atfı ve §6.1 haritası kırılır; P2'de yeniden değerlendirilebilir.
