PROMPT İNCELEME KAYDI v1
wp: WF-S17-DL2
prompt: PSS-WF-S17-DL2-P v1.0
prompt-sha256: b081aafc708dc3e38424dddb912e9f64abe2e133e7e587bba3616c948e0872ba
kaynak-paketi-sha256: 9ee94930335f8ef807e032edaef6094e15ff01b550a070fad1d82b015c7158d2
yazar: CT · İş akışı prompt yazarı (commit fe4a06bbc)
inceleyen: Bağımsız inceleme alt ajanı (Claude Opus, prompt'u yazmayan; WF-S17-DL2 v1.0 incelemesi 01)
karar: KOŞULLU — Kapsam, izinli yollar, satır atıfları, taban ölçütü ve Mongo ortamı doğru. Bir sabotaj koda göre yeşil kalıyor ve durma koşulunu kesin tetikliyor (F-1); beş ORTA bulgu işi yanlış geçirebilir ya da yanlış durdurabilir. v1.1 ve yeni inceleme kaydı olmadan dağıtılmaz.
kontrol sözleşme: eksik — execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md §17.9/7-14, 17, 18: AC-W17 geçiş satırları (F-3) ve §17.9/10 RequestInfo ikizi (F-5) eksik.
kontrol dosyalar: eksik — aynı paket §17.9/14: üretim yolları doğru; bekçi mevcut dosyaya ekleniyor, DMR kanıtı kayboluyor (F-4).
kontrol ct-onayı: tamam — aynı paket §17.8/1, §17.9/17: S17-6 Expected HEAD'de var; yetki yerel commit ile sınırlı; TRM-15 ve O-S17-1 açık.
kontrol kabul: eksik — aynı paket §17.9/7-11: S6 (F-1), S3 ve S2 (F-2), sabotajsız davranışlar (F-6).
kontrol durma: eksik — aynı paket §17.9/10 ve scripts/known_failures.py:664: F-1 yanlış durdurur; test güncelleme izni regresyonu gizleyebilir (F-7).
kontrol risk: tamam — aynı paket Teslim dilimleri DL-2 (E4): HIGH doğru; slot 7, env adları ve donanımlar kodda doğru.

| ID | Önem | Yer | Bulgu | Gerekli değişiklik |
|---|---|---|---|---|
| F-1 | ENGEL | v1.0:124, :91, :143 | Eşleme kaldırılınca `AC_W7_requestinfo_b_duplicate_key_is_replay` yeşil kalır: AfterUnavailableAsync ikizi bulur, 200 döner. Kesin yanlış DUR. | Teste `PreviousTaskStatus == ikiz FromState` ekle; S6'ya `AC_W14_requestinfo_b_other_actor` adını ver. |
| F-2 | ORTA | :90, :123 | S3'te son sayımlar yeşil kalabilir; S2 tarifi eksik. | Bırakma anında (a) = N kaydedilir; S2 sayaç dahil iç depoya gider. |
| F-3 | ORTA | :103, :120 | CRM ve MDM karar yolu tabloda yok; metni değişen tekrar yeni 409 ile kırılır. | İki satır, rapor şablonu, sonuç adıyla. |
| F-4 | ORTA | :6, :53, :101, :110, :121 | Bekçi mevcut dosyaya ekleniyor; CT kararı ve DMR kanıtıyla ters. | Yeni dosya `WorkflowRetentionSourceGuardTests.cs`; DMR boş. |
| F-5 | ORTA | :91, :98 | RequestInfo özdeş ikizi enjekte Replayed dalı adlandırılmamış. | `AC_W7_requestinfo_c_injected_identical_twin_is_replayed`. |
| F-6 | ORTA | :123-:133 | AC_W10 request-info, legacy fingerprint, wire ve AC_W7 a/c için sabotaj yok. | S9b, S10b; S5 ve S11'e ad; requestinfo_a kırmızı beklenmez. |
| F-7 | ORTA | :141 | Mevcut test güncelleme izni sınırsız. | Yalnız Assert; önceki kırmızı rapora; WorkflowCrossServiceTests ve tüketici sınıfları güncellenmez. |
| F-8 | DÜŞÜK | :102 | "Adını geçiren" alt dizge yanlış kırmızı verir. | Tam kelime; kaçış listesi. |
| F-9 | DÜŞÜK | :80, :95 | Yarım başlatmanın true değeri sabitlenmemiş. | Bayrak testine satır. |
| F-10 | DÜŞÜK | :103 | Timesheet ek çağıranları, HCM gövde uyumsuzluğu. | Rapora not. |

karar `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT`: UYGUN — Expected HEAD'de yok; kodla eşleyen tüketici yok.
karar yarım başlatmayı tamamlayan yanıtta `idempotentReplay = true`: UYGUN — §17.7/5 ve kod yorumu :404; testi F-9.
karar güvenilir örneğin genel uçtan tekrarı 409: UYGUN, açık kayıtla — bu anahtarı genel uçla gönderen çağıran bulunmadı.
karar Cancel geç ikizi InvalidState: UYGUN — §17.9/17 yalnız Approve ve Reject.
karar mevcut test güncelleme politikası: KOŞULLU — F-7 şartlarıyla.

bulgu: diğer · açık · F-1 (ENGEL) S6 kimlikli ikiz testi yeşil kalır; kesin yanlış DUR.
bulgu: diğer · açık · F-2 (ORTA) AC-W5 bırakma anı ölçümü ve S2/S3 tarifi eksik.
bulgu: diğer · açık · F-3 (ORTA) AC-W17 tablosunda CRM ve MDM karar yolu yok.
bulgu: diğer · açık · F-4 (ORTA) bekçi mevcut dosyaya ekleniyor.
bulgu: diğer · açık · F-5 (ORTA) RequestInfo özdeş ikizi adlandırılmamış.
bulgu: diğer · açık · F-6 (ORTA) sabotajsız davranışlar.
bulgu: diğer · açık · F-7 (ORTA) mevcut test güncelleme izni sınırsız.
bulgu: diğer · açık · F-8 (DÜŞÜK) tam kelime şartı.
bulgu: diğer · açık · F-9 (DÜŞÜK) yarım başlatma bayrağı.
bulgu: diğer · açık · F-10 (DÜŞÜK) rapor notu.
bulgu: diğer · kapandı · Ölçülmedi: test, build, sabotaj koşulmadı; sabotaj sonuçları kod okumasıdır; sürücünün yinelenen anahtar istisna tipi gerçek mongod'da ölçülmedi.
kritik-açık: 0
engelleyici-açık: 1
