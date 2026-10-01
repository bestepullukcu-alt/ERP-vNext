# TALEP — REQ-WCN-01 · İddia onayları (MLR) için WorkCenterNext iyileştirmeleri

> **Kimden:** Commercial Suite / CRM İddialar v2 ekibi (CT).
> **Kime:** WorkCenterNext (Görev Merkezi) + MOD-0023 iş akışı projeksiyonunun sahibi olan geliştirici.
> **Tarih:** 2026-09-29.
> **Durum:** AÇIK — **bu dört maddeye CRM ekibi dokunmayacak.** Kod WorkCenterNext / Platform WorkAggregation tarafında.
> **Kanıt:** CL-E4-1 canlı uçtan uca test, 2026-09-29, tenant 97c5. Kayıt: `WP-CL-FE-4-country-version-form.md` §37-E4, 1. ve 2. bölüm.

---

## 1. Ne bağladık (bağlam)

CRM'deki **iddialar** (promosyon/medikal ifadeler) yayına girmeden önce üç aşamalı **MLR onayından** (Medikal → Hukuk → Ruhsat) geçiyor. Onay için ayrı bir ekran yazmadık; ortak altyapıyı kullanıyoruz:

| Parça | Nerede | Ne yapıyor |
|---|---|---|
| **Onay akışı** | Platform **MOD-0023 Workflow** | Şablonlar `CLAIM-CORE-MLR` (çekirdek) ve `CLAIM-LOCAL-MLR-{ÜLKE}` (TR, BY, UZ, TM, GE, AZ). Tek aşama `mlr`, 3 **sıralı** adım (`medical` → `legal` → `regulatory`). Adaylar **pozisyon** (`position:{guid}`): Medikal Direktör / Uzman, Hukuk Müşaviri / Uzmanı, Ruhsat Müdürü / Uzmanı. |
| **Başlatma** | CRM `ClaimReviewHandlers` | Kullanıcı "Onaya gönder" deyince CRM, kullanıcının token'ıyla `StartAsync` çağırıyor ve **DisplayContext** geçiyor: başlık `İddia onayı · CLM-ALMIBA-02 v1.0`, açıklama, modül `crm`, kaynak bağlantısı, etiketler. |
| **Görev Merkezi'nde gösterim** | Platform `WorkflowApprovalWorkItemProvider` | Bekleyen görevleri iş öğesi olarak projekte ediyor. CRM'in kendi çözücüsü olmadığı için başlık ve bağlantı **fallback çözücüden** (başlatırkenki DisplayContext anlık görüntüsü) geliyor. |
| **Eylem** | Platform `WorkflowApprovalWorkItemActionDispatcher` | "Onayla" / "Reddet" / "Devret" → MOD-0023 approve / reject / delegate. `reasonCode` boşsa `WORKCENTER_{EYLEM}` yazılıyor. **`comment` alanı zaten destekleniyor** (satır 72–93). |
| **Sonuç** | MOD-0023 → `platform.workflow.instance.completed.v1` → CRM tüketicisi | Son adım onaylanınca CRM iddiayı `approved` yapıyor. **Bu kısım canlıda sorunsuz çalıştı.** |

**Canlı sonuç:** Onay zinciri doğru çalışıyor. Çekirdek iddia ve TR ülke sürümü üç adımdan geçip onaylandı; SoD de doğru (gönderen kendi iddiasını onaylayamıyor). Aşağıdaki dört madde **iş akışını değil, onaylayanın deneyimini** ilgilendiriyor.

---

## 2. İhtiyaçlar

### W-1 · İş öğesi başlığı hangi adımda olunduğunu göstermiyor
- **Gözlem:** Aynı iddia için üç adımın üç iş öğesi de aynı başlıkla geliyor: "İddia onayı · CLM-ALMIBA-02 v1.0". Onaylayan, Medikal mi Hukuk mu Ruhsat mı onayladığını göremiyor. Tek kişi üç pozisyonu birden tutabildiği için bu önemli; canlıda sema üç adımı da onayladı.
- **Neden:** Başlık, örnek (instance) başına bir kez verilen DisplayContext'ten geliyor ve adım bilgisini içermiyor.
- **İstek:** İş öğesinde **adımın görünen adı** gösterilsin (şablondaki adım adı, ör. "Medikal inceleme"). Başlığa ek ("… · Medikal inceleme") ya da ayrı bir rozet / alt satır olabilir; biçim WCN'nin kararı.
- **Kabul:** Bir örneğin ardışık adımları listede ve detayda ayırt edilebiliyor. Başka modüllerin iş öğeleri bozulmuyor.

