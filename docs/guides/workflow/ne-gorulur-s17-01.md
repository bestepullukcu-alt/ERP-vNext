# İş akışı: aynı anahtar ve silmeme — ekranda ne görülür (kılavuz değil, not)

> Kime: iş akışı konsolunu (Platform › İş akışı, Görevler) kullanan ve destek veren kişiler. Durum: **taslak**.
> Bu not, MOD-0023 §17 işinin (TRM-15, DL-1 ve DL-2) kullanıcı gözüne ne yansıdığını anlatır. Ekran **canlı denenmedi**; metinler koddan okundu.

## Neden kullanıcı kılavuzu yok

Kılavuz, kullanıcının yeni yapacağı bir işi öğretir. Bu işte **ekran değişmedi**: yeni sayfa, düğme, alan, menü ya da yetki yok; ön yüz dosyalarına dokunulmadı
(değişiklik yalnız `services/**` ve `tests/**` altındadır). Değişen, servisin aynı istek tekrarlandığında verdiği **yanıttır**. Bu yüzden `user-manual-generator` ile HTML kılavuz
üretilmedi; yerine bu kısa "ne görülür" notu yazıldı. Ekran değişirse (örneğin yeni hata kodu için ayrı metin eşlemesi) kılavuz ayrıca düşünülür.

Mühendis belgeleri: davranışın tamamı `docs/reference/architecture/api/mod-0023-workflow-idempotency.md`, karar gerekçesi
`docs/records/decisions/2026-11/mod0023-s17-same-key-replay-adr-01.md`.

## Ekranda aynı kalanlar

- Başlat, Onayla, Reddet, Bilgi iste, Devret, İptal diyalogları ve görev listeleri.
- **Anahtar alanı** her diyalog açılışında yeni bir GUID ile dolar, düzenlenebilir, yanında "yenile" düğmesi vardır; başarısız denemeden sonra diyalog açık kalır ve anahtar aynı kalır
  (`frontend/Diten.Web/wwwroot/assets/js/Platform/Workflow/index.js:1169-1180`, `tasks.js:602`; okuma).
- Daha önce işlenmiş bir istek aynen tekrarlanırsa gösterilen sarı uyarı: **"Already processed — idempotent response returned."**
  (`index.js:1043-1046`, `tasks.js:421-424`). Bu, hatalı bir şey olmadığını söyler; işlem bir kez yapılmıştır.
- Geçmiş ve listeler yeni alanları (`idempotencyKey`, `approvalTaskId`, `isDeleted`) **göstermez**; ön yüz bu alanları kullanmaz (ön yüz değişmedi).
- Silinmiş kayıtlar ekranda hiçbir yerde görünmez; kayıtlar zaten silinmez.

## Görebileceğiniz mesajlar

Konsol, 403/0/503/404 dışındaki hatalarda **sunucunun mesajını olduğu gibi** gösterir (`index.js:101-107`). Sunucu mesajları işleyicilerde İngilizce sabit metindir
(okuma: `WorkflowTaskTransitionSupport.cs`, `StartWorkflowInstanceHandler.cs`); Türkçe arayüzde de İngilizce görünür. Bu işte yeni çeviri anahtarı eklenmedi.

| Ne zaman | Konsolda görünen | Anlamı | Ne yapılır |
|---|---|---|---|
| Aynı anahtarla, ama **başka içerikle** görev eylemi (neden, açıklama, kanıt, imza, hedef değişmiş) | "The idempotency key is already bound to a different request body." (409, kod `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT`) | İlk gönderim **işlenmiş olabilir**; anahtar o içeriğe bağlı. | Önce görev listesini/geçmişi yenileyip eylemin kaydedilip kaydedilmediğine bakın. Kaydedilmediyse "yenile" ile yeni anahtar alın. Aynı anahtarı değiştirilmiş içerikle tekrar göndermeyin. |
| Aynı anahtarla, ama **başka olgularla** başlatma (şablon, nesne, aday, son tarih, neden değişmiş) ya da **başka kullanıcıyla** | "The idempotency key is already bound to different workflow start facts." (409, `WORKFLOW_INSTANCE_START_CONFLICT`) | Aynı anahtarla önce başka bir başlatma istenmiş. | Örneğin açılıp açılmadığına Örnekler listesinden bakın; açılmadıysa yeni anahtarla başlatın. |
| **İptal**'te başka kullanıcının aynı anahtarı | **"Permission missing or not assigned."** (`index.js:102`) | Gerçek neden "yetki yok" değildir: iptal tekrarı artık yalnız ilk iptali yapan kişiye aittir (403 `WORKFLOW_ACTOR_DENIED`). Konsol her 403'ü "yetki eksik" diye gösterir (tüketici tablosu 11a). | Yetkiyi değiştirmeyin; görevin zaten başka biri tarafından iptal edilip edilmediğine geçmişten bakın. |
| Başlatma tekrarında yarışta kaybedilmiş çağrı | "Workflow gateway proxy unavailable." (503) | Konsol her 503'ü bu metinle gösterir, sunucu mesajını göstermez. Gerçek mesaj: "…replay the same key to complete it." | Aynı anahtarla yeniden gönderin. |
| Daha önce işlenmiş istek aynen tekrar | Sarı "Already processed…" uyarısı | Sorun yok. | Bir şey yapmayın. |
| Zaten karara bağlanmış görev (başka kullanıcı/anahtar) | "Workflow task is not in a transitionable state." (409) ya da "Workflow task version is stale." (409) | Görev artık karar verilebilir değil. Bu mesajlar DL-2'de değişmedi. | Listeyi yenileyin. |

Task Center (WorkCenterNext) bu işte değişmedi. Orada kayıp yanıttan sonra farklı gerekçe/imza ile aynı öğeye yeniden karar verilirse yeni 409 kodu için ön yüzde eşleme yoktur: genel bir uyarı gösterilir ve öğe yeniden yüklenmez
(tüketici tablosu satır 7, çıkarım; **karar bekleyen bulgu**).

## Bilinen sınırlar

- Mesaj metni ve 403/503 yorumu için ayrı bir düzeltme yapılmadı; bu, tüketici tablosundaki "yalnız sözcük" kararıdır (bulgu 5) ve sahibe bırakılmıştır.
- Bu not ekranda denenmedi; kodu okuyarak yazıldı. Canlı doğrulama (konsolda aynı anahtarla iki istek) yapıldığında bu not o kayıtla güncellenmelidir.
