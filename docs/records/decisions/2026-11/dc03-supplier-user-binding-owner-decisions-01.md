# DC-03 — tedarikçi kullanıcısı bağlama (supplier_user): sahip kararları 01

- Konu: MOD-0148 Supplier Portal v1 kod kapısı DC-03 (karar 2026-10-21, MVP-6 plan v10.0 :90 C4; D9 tetiği
  `docs/records/decisions/2026-10/mvp6-mod0148-start-owner-decision-2026-10-22.md`). Sahip: merkez (Beste).
- Girdi: CT salt okunur araştırması (2026-11-13; alt ajan acd17c1538960f9e0) — kodda yapı yok; Blueprint SoR haritası "external identity
  bindings" sahipliğini MOD-0360'a verir; D1–D8 taslağı main dışı dallarda (`origin/docs/sce/dc03-supplier-actor-binding-draft-01`
  `eaa7bdd06`, `origin/feature/sce/mod-0148-dc03-decision-prep-01` `67a30c7f0`), #172 birleştirilmeden kapandı.

## Kararlar

- **2026-11-13 — M2 kabul (Beste, "evet"; kaynak: sahip sohbet cevabı):** Natig'in isteği
  (`docs/roadmap/plans/mvp6-mod0147-v1-scorecard-01/DC03-BINDING-CANONICAL-CODE-REQUEST.md`) kabul edildi: bağlama kaydı, bağlama anında
  MOD-0140'ın döndürdüğü kanonik `SUP-` kodunu saklar; kanonik kod değişirse bağlamayı DC-03 sahibi (merkez) düzeltir.

- **2026-11-13 — Kimlik (Beste, "tamam bu olabilir"):** DC-03 bağlama işi **MOD-0360-FU01** olarak açılır (Blueprint SoR: MOD-0360
  Tenant User & Identity Management — "external identity bindings"). Pack açılmadan önce `verify_module_id.py --check-id MOD-0360-FU01
  --name "Supplier User Binding" --parent MOD-0360` koşulur (fail-closed).
- **2026-11-13 — Token süresi (Beste, "Öneriyi kabul ediyorum"):** D4 düzeltildi: `supplier_user` erişim anahtarı **15 dk**, arka planda
  sessizce yenilenir; oturum (yenileme anahtarı) en çok **8 saat**, sonra yeniden giriş + e-posta kodu.
- **2026-11-13 — Taban:** paket, main dışı D1–D8 taslağını (D4 yukarıdaki gibi) esas alır; D1–D8 paketin S0 diliminde sahip onayına
  yeniden sunulur (bağlayıcı kayıt bu dosyaya yazılır).
- **Kiracı modeli (CT açıklaması, sahip sorusu üzerine):** tedarikçi kendi kiracısını açmaz; hizmet verdiği firmanın kiracısında (ör. GMG)
  `supplier_user` hesabıdır; başka bir kiracıda ayrı hesap (DC-03 C4 "kiracı başına e-posta başına tek hesap").
- **2026-11-13 — r1 incelemesi sahip kararları (Beste, "hepsi öneri gibi"; inceleme `docs/records/reviews/MOD-0360-FU01/2026-11-13-ce91314f.md`):**
  F-2 — erişim anahtarının bitişi oturum bitişini **asla geçmez**: exp = min(nbf + 15 dk, oturum sonu); oturum tam 8 saatte biter.
  F-5 — bağlamada `eligibleForTransactions` yanında tedarikçinin **onay durumu (onboardingStatus) ve satın almaya açıklığı
  (purchasingEnabled)** da denetlenir; reddedilmiş ya da satın almaya kapalı tedarikçiye portal açılmaz.
- **2026-11-13 — Auth soruları SQ-1…SQ-6, SQ-12, SQ-13, SQ-15 (Beste, merkez — Platform / Auth işini merkez yürütür: "diğerleri öneri
  gibi olsun"; Ali'ye bilgi verilir; paket MOD-0360-FU01 r2 8086bf9a 23):**
  SQ-1 hesap düzeyinde ayırt edici: **`AccountKind.Supplier = 3`** (taslağın "hesap Human kalır" metni değişir).
  SQ-2 v1 iki adımlı doğrulama **yalnız e-posta kodu**; challenge kaydına **kapı alanı** eklenir (kiracı kapısı tedarikçi challenge'ını kabul etmez).
  SQ-3 S2S `binding_rev` ucu: ayrı audience **`TRUSTED_SUPPLIER_BINDING_CONSUMER`** (MOD-0140 DL-7 audience'ından ayrı; MOD-0012-FU01
  `TRUSTED_SECRET_VAULT_CONSUMER` deseni), kabul edilen tek `service_name` **`Diten.SupplyChain`** (MOD-0148 portalını taşıyan servis);
  başka servis adı 403.
  SQ-4 D7 MOD-0140 S-4'e hizalanır (servis token'ı `POST /validate` ve tekil `GET /{supplierId}/eligibility`).
  SQ-5 Bölüm 5'teki Auth dosya listesine XMC-001 izni (merkez).
  SQ-6 ayrı **`auth.supplier-bindings.read`** anahtarı; bağa görünen kayıt kodu verilmez (RCS-001 "kod yok"); kod düzeltmesini kiracıda
  `auth.supplier-bindings.manage` yetkili hesap yürütür.
  SQ-12 (a) yalnız `[Authorize]` taşıyan uçların envanteri çıkarılır, her biri AC-15 kapsamına (tedarikçi token'ı reddi) eklenir — en az Auth uçları.
  SQ-13 (1) tedarikçi hesabı kiracı kullanıcı kotasına **sayılmaz** (ayrı sayım); (2) davet bağlantısı bugünkü Platform bildirim akışıyla;
  (3) v1 parola belirleme bugünkü anonim `/api/users/set-password` + kiracı Web sayfasıyla.
  SQ-15 son aktif bağ kaldırılınca hesap **pasif**; kişisel veri saklama kiracının kullanıcı saklama kuralını izler.
  Açık kalanlar: SQ-7, SQ-8, SQ-11, SQ-14 (Natig), SQ-9, SQ-10 (Cihan).
