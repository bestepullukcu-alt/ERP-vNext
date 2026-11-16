---
id: MOD-0024-FU01
name: Quick Create Submit Lock
parent: MOD-0024
parent_pack: MOD-0024-task-engine-create-runtime.md
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: TenantScopedEntity
status: draft
owner: platform-team (Görev Merkezi pilot hattı LANE-001)
branch: feature/pss/mod-0024-fu01-quick-create-lock
started: 2026-11-16
target: TBD
form_field_count: 20
backlog: BL-325 (kısmi)
pack_standard: kisa-v1
plan_row: plan satırı yok — MVP planı depoda değil; iş canlı hata kaydı BL-325'ten doğdu
business_process: yok — takip paketi: üst paketin süreci (üst pakette süreç sayfası yok; bölüm 7, karar 1)
conditional: [ekran, durum-akisi]
---

## 1. Ne ve neden
Görev Merkezi'nin hızlı görev oluşturma panelinde "Oluştur"a (ya da Enter'a) art arda basmanın ikinci bir görev üretmesini,
gönderim sürdüğü sürece gönderimi kilitleyerek önlemek; istek hata dönünce düğme yeniden açılır. Roller: görev oluşturan
kiracı kullanıcısı (`platform.tasks.create`). Süreçte karşıladığı adım: üst paketin görev oluşturma adımı, hızlı derinlik
(üst paket §12 K9 / DEV-1); ayrı süreç sayfası yok (bölüm 7, karar 1). Servis sapması yok: yalnız Diten.Web ön yüz dosyası
değişir, Diten.Platform sunucu koduna dokunulmaz. BL-325 bu paketle kapanmaz (bölüm 2).

## 2. Kapsam
- Kapsam: `frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/quick-create.js` içinde gönderim kilidi — düğme tıklaması,
  Enter kısayolu ve `WcnQuickCreate.submit()`'in doğrudan çağrısı aynı kilitten geçer; yeni test dosyası
  `frontend/Diten.Web/tests/tasks-quick-create-submit-lock.test.js`.
- Kapsam dışı: sunucuda idempotency anahtarı ve aynı anahtarla iki isteğin tek görev ürettiğini ölçen test (BL-325 kapanış
  ölçütü b ve c; kardeş kayıt BL-306) → MOD-0024 altında ayrı takip paketi; kimliği o paket açılırken verilir.
- Kapsam dışı: ayrıntılı form (`/Tasks/Create`, `Tasks/form-page.js`, `Tasks/form.js`) gönderim kilidi (BL-325 ölçütü a'nın
  ikinci yolu) → ayrı takip paketi.
- Kapsam dışı: sayfa yenileme, iki sekme, ağ katmanının isteği yinelemesi — istemci kilidi bunları kapsamaz → sunucu anahtarı
  paketi.
- Kapsam dışı: bekleme göstergesi ve yeni görünen metin → bölüm 7, karar 2.
- Erteleme bedeli (Ç22): sunucu anahtarı ve ayrıntılı form kilidi ertelendikçe iki sekme, ağ yinelemesi, yanıtı kaybolan
  istekten sonraki yeniden basış ve ayrıntılı formdaki çift basış yine çift görev üretir; bu kopyalar elle temizlenir ve
  istemci anahtarı sonradan iki yüzeye birden eklenirken bu paketin kilidi yeniden açılır.
- Sahip olunan nesneler: yeni nesne yok; var olan `WcnQuickCreate.submit` davranışı.

