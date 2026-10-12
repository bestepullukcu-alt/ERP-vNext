# Tek somut owner karar paketi — HENÜZ ONAYLANMADI / UYGULANMADI

Bu paket yalnız A / 3.0.0-rc.1 temelinden hazırlanmıştır. B tarihsel kalır.
Final artifact içindeki `3.0.0 / FROZEN` hedef byte’ları gösterir; mevcut canonical
sözleşmenin sürümünün değiştiğini veya owner’ın sürümü seçtiğini göstermez.

## Tek karar hedefi

| Alan | Exact değer |
|---|---|
| Final-proposed YAML SHA256 | `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab` |
| Final-proposed annex SHA256 | `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64` |
| Publication-proposed patch SHA256 | `064827b512586bb17813a8e3703cc186f4a94295c590329674897ba31327aa23` |
| Canonical YAML preimage | `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` |
| Korunacak canonical v2 annex | `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda` |
| YAML yayın hedefi | `docs/analysis/contracts/sandop-capacity.openapi.yaml` |
| Eklenecek annex hedefi | `docs/analysis/contracts/sandop-capacity-semantics-v3.0.0.md` |

Publication patch yalnız YAML’ı değiştirir ve yeni annex’i ekler. Eski v2 annex, A,
B ve tarihsel consent/provenance silinmez veya değiştirilmez. Annex’te kalıcı
“UNAPPROVED/noncanonical” etiketi yoktur; hazırlanmış ama uygulanmamış olma durumu
bu karar dosyasında ve SOP-22.md’de tutulur.

## Tek mesajda verilebilecek dört ayrı karar — aşağıdaki metin taslaktır

> **MVP6-CAPACITY-DUPLICATE-FINAL-RELEASE-PREP-01 kararım:**
>
> **1. Final sürüm ve exact artifact seçimi.** Yalnız A’dan türetilmiş
> SANDOP-CAPACITY **3.0.0 / FROZEN / wire v1** finalini seçiyorum. YAML
> `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab`, annex
> `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64` ve patch
> `064827b512586bb17813a8e3703cc186f4a94295c590329674897ba31327aa23`
> bu kararın tek exact hedefidir. A→final değişikliği metadata/path/status
> düzeyindedir; business semantics aynıdır. B’yi tarihsel koruyorum.
> Major sürümün aynı-route/wire-v1 için negotiation veya runtime güvenliği
> sağlamadığını, yeni 409 kodunun strict-code tüketicileri kırabileceğini kabul ediyorum.
>
> **2. MOD-0190 design-consumer consent.** MOD-0190 sahibi sıfatıyla yukarıdaki
> exact üç hash için ayrı design-consumer consent veriyorum. Altı S&OP operasyonu
> değişmez; ortak artifact/pin değişimini ve aynı-route sınırını kabul ediyorum.
> Bu consent çalışan consumer, HTTP, rollout, pack promotion veya runtime uptake
> kabulü değildir; eski 2.0.0 consent’inin taşınması değildir.
>
> **3. MOD-0192 design-consumer consent.** MOD-0192 sahibi sıfatıyla aynı exact
> üç hash için ayrı design-consumer consent veriyorum. createCapacityScenario’nun
> `409 CAPACITY_SCENARIO_NAME_CONFLICT` kodunu ve exact mesajını, active exact-name
> uniqueness’i, deterministik/proven unique-index loser eşitliğini, lifecycle/fixture/
> receipt precedence’ini ve unknown-commit sınırını kabul ediyorum. Bu consent
> gerçek race/HTTP kanıtı veya Lane B source uygulama yetkisi değildir.
>
> **4. Koşullu tek-yazar canonical publication.** Yukarıdaki iki design consent
> ve final sürüm kararı kayda bağlandıktan; exact preimage/target/patch hash’leri,
> bağımsız final static VER, disposable byte-equality ve uygulanabilir mevcut
> publication/guard kapıları doğrulandıktan sonra, ayrı
> **MVP6-CAPACITY-DUPLICATE-PUBLICATION-01** işinde tek contract publication owner’ın
> yalnız yukarıdaki iki canonical hedef için bu patch’i uygulamasını yetkilendiriyorum.
> YAML preimage’ı veya yeni annex hedefinin yokluğu değişirse overwrite yapılmasın;
> yeni exact disposition için durulsun. İki dosya tek kontrollü işlemde doğrulansın;
> yarım yayın veya başka writer ile çakışma bırakılmasın. Kapı başarısızsa yayın
> yapılmasın; bu karar guard değişikliği/istisnası/activation yetkisi değildir.
>
> 22 Eylül 2026 08:42 UTC tarihli repo dışı SANDOP uygulama/SDK/entegrasyon
> “Yoktur” beyanımı yalnız o tarih ve kapsamda kullanıyorum; yeni consumer yokluğunu
> garanti etmiyorum. Sonradan bulunan consumer için runtime cutover öncesi ayrı
> disposition gerekir. Specification yayını runtime deployment değildir.
> Strict-code consumer readiness, mixed-version cutover ve rollback planı ayrı
> runtime kapıları olarak kalır; info.version bunların yerine geçmez.
>
> Canonical publication dışındaki production source, Lane B duplicate-name davranışı,
> Program.cs, shared permission, gateway, UI, live producer/optimizer, publisher,
> migration, rollout, E5/G5, pack promotion ve commit/push/stash yetkisi vermiyorum.
> Bu dört kararı, yetkili olduğum iki ayrı design-consumer rolünü açıkça üstlenerek
> veriyorum; teknik PASS’i ürün veya CT runtime acceptance saymıyorum.

## Karar uygulanmadan önce

Bu dosya consent kaydı değildir. Gerçek kullanıcı kararı gelince exact metin, tarih,
kimlik/kapsam ve üç hash ayrı append-only owner kaydında bağlanmalıdır. Artifact’ların
bir byte’ı değişirse karar kapsamı otomatik taşınmaz. Hazırlık yazarı bu görevde
canonical/guard/source yazmadı; yukarıdaki publication yetkisi henüz verilmiş değildir.
