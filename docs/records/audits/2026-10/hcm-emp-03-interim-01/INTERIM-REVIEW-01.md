# HCM-EMP-03 — ara inceleme 01 (alt ajan; resmî inceleme DEĞİL)

> Sahibin gece talimatı (Yönetici · Ofis aktardı, 2026-10-29): Codex yokken bağımsız incelemeyi alt ajanlar yapsın; ara
> kontroldür, `reviews.tsv` / `approvals.tsv`'ye satır yazılmaz. Resmî inceleme Codex turudur.

- Hedef: MOD-0251 revizyon 4 taslağı (commit'siz çalışma ağacı) @ sha256 `2f44256730e23ee84a8488bdd712437094c40bd5cb970153f6b6f6a694f3b17e`.
- İnceleyen: taze bağlamlı salt-okuma alt ajanı (Explore, model sonnet; ana oturum Opus). Kendisine CT'nin sonucu
  verilmedi; paket, kod, kurallar ve önceki üç resmî inceleme verildi.
- Sonuç: **KOŞULLU.** Kapanan: inceleme 03 F-26. Yeni: AK-27…AK-31 ORTA, AK-32, AK-33 DÜŞÜK; ENGEL yok.

## Bulgular (özet) ve CT kararı

| Bulgu | Özet | CT kararı / ölçüm |
|---|---|---|
| AK-27 | DL-7 çıkışı olay adı ve "güvenli değişiklik" bilgisini merkezi günlükte arıyor; ortak kayıtta bu alanlar yok | Kabul. `CentralAuditOutboxRecord.cs` alanları: Category, Operation, EntityType, EntityId, RequestType, actor, correlation — olay adı ya da önce/sonra yok. AE-3'ün iki sonucu için asgari ölçüm ve olay adı → RequestType/Operation eşlemesi yazılmalı. |
| AK-28 | "Denetim hatası" iki anlamda | Kabul. Yol `b`'de yalnız yerel outbox yazımı işlem içindedir; teslim (Retry/Rejected) sonra ve eşzamansızdır; yazmayı durdurmaz. |
| AK-29 | Enlist edilmeyen başarı açıklaması yanlış | Kabul. `CentralAuditRecorder.cs:160-169`: `RequireEnlistment` açıkken enlist etmeyen başarı hiç kayıt yazmaz, yalnız bilgi günlüğü. Inventory'deki yapısal koruma testi (her Mongo işlemi enlist eder) DL-7'ye eklenmeli. |
| AK-30 | GWT-A11 denetim kısmı ölçülemez | Kabul. Okuma ya da ret için Audited Events'te satır yok. |
| AK-31 | DL-2 ve DL-7 aynı yazma yoluna dokunuyor; ayrı commit kanıtı birbirini korumaz | Kabul. P2 kapanışı için ikinci dilimin birincinin testlerini yeniden koşması ya da birleşik koşu kuralı gerekir. |
| AK-32 | DL-2 bölümlerinde denetim kalıntıları; eşleme sütunları | Kabul. |
| AK-33 | AE-2 "Blocks" A11'i saymıyor; Submit/Consume işaret/davranış ayrımı | Kabul. |

Sonraki adım: revizyon 4 taslağı aynı dosyada düzeltilir, yeni taze bağlamlı ara inceleme koşulur.

## Ara inceleme 02 (aynı yöntem, yeni taze bağlam)

- Hedef: revizyon 4 taslağı @ sha256 `27337c04e5978d999b4844e911bc46bc50fe981d52e50b5b3f4c6479d577d84a` (AK-27…AK-33 düzeltmesinden sonra).
- Sonuç: **KOŞULLU.** Kapanan: F-26, AK-28, AK-30, AK-31, AK-32, AK-33. Açık/yeni: AK-34 (AK-27 devamı), AK-35, AK-36 (AK-29 devamı), AK-37 ORTA; AK-38, AK-39, AK-40 DÜŞÜK. ENGEL yok.

| Bulgu | Özet | CT kararı / ölçüm |
|---|---|---|
| AK-34 | Ret ve çatışma satırları ortak iletici üçlüsüyle ayırt edilemiyor; iletici yalnız Succeeded/Unconfirmed gönderiyor; "reason code" alanı yok; `employee.access_denied` için RequestType "none" Platform'da reddedilir | Kabul. Bu satırlar AE-2/AE-3 kararına bağlanmalı, ölçülmeyen alan istenmemeli. |
| AK-35 | Teslim kesintisi "Retry durumu" diye yazılmış; satır Pending kalır, Attempts/LastError değişir; sağlık yalnız yapılandırılmamış, 10 dk'yı aşan bekleyen ya da Rejected varsa Degraded | Kabul. |
| AK-36 | Yapısal koruma Inventory modelinden eksik: StartTransaction yasağı, sabit işlem sayısı, adı konmuş istisna; HCM'de bugün hiç işlem yok | Kabul. |
| AK-37 | Kiracı kaynağı | **Kabul, kodla doğrulandı.** `TenantResolutionMiddleware.cs:34-59` kiracıyı `X-Tenant-Id` başlığından alır, JWT'yi yalnız çelişki için okur (BL-324 güven kararı); `ICentralAuditScope.cs:7-10` kiracının imzalı belirteçten gelmesini şart koşar ve Platform yeniden denetlemez. DL-7 kapsamına HCM kapsam bağdaştırıcısı (JWT'den çözülmüş kiracı, kullanıcı, korelasyon) ve yapılandırma anahtarları girmeli; BL-324 açık karar. |
| AK-38 | "Nesne" sütununda iki tür; bildirim tek EntityType taşır | Kabul. |
| AK-39 | "Denetim hatası" / "kontrollü hata" dil kalıntıları; "never values" ile tablo çelişkisi | Kabul. |
| AK-40 | DL-7 "DL-2'ye bağlı değil" ama üç ölçüm DL-2 sonucuna dayanıyor | Kabul. |