## 3. Bağlar
| Kaynak | Atıf | Giren | Girmeyen |
|---|---|---|---|
| Süreç | süreç sayfası yok (bölüm 7, karar 1) | görev oluşturma adımı, hızlı derinlik | ayrıntılı formla oluşturma → ayrı takip paketi |
| Plan | plan satırı yok · ürün iş listesi BL-325 "Oluştur'a iki kez basmak iki görev yaratıyor" | BL-325 kapanış ölçütü a'nın hızlı oluşturma yolu | BL-325 ölçütü a'nın ayrıntılı form yolu; ölçüt b ve c |
| Üst paket | MOD-0024 · `MOD-0024-task-engine-create-runtime.md` (ready-for-dev) §12 K9 / DEV-1 | hızlı oluşturma paneli, taslak devri, `wcn:task-created` olayı — atıfla yeniden kullanılır | üst paketin sözleşmesi ve ölçütleri değişmez; üst pakete dilim eklenmez (KP9) |
| Var olan kod (değişen) | `WorkCenterNext/quick-create.js` | `submit` ve `#quickSubmit` bağlaması | `open`, `close`, `readDraft`, `openDetailed`, seçici yüklemesi, olay adı ve ayrıntısı, hata penceresi seçimi |
| Var olan kod (yalnız okunur) | `Tasks/api.js` (`create`, `failureMessage`), `Tasks/form.js` (`validateDraft`, `buildCreatePayload`), `shared/diten-enter-primary.js`, `Views/Tasks/_QuickCreateOffcanvas.cshtml` | çağrılır; testte gerçek dosya yüklenir | bu dosyalar değişmez; düğmenin `data-dt-primary="enter"` işareti aynen kalır |
| Var olan testler | `tests/tasks-quick-create.test.js`, `tests/tasks-quick-create-golden.test.js` | değiştirilmeden yeşil kalır | bu dosyalara ekleme ya da düzeltme yapılmaz |
| Başka modül | yok (XMC-001 gerekmez) | yok | Diten.Platform `TasksController` ve komutları, ağ geçidi, `ocelot.json`, Görev Merkezi `app.js` |

## 4. Sözleşme
Nesneler ve uç noktalar

| Nesne | Tür | Açıklama | Dilim |
|---|---|---|---|
| `WcnQuickCreate.submit` | dışa açık arayüz | bir gönderim sürerken yeni çağrı istek göndermez, pencere açmaz, `null` döner; gönderim sürerken `#quickSubmit` pasiftir; gönderim bitince (başarı, hata, devir, istisna) kilit kalkar; imza ve başarı dönüşü değişmez | DL-1 |
| `POST api/v1/tasks` (ön yüz `/Tasks/api/*` vekili üzerinden) | uç nokta | değişmez; yalnız çağrı sayısı ölçülür | DL-1 |

Alanlar

| Nesne.alan | Tip | Zorunlu | Kural | Dilim |
|---|---|---|---|---|
| yeni alan yok | yok | hayır | veri ve şema değişmez | DL-1 |

İzinler

| Anahtar | Tür | Kim | Uç nokta | Dilim |
|---|---|---|---|---|
| `platform.tasks.create` | standart (var olan, değişmez) | görev oluşturabilen kiracı kullanıcısı | `POST api/v1/tasks` | DL-1 |

Denetlenen olaylar

| Komut | İşlem adı | Yol ya da istisna sınıfı + gerekçe | Dilim |
|---|---|---|---|
| var olan görev oluşturma (`CreateTaskItemCommand`) | değişmez | üst paketteki yol aynen; bu paket yeni yazma komutu eklemez, sunucu koduna dokunmaz | DL-1 |

## 5. Acceptance Criteria

