# MOD-0024-FU01 — Quick Create Submit Lock · kararlar

> Sözleşme: `../MOD-0024-FU01-quick-create-submit-lock.md`. Bu dosya onaya bağlı değildir; işçi okumaz, denetçi ilk turda
> okumaz (KP1). Yazan: paket yazarı (`ees-module-pack-author`), LANE-001 pilot hattı.

## Sürümler
| Sürüm | Tarih | Ne değişti |
|---|---|---|
| r1 | 2026-11-16 | ilk taslak (`status: draft`), taban main `c3547081a` |
| r2 | 2026-11-16 | denetçi KOŞULLU, tek düzeltme turu: B1 — AC-1/AC-4 sabotajı kilidin iki parçası birlikte, AC-2 yalnız bayrak, AC-3 yalnız pasifleştirme; B3 — §4 `submit` satırına "`TASK_FIELD_VALUE_INVALID` uyarısı beklenirken kilit tutulur". Ölçüt numaraları değişmedi. DL-1 6632 bayt (hedef 6 KB aşıldı, uyarı sınırı 10 KB'ın altında; sabotaj tarifi ölçüt metninde durmalı, bölünmedi) |

## Ölçümler (main `c3547081a`, paket yazarı kendisi okudu)
- `WorkCenterNext/quick-create.js` (209 satır): `submit` (84–140) içinde çift gönderim koruması yok; 158. satırda
  `#quickSubmit` tıklaması doğrudan `submit`'e bağlı. Doğrulama reddi uyarıyı beklemeden döner; `TASK_FIELD_VALUE_INVALID`
  yolu uyarıyı **bekler**, sonra `openDetailed()`; öteki hatalar hata penceresini beklemeden açar. Başarıda
  `clearDraft` → `resetDraft` (başlık, kişi ve tarih boşalır) → `close` → `wcn:task-created`. `TasksApi.create` istisna
  atarsa `submit`'in sözü reddedilir (yakalama yok).
- `Tasks/api.js` `request` (24–36): `fetch` hatasında fırlatmaz, `{ ok:false, status:0, reasonCode:'UNAVAILABLE' }` döner.
- `Views/Tasks/_QuickCreateOffcanvas.cshtml:113`: düğme `data-dt-primary="enter"`, `type="button"`.
- `shared/diten-enter-primary.js`: pasif birincil düğmeye basmaz; aynı panelde 800 ms (`IN_FLIGHT_MS`) içinde ikinci
  Enter'ı bastırır. Bu yüzden Enter ölçütü (AC-4) 800 ms'den sonra ölçülür; yoksa test kilit olmadan da yeşil kalırdı.
- `WcnQuickCreate.submit` yalnız `quick-create.js` içinde ve testlerde çağrılıyor; `app.js` yalnız `open()`'ı çağırıyor.
- Var olan testler: `tests/tasks-quick-create.test.js`, `tests/tasks-quick-create-golden.test.js` (vitest, jsdom).
- `CreateTaskItemCommand` sunucu komutudur; bu paket ona dokunmaz.

## Yargı kararları
1. **`business_process: yok — takip paketi: üst paketin süreci`.** Üst paket (create-runtime) eski standartla yazılmış,
   süreç sayfası yok; depoda hiç `SUR-` dosyası yok. Şablonun kullanım notu takip paketinin "üst paketin sürecine atıf
   yapar ya da `yok — takip paketi` yazar; süreç yazmaz" dediğini, KP12'nin ise "üst paketin süreci yoksa takip paketi
   süreci yazar" dediğini okudum; ikisi bu durumda çelişiyor. Bu paket yeni süreç adımı getirmeyen bir kusur düzeltmesi
   olduğu ve hattın brifi yazılacak dosyaları sınırladığı için süreç yazılmadı; karar onaylayıcıya bölüm 7, karar 1
   olarak bırakıldı. `yok — teknik` seçilmedi: iş kullanıcıya görünen bir oluşturma adımıdır, altyapı değildir.
2. **`plan_row: plan satırı yok — …`.** MVP planı depoda değil; değer uydurulmadı. İşin kaynağı ürün iş listesi BL-325.
3. **`conditional: [ekran, durum-akisi]`.** `ekran`: arayüz davranışı değişiyor (birincil düğmenin durumu, Enter).
   `durum-akisi`: standardın "tekrar güvenliği (anahtar)" ve "yarıda kalan iş" soruları tam bu kusurun konusu; açılmasa
   denetçi "açılmamış ama gereken bölüm" bulurdu. Açılmayanlar: `sozlesme-degisikligi` (başka modül `submit`'i tüketmiyor,
   uç nokta değişmiyor), `gozlem` (arka plan işi ya da servisler arası yeni çağrı yok), `veri-degisikligi` (şema yok),
   `rota` (yeni rota yok), ötekiler işin türüne uymuyor.
4. **Kimlik kaydı satırı eklendi** (`execution/registries/module-id-registry.md`, `MOD-0024-WC1B` satırının altına).
   Gerekçe: eski paket standardı künye `id`'sini "Registry-controlled ID (`MOD-NNNN-FUxx`)" diye tanımlıyor; kayıt dosyası
   FU satırı açmadan önce ön kontrolü şart koşuyor; bu alanda 53 FU paketinin 47'si kayıtta satır taşıyor. Kimlik aracı
   satırsız da OK veriyordu; satır eklendikten sonra `--check-id` yine OK (çıkış 0), `--check-all` çıkış 0.
5. **Künye alanları üst paketten** (KP9): `golden_reference: compact`, `form_field_count: 20`, `service: Diten.Platform`,
   `shell: tenant`, `entity_base: TenantScopedEntity`. Değişen dosya Diten.Web'dedir; bölüm 1 bunu "servis sapması yok"
   diye söyler. Eklenen alanlar (KP11, yalnız ekleme): `parent`, `parent_pack`, `backlog`. `owner` üst paketteki ekip
   adıdır (rol), kişi adı yazılmadı.
6. **Kilidin yeri.** Ölçütler kilidin yalnız düğmenin pasifliğinde değil `submit`'in kendisinde durmasını ister (AC-2):
   Enter yolu ve doğrudan çağrı da aynı kapıdan geçsin, düğme sayfada olmasa bile çift istek gitmesin.
7. **Hata yolları ayrı ölçüt** (KP5): sunucu hatası (AC-5), ağ hatası gerçek `api.js` ile (AC-6), istisna (AC-7),
   doğrulama reddi (AC-8), ayrıntılı forma devir (AC-9). AC-9'da kilit bekleyen uyarı boyunca tutulur, çünkü o uyarı
   bugün de bekleniyor ve açıkken ikinci basış ikinci isteği gönderirdi.
8. **Sabotaj beklentisi ölçüt metninde.** AC-1, AC-2, AC-4 ve AC-5 kilidin (ya da hata yolundaki açılışın) geri alınınca
   kırmızıya dönmesini metinde söyler; kanıt çalıştırma kaydına girer (İ6).
9. **Yeni görünen metin yok.** Bekleme göstergesi bölüm 7, karar 2'ye açık karar olarak yazıldı (varsayılan: yok).
10. **"Detaylı form" düğmesi kilitlenmez** (varsayılan): kişinin koyduğu kapsam tek düğmedir; artık risk bölüm 7, karar 3.

## Boyut (pack_contract_check okuyucularıyla, r1)
Ortak kısım 5754 bayt (hedef ≤ 8 KB) · DL-1 5884 bayt (hedef ≤ 6 KB); r2'de 6632 bayt · tek dilim.
