# WorkCenter ("Görev Merkezi") — Frontend Rebuild Spec (v2)

> **Faz:** YALNIZCA frontend rebuild (mock-driven). Backend (MOD-0024 aggregation) YOK — frontend kesinleşip kontrat çıkınca gelir.
> **İzolasyon:** Yeni `/WorkCenterNext` route + yeni dosyalar. Mevcut `/WorkCenter` HİÇ bozulmaz; hazır olunca swap.
> **Kaynaklar:** kod-taraması + domain araştırması (Fiori My Inbox / ServiceNow My Work / SAP Task Center) + blueprint (124 modülün "My Work/Approvals Inbox"unu birleştiren kişisel iş ön-kapısı) + 3 harici LLM mimari incelemesi (2 tur, tam konverjans).
> **v2 revizyon tarihi:** 2026-07-14. **v1'den fark:** evrensel kabul-kapısı → `assignmentMode`-driven; evrensel lifecycle → capability-based ince overlay; +claim/inquire/snooze/outbox/blocked-signal/scoped-delegation; kontrat zenginleştirildi.

## 1. Arketip (KİLİTLİ)
Görev Merkezi = **cross-modül kişisel aksiyon yüzeyi** — tüm modüllerden gelen **approval + task + review + issue + exception**'ın *kullanıcının* önüne geldiği, **basit ve sık aksiyonların yerinde bittiği** yer. **Proje aracı DEĞİL** (Gantt/ağır dependency yok). Karmaşık işler kaynak modülde biter (bkz. §5 N-alan kuralı). PPM (MOD-0117) ve Cockpit (üst-yönetim gözetimi) AYRI, tamamlayıcı merkezlerdir.

**İsim:** kullanıcı-etiketi **"Görev Merkezi"** (≈ SAP Task Center) — ERP'deki "İş Merkezi / Work Center = üretim kapasite birimi" (PP modülü) ile çakışmaz. Kod-adı `WorkCenterNext` kalır; ileride PP-türü bir modül gelirse iç isim çakışması için ADR açılır.

## 2. Çekirdek ilke — SOURCE-AGNOSTIC / PPM-hazır
Her work-item **kaynaktan bağımsız** bir zarf taşır. Görev Merkezi **hiçbir modüle özel mantık gömmez.** Bugün mock; yarın PPM/Finance/MDM… hepsi **aynı kontratla** besler (WC-1 + WC-5 seam'leri mock'ta baştan yaşar). **Rebuild'in kalbi budur.**

**WorkCenter durumu SAHİPLENMEZ.** Gerçek workflow/status/time/dependency **kaynakta** yaşar. WorkCenter yalnızca **ince kişisel katman** tutar: pin, snooze, kişisel plan-tarihi, seen/unseen, kişisel not, hatırlatma. "Tamamla" bir WorkCenter state-geçişi değil, kaynağa **komut**tur; sonucu kaynak belirler.

## 3. Work-item kontratı (mock shape — WC-1 tohumu)
> **DİKKAT:** Bu şema backend fazının girdisidir; "geçici" diye savruk yazma. Aşağıdaki alanların çoğunun *çözümü* backend'e ait (§B), ama *temsili* mock'ta baştan olmalı.