- [ ] AC-1 (zorunlu) — İlk istek yanıt dönmeden `#quickSubmit` iki kez tıklanınca `TasksApi.create` bir kez çağrılır, bir `wcn:task-created` olayı yayılır ve panel bir kez kapanır; test yanıtı kendisi bitirene kadar bekleyen bir istekle ölçer ve `submit`'teki kilit geri alınınca iki çağrı sayarak kırmızıya döner · dilim: DL-1 · test: birim
- [ ] AC-2 (zorunlu) — Düğmeye basılmadan `WcnQuickCreate.submit()` ilk istek sürerken ikinci kez çağrılınca ikinci çağrı istek göndermez, hiçbir pencere açmaz ve `null` döner; kilit yalnız düğmenin pasifliğine dayanırsa test kırmızıya döner · dilim: DL-1 · test: birim
- [ ] AC-3 (zorunlu) — İstek sürerken `#quickSubmit` `disabled` durumundadır; başarılı yanıttan sonra düğme yeniden açıktır; panel `open()` ile yeniden açılıp (tarih bugüne döner) yeni başlık yazılınca ikinci basış ikinci isteği gönderir (kilit sonraki görevi engellemez) · dilim: DL-1 · test: birim
- [ ] AC-4 (zorunlu) — Panel açıkken başlık alanında Enter'a basılınca gönderim başlar; ilk istek sürerken, Enter kuralının kendi 800 ms bastırma süresi geçtikten sonra Enter'a yeniden basılınca ikinci istek gitmez (gerçek `shared/diten-enter-primary.js` yüklenir); `submit`'teki kilit geri alınınca test kırmızıya döner · dilim: DL-1 · test: birim
- [ ] AC-5 (zorunlu) — Sunucu hata dönünce (ör. 403 ya da 500, `ok:false`) hata penceresi bugünkü gibi bir kez açılır, `submit` bittiğinde `#quickSubmit` yeniden açıktır ve yeniden basış yeni bir istek gönderir; hata yolunda kilit açılmazsa test kırmızıya döner · dilim: DL-1 · test: birim
- [ ] AC-6 (zorunlu) — Ağ hatasında (gerçek `Tasks/api.js` ile, `fetch` hata atınca `reasonCode: UNAVAILABLE`) hata penceresi açılır ve `#quickSubmit` yeniden açıktır · dilim: DL-1 · test: birim
- [ ] AC-7 (zorunlu) — `TasksApi.create` istisna atarsa `submit`'in sözü bugünkü gibi reddedilir (istisna yutulmaz) ve `#quickSubmit` yeniden açıktır · dilim: DL-1 · test: birim
- [ ] AC-8 (zorunlu) — Doğrulama reddinde (eksik başlık ya da tarih) istek gitmez, uyarı bir kez açılır ve `#quickSubmit` pasif kalmaz; alanlar doldurulup yeniden basılınca istek gider · dilim: DL-1 · test: birim
- [ ] AC-9 (zorunlu) — Sunucu `TASK_FIELD_VALUE_INVALID` dönünce uyarı penceresi açıkken (test pencereyi elle kapatana dek) ikinci basış istek göndermez; pencere kapanınca taslak `/Tasks/Create`'e devredilir ve `#quickSubmit` pasif kalmaz · dilim: DL-1 · test: birim
- [ ] AC-10 (zorunlu) — Yerel geliştirme ortamında Görev Merkezi "+ Yeni ▸ Görev" panelinde istek yavaşlatılmışken (tarayıcı ağ kısıtlaması) "Oluştur"a hızlı iki kez basılınca İşlerim'de tek yeni görev ve tek "eklendi" bildirimi görünür; düğme istek sürerken pasif görünür · dilim: DL-1 · test: canlı tur

## 6. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | süreç adımı | plan | ölçütler | bağımlı | kanıt |
|---|---|---|---|---|---|---|---|
| DL-1 | Hızlı oluşturma gönderim kilidi | Sözleşme, Acceptance Criteria, K-ekran. Ekran, K-durum-akisi. Durum değiştiren akış | görev oluşturma adımı, hızlı derinlik (teknik: BL-325 kusur düzeltmesi) | plan satırı yok | 1–10 | bağımsız | otomatik test, canlı tur |