## Ara inceleme 03 (aynı yöntem, yeni taze bağlam)

- Hedef: revizyon 4 taslağı @ sha256 `c682c74b0269c62066f4f10284db56b35e7310602b38ad2557f89d54fc89b8fd`.
- Sonuç: **KOŞULLU.** Kapanan: resmî inceleme 03 F-26 ve ara AK-27…AK-40 (hepsi). Yeni: AK-41…AK-44 ORTA (onaydan önce), AK-45…AK-50 DÜŞÜK (DL-7 kararlarına kadar bekleyebilir). ENGEL yok.
- Etiket notu: ara bulgular resmî inceleme numaralarıyla çakışmasın diye `AK-` önekiyle yazılır (resmî inceleme 04, F-27den başlar). Bu not ilk yazıldığında F- önekiyle yazılmıştı; içerik aynı.

| Bulgu | Özet | CT kararı / ölçüm |
|---|---|---|
| AK-41 | Korelasyon kimliği JWTden gelmez; HCMde korelasyon bağlamı yok, kaydedici olay kimliğine düşer | Kabul, kodla doğrulandı (InventoryCentralAuditScope.cs:19,62-68 ICorrelationContext; HCMde yok). |
| AK-42 | Kiracı çelişkisi ölçümü bugün zaten geçiyor; asıl risk tenant_id claimi olmayan belirteç + başlık | Kabul. |
| AK-43 | Paket ara etiketleri resmî numaralarla çakışıyor | Kabul; AK- önekine geçildi. |
| AK-44 | Paket sahibi (OWN) TBD; ilk onay HIGH; sahip alanı incelemeden sonra yazılırsa sürüm bayatlar | Kabul — sahip kararı. **Kapandı (2026-10-29):** sahip sohbette `ali.tufanoglu`'yu seçti (izin kulesi MOD-0280-FU01 ile aynı biçim); künyeye inceleme 04'ün hedef sürümünden önce yazıldı. |
| AK-45…AK-50 | AE-2 iki ret yolu; outbox durum modeli (AwaitingCommand, orphan, Abandoned, Rejected nedenleri); sağlık eşlemesi ve eski izlerin akıbeti; kişisel veri anlatımı; Submit/Consume ölçümü; eski ifadeler | Kabul; DL-7 kararlarına kadar bekleyebilir, yine de düzeltildi. |
