---
decision_id: PVG-MOD0230-OPEN-DECISIONS-01
status: proposed — sahip 10/10 "Evet" işaretledi (2026-10-21, sohbette "2 — hepsine evet"); OD-PVG-4, 5, 8, 10 tam karar; OD-PVG-1, 2, 3, 6, 7, 9 PVG tarafı karar, diğer sahiplerin onayı bekliyor (taslak, commit edilmedi)
prepared_at_local: 2026-10-21
prepared_by: PVG Control Tower (Claude Code oturumu); bu kayıt kendi adına karar vermez
decided_by: depo sahibi (rol) — her satırda "Sahip işareti" alanı; kişi adı ve e-posta yazılmaz
source: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 8b66d283b, bölüm "Open Decisions (Owner Decision Pending)" (OD-PVG-1…10) ve "Build-Gate Gaps (Measured 2026-10-21)" (G-1…G-15)
scope: yalnız MOD-0230 Case Intake & Triage, build/test kapısı; operasyonel runtime kapısı kapalı kalır
bound_to: dal `chore/ct/pvg-ct-01` @ 8b66d283b (push edilmedi); paket sha256 8b615be0be4cc23e6fffb034bcb555e360aa8783fdbfd0822215816cb5efd2d7
---

# MOD-0230 — paketteki 10 açık karar (OD-PVG-1…10): sahip işareti taslağı

> Bu dosya bir **taslaktır**. Hiçbir satır, sahip işaretlemeden karar sayılmaz. İşaretlenen bir "Evet" yalnız PVG
> tarafının kararıdır; satırda "Ayrıca gereken onay" yazıyorsa o taraf onaylamadan iş başlamaz (XMC-001). Bu kayıt
> onay satırı, paket değişikliği, kod izni ya da dağıtım izni değildir. Kararlar işaretlendikten sonra paket ayrı
> bir revizyonla güncellenir ve inceleme 02'ye o sürüm gider.

## Nasıl işaretlenir

Her satırda `[ ] Evet  [ ] Hayır` alanından birini `[x]` yapın. "Hayır" ise not alanına istediğiniz seçeneği bir
cümleyle yazın. Hiç işaret yoksa satır açık kalır; paketteki ilgili kabul ölçütü (BG-n) de açık kalır.

## Özet

Sahip işareti (2026-10-21): on satırın onu da **Evet**. Son sütun, işaretin tek başına yetip yetmediğini gösterir.

| # | Konu (düz Türkçe) | Öneri, tek cümle | Sizin işaretiniz yeter mi? |
|---|---|---|---|
| OD-PVG-1 | İzin anahtarlarının adı | `pvg.case-intake-triage.*` ailesi kullanılsın, koddaki `pvg.mod0230.intake.*` değiştirilsin | Hayır — MOD-0018 sahibi katalog ve grant'ı onaylar |
| OD-PVG-2 | Kiracı ve kullanıcının servise nasıl ulaştığı | Servis JWT'yi kendisi doğrulasın, kiracıyı Gateway'in yazdığı başlıktan alsın | Hayır — Enterprise Architecture ve MOD-0018 sahibi (claim adları) |
| OD-PVG-3 | Hata kodları | Bulunamayan ve başka kiracıya ait kayıt 404, yetkisiz 401/403, eksik bağlam 400, çakışma 409 | Kısmen — düzenlenmiş hata zarfı için MOD-0041 / Ops |
| OD-PVG-4 | Arka uç dosya düzeni | Bugünkü `RegPvBase` düzeni belgelenmiş sapma olarak kalsın, ad değişmesin | Evet |
| OD-PVG-5 | Açılışta eksik bağımlılık | Üretim-benzeri ortamda eksik bağımlılıkla servis açılmasın; her ortamda hazır kontrolü 503 dönsün | Evet |
| OD-PVG-6 | "Runtime yetkili" bayrağı | Bayrak yalnız testlerde açılabilsin; commit'li bir dosyada açılırsa CI kırmızı versin | Kısmen — Enterprise Architecture ile birlikte |
| OD-PVG-7 | Port | 5011 PVG servisine verilsin, yerel `web-verify` başka porta taşınsın | Hayır — port tahsisi Enterprise Architecture kararıdır |
| OD-PVG-8 | Liste veri modu | Liste sunucu sayfalı olsun (`data_mode: server`) | Evet |
| OD-PVG-9 | Denetim kaydı yolu | Ortak denetim ileticisi (K4) ve MOD-0021 sözleşmesi beklensin; o zamana kadar hiçbir kayıt testler dışında yazılamasın | Hayır — Control Tower (K4 paketi) ve MOD-0021 sahibi |
| OD-PVG-10 | Liste ekranı denetim aracının açıkları | Toplu silme, dışa/içe aktarma ve tümünü seç maddeleri "bilinen sapma" sayılsın, kalan beş madde düzeltilsin | Evet |

