# WORK PACKAGE — WP-VP-4L · Tanımsız sıklık = haftada 1 + haftaya ek ziyaret (backend + Web)

> **CT (SoR), 2026-10-08.** Kullanıcının manuel testi.
> - **Canlı durum** (plan `f2c6014d`): 54 doktorun hepsinde sıklık tanımsız. Bugünkü kural "tanımsız = dönemde 1" olduğu için 54 ziyaretin hepsi 41. haftada onaylandı; 42–53. haftalar boş.
>   - "Bu haftayı üret" aynı sonucu veriyor.
>   - Hedefler 42. haftada 54 doktoru "seçili" gösteriyor ama haftada ne rota ne süre var. Seçimler döneme ait, haftaya değil; ekran bunu anlatmıyor.
> - **Kullanıcı kararları (2026-10-08):**
>   - **Sıklığı tanımsız doktor = haftada 1** ("sıklığı tanımsız olanlar haftada 1 olabilir").
>   - **Haftaya ek ziyaret: evet.** Temsilci, sıklığı dolmuş (ya da sıklık kuralına göre o haftaya düşmeyen) bir doktoru belirli bir haftaya ayrıca ekleyebilir.
> - **Not:** Ziyaret Çalışma Alanı mockup'ı bekleniyor ([brief](mockups/visit-workspace/BRIEF-visit-workspace-calendar.md)). Bu paket bugünkü Ziyaret Planlama'yı kullanılabilir kılar; davranış (kural + veri) çalışma alanına aynen taşınır.
> - **İki bölüm, paralel:**
>   - 4L-BE: `C:\tmp\vp-4l-be`, dal `wp/vp-4l-be`;
>   - 4L-WEB: `C:\tmp\vp-4l-web`, dal `wp/vp-4l-web`.
>   - İkisi de test dalı başından. Commit dala, push YOK.

## 4L-BE — CRM

### 1. Tanımsız sıklık varsayılanı: haftada 1
- **Bugün:** `VisitPlanningEngine` ⑦ ve `ContactPeriodStatusReader.RequiredInPeriod` tanımsız sıklığı **dönemde 1** sayıyor.
- **Yeni kural:** tanımsız (ve çözülemeyen) sıklık = **çalışma haftası başına 1**. Dönemde gereken = `FrequencyExtendPlanner.UnitsIn("week", dönem)` (dönemin çalışma haftası sayısı; tamamen tatil olan hafta sayılmaz).
- **Tek yer:** sabit / yardımcı (ör. `FrequencyDefaults.UnknownPerWeek = 1`); motor ve 3D durum okuyucusu **aynı** yardımcıyı kullanır (3D / 3A uyumsuzluğu bir kez yaşandı, CT düzeltmesi `ccd93de04`).
- `frequencyStatus` `unknown` kalır (kaynak bilgisi). Ek alan **`frequencyDefault: "weekly"`**: slot, doktor içerik özeti, 3D durum (Web / mobil "haftada 1 (varsayılan)" yazar).
- **Dağılım:** mevcut k·H/Z dağıtımı aynen → haftada bir ziyaret.
  - Gün bütçesi / taşma kuralları (3B / 4E / 4G / 4I) aynen; sığmayan sonraki haftaya **kaymaz**, çünkü her haftanın kendi ziyareti var.
  - Haftasında sığmazsa `shifted` yerine yeni neden **`week_full_skipped`** ile o haftanın ziyareti atlanır.
  - **CT önerisi:** haftalık sıklıkta kaydırma zinciri oluşmasın. Ajan mevcut kaydırma koduyla çelişirse raporlar, en yakın tutarlı davranışı uygular.
- **Onaylı haftalar** etkilenmez (yazılmış ziyaretler sabit, S-1).