## 7. Açık kararlar
| # | Karar | Kim | Hangi dilimi bekletir |
|---|---|---|---|
| 1 | Üst pakette süreç sayfası yok. Bu paket yeni süreç adımı getirmeyen bir kusur düzeltmesi olduğu için süreç yazmaz ve `yok — takip paketi` biçimini kullanır. Onaylayıcı KP12'yi "üst paketin süreci yoksa takip paketi süreci yazar" diye okursa görev oluşturma süreci iş analisti işiyle bu PR'a eklenir. | hattın asıl onaylayıcısı (yedek onaylayıcı) | DL-1 — onaydan önce kapanır |
| 2 | Bekleme göstergesi: varsayılan yalnız düğmenin pasif olmasıdır; dönen simge ya da "Oluşturuluyor…" metni yoktur (yeni metin 7 dil çeviri işi açar). Onaylayıcı gösterge isterse paket yeni sürümle ölçüt ve dil işi ekler. | hattın asıl onaylayıcısı (yedek onaylayıcı) | DL-1 — onaydan önce kapanır |
| 3 | "Detaylı form" düğmesi istek sürerken açık kalır (varsayılan; kişinin koyduğu kapsam tek düğmedir). İstek sürerken ona basılırsa hızlı istek yine görev oluşturabilir ve ayrıntılı formdan ikinci görev doğabilir; onaylayıcı isterse aynı dosyada o düğme de kilitlenir ve bir ölçüt eklenir. | hattın asıl onaylayıcısı (yedek onaylayıcı) | DL-1 — onaydan önce kapanır |

## K-ekran. Ekran
| Soru | Cevap | AC | Dilim |
|---|---|---|---|
| Ekran kalıbı ve gerekçesi? | değişmez: var olan hızlı oluşturma yan paneli (üst paket §12 K9 / DEV-1); yeni ekran yok | — | DL-1 |
| Altın referans ve `data_mode`? | compact (üst paketten); veri okuma ve yazma biçimi değişmez | — | DL-1 |
| Klavye? | Enter (`data-dt-primary="enter"`) tıklamayla aynı gönderimdir; düğme pasifken Enter kuralı basmaz (UXH-001), kilit `submit`'te de durur | 4 | DL-1 |
| Odak ve etiket? | değişmez; düğme metni aynı, yeni görünen metin yok (gösterge: bölüm 7, karar 2) | — | DL-1 |
| Yükleniyor durumu? | istek sürerken birincil düğme pasif | 3 | DL-1 |
| Hata durumu? | hata penceresi bugünkü gibi; düğme yeniden açılır | 5, 6, 7 | DL-1 |
| Doğrulama reddi ve ayrıntılı forma devir? | uyarı bugünkü gibi; kilit kalmaz | 8, 9 | DL-1 |
| Yetkisiz durum? | 403 hata penceresi bugünkü gibi ve düğme açılır; yetkisize panelin çizilmemesi üst paketin işidir, değişmez | 5 | DL-1 |
| Boş durum? | uygulanmaz: form paneli, liste yok | — | DL-1 |
| Sayfa yardımı ve tur? | değişmez; yeni kısayol yok | — | DL-1 |
| Seçim listesi kaynağı ve menü anahtarı? | değişmez; yeni seçim listesi ve menü öğesi yok | — | DL-1 |

## K-durum-akisi. Durum değiştiren akış
| Soru | Cevap | AC | Dilim |
|---|---|---|---|
| Eşzamanlılık (sürüm ya da koşullu yazma)? | gerekmez: yeni kayıt oluşturulur, var olan kayıt değişmez | — | DL-1 |
| Tekrar güvenliği (anahtar)? | yalnız istemci kilidi: aynı sayfada bir gönderim sürerken ikinci gönderim yok; sunucu anahtarı kapsam dışı (bölüm 2) | 1, 2, 4 | DL-1 |
| Kilidin sınırı? | sayfa belleğindedir; yenileme, iki sekme ve ağ yinelemesi korunmaz (bölüm 2, kapsam dışı) | — | DL-1 |
| Yarıda kalan işin telafisi? | yanıt kaybolursa (`UNAVAILABLE`) kullanıcı hata görür, düğme açılır; sunucu kaydı oluşturmuşsa yeniden basış ikinci görev üretebilir — bilinen artık risk, sunucu anahtarıyla kapanır | 6 | DL-1 |
| Yeniden deneme sınırı? | otomatik yeniden deneme yok; yeniden deneme kullanıcının yeni basışıdır | 5 | DL-1 |
| İşlem (transaction), tekli Mongo? | gerekmez: sunucu kodu değişmez | — | DL-1 |