### W-2 · Onaylarken yorum yazılamıyor
- **Gözlem:** "Onayla" onay penceresinde yalnız başlık ve "Evet, onayla" var. MLR'de onaylayanın notu ("Hukuk: 'destekler' ifadesi uygun" gibi) denetim kaydının parçası; şu an hiçbir not toplanamıyor. History'de yalnız `actionReasonCode=WORKCENTER_APPROVE` var.
- **Neden:** Dispatcher `payload.Comment`'i MOD-0023'e iletiyor, ama arayüzdeki onay penceresi yorum alanı sunmuyor.
- **İstek:**
  - Onayla / Reddet / Devret pencerelerine **yorum alanı** eklensin. Onayda isteğe bağlı olsun; **Reddet'te zorunlu** olmasını öneriyoruz.
  - Yorum `payload.comment` olarak gönderilsin.
  - WCN detayındaki Etkinlik akışında görünsün.
- **Kabul:** MOD-0023 `GET api/v1/workflow/instances/{id}/history` yanıtında adımın yorumu görünüyor (CRM onay geçmişinde gösterecek).
- **Opsiyonel:** Şablon başına "onay yorumu zorunlu" ayarı. MLR için ileride isteyebiliriz; şimdilik gerekli değil.

### W-3 · Detayda "Atanan: Atanmamış" görünüyor
- **Gözlem:** Görev detayında "Atanan: Atanmamış" yazıyor. Görev bir kişiye değil **pozisyona** atanmış (Medikal Direktör / Medikal Uzman), ve bu doğru bir durum. Ama kullanıcı "kimse sorumlu değil" diye okuyor.
- **Neden:** `app.js:3113` → `item.assignee || t('SummaryUnassigned')`. Projeksiyon, pozisyon adaylarını `assignee`'ye çevirmiyor.
- **İstek:** Doğrudan atanan kişi yoksa **aday pozisyonların adları** gösterilsin ("Medikal Direktör, Medikal Uzman"). Havuz mantığı sürüyorsa "Havuzda · Medikal Direktör, …" gibi bir ifade de olur. Pozisyon adları Organizasyon'dan (pozisyon ana verisi) çözülmeli.
- **Kabul:** Pozisyona atanmış adımda pozisyon adları görünüyor; kişiye atanmışta kişi adı eskisi gibi.

### W-4 · Detay sayfasından onaylayınca "İstenen iş öğesi bulunamadı" çıkıyor
- **Gözlem:** `/WorkCenterNext/Details/{id}` sayfasında "Onayla" → başarı bildirimi geliyor, ama sayfa hemen boş duruma düşüyor: "İstenen iş öğesi bulunamadı. [Görev Merkezi'ne dön]". Kullanıcı bir hata olduğunu sanıyor.
- **Neden:** Tamamlanan görev projeksiyondan düşüyor; detay yeniden yüklenince `DetailItemNotFound` gösteriliyor (`app.js` ~7080).
- **İstek:** Başarılı bir eylemden sonra **listeye dönülsün** (bildirim korunarak). Sıradaki adım aynı kullanıcıya düşüyorsa doğrudan o iş öğesi açılabilir; bu isteğe bağlı. Ya da detay "Tamamlandı" durumunu göstersin; ama "bulunamadı" görünmesin.
- **Kabul:** Onay / red / devir sonrası kullanıcı hata benzeri bir ekran görmüyor.

---