### 2. Haftaya ek ziyaret (temsilcinin seçimi)
- **Saklama:** `PlanningSession.Weeks[]` hafta kaydına **`ExtraTargets[]`** `{ targetType, targetId, contactId?, accountId? }` (class-map).
- **Yazma:** **mevcut** oturum güncellemesiyle (`PUT sessions/{id}`), `dayPins` deseninin aynısı:
  - ek alan `weekExtras: { weekStart, targets: [...] }`;
  - gönderilmez → korunur; `targets: []` → o haftanınkiler temizlenir.
  - **Yeni yazma komutu YOK.**
- **Kurallar:**
  - yalnız **taslak / boş** hafta, geçmiş ve onaylı değil → değilse `409 week_not_editable` (mevcut hafta kodlarından uygun olanı kullan, yoksa yeni kod);
  - hedef **planın seçimindeki** bir doktor / eczane olmalı → değilse `400 extra_target_not_in_plan`;
  - aynı hafta + aynı hedef bir kez;
  - hedefin o hafta zaten sıklıktan ziyareti varsa ek **yazılmaz** ve yanıt `ignored` olarak döner. **CT önerisi:** sessiz tekrar yok.
  - izin engelli (`consent_blocked`) doktora ek ziyaret yok (mevcut kural).
- **Motor:**
  - ek ziyaret o haftanın ziyaret listesine **ek** olarak girer, gün ataması aynı kurallarla;
  - sabitler (`dayPins`) ek ziyarete de uygulanır;
  - ek ziyaret **başka haftaya kaymaz**; haftaya sığmazsa `unscheduled` listesinde `extra_no_room`.
- **Okuma alanları:**
  - önizleme slotuna `isExtra: bool`;
  - hafta DTO'suna `extraTargets[]`;
  - 3D doktor durumuna `extraThisWeek` (seçili hafta verilirse);
  - `done / planned / remaining` sayımında ek ziyaret **sayılır** ama `remaining` 0'ın altına düşmez; fazla ziyaret `overFrequency: int` ile döner.
- **Onay:** onaylanan haftanın ek ziyaretleri planlanan ziyaret olarak yazılır; `selection` köken bilgisinde `extra = true` (ek alan). Yeniden açma kuralları aynen.

### Acceptance (4L-BE)
1. Tanımsız sıklık: 13 haftalık dönemde doktor başına gereken 13; her taslak haftada bir ziyaret; 3D durumu aynı sayıyı veriyor (motor ve okuyucu aynı yardımcı).
2. Tanımlı sıklık (ör. ayda 2) etkilenmiyor.
3. Ek ziyaret: taslak haftaya eklenir, önizlemede `isExtra`, onayda yazılır; onaylı / geçmiş haftaya 409; plan dışı hedefe 400; izin engelliye yok; sığmazsa `extra_no_room`.
4. `weekExtras` gönderilmezse korunur, `[]` temizler.
5. Mimari 27 (yeni yazma komutu yok).
- **Sabotajlar (kırmızı kanıtla, geri al):**
  - motor ile okuyucuyu ayrı varsayılanla bırak → test 1 kırmızı;
  - ek ziyareti onaylı haftaya izin ver → test 3 kırmızı.

## 4L-WEB — Ziyaret Planlama

### 3. Metin
- "dönemde 1 (varsayılan)" → **"haftada 1 (varsayılan)"**: doktor listesi, doktor paneli, Hedefler tablosu; `frequencyDefault` alanından.
- 7 dil.

### 4. Hedefler — hafta bilinci
- **Başlık alt satırı:** "42. Hafta · **12 ziyaret bu hafta** · 54 doktor dönem hedefinde". Bugünkü "54 doktor seçili" yanıltıcı.
- **Doktor tablosu, "Durum" sütununa haftalık bilgi:**
  - "**Bu hafta planlı**" (sıklıktan);
  - "**Ek ziyaret**" (rozet);
  - "Bu hafta yok".
