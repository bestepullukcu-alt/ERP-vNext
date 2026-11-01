# MOD-0230 — bağımsız inceleme isteği 15 (SOP §20.4 / CT-APR-02)

- **Hedef 1:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `985e57556af9d65fcf942eeb6cb10070d08815775dec759bad9ac79830db2003` (revizyon 14).
- **Hedef 2:** `execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md` @ sha256 `b8d838f6ce1e9d9a169445ecb537a4678f289dc359b30567a43f8463941eeea6`. Bu DCP-004'ün CT-APR-02 kapsamındaki ilk incelemesi.
- **Aynı commit'te değişen ve onay satırı gerektirmeyen dosya:** `execution/domains/pharmacovigilance/domain-config.md` @ sha256 `ba5c9414a2278b7715c9cf7b9750de44fa655ac3349e167050c961f3ace91fe5`.
- **Kaynak commit:** `9a7a6df30`, dal `docs/pvg/mod0230-rev14`, yerel, push edilmedi. Ayrı daldadır: CT dalına revizyon onaylanmadan girerse WP-0230-DL7B'nin Expected HEAD'i onaysız paketi taşır (DEPO-01 "Gerileme değerlendirmesi").
- **Risk:** İki kart da HIGH hesaplıyor (paket kartı `976784bb…`, DCP-004 kartı `5302196d…`); bu yüzden bağımsız inceleme gerekli. `pack_approval.py check --ref 9a7a6df30` sonuçları:
  - paket: "onaydan sonra değişti, yeniden onay gerekli";
  - DCP-004: "onay yok, kural başlangıcından sonra değişti".
- **Önceki sürüm ve incelemeler:** Paket revizyon 13 (`ee175d00…`), `main`'de onaylı (#290, HIGH); inceleme 14 UYGUN. DCP-004 2026-08-09'dan beri `approved`; `approvals.tsv`'de satırı yok.
- **Bu sürümün nedeni:** Sahip kararı DEPO-01 (2026-10-29): MOD-0230 yalnız yapı/test kapısında Mongo kalıcı depoya geçer. DEPO-02: DCP-004 değişikliğini sahip tek başına onaylar. DEPO-03: 12 açık sorunun CT önerileri kabul edildi. Değişiklik şunları kapsar:
  - yeni DL-10 dilimi;
  - AC-13 yeniden yazıldı, AC-35 eklendi (AC sayısı 35);
  - yeni "Build/Test Persistence (Mongo Store)" bölümü;
  - DCP-004 §10 ve §11'e yalnız MOD-0230 için istisna, yeni `## 8.1 Delivery slices` tablosu;
  - domain-config'te dört yer.
- **Yetki:** sahibin sözleri: "Mongo'yu aç (Önerilen)", "Benim onayım yeter", "Hepsini kabul (Önerilen)" (2026-10-29).
- **İsteyen / hazırlayan / tarih:** depo sahibi (rol) · PVG Control Tower. Taslağı bir alt ajan yazdı; dört S-93 ön incelemesinden geçti. CT değişikliği ölçtü · 2026-10-29.
- **İnceleyen:** yeni bir Codex oturumu. İstenen: en güçlü model, effort high. Yanıtın başına model, effort ve oturum kimliği yazılır.
- **ZIP:** `MOD-0230-review-15-9a7a6df30.zip`, sha256 `cb42b5b364281cda2721eae4b660229a5fd5eafdf014bd0edc87c9fafa279855`, 551 738 bayt. İçinde 62 dosya ve `MANIFEST.sha256` var.
  - Yeri: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-15/` (depoya girmez).
  - Kaynak dosyalar `git archive 9a7a6df30` ile, önceki sürümler `git archive a90878348` ile alındı.
  - E-posta taraması:
    - DCP-004 başlığındaki sahip adresi, depoda zaten var olan satır;
    - testlerdeki `@example.test` ve `@example.invalid` adresleri.
    - Yeni adres yok.
- **Bulgu dosyaları:**
  - `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-985e5755.md`
  - `docs/records/reviews/DCP-004/<YYYY-AA-GG>-b8d838f6.md`
  - İnceleyenin çıktısı olduğu gibi kaydedilir.
- **Prompt bloğunun SHA-256'sı:** `d5d3f305bd8574b56852f00e6a51f24e72cf21999f9a5275942aca9313abfffb` (ilk ```text bloğunun içeriği, son satır sonu dahil).

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0230 module pack revizyon 14 ve DCP-004 bağımsız incelemesi 15 (SOP §20.4 / CT-APR-02). İnceleyensin; belgeleri yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki iki bulgu bloğudur.