```
// Kimlik + köken
id, sourceModule, sourceType, sourceId, deepLink,
tenantId,                                            // (mock: sabit; backend: gerçek)
sourceVersion, etag,                                 // freshness/concurrency (mock: temsili; backend: çözüm)

// Sınıflandırma — iki eksen (UI intent-driven; obje-tipi metadata)
workIntent,                                          // approval|task|review|issue|exception (kullanıcı NE yapacak)
sourceObjectType,                                    // invoice|vendor|deviation|wbs-task… (neyin üstünde)

// Atama modeli (KABUL DAVRANIŞINI BELİRLER — §4)
assignmentMode,                                      // direct|approval|groupQueue|offered
assignee, group, requester, viewerRole,             // Owner|Approver|Reviewer|Creator
onBehalfOf,                                          // vekâlet: kimin adına (null=kendi)

// Yetenekler (LIFECYCLE'I BELİRLER — §5)
capabilities[],                                      // pack: Planning|Execution|TimeTracking|Review|InformationRequest|Checklist|Attachments
actionDepth,                                         // inline | deeplink  (N-alan kuralının sonucu)

// Durum & zaman
status,                                              // NORMALIZE küçük set (aşağıda) — kaynağın native'i detayda
slaState, sourceDueDate,                             // overdue|due-soon|on-track|no-sla  (KAYNAK deadline)
plannedDate,                                         // KİŞİSEL plan (kaynağınkiyle çelişince uyarı)
priority,                                            // high|medium|low
blockedState: { blocked, blockedBy[], reason },     // KAYNAK-hesaplı; UI aksiyonu disable eder

// Aksiyon güvenliği (her aksiyon için)
actions[]: { code, label, semanticType,             // approve|reject|claim|inquire|complete…
             requiresConfirmation, requiresReason,
             riskLevel, supportsBulk, idempotencyKey },

// Kişisel katman (WorkCenter'ın SAHİP olduğu tek yer)
personal: { pinned, snoozedUntil, seen, note },
flags[]                                              // Blocked|Waiting|Review|Approval|Checklist (label+kind)
```
**Normalize status seti:** `Pending` · `In Progress` · `Waiting` · `Done` · `Cancelled`.
**Sistem durumları (mock temsili, backend çözer):** "record changed" · "your authority ended" · "source unreachable" · "in progress".

## 4. Bilgi mimarisi (KİLİTLİ — v3, 3-LLM konverjansı 2026-07-14)
> **TEMEL YASA — eksen başına tek mekanizma (Fable/ChatGPT/Gemini ortak):**
> **SAHİPLİK → sekme · DURUM → segment (tab-içi) · TİP → filtre çipi.** Üç ekseni tek seviyeye
> koymak (eski 4-düz-sekme) "işim nerede?"yi belirsizleştirir. Her eksen kendi UI mekanizmasına oturur.