## 3. Bilgi amaçlı (talep değil)
- **Başlık dili:** DisplayContext başlığı CRM tarafında Türkçe kuruluyor ("İddia onayı · …") ve örnek başına anlık görüntü olarak saklanıyor. Görev Merkezi'nin çok dilli olması gerekiyorsa ileride bir **başlık anahtarı + parametreler** sözleşmesi konuşulabilir. Şimdilik talep etmiyoruz.
- **Kaynak bağlantısı:** "Kaynak kaydını aç" bağlantısının 404 vermesi **bizim hatamız**; CRM yanlış adres veriyordu. **CL-FIX-1**'de düzeltiyoruz; WCN tarafında iş yok. Yalnız, düzeltmeden önce başlatılmış açık örnekler eski bağlantıyı taşımaya devam eder.
- **Bağımsız, önceden var olan konu:** WCN'de "3 iş öğesi sözleşme hatası" uyarısı (CAPABILITY_CONTAINER_REQUIRED fikstürleri) ve eksik bir dev-reference resx anahtarı görüldü. İddialarla ilgisi yok; bilginize.

## 4. Yeniden üretme
1. Admin User ile bir çekirdek iddiayı (≥1 kanıt) "Onaya gönder" → `CLAIM-CORE-MLR` örneği başlar.
2. Üç MLR pozisyonunu tutan kullanıcıyla (97c5'te **sema pullukcu**) `/WorkCenterNext`:
   - W-1: üç adımın başlığı aynı;
   - W-3: detayda "Atanmamış";
   - W-2: "Onayla" penceresinde yorum yok;
   - W-4: detaydan onaylayınca "bulunamadı".

---

## 5. Görev Merkezi / Platform cevabı (2026-10-01) — iş paketi `WP-WCN-APPROVAL-UX-01`
| # | Cevap | Not |
|---|---|---|
| W-1 Adım adı | **KABUL** | Adımın görünen adı **ayrı rozet** olarak (şablondaki ad). Başlık DisplayContext'teki haliyle kalır. |
| W-2 Yorum | **KISMEN VAR + KABUL** | Ret bugün de zorunlu gerekçe soruyor. Adımda `commentRequired` işaretliyse onay da zorunlu yorum soruyor. **Eksik olan:** zorunlu değilken isteğe bağlı not alanı → Onayla penceresine eklenecek. Yorum `GET instances/{id}/history` → `comment` alanında. **Kapsam dışı:** Görev Merkezi Etkinlik akışında görünmesi. |
| W-3 Atanmamış | **KABUL** | Kişi atanmamışsa "Onay bekleyen: Medikal Direktör, Medikal Uzman". "Havuzda" ifadesi kullanılmayacak. |
| W-4 Bulunamadı | **KABUL** | Başarılı karardan sonra listeye dönüş, bildirim korunur. Sıradaki adımın otomatik açılması yok. |
| Bilgi | — | "3 iş öğesi sözleşme hatası" onlarda; düzeltme main'i bekliyor. Başlık dili ve kaynak bağlantısı: onlarda iş yok. |

**Bizim tarafa etkisi:**
- Bilgi Yolu (KP-2) kendi inceleyici görünümünde yorumu ve adım adını history ucundan gösteriyor, uyumlu.
- MLR şablonlarında (`CLAIM-*`, `KP-MLR-*`) `commentRequired: false` kalır. Ret yorumu motor + CRM'de zaten zorunlu.

**Onlardan bize karar sorusu:** "Aynı kişi aynı kayıtta birden fazla MLR adımını onaylayamaz" kuralı gerekli mi? Gerekliyse şablon bazında açılabilir seçenek olarak motora ayrı iş yazacaklar.
- **CT önerisi:** şablon bazında seçenek olarak **iste**, varsayılan **kapalı**. Bizim MLR şablonlarımızda şimdilik **kapalı** kalır.
- **Gerekçe:** organizasyon kararı (2026-09-28): bir kişi birden fazla MLR fonksiyonunu üstlenebilir; canlıda üç pozisyon tek kişide (sema). Ekip büyüyünce (ayrı Medikal / Hukuk / Ruhsat kişileri) `KP-MLR-*` ve `CLAIM-*` şablonlarında açılır.
- **Karar:** kullanıcı onayı bekleniyor.
