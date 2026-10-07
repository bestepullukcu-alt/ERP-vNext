# WORK PACKAGE — WP-VP-4E · Coğrafyaya duyarlı gün ataması + temsilcinin gün sabitlemesi (backend)

> **CT (SoR), 2026-10-08.**
> - **Kullanıcı:** "Dr. X'i Salı yerine Perşembe" taşıması için "evet ekleyelim, ikisi de olabilir (Haftalar + Rota) ama rota normalde o hafta için en iyi planı oluşturması gerekmez miydi" (2026-10-08).
> - **Kapsam:** yalnız CRM (motor + oturum seçimi). Ekran: Haftalar'da sürükle **4D**, Rota'da günler arası taşıma **4F**.
> - **Paralel:** WP-VP-4C (Web Hedefler) ile paralel olabilir (dosya çakışması yok).
>
> **Çalışma yeri:** worktree `C:\tmp\vp-4e`, dal `wp/vp-4e`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Bugün (CT kod okuması)
- `DayBalancer` (3B, `Application/Features/VisitPlanning/DayBalancer.cs`) kurum gruplarını büyükten küçüğe **en düşük doluluklu** güne koyuyor. **Konum hesaba katılmıyor.**
- Rota iyileştirici (`TimeWindowInsertionEngine`) **gün başına** çağrılıyor; gün içinde sıra ve saat üretiyor.
- Elle sıra (`ManualVisitOrder`, 3A'da hafta başına) yalnız gün içi sırayı belirliyor, **günü değiştirmiyor** (3B kuralı).
- Hesapların koordinatı var (`Latitude`, `Longitude`; konumsuz → `missing_location`).

## NE
### 1. Coğrafyaya duyarlı gün ataması (sistem önerisi)
- Gün dengeleyici kurum gruplarını günlere **coğrafi kümelere göre** dağıtır. Kısıtlar 3B'deki gibi korunur: günlük bütçe, yarım gün, tatil / hafta sonu, aynı kurum aynı gün.
- Önerilen yöntem (saf, deterministik, test edilebilir):
  - **(a)** haftanın çalışma günü sayısı kadar küme tohumu seç: birbirinden en uzak grup merkezleri (deterministik: en büyük grup + en uzak).
  - **(b)** grupları büyükten küçüğe, **bütçeye sığan** günler arasından merkezine en yakın güne ata (eşitlikte düşük doluluk).
  - **(c)** her atamada gün merkezini yük ağırlıklı güncelle.
  - **(d)** sığmayan → 3B taşma kuralı.
  - Yöntemin kendisi ajanın; ölçütler aşağıda.
- **Ölçüt:** aynı veride haftalık **toplam yol süresi** (rota iyileştiricinin verdiği) 3B'ye göre azalır; gün yük farkı 3B'nin sınırını aşmaz. Test verisinde iki uzak küme → iki ayrı gün.
- Konumsuz grup: konumlu kümeye bağlanmaz, yükü en düşük güne (3B davranışı).
- Doktorun müsaitlik penceresi (varsa) bugünkü gibi rota adımında; uyumsuzsa 3B'deki "diğer günleri dene" kuralı.

### 2. Gün sabitlemesi (temsilcinin düzeltmesi)
- **Plan alanı:** hafta başına `DayPins[]` `{ targetType, targetId, contactId?, date, scope }`. Mevcut hafta / seçim yapısına ek; class-map.
- **`scope` (kullanıcı sorusu 2026-10-08: "hastaneye bağlı eczaneler de birlikte taşınıyor mu?"):**
  - `visit`: yalnız o ziyaret (tek doktor ya da tek eczane);
  - `institution`: o haftadaki **kurum grubu** — kurumun bütün doktorları + **bağlı eczaneleri**. Grup kuralı `DayBalancer`'ın kurum grubu kuralının **aynısı** (3B: hesap ilişkisiyle kuruma bağlı eczane); tek yerde tanımlı, motor çözer, istemci grubu hesaplamaz.
  - Grup sabiti grubun bütün üyelerini o güne koyar.
  - Aynı hafta içinde bir üyenin ayrıca `visit` sabiti varsa **`visit` kazanır** (temsilci grubu taşıyıp sonra bir doktoru ayırabilir).
- **Yazma yolu: yeni komut YOK.** Mevcut oturum güncellemesi (`PUT …/sessions/{id}`) `dayPins` alır:
  - `{ weekStart, pins[] }` → o haftanın sabitleri değişir;
  - null = koru; boş liste = o haftanın sabitlerini temizle.
  - **Mimari listesiz sayı 27 sabit.**
- **Kurallar:**
  - yalnız **taslak** hafta (onaylı → 409 `week_already_approved`; geçmiş → 409 `week_in_past`);
  - `date` o haftanın içinde ve **çalışma günü** (tatil / hafta sonu → 400 `pin_not_working_day`);
  - hedef o haftanın ziyaretlerinden biri olmalı (değilse sabit yok sayılır + uyarı `pin_target_not_in_week`).
- **Motor:**
  - sabitlenmiş ziyaretler önce kendi günlerine konur;
  - kalan gruplar madde 1 ile sabitlerin etrafına dağıtılır;
  - **Sabit güne sığmazsa — kullanıcı kararı 2026-10-08: "sığanı tut, kalanı ertesi güne":**
    - Sabitlenen ziyaretler o günün çalışma penceresine (bütçe + mesai bitişi) **rota sırasıyla** yerleşir.
    - **Sığmayanlar** (ör. 17:00'de başlayan 10 doktor + 2 eczanenin 18:00'den sonrası) aynı haftanın **sonraki uygun çalışma gününe** otomatik sabitlenir (`DayPins`'e yazılır, `autoPinned = true`, kaynak sabit referansı).
    - Haftada yer yoksa sonraki taslak haftaya 3B taşma kuralıyla kayar; neden `pin_overflow`.
    - **Mesai aşımı hiç planlanmaz.**
    - Önizlemede `pinOverflow[]`: `{ targetType, targetId, displayName, fromDate, toDate, reason }`. Ekran temsilciye gösterir ("2 eczane sığmadı → Cuma'ya taşındı").
    - Doktor / eczane müsaitlik penceresi varsa uyulur; pencereye uymayan da aynı yolla ertesi uygun güne.
  - `scope = visit` ise aynı kurumun diğer ziyaretleri çekilmez (yalnız o taşınır); `scope = institution` ise grup birlikte taşınır;
  - **Sistem bölmesi (sabitsiz, 3B bölme kuralı):** tek güne sığmayan kurum grubu bölünürken **bağlı eczaneler** hastanenin en çok doktorunun düştüğü günde kalır (hastaneden ayrı güne düşmez; sığmazsa o grubun ikinci gününe).
  - önizlemede gün satırı / slot için `groupKey` (kurum grubu kimliği) döner, ekran "bu kurumdaki diğer ziyaretler (N doktor, M eczane)" sorusunu buradan kurar.
- **Elle gün içi sıra** (`ManualVisitOrder`) sabitlemeyle birlikte çalışır: önce gün (sabit / motor), sonra gün içi sıra.
- **Önizleme:**
  - slotta `isPinned`;
  - gün özetinde `overCapacity`;
  - uyarılar (`pin_not_working_day` vb. okumada yok sayılan sabitler için);
  - hafta onayı sabitli günleri olduğu gibi yazar.
- **Yeniden aç:** onaylı hafta yeniden açılınca sabitler korunur (taslak tekrar kurulurken kullanılır).

### 3. Okuma
- Oturum ayrıntısı hafta başına `dayPins` döndürür (4D / 4F ekranı için).

## KORU / YAPMA
- **Yeni yazma komutu YOK** (listesiz 27).
- 3A (hafta / onay / sıklık), 3B (bütçe / yarım gün / taşma / izin) ve 3C (ürün listesi) kuralları değişmez; yalnız gün seçimi coğrafyaya duyarlı + sabitler.
- Rota iyileştiricinin gün içi algoritması değişmez.
- Eski `committed` planlara dokunma (4A: sabit, üretim yok).
- Göç / seed / grant / indeks YOK. Mobil yalnız ek alan.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
CRM (2419/0/5; kararsızlar bilinen), Web (dokunulmaz; 4C paralel), mimari (27). Build 0 hata.

**Yeni testler (üretim koduyla):**
1. İki uzak coğrafi küme (ör. Kadıköy + Bakırköy kurumları), 2 gün → her küme ayrı güne; 3B'ye göre haftalık toplam yol süresi azalır (aynı veriyle karşılaştırma).
2. Kısıtlar korunur: bütçe, yarım gün, tatil, aynı kurum aynı gün (3B testleri yeşil).
3. Sabit: taslak haftada doktor Salı → Perşembe; önizlemede Perşembe + `isPinned`; kurumun diğer doktorları yerinde.
4. **Sabit sığmazsa:** gün 17:00'ye kadar dolu, 10 doktor + 2 eczane (2,5 sa) bu güne `institution` sabiti → mesai içinde sığanlar o gün (rota sırasıyla), kalanlar sonraki çalışma gününe `autoPinned`; hiçbir ziyaret mesai bitişinden sonra değil; `pinOverflow` listesi dolu; haftada yer yoksa sonraki taslak haftaya `pin_overflow`.
4c. Sistem bölmesinde bağlı eczaneler hastanenin ana gününde kalır.
4b. `scope = institution`: hastane + 2 doktor + bağlı 1 eczane birlikte Perşembe'ye; aynı haftada bir doktorun `visit` sabiti Salı → o doktor Salı, diğerleri Perşembe (`visit` kazanır); `groupKey` önizlemede.
5. Onaylı haftaya sabit → 409; tatile sabit → 400 `pin_not_working_day`; haftada olmayan hedef → yok sayılır + uyarı.
6. `dayPins` null → korunur; [] → temizlenir.
7. Hafta onayı sabitli günleri olduğu gibi yazar; yeniden açınca sabitler korunur.
8. Konumsuz grup kümeye bağlanmaz (3B davranışı).

**Sabotaj (kırmızı kanıtla, geri al):**
1. Coğrafi atamayı kapat (yalnız doluluk) → test 1 kırmızı.
2. Sabitleri yok say → test 3 kırmızı.

### E4 (CT, fleet; kullanıcı onaylı test kaydında)
- `23b1706a` taslak haftasında gün dağılımının coğrafi kümelenmesi (ilçeler).
- Bir doktoru başka güne sabitleme (API; ekran 4D / 4F).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VP-4E · Coğrafyaya duyarlı gün ataması + temsilcinin gün sabitlemesi (backend)
Repository: C:\tmp\vp-4e (worktree) · Branch: wp/vp-4e · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-4E-geo-aware-days-and-day-pins.md — önce tamamını oku. Ayrıca: …/WP-VP-3A, 3B, 3C, 4A belgeleri (§37) · …/DESIGN-VP-FAZ3-planning-engine.md · services/Diten.CrmService/src/**/Features/{VisitPlanning,RouteOptimization}/** · **/Domain/Entities/PlanningSession.cs.
NE:
(1) Coğrafyaya duyarlı gün ataması: DayBalancer kurum gruplarını coğrafi kümelere göre günlere dağıtır (deterministik; ör. en uzak tohumlar + bütçeye sığan en yakın gün merkezi + yük ağırlıklı merkez güncelleme); 3B kısıtları korunur (bütçe, yarım gün, tatil, aynı kurum aynı gün, taşma); konumsuz grup 3B davranışı; ölçüt: aynı veride haftalık toplam yol süresi 3B'den az, yük farkı 3B sınırında.
(2) Gün sabitlemesi: PlanningSession hafta başına DayPins[] {targetType,targetId,contactId?,date,scope visit|institution} (class-map); institution = kurumun o haftadaki doktorları + bağlı eczaneleri (DayBalancer kurum grubu kuralının aynısı, motor çözer), aynı üyede visit sabiti varsa visit kazanır, önizlemede groupKey; yazma mevcut oturum güncellemesiyle dayPins {weekStart,pins[]} (null=koru, []=temizle) — YENİ KOMUT YOK; yalnız taslak hafta (409 week_already_approved / week_in_past), tarih o hafta + çalışma günü (400 pin_not_working_day), haftada olmayan hedef yok sayılır + uyarı pin_target_not_in_week; motor önce sabitleri koyar, kalanı etrafına dağıtır; SABİT SIĞMAZSA (kullanıcı kararı): mesai içinde sığanlar o gün (rota sırası), sığmayanlar aynı haftanın sonraki uygun çalışma gününe autoPinned (DayPins'e yazılır), haftada yer yoksa sonraki taslak haftaya pin_overflow, mesai aşımı HİÇ planlanmaz, önizlemede pinOverflow[]; sistem bölmesinde bağlı eczaneler hastanenin ana gününde kalır; scope=visit'te kurumun diğerleri çekilmez; ManualVisitOrder gün içi sıra; önizlemede isPinned + autoPinned + pinOverflow + uyarılar; onay sabitli günleri yazar; yeniden açınca sabitler korunur.
(3) Ayrıntı DTO hafta başına dayPins.
KORU/YAPMA: YENİ YAZMA KOMUTU YOK (listesiz 27); 3A/3B/3C kuralları değişmez (yalnız gün seçimi + sabit); rota iyileştiricinin gün içi algoritması değişmez; eski committed planlara dokunma; göç/seed/grant/indeks YOK; mobil yalnız ek alan.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — CRM Application (2419/0/5; PII + ContactWorkbook bilinen kararsız) · Web (dokunulmaz) · mimari (38/1, 27 sabit); build 0 hata. Yeni testler WP Acceptance 1–8 + 4b + 4c. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "feat(crm): WP-VP-4E — geography-aware day assignment and rep day pins on draft weeks" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), kümeleme yöntemi ve ölçülen yol süresi farkı, sabit kuralları, mobil için yeni alanlar. §22 TÜRKÇE. K13.
```
