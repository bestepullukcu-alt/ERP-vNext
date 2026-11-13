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
