# Canlıya geçiş — Toplantılar + Görev motoru + Auth etiket okuma (Eylül 2026)

> Kime: canlıya alacak ekip ve yönetici. Ne işe yarar: bu turun main'e birleştirme sırasını, canlıdan önce
> yapılması gereken ayarları ve canlıdan sonra koşulacak kısa kontrolü tek yerde toplar.
> Hazırlayan: CONTROL TOWER, 2026-09-14. Değerler ölçüldü; ölçülmeyen yerde "ölçülmedi" yazar.

## 0. Bu turda ne var

| PR (sırayla) | Dal | İçerik |
| :-- | :-- | :-- |
| 1 | `feature/infra/auth-display-label` | Kullanıcı kimliğinden görünen ad okuma ucu; CI kapısını 2026-08-30'dan beri kırmızı tutan iki eski testin düzeltmesi (BL-393) |
| 2 | `feature/pss/mod-0024-review-meeting-policy` | Görev motoru: kapanış alanları, dosya ekleri, çıktı dosyası zorunlu tür, atama kapsamı güvenliği, görev detayını yalnız ilgili kişinin açması (BL-349), görev türü eşzamanlılığı (BL-375), inceleme toplantısı politikası |
| 3 | `feature/mg/mod-0357-management-review-cadence` | Toplantılar (MOD-0357): kayıt, ekranlar, görev köprüsü, davet + kabul/ret, `.ics`, tutanak, devam toplantısı, türler, seri toplantı; Görev Merkezi davet kartı; e-posta tekrar denemesi düzeltmeleri (BL-374, BL-385) |

## 1. Birleştirme

- Sıra **1 → 2 → 3**. Her PR **normal birleştirme (merge commit)** ile alınır, squash ile değil: 3 numara 2'yi, 2 numara 1'i
  içerir; squash aynı işi iki kez gösterir ve yeniden çakıştırır.
- Her PR'da `phase1-gates` yeşil olmalı. Yerel ölçüm (2026-09-13, temiz checkout): üç dalda da kiracı 3/3, mimari 18/18,
  Web 137/159/159, "gates passed".
- ⚠ Kapıyı yerelde **ana checkout'ta koşmayın**: içindeki `.claude/worktrees/` kopyaları mimari testlerde sahte kırmızı
  üretir (BL-383). Ayrı bir worktree ya da temiz kopya kullanın.
- Main bu arada ilerlerse: main'i 1'e, 1'i 2'ye, 2'yi 3'e alıp kapıyı yeniden koşun.

## 2. Canlıdan önce: yedek