- **Yeni eylem** (satır menüsü ya da küçük düğme), yalnız taslak / boş haftada:
  - "**Bu hafta da ziyaret et**" → `weekExtras` güncellemesi;
  - ek ziyarette "**Ek ziyareti kaldır**".
  - Toplu: seçili doktorlara "Bu haftaya ekle (N)".
- **Hata mesajları:** `extra_target_not_in_plan` ("Önce doktoru plana ekleyin"), onaylı hafta ("Haftayı yeniden açın"), `extra_no_room` ("Bu haftada yer yok").
- Boş haftanın boş durum metnine ikinci yol: "Hedefler'den doktorları **bu haftaya ekleyebilirsiniz**."

### 5. Haftalar ve Rota
- **Ek ziyaret:** gün satırında ve Rota durağında küçük "ek" rozeti; doktor panelinin dönem geçmişinde "Ek ziyaret".
- Kaydırılan / sığmayan listesinde `extra_no_room` ve `week_full_skipped` nedenleri (yerel etiket).

### Acceptance (4L-WEB)
1. "haftada 1 (varsayılan)" metni `frequencyDefault`'tan; eski metin kalmadı.
2. Hedefler başlığı haftalık ziyaret sayısını ve dönem hedef sayısını ayrı gösteriyor.
3. "Bu hafta da ziyaret et" / "Ek ziyareti kaldır" doğru `weekExtras` gövdesini gönderiyor; onaylı haftada görünmüyor; toplu ekleme.
4. Ek rozeti Haftalar / Rota / doktor panelinde.
5. Yeni metinler 7 dilde; argümansız Localizer'da `{0}` yok.
- **Sabotaj:** onaylı haftada "Bu hafta da ziyaret et"i göster → test 3 kırmızı.

## KORU / YAPMA (iki bölüm)
- **Yeni yazma komutu YOK** (mevcut oturum güncellemesi; listesiz 27).
- 3A / 3B / 3C / 4E / 4G / 4I kuralları aynen (yalnız tanımsız sıklık varsayılanı + ek ziyaret).
- Onaylı haftalar sabit.
- ARCH GATE, TenantId, UAS-001, 7 dil.
- Göç / seed / grant / indeks YOK; canlı veriye yazma yok; kullanıcının sekmesine enjeksiyon yok.
- 4K (rapor kuralları) bu pakette değil (kullanıcı: mockup sonrası).
- **Mobil:** ek alanlar (`frequencyDefault`, `isExtra`, `extraTargets`, `extraThisWeek`, `overFrequency`) ve yeni kodlar Faz 5 notuna ek olarak bildirilecek. **M10 metni** "haftada 1 (varsayılan)" olur.

---