## Kararlar

### OD-PVG-1 — İzin anahtarlarının adı

- **Olgu (G-3):** kod bugün `pvg.mod0230.intake.{read,create,update,triage,route}` anahtarlarını denetliyor; paket
  `pvg.case-intake-triage.*` öneriyor. İki aileden hiçbir anahtar henüz seed edilmedi, rol'e verilmedi ya da
  `[HasPermission]` ile kullanılmadı; yani "yayımlanmış" anahtar yok.
- **Öneri:** `pvg.case-intake-triage.{read,create,update,triage,route}`. Koddaki beş sabit ve bir test değişir; eski
  ad için takma ad gerekmez.
- **Gerekçe:** izin anahtarı standardı (PKS-001 §1) ortadaki parçanın kaynak adı olmasını ister, modül kimliği
  (`mod0230`) değil; ad Gateway route'u ve ekran adresiyle aynı olur.
- **Etkisi:** paket kabul ölçütleri BG-2 ve BG-4 açılır (DL-6, DL-7 dilimleri).
- **Ayrıca gereken onay:** MOD-0018 / AuthService sahibi — katalog ve grant sahipliği, `pvg.*` ad alanının PKS-001
  §4 tablosuna eklenmesi.
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: PVG tarafı evet (sahip, 2026-10-21, "hepsine evet"). MOD-0018 / AuthService sahibinin katalog-grant ve `pvg.*` ad alanı onayı bekleniyor — satır o onaya kadar uygulamaya açık değil.

### OD-PVG-2 — Kiracı ve kullanıcının servise nasıl ulaştığı

- **Olgu (G-1, G-2):** PVG servisi JWT doğrulamıyor; kiracı ve kullanıcıyı isteğin `X-Diten-Tenant-Context` ve
  `X-Diten-Actor-*` başlıklarından okuyor. Gateway kiracıyı `X-Tenant-Id` başlığına yazıyor ve `X-Diten-*`
  başlıklarına dokunmuyor; yani bugün kiracıyı istemci belirleyebiliyor.
- **Öneri:** servis JWT'yi diğer kiracı servisleri gibi kendisi doğrular; kullanıcıyı doğrulanmış token'dan, kiracıyı
  Gateway'in yazdığı `X-Tenant-Id`'den alır ve token'daki kiracıyla eşleşmesini ister; `X-Diten-*` başlıkları yok
  sayılır. Gateway kodu değişmez.
- **Gerekçe:** tek kiracı kaynağı (multi-tenancy kuralı), token–kiracı eşleşmesi (security-jwt kuralı).
- **Etkisi:** BG-5 (DL-4).
- **Ayrıca gereken onay:** Enterprise Architecture (servisler arası sözleşme); MOD-0018 sahibi (claim adları).
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: PVG tarafı evet (sahip, 2026-10-21). Enterprise Architecture ve MOD-0018 sahibinin (claim adları) onayı bekleniyor — satır o onaylara kadar uygulamaya açık değil.

### OD-PVG-3 — Hata kodları

- **Olgu (G-8):** servis başarı dışında her sonucu 409 döndürüyor; bulunamayan kayıt ve yetkisiz istek de 409.
- **Öneri:** bulunamayan ve başka kiracıya ait kayıt → 404 (kayıt verisi yok); token yok/geçersiz → 401; izin yok →
  403; kiracı bağlamı eksik ve doğrulama hatası → 400; mükerrer, akış/kanıt engeli ve eşzamanlılık çakışması → 409;
  beklenmeyen hata → boş olmayan güvenli zarfla 500.
- **Gerekçe:** multi-tenancy ve security-jwt kuralları ile paketin "Failure Path to Verify" bölümü.
- **Etkisi:** BG-6 (DL-4).
- **Ayrıca gereken onay:** MOD-0041 / Ops — yalnız düzenlenmiş hata zarfının biçimi için.
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: PVG tarafı evet (sahip, 2026-10-21). Durum kodları kararlaştı; düzenlenmiş hata zarfının biçimi MOD-0041 / Ops onayını bekliyor.