- Platform veritabanının ve Hangfire deposunun (`BackgroundJobs:StorageDatabaseName`) yedeği alınır.
- **Geri alma yerine ileri düzeltme.** Eski sürüm, yeni sürümün yazdığı alanları okurken çökebilir (BL-384: 2026-09-13'te
  dev'de `RecordLink.IdempotencyKey` ile yaşandı). Bu turla gelen bilinmeyen alan toleransı (`4663682c`) yalnız **bu sürümden
  sonraki** geri almaları korur; bugünkü canlı sürüm bu toleransa sahip değildir, ona geri dönüş yine çöker.

## 3. Arka plan işleri (Hangfire)

`appsettings.json` varsayılanı: `BackgroundJobs:Enabled = false`, `RegisterStandardJobs = true`, `EnabledJobs = {}`.
Kapalı kalırsa aşağıdaki "bu tur için gerekli" işler **hiç çalışmaz**; hata vermez, sessizce durur.

| İş kimliği | Zamanlama | Bu tur için |
| :-- | :-- | :-- |
| `Diten.Platform.MOD-0027.EmailDispatchJob` | her dakika | **Gerekli** — başarısız e-postanın tekrar denemesi |
| `Diten.Platform.MOD-0357.MeetingSeriesSweepJob` | saatte bir | **Gerekli** — seri toplantı üretimi |
| `Diten.Platform.MOD-0024.TaskRecurrenceSweepJob` | saatte bir | **Gerekli** — tekrarlayan görevler |
| `Diten.Platform.MOD-0024.TaskDueSoonSweepJob` | saatte bir | **Gerekli** — "süresi yaklaşıyor" bildirimi |
| `Diten.Platform.MOD-0023.WorkflowEscalationSweepJob` | 5 dakikada bir | Bu turun konusu değil |
| `Diten.Platform.MOD-0297.TrialExpiryScanJob` · `SubscriptionRenewalJob` | günlük | Bu turun konusu değil |
| `Diten.Platform.MOD-0033.QuotaResetJob` · `MOD-0018.EntitlementCacheRefreshJob` · `MOD-0034.WebhookRetryJob` · `MOD-0021.AuditLogArchiveJob` · `MOD-0009.ProvisioningRetryJob` | çeşitli | Bu turun konusu değil |

Açmak için: `BackgroundJobs:Enabled = true` ve her iş için `BackgroundJobs:EnabledJobs:<iş kimliği> = true`.
Kapatılan bir iş Hangfire'dan artık siliniyor (`RemoveIfExists`), yani bayrağı geri almak işi gerçekten durdurur.
Dashboard canlıda kapalı kalır.

## 4. E-posta

- Gönderim `TenantMessagingSettings` platform varsayılan satırından çözülür; satır başlangıçta `Smtp` bloğundan türetilir.
- SMTP sağlayıcısı **her gönderimde kimlik doğrular**. Sunucu AUTH sunmuyorsa her posta `ProviderUnknown` ile düşer ve
  toplantı yine oluşur (posta hatası kaydı geri almaz) — kutu boş kalır, ekran sağlıklı görünür.
- Canlıdan sonra bir gerçek davetle doğrulanır (bkz. §7).

## 5. İzinler

- Bu turda main'e yeni giren izin anahtarları (ölçüldü): `platform.meetings.series-manage`, `platform.tasks.read-all`.
  Diğer `platform.meetings.*` anahtarları main'de zaten var.
- `platform.tasks.read-all` **yalnız açıkça verilir**: hiçbir varsayılan role, SuperAdmin'e ya da modül yetkilendirmesiyle
  otomatik gitmez. Vermediğiniz sürece herkes yalnız ilişkili olduğu görevleri açar.
- Rollerin toplantı ve görev izinleri canlıda gözden geçirilir. Dev'de "Task-Manager" rolünün **hiç izni yoktu**; canlıdaki
  rollerin durumu ölçülmedi.

## 6. Organizasyon verisi

- Toplantı düzenleyen, katılımcı olan ya da başkasına görev atayan her kullanıcının **aktif bir pozisyonu** olmalı.
  Yoksa: katılımcı seçicisi boş, `MEETING_ORGANIZER_INVALID`, başkasına görev verilemez.
- Kiracının **aktif bir kök birimi** olmalı; yoksa görev açılamaz (`ORGANIZATION_UNIT_UNRESOLVED`). Yeni kiracı kurulumu bu
  birimi oluşturmuyor ve oluşturamaz: birim bir tüzel kişiliğe bağlı olmak zorunda (BL-366). Sıra: MDM'de tüzel kişilik oluştur ve
  aktifleştir → Organizasyon ekranından kök birim → pozisyonlar → atamalar.

## 7. Canlıdan sonra: 20 dakikalık kontrol

1. Giriş → Görev Merkezi açılır; "bazı kaynaklar yanıt vermedi" uyarısı yok.
2. Dosya ekiyle görev oluştur → görevi başlat → kapat: kapanış alanları görünür, değer görev detayında durur.
3. Görevden "İnceleme toplantısı planla" → toplantı oluşur, eylem "zaten planlandı" diye kapanır.
4. İkinci bir kullanıcıyla toplantı oluştur → davet postası `.ics` ekiyle gelir.
5. İkinci kullanıcı Görev Merkezi'nde daveti kabul eder → düzenleyen cevabı görür.
6. Toplantı saatini değiştir → güncelleme postası (aynı UID, daha yüksek SEQUENCE); iptal et → iptal postası.
7. Görevle ilgisi olmayan bir kullanıcı görevin adresini açar → "bulunamadı".
8. `/health`: dev'de `business_reference_data_provider` bu turdan önce de kırmızıydı; canlıdaki değeri ayrıca okunur.

## 8. Bilinen açıklar (bu turu engellemez)

| Kayıt | Konu |
| :-- | :-- |
| BL-386 · BL-389 · BL-390 · BL-391 | Düzeltildi, `feature/mg/mod-0357-ui-polish` (`d9dfec87`) dalında; bu tura katılıp katılmayacağı sahip kararı. Katılmazsa canlıda çıkarılan katılımcının takviminde toplantı kalır |
| BL-387 | Seri toplantıda düzenleyen de davet alıyor — sahip kararı |
| BL-388 | Görev alanı tanımında "Sıra" boşken kaydın düşmesi bu turda düzeltildi (`0d551337`); aynı ham hata metni 21 başka ekranda duruyor |
| @ ile etiketleme | `feature/pss/mod-0024-task-mentions` (`b9476a4e`) dalında hazır; bu tura katılıp katılmayacağı sahip kararı |
| BL-395 | Aynı makinede eşzamanlı Platform test koşuları paylaşılan test veritabanını silebilir; kırmızı tekrar koşuda kayboluyorsa kod hatası değildir |
| BL-392 | İş Raporu kiracı geneli okuma izni varsayılan rollere dağılabilir — sahip kararı |
| BL-347 | İş Raporu indirmesi denetim izi bırakmıyor |
| BL-393 notu | Tam Platform/Auth test paketleri ve ön yüz testleri CI'da koşmuyor; main'de önceden kırmızı olanlar: Doküman Yönetimi 15, İş Referans Verisi 53, Auth 3, ön yüz 25 |