## §36.1 Agent Prompt — 4L-BE (paste-ready)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VP-4L · 4L-BE — tanımsız sıklık = haftada 1 + haftaya ek ziyaret (CRM)
Repository: C:\tmp\vp-4l-be (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vp-4l-be · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VP-4L-weekly-default-week-extras.md — 4L-BE (madde 1–2) + kullanıcı kararları. Bağlam: WP-VP-3A §37 (FrequencyExtendPlanner.UnitsIn, RequiredInPeriod, k·H/Z), 3D + CT düzeltmesi ccd93de04 (motor/okuyucu aynı kural), 3B/4E/4G/4I §37 (gün bütçesi, kaydırma, DayPins deseni). Kod: services/Diten.CrmService/src/**/Features/VisitPlanning/** (VisitPlanningEngine ⑦, FrequencyExtendPlanner, TargetStatus/ContactPeriodStatusReader, PlanningSession, UpdatePlanningSessionSelection + DayPins deseni) · Api/Models/CRM/VisitPlanningRequests.cs.
NE: (1) tanımsız sıklık = çalışma haftası başına 1 (UnitsIn("week")); TEK yardımcı, motor + 3D okuyucu aynı; frequencyStatus unknown kalır + ek alan frequencyDefault:"weekly"; haftasında sığmayan haftalık varsayılan ziyaret zincirleme kaymaz (week_full_skipped; çelişki varsa raporla). (2) haftaya ek ziyaret: PlanningSession.Weeks[].ExtraTargets (class-map), MEVCUT PUT sessions/{id} ile weekExtras {weekStart, targets[]} (yok=koru, []=temizle; dayPins deseni); yalnız taslak/boş hafta (yoksa 409), hedef planın seçiminde olmalı (400 extra_target_not_in_plan), tekrar yok, izin engelliye yok; motor ekleri o haftaya ekler (aynı gün kuralları + dayPins), başka haftaya kaymaz (unscheduled extra_no_room); okuma: slot isExtra, hafta extraTargets, 3D extraThisWeek + overFrequency; onayda planlanan ziyaret selection.extra=true.
KORU/YAPMA: YENİ YAZMA KOMUTU YOK (listesiz 27); 3A/3B/3C/4E/4G/4I kuralları aynen; onaylı haftalar sabit; göç/seed/grant/indeks yok; canlı veriye yazma yok; TenantId; ARCH GATE; mobil yalnız ek alan; 4K kuralları bu pakette YOK.
DOĞRULA (E2): CRM (2457/0/5 tabanı; bilinen kararsız testler olabilir) · Web 810/0 (dokunulmaz) · mimari 27. Testler 4L-BE Acceptance 1–5; sabotaj 2 (kırmızı kanıtla, geri al — commit'lenmemiş başka iş varsa `git checkout --` kullanma). dotnet test -o kullanırsan çıktı REPO İÇİNDE.
Commit: "feat(crm): WP-VP-4L-BE — unknown frequency = weekly default, per-week extra visits" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, kaydırma kararı, mobil için yeni alan/kodlar, elle denenecekler. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — 4L-WEB (paste-ready)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VP-4L · 4L-WEB — "haftada 1 (varsayılan)", Hedefler hafta bilinci, haftaya ek ziyaret (Web)
Repository: C:\tmp\vp-4l-web (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vp-4l-web · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VP-4L-weekly-default-week-extras.md — 4L-WEB (madde 3–5); sözleşme 4L-BE madde 1–2'de (frequencyDefault, weekExtras, isExtra, extraTargets, extraThisWeek, overFrequency, extra_target_not_in_plan, extra_no_room, week_full_skipped). Bağlam: WP-VP-4H/4I/4J §37. Kod: frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/** · Views/CRM/VisitPlanning/** · Resources/Views/CRM/VisitPlanning/**.
NE: (3) "dönemde 1 (varsayılan)" → "haftada 1 (varsayılan)" (frequencyDefault). (4) Hedefler: başlık "N ziyaret bu hafta · M doktor dönem hedefinde"; doktor Durum'unda "Bu hafta planlı / Ek ziyaret / Bu hafta yok"; taslak/boş haftada "Bu hafta da ziyaret et" / "Ek ziyareti kaldır" + toplu "Bu haftaya ekle (N)" → mevcut PUT sessions/{id} weekExtras; hata mesajları; boş hafta metnine "Hedefler'den bu haftaya ekleyebilirsiniz". (5) "ek" rozeti Haftalar/Rota/doktor paneli; extra_no_room / week_full_skipped yerel etiketler. Backend alanları yoksa (4L-BE paralel) eylemler gizli kalır, hata yok.
KORU/YAPMA: backend'e dokunma; yeni yazma ucu yok; 4C–4J davranışları ve mockup v3 Hedefler tasarımı aynen; Rota tasarımı aynı; UAS-001; 7 dil + TR diakritik; argümansız Localizer değerinde {0} yok; RTL bidi/ratio/isolate; kullanıcının oturum açık sekmesine harness/mock enjekte etme; canlı veriye yazma yok.
DOĞRULA (E2): Web (810/0 tabanı) · CRM 2457/0/5 (dokunulmaz) · mimari 27; JS node --check; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad> (REPO İÇİNDE). Testler 4L-WEB Acceptance 1–5; sabotaj 1 (kırmızı kanıtla, geri al — `git checkout --` kullanma).
Commit: "feat(web): WP-VP-4L-WEB — weekly default label, week-aware targets, per-week extra visits" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, elle denenecekler. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — 4L-WEB E2 ACCEPTED (2026-10-08)
**Commit'ler:** `b403aa18f` (ajan `019c13c10`, eşit yükseklik düzeltmesi `7e562ce28` üstüne rebase, çakışmasız) + CT testi `09904ec07`. Push: test dalı.

**CT K13:**
- Web 810 → **816/0** (ajan +5, CT +1). CRM dokunulmadı.
- Mimari: Web değişikliği; yazma komutu yok (27).

**Kod okuması:**
- `weekExtras` gövdesi mevcut `PUT sessions/{id}` ile `{ weekExtras: { weekStart, targets }, expectedVersion }`; hata sonrası tablo yeniden çiziliyor.
- Eylemler yalnız taslak / boş hafta (`EXTRA_WEEK_STATUSES`) **ve** sunucu alanları (`extraTargets` / `isExtra`) varken (`supportsExtras`).
- `frequencyDefault` → "haftada 1 (varsayılan)"; eski anahtar kalktı.

**CT sabotajı:**
- Önce `supportsExtras` → `true ||` (sunucu alanları yokken eylemler açık): **yeşil kaldı** — koruma yoktu.
- CT testi eklendi (`Without_the_servers_extra_fields_no_extra_visit_action_is_offered`); aynı sabotajda **kırmızı**; geri alındı.

**4L-BE gelince E4 birlikte:**
- 42. hafta başlık sayıları ve Durum sütunu;
- "Bu hafta da ziyaret et" / kaldır / toplu ekleme;
- ek rozeti (Haftalar / Rota / panel);
- onaylı haftada eylem yok;
- plan dışı doktorda 400 mesajı.

## §37 CT kabul — 4L-BE E2 ACCEPTED (2026-10-09)
**Commit:** `ac73da19a` (ajan `4e28b4a79`, test dalına rebase, çakışmasız). Push: test dalı.

**CT K13:**
- CRM 2457 → **2466/0/5** (+9). Worktree'de ve birleşik koşuda aynı.
- Web **816/0** (4L-WEB artık sunucu alanlarını görüyor).
- Mimari: listesiz **27**.

**Kod okuması:**
- `FrequencyDefaults` tek yer: `UnknownRequiredInPeriod = UnitsIn("week")`; motor ve 3D okuyucu aynı yardımcıyı kullanıyor.
- `weekExtras` mevcut güncellemede: dönem okunamazsa 400; onaylı hafta 409, geçmiş hafta 409; plan dışı hedef 400 (aynı istekteki seçim sonrası kontrol).
- `PlanningWeekExtra` class-map'e kayıtlı (string GUID); eski oturum boş liste okuyor.

**Ajanın bilinçli sapmaları (CT kabul):**
- Ek ziyaretler `PlanningSession.WeekExtras` içinde (DayPins deseni); `Weeks[]` yalnız onaylı haftaların kaydı.
- "zaten planlı" durumu yazmada `ignored` değil, önizlemede `extra_already_planned` uyarısı.
- 3D okumalarına `?weekStart=` (şimdilik yalnız `extraThisWeek` için).
- Test ortamının sahte sıklık çözücüsü açıkça "dönemde 1" (3B / 4E / 4G testlerinin anlamı korunsun).

**CT sabotajı:** plan dışı hedef kontrolü kapatıldı → 1 kırmızı (`Extra_visits_are_written_for_a_plan_target_on_a_draft_week_only`). Geri alındı.

**Sıradaki:** Hedefler sayı paketi (kullanıcı onayı): 3D `weekStart` parametresi `dueThisWeek` ve hızlı süzgeç sayılarına da uygulanacak.