### OD-PVG-4 — Arka uç dosya düzeni

- **Olgu (G-11):** paket Golden Reference `Features/CaseIntakeTriage/…` düzenini ve `*Handler` adlarını şart koşuyor;
  kod `Application/RegPvBase/*` altında tek bir uygulama servisi ve `CreateIntakeDraftCommand` gibi adlar kullanıyor.
- **Öneri:** bugünkü düzen slice 1 için belgelenmiş sapma (teknik borç) olarak kalsın; yeniden adlandırma yapılmasın;
  denetim yolu (DL-5) geldiğinde yeniden bakılsın.
- **Gerekçe:** servis MediatR kullanmıyor; dört komutun adını değiştirmek denetim defterinin borç listesine yeni ad
  ekler, oysa liste yalnız küçülebilir (AUD-001); iki test süiti bugünkü adları sabitliyor.
- **Etkisi:** DL-2 önkoşulu kapanır.
- **Ayrıca gereken onay:** yok.
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: Evet (sahip, 2026-10-21). Ek onay gerekmez.

### OD-PVG-5 — Açılışta eksik bağımlılık

- **Olgu (G-6):** üretim-benzeri ve yetkili ayarla, adaptörler kayıtlı değilken servis açılıyor, hazır kontrolü 200
  dönüyor ve iş uçları boş gövdeli 500 veriyor (PVG-M1-02 ölçümü B3, 2026-10-20).
- **Öneri:** üretim-benzeri ortamda uygulama servisinin bağımlılıklarından biri bile çözülemiyorsa servis açılmaz;
  her ortamda gerekli bir port ya da depo eksikse `/health/ready` 503 döner.
- **Gerekçe:** açılışta reddetmek en güçlü biçimdir ve bugünkü iki bayrak reddiyle tutarlıdır; hazır kontrolü
  üretim dışı ortamları da kapsar.
- **Etkisi:** BG-8 (DL-3).
- **Ayrıca gereken onay:** yok (açılış politikası paketin "Required Approval Gates" listesinde de durur).
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: Evet (sahip, 2026-10-21). Ek onay gerekmez.

### OD-PVG-6 — "Runtime yetkili" bayrağı

- **Olgu (G-7):** `Pvg:Runtime:OperationalRuntimeAuthorized` bir ortam değişkeni ya da komut satırı ayarıyla
  açılabiliyor; hiçbir onay kanıtına bağlı değil.
- **Öneri:** operasyonel runtime onay satırı onaylanana kadar bayrak yalnız testlerin içinde `true` olabilir;
  commit'li bir yapılandırma, başlatma profili, iş akışı ya da dağıtım dosyası bayrağı açarsa CI taraması kırmızı
  verir. Runtime ileride onaylanınca servis ayrıca o onay satırına bir referans ister.
- **Gerekçe:** bir bayrak onayı kanıtlayamaz; tarama bugün ucuz ve ölçülebilir.
- **Etkisi:** BG-9 ve BG-10 (DL-3).
- **Ayrıca gereken onay:** Enterprise Architecture (onay satırına bağlama biçimi için).
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: PVG tarafı evet (sahip, 2026-10-21). CI taraması kararlaştı; onay satırına bağlama biçimi Enterprise Architecture onayını bekliyor.

### OD-PVG-7 — Port

- **Olgu (G-13):** 5011 `AGENTS.md` §3 port tablosunda yok, "EA kararı bekliyor" listesinde (`AGENTS.md:85`);
  depodaki `.claude/launch.json` 5011'i Diten.Web `web-verify` için kullanıyor.
- **Öneri:** Enterprise Architecture 5011'i `AGENTS.md` §3'te `Diten.PvgService`'e tahsis eder; `web-verify`
  başka porta taşınır.
- **Gerekçe:** 5011 zaten `Program.cs`'te, dört Gateway route'unda, domain-config'te ve DCP-004'te; taşınacak tek şey
  yerel bir başlatma girdisi.