Kaynak: ZIP MOD-0230-review-15-9a7a6df30.zip · sha256 cb42b5b364281cda2721eae4b660229a5fd5eafdf014bd0edc87c9fafa279855 · kaynak commit 9a7a6df30 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef 1: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 985e57556af9d65fcf942eeb6cb10070d08815775dec759bad9ac79830db2003
Hedef 2: execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md @ sha256 b8d838f6ce1e9d9a169445ecb537a4678f289dc359b30567a43f8463941eeea6
Biri tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Ne değişti: Sahip 2026-10-29'da MOD-0230 (vaka girişi) için yalnız yapı/test kapısında (yerel/dev/CI) Mongo kalıcı depoyu açtı; canlı kullanım kapısı kapalı kalır, MOD-0231/0232/0234 için hiçbir şey değişmez (karar kayıtları pvg-mod0230-kalici-depo-01/02/03). Aynı commit üç belgeyi değiştirir: paket (revizyon 13 → 14), DCP-004 ve domain-config. Değişiklik farkları excerpts/*-diff-*.diff; önceki sürümler excerpts/*-previous-*.md. Paket revizyon 13 onaylı (HIGH); DCP-004 2026-08-09'dan beri approved ama onay aracında satırı yok, bu değişiklikle ilk kez onay kapsamına girer.

Önce oku (hepsi ZIP'te): iki hedef ve domain-config; üç fark dosyası ve önceki sürümler; karar kayıtları kalici-depo-01/02/03 ve secenek-kaynagi-01; REVIEW-REQUEST-14.md; kurallar (module-pack-standard, capability-pack-standard, audit-trail-standard, mongo-indexing, entity-base-template, multi-tenancy, agents-core); central-audit-connect.md ve control-tower-sop.md; service-tests.yml; CentralAudit paket kaynakları; Inventory ve SupplyChain'in CentralAudit + Mongo bağlantısı (Program.cs, InventoryLedgerRepository.cs, ShipmentRepository.cs); PvgService kaynakları ve testleri (host, uçlar, uygulama servisi, depo arayüzü, bellek içi depo, index kataloğu, csproj dosyaları, mevcut mimari ve sözleşme testleri); MongoTestDatabaseGuardTests.cs; scripts/pack_approval.py ve scripts/ct_route.py; excerpts/pack-approval-card-*.txt ve pack-approval-check-*.txt; execution/approvals/*. excerpts/audit-slice-decision-note-DRAFT-not-normative.md yalnız bağlamdır (D-1…D-7 açık denetim kararları), normatif değildir.
Kod koşamazsın; iddiaları ZIP'teki metne ve kaynaklara karşı statik olarak kendin ölç.

İnceleme derinliği:
- Kapsam: Mongo istisnası gerçekten yalnız MOD-0230 ve yalnız yapı/test kapısıyla mı sınırlı? DCP-004 §10/§11, domain-config ve paket metni canlı kullanımı, seed'i, izin seed'ini, arka plan işini ya da MOD-0231/0232/0234'e bir şeyi açıyor mu? Varsayılan depo bellek içi mi kalıyor?
- Tutarlılık: Üç belge birbiriyle ve karar kayıtlarıyla çelişiyor mu? Paket içinde eski "in-memory only" ya da "Mongo yasak" cümlesi kaldı mı? Revizyon notu değişikliği doğru anlatıyor mu?
- Ölçütler: AC-13 ve AC-35 ölçülebilir mi? Her adlı test ve sabotajı uygulanabilir mi (sabotaj testi kırmızıya döndürüyor mu, derlemeyi kırmıyor mu, yalnız DL-10 izinli yollarına mı dokunuyor)? Kiracı izolasyonu (çapraz kiracı 404), TenantId-önce index, soft delete, aynı işlemde yazma ve açılışta fail-closed ölçülüyor mu? Ölçütlerden biri revizyon 13'teki onaylı bir ölçütü daraltıyor mu?
- Dilimler: DL-10 satırı ve DCP-004 §8.1 tablosu onay aracının okuyacağı biçimde mi; DL-10'un önkoşulları, izinli yolları, yasakları ve kanıt seviyesi tutarlı mı; DL-5 (denetim) açık kararları (D-1, D-2, D-4, D-6, D-7) önceden bağlanmış mı?
- Güvenlik: Bağlantı dizesi gizli bilgi sağlayıcısından mı geliyor; commit edilen yapılandırma Mongo'yu fiilen açıyor mu; test verisi sentetik mi?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz). İki blok, bu sırayla:
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 985e57556af9d65fcf942eeb6cb10070d08815775dec759bad9ac79830db2003
F-1 | ENGEL | <bulgu; satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)

# DCP-004 — bağımsız inceleme
hedef: execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md @ b8d838f6ce1e9d9a169445ecb537a4678f289dc359b30567a43f8463941eeea6
F-1 | … (aynı biçim; numaralama her blokta F-1'den başlar)
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>

Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma; domain-config ile ilgili bulgu, ilgili olduğu hedefin bloğuna yazılır.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-15/MOD-0230-review-15-9a7a6df30.zip
sha256 cb42b5b364281cda2721eae4b660229a5fd5eafdf014bd0edc87c9fafa279855
```