### Sekmeler = SAHİPLİK (birincil + ikincil)
- **Gelen Kutusu** (birincil, varsayılan) — sana gelen, henüz üstlenmediğin: atanan görevler (sert kabul kapısı) + onaylar (yerinde karar, en üstte ayrı bant). "Karar/kabul modu."
- **İşlerim** (birincil) — üstlendiğin, aktif çalıştığın. "Yürütme modu."
- **Havuz** (ikincil, sayaçlı — "0" bile göster ki keşfedilsin) — sahipsiz grup/teklif işi (claim/release).
- **Geçmiş** (ikincil, salt-okunur, son 90 gün) — Tamamladıklarım/Onayladıklarım/Devrettiklerim.
- Sağ üst: vekâlet seçici (N-yönlü, "X adına" bandı) · (Bugün = İşlerim'in zaman-kesiti → v1.5).

**FABLE YASASI (kritik):** *Sekme YALNIZCA sahiplik değişince değişir* — kabul→İşlerim, tamamla→Geçmiş, bırak→Havuz. **Durum değişimi (snooze/inquire/blocked) öğeyi sekmede TUTAR** (segment değişir, sekme değil). Öğelerin sekmeler arası "ışınlanması" güven kaybettirir. *(v2'deki "Havuz & Bekleyen" birleşik sekmesi bu yüzden KALDIRILDI: Bekleyen artık İşlerim içinde segment.)*

### Segmentler = DURUM (tab-içi, ≤3 — segment obezitesine hayır)
- **Gelen Kutusu:** Tümü · Kabul bekleyen · Karar bekleyen (onaylar üst bant).
- **İşlerim:** **Aktif · Bekleyen · Planlı.** (Bloke/Snooze/Vekâlet = filtre, segment DEĞİL.)
- **Havuz:** Uygun · Üzerime aldıklarım. **Geçmiş:** zaman gruplaması.

### Çipler = TİP + SİNYAL (her konteynerde aynı, sayaçlı, çoklu-seçim, URL'e yazılır)
- **Tip çipleri:** Onay · Görev · İnceleme · Sorun · İstisna.
- **Sinyal çipleri (AYRI):** Bloke · SLA-riski · Eskale. *(ChatGPT: "exception sinyal" — v1'de tip kalır, ama sinyaller ayrı çip ekseni.)*
- **Gelişmiş filtre paneli:** kaynak modül · öncelik · atama modu · vekâlet · sabitli. Kaydedilmiş görünümler = filtre kombinasyonu (v1.5).

### KABUL davranışı = `assignmentMode` (sahiplik geçişini belirler)
| assignmentMode | Nerede | Kabul davranışı | Sekme geçişi |
|---|---|---|---|
| `direct` | Gelen Kutusu | **Kabul et (üstlen)** → İşlerim (sert kapı; kabule kadar Başlat yok) | kabul → İşlerim |
| `approval` | Gelen Kutusu (üst bant) | **Kapısız yerinde karar:** Onayla/Reddet/Bilgi-iste/Geri-gönder | karar → Geçmiş |
| `groupQueue` | Havuz | **Üzerime Al (Claim)** / **Bırak (Release)** | claim → İşlerim · release → Havuz |
| `offered` | Havuz | **Kabul / Reddet** | kabul → İşlerim |

## 5. Lifecycle + DERİNLİK — capability-based (KİLİTLİ — v3)
Evrensel Plan→Start→Timer→Complete→Review KALDIRILDI. Item `capabilities[]` beyan eder; UI **sadece geçerli adımı/bloğu** çizer. **Eskinin derin görev ekranı (subtask/dependency/checklist/time/activity) geri gelir — ama capability-koşullu bloklar olarak tek detay şablonunda** (3-LLM ortak kararı).

**Capability sözlüğü (kaynak beyan eder):**
`stages` (kaynağın step-bar'ı — evrensel DEĞİL) · `planning` (kişisel plan-tarihi) · `execution` (başlat/duraklat) · `timeTracking` (timer+manuel süre, sahiplik kaynakta) · `checklist` (X/Y, etkileşimli) · `subtasks:full|readonly` · `dependencies` (tipli FS/FF/SS/SF, salt-okunur) · `activity` (yorum, kaynakla TEK akış) · `attachments` · `reviewFlow` · `informationRequest`.

**DERİNLİK KURALI (Fable, N-alan kuralının genellemesi):** *"işi YÜRÜT" aggregator'da, "işi TANIMLA" kaynakta.*
- **Aggregator'da (yerinde):** checklist işaretle · süre gir · yorum yaz · ek ekle · kişisel plan-tarihi · basit/tek-seviye subtask tamamla.
- **Kaynağa deep-link:** WBS/hiyerarşi düzenle · dependency graph oluştur/değiştir · Gantt/çizelge · çok-satırlı muhasebe · sözleşme redline · büyük/modüle-özgü form.
- **Tipli dependency:** salt-okunur gösterilir **+ `blockedState`'i besler** (FS+öncül bitmemiş → hard-blocked/aksiyon kilitli; SS+öncül başlamış → ready). Editör kaynakta.
- **subtasks:** `full` beyan eden kaynak (basit görev modülleri) → aggregator'da CRUD; WBS'i olan kaynak → yalnız `readonly` (ilerleme + link).

### Tek detay şablonu (split-detail sağ panel = tam ekranın aynısı)
1. **Başlık bandı:** tip rozeti · başlık · kaynak modül + deep-link · SLA · blocked rozeti (tipli "FS: X bitmeden başlayamaz") · sahiplik/"X adına".
2. **Step-bar:** yalnız `stages` capability varsa (kaynağın aşamaları).
3. **Aksiyon barı:** capability + durum + assignmentMode'dan türetilir (tek satır, koşullu).
4. **Özet + bağlam:** açıklama · requester · kaynak alanları (readonly) · kişisel plan-tarihi.
5. **Yürütme blokları (capability-koşullu):** checklist · subtask · time-entry · ekler.
6. **Bağımlılık + engel:** tipli dependency listesi (readonly) · blocked nedeni · önceki/sonraki.
7. **Activity + audit:** yorum (kaynakla tek akış) · denetim izi ("X adına" damgaları).
8. **Kişisel katman şeridi:** pin · snooze · plan-tarihi (çelişki uyarısı) · kişisel not.

### note / meeting — TİP DEĞİL (3-LLM ortak)
Bir öğenin tip olması için: **atanabilir + durum makinesi + aksiyon barı** taşıması şart. note/meeting üçünü de geçemez.
- **meeting** = iş üreten bağlam → "Bugün" ajandasında zaman bloğu (item değil). Aksiyon gerekiyorsa (RSVP/tutanak-onayı/follow-up) normal `task`/`review` üretir, `sourceModule: meeting`.
- **note** = (a) öğeye iliştirilmiş → kişisel katmanın parçası (`personal.note`); (b) bağımsız → kapsam dışı ya da "kendine görev". "Oku-onayla" gerekiyorsa `acknowledgment task` üretir.

**Eski §5 (3 derinlik seviyesi) — bu bölüme dahil edildi; aşağıdaki referans korunur:**

**3 derinlik seviyesi (ChatGPT):**
- **L1 — Hızlı aksiyon:** Onayla/Reddet/Claim/Tamamla/Bilgi-iste. WorkCenter kapsar.
- **L2 — Zengin gömülü:** plan-tarihi, checklist, süre, yorum, ek, basit form, incelemeye-gönder. Standart bileşenlerle WorkCenter kapsar.
- **L3 — Kaynak ekranı:** WBS düzenleme, çok-satırlı muhasebe dağıtımı, sözleşme redline, kapasite planlama. Split-detail sağ-üstte devasa **"Kaynağında Aç"** butonu → tek tıkla kaynağa zıpla.

**N-alan kuralı (Fable — ölçülebilir sınır):** aksiyonun tamamlanması **≤ N alan** girdi gerektiriyorsa `actionDepth=inline` (yerinde biter); aşıyorsa `actionDepth=deeplink` + **"dönünce durumu tazele"**. Bazı modüller her zaman L3'e ihtiyaç duyar — bu başarısızlık DEĞİL; başarısızlık deep-link dönüşünde durumu tazeleyememektir. **Mock turunda ikisini de test et:** bir "yerinde biten" akış + bir "N'i aşan, deep-link'li" akış.

**Bağımlılık:** graph'ı yönetmez; kaynağın hesapladığı `blockedState` sinyalini alır → bloke satır grayed-out, aksiyon disabled, "Bloke eden görevler" popover.

## 6. Tip-özel aksiyonlar
| workIntent | L1 aksiyonlar |
|---|---|
| approval | Onayla · Reddet · Bilgi İste · Geri Gönder · Delegate |
| task | (mode'a göre kabul) · Planla · Başlat · Tamamla · Yeniden ata |
| review | Onayla(Sign-off) · İade |
| issue | Üstlen · Çöz |
| exception | Acknowledge · Aksiyon al |
(meeting · note → v1.5). Her aksiyon `requiresConfirmation`/`requiresReason`'a göre teyit/neden yakalar; her karar audit-stamp'lenir (ileride audit altyapısına bağlanır).

**Bulk (mock'ta şimdi tasarla):** tablo çoklu-seçim → footer bulk barı → **ilerleme çubuğu → kısmi-başarısızlık** ("200 seçildi · 187 başarılı · 13 hata", hatalı satırlar kırmızı). Idempotency/retry = backend; **UI deseni mock'ta.**

**Vekâlet:** binary toggle değil — **N-yönlü kişi/grant seçici**; birleşik görünümde her satırda **"X adına" rozeti**; aksiyon anında **"X adına onayla" dialogu**; ekranı çevreleyen **amber "Vekalet Modu" bandı**; **kendi-acil rozeti hep görünür.**

## 7. Ortak görünümler (v1)
- **List (VARSAYILAN)** — SLA-state gruplu (Overdue/Due-soon/On-track/No-date), sticky başlık + canlı sayaç, satır: title + 4-6 chip, hover quick-action.
- **Split-detail** — approval inbox'ın kalbi: kaynak bağlamı + tip-özel aksiyon barı + audit izi + "Kaynağında Aç". Klavye j/k gez, a/r onayla/reddet **(teyitli)**.
- **Table** — sıralanabilir kolon, multi-select, footer bulk-action barı. **Klavye tabloda da çalışır** (v1 a11y şartı).
- **Focus / Today** — overdue + due-today + pinned.
- **Snooze / follow-up / pin** — kişisel katman etkileşimleri.

### v1.5
Kanban · saved views · ⌘K · meeting/note tipleri · team scope · notes/@mentions · density · multi-sort · tam outbox (arama/filtre/recall/rapor) · attention-score (açıklanabilir).

### ASLA (over-engineering)
Gantt/Timeline · WorkCenter'ın timer/status/dependency SAHİPLENMESİ · serbest inline-edit (kaynak otoriter) · policy-deadline'da drag-reschedule · L3 işi WorkCenter içinde render.

## 8. Backend/kontrat fazına ertelenen (B) — mock'ta yalnız TEMSİL
tenant/global id · version/etag/concurrency · idempotency · reconciliation/retry · privacy/redaction/search-index · provider certification + kontrat versiyonlama · SLA hesap + escalation ownership · delegation gerçek yetki kapsamı · action schema/eligibility · resmî time-service · audit correlation/retention · kaynak-down işlem modeli · priority kalibrasyon algoritması. **Frontend mock bunların teknik çözümünü YAPMAZ**; yalnız kullanıcı durumlarını temsil eder ("kayıt değişti", "yetkiniz sona erdi", "kaynak erişilemiyor", "işlem sürüyor").

## 8b. GAP listesi (2026-07-14 tam tarama — rebuild kapsamı)
🟢 **Faz 2 (derinlik, "işi yürüt"):** checklist UI · subtask UI (full/readonly) · tipli dependency listesi · time-entry/timer · yorum yazma (activity tek akış) · ekler · kişisel not.
🟡 **Faz 3 (genişletilmiş):** Kanban (salt-okunur, kolon=durum, sürükleme sadece kişisel plan/pin) · Takvim (salt-okunur deadline kümelenmesi) · Gelen Kutusu'nda **onaylar üst bandı** · gelişmiş filtreler (öncelik/tarih/atama-modu) · **N-yönlü vekâlet** (+ birleşik "Tümü") · **havuz grup seçici** · **stale/sistem durumları** ("kayıt değişti"/"kaynak erişilemez"/"yetkin bitti") · **"+ Yeni"** (① self-task inline · ② kaynakta-oluştur launcher — WorkCenter modül formu KURMAZ).
🔵 **Ertelenmiş (v1.5/v2/backend):** saved views · ⌘K · Bugün'de toplantı ajandası · bildirim çanı/push (WC-4 backend) · density/multi-sort · reconciliation paneli. Gantt = **ASLA**.

## 9. İzolasyon (KESİN)
Yeni route `/WorkCenterNext`, yeni Views/`WorkCenterNext/` + js/`WorkCenterNext/`. Eski `/WorkCenter` **HİÇ değişmez**. Mock-driven; **sıfır backend/API çağrısı.**

## 10. "Bitti" tanımı (bu faz)
İşlerim/Havuz/Geçmiş sekmeleri (mode-driven) · assignmentMode'a göre kabul akışları (direct/approval/groupQueue/offered) · capability-based aksiyon barı (inline vs deeplink iki mock akış) · blocked-signal · vekâlet (banner+rozet+dialog) · onayda teyit + geri-al · bilgi-iste/geri-gönder + waiting-on · snooze/pin/plan-tarihi (sourceDueDate çelişki uyarısı) · bulk kısmi-başarısızlık UX · minimal outbox · List+Split+Table+Focus · normalize SLA/status + **7-dil** etiket · empty/loading/stale states · klavye loop (tabloda dahil). **Sonraki faz:** bu frontend'den kontratı çıkar → MOD-0024 backend.