- **Etkisi:** paketin runtime kapısı satırı "Port allocated in AGENTS.md §3" ve "Required Approval Gates" port satırı.
- **Ayrıca gereken onay:** Enterprise Architecture (tahsis kararı; `AGENTS.md` değişikliği). Sizin "Evet"iniz
  yalnız PVG'nin bu talebi EA'ya göndermesine izin verir; `.claude/launch.json` değişikliği depo sahibinin (sizin)
  ayrı onayıdır.
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: PVG tarafı evet (sahip, 2026-10-21): PVG bu talebi Enterprise Architecture'a götürür. Port tahsisi EA kararıdır ve bekleniyor; `.claude/launch.json` değişikliği depo sahibinin ayrı onayıdır, bu işaret onu vermez.

### OD-PVG-8 — Liste veri modu

- **Olgu (G-14):** paket künyesinde `data_mode` yok; liste ucu sunucu tarafında sayfalıyor (`pageNumber`,
  `pageSize`); vaka kayıt sayısı sınırsız.
- **Öneri:** `data_mode: server`; künyeye bir sonraki paket revizyonunda, tabloya `data-dt-data-mode="server"`.
- **Gerekçe:** module-pack-standard §3 sınırsız varlık listesini `server` olarak tanımlar.
- **Etkisi:** BG-14 (DL-7); künye değişikliği bir sonraki paket revizyonunda yapılır.
- **Ayrıca gereken onay:** yok.
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: Evet (sahip, 2026-10-21). Künye değişikliği bir sonraki paket revizyonunda.

### OD-PVG-9 — Denetim kaydı yolu

- **Olgu (G-5):** dört yazma komutu GxP kaydıdır (AUD-001 K2: denetim kaydı yoksa işlem yok); bugün denetim yazımı
  yok. PVG servisinin denetim izi olmadığı için AUD-001 §5 K4 yeni yazma komutlarını ortak iletici paketi gelene kadar
  durdurur.
- **Öneri:** ortak iletici (K4, yol `b`) ve MOD-0021 sahip sözleşmesi beklenir, sonra DL-5 teslim edilir; o zamana
  kadar hiçbir MOD-0230 mutasyonu testler dışında başarılı olamaz (bugünkü "hepsini reddet" izin kapısı kalır).
- **Gerekçe:** yol `a` yalnız Platform içindir; MDM ileticisini kopyalamak AUD-001 §5'te yasak.
- **Etkisi:** BG-11 (DL-5) bu iki bağımlılık gelene kadar açık kalır.
- **Ayrıca gereken onay:** Control Tower (K4 ortak iletici paketi — PVG'nin değil altyapının işi), MOD-0021 sahibi.
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: PVG tarafı evet (sahip, 2026-10-21). Ortak denetim ileticisi (K4, Control Tower altyapı paketi) ve MOD-0021 sahip sözleşmesi bekleniyor; o zamana kadar hepsini reddeden izin kapısı kalır.

### OD-PVG-10 — Liste ekranı denetim aracının açıkları

- **Olgu (G-14):** `verify_datatable_page.py` bu ekranda 18 açık veriyor (2026-10-21, çıkış 1); bunların çoğu
  pakette kalıcı olarak dışlanmış özellikler: toplu silme, dışa aktarma, içe aktarma, tümünü seç ve bağlantıları.
- **Öneri:** dışlanmış özelliklere bağlı maddeler bu modül için "bilinen sapma" olarak kaydedilsin; kalan beş madde
  (veri modu, iskelet partial'ı, filtre partial'ı, form ViewModel'i, `_IndexL10n` partial'ı) DL-7'de düzeltilsin.
- **Gerekçe:** liste ekranı dokunma protokolü sahip yanıtı ister; dışlamalar pakette zaten sahip kararıdır.
- **Etkisi:** BG-14 (DL-7).
- **Ayrıca gereken onay:** yok.
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: Evet (sahip, 2026-10-21). Ek onay gerekmez.

## İşaretten sonra (Control Tower yapar, her adımı ayrıca sorar)

1. İşaretli kaydı commit etmek (`status: approved` yalnız işaretlenen satırlar için; açık satırlar açık kalır).
2. Paketi `module-pack-author` ile güncellemek: kararları "Open Decisions" bölümüne yazmak, OD-PVG-8 için künyeye
   `data_mode: server` eklemek.
3. "Ayrıca gereken onay" satırları için ilgili sahiplere soru taslağı hazırlamak (`ct_route.py`; gönderilmez).
4. İnceleme isteği 02'yi yeni paket sürümü ve karşılaştırma notunun §3 itirazlarıyla hazırlamak.
