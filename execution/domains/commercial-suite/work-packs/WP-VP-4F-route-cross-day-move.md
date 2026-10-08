# WORK PACKAGE — WP-VP-4F · Rota: ziyareti başka güne taşıma

> **CT (SoR), 2026-10-08.**
> - **Kullanıcı:** "ikisi de olabilir" (Haftalar + Rota'da günler arası taşıma), 2026-10-08.
> - **Kapsam:** yalnız Web, Rota sekmesi. Backend WP-VP-4E (`dayPins`).
> - **Ön koşul:** 4E ve 4D birleşmiş olmalı (4D'nin taşıma ve sabit simgesi bileşenleri yeniden kullanılır).
> - **Rota tasarımı korunur:** gün sekmeleri, durak listesi, harita aynı. Yalnız günler arası taşıma eklenir (kullanıcının bu turdaki açık isteği).
>
> **Çalışma yeri:** worktree `C:\tmp\vp-4f` (4D kabulünden sonra), dal `wp/vp-4f`. Commit bu dala, push YOK.

## NE
1. Rota'daki durak, **gün sekmesinin üstüne sürüklenince** o güne taşınır. Klavye alternatifi: durak menüsünde "Güne taşı…".
   - Kurum durağı (çok ziyaretli durak) → `scope = institution`; tek doktor → 4D'deki aynı soru ("Hepsini taşı" / "Yalnız bu doktor").
2. Taşıma = 4E `dayPins` (mevcut oturum güncellemesi) → önizleme tazelenir; hedef gün sekmesi açılır, durak yeni sırada ve saatte görünür.
3. Gün içi sürükle-bırak (elle sıra) **aynen** kalır.
4. Sabit durak simgesi (`isPinned`) + "Sabiti kaldır" (4D bileşeni).
5. Kurallar: yalnız taslak hafta; tatil / hafta sonu sekmesine bırakma kapalı; sığmayanlar 4E `pinOverflow` ile ertesi güne taşınır, Rota bilgi mesajı gösterir (4D ile aynı metin).

## KORU / YAPMA
- Rota görünümü ve gün içi davranışı değişmez. Backend'e dokunma. Yeni yazma uç YOK. 7 dil.

## Acceptance
- Web testleri: gün sekmesine bırakma → doğru `dayPins`; gün içi sıra eski yolla (`ManualVisitOrder`); onaylı haftada kapalı; tatil sekmesine bırakma yok.
- Sabotaj: gün içi sürüklemeyi `dayPins`'e çevir → test kırmızı.
- E4: taslak haftada bir durağı Salı → Perşembe (kullanıcı onaylı).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (4D kabulünden SONRA)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VP-4F · Rota: ziyareti başka güne taşıma
Repository: C:\tmp\vp-4f (worktree) · Branch: wp/vp-4f · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VP-4F-route-cross-day-move.md — önce oku. 4E ve 4D §37'lerini oku (dayPins, scope, autoPinned, pinOverflow; weeks.js taşıma/sabit bileşenleri ve DROPPABLE_DAY_KINDS). Ayrıca: frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/** · Views/CRM/VisitPlanning/Details.cshtml.
NE: (1) Rota'da durak gün sekmesine sürüklenince o güne taşınır + klavye alternatifi "Güne taşı…"; kurum (çok ziyaretli) durağı = scope institution, tek doktor = 4D'deki "Hepsini taşı / Yalnız bu doktor" sorusu (groupKey); (2) taşıma = 4E dayPins (mevcut oturum güncellemesi), önizleme tazelenir, hedef gün sekmesi açılır; (3) gün içi sürükle-bırak (ManualVisitOrder) aynen; (4) isPinned simgesi + Sabiti kaldır + autoPinned işareti (4D bileşenleri, yeniden yazma); (5) yalnız taslak hafta, tatil/hafta sonu/geçmiş gün sekmesine bırakma yok; sığmayanlar 4E pinOverflow ile ertesi güne gider → 4D ile aynı bilgi mesajı ("N ziyaret sığmadı → {gün}"); mesai aşımı yok.
KORU/YAPMA: Rota görünümü ve gün içi davranış değişmez; backend'e dokunma; yeni yazma ucu yok; 7 dil.
DOĞRULA (E2): Web (774/0) · CRM (2437/0/5, dokunulmaz) · mimari 27; JS node --check; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Testler belge Acceptance; sabotaj 1 (kırmızı kanıtla, geri al).
Commit: "feat(web): WP-VP-4F — move a route stop to another day (day pin)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, elle denenecekler. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-08)
**Commit:** `2966bbf72` (ff). Push: test dalı.

**CT K13:** Web 774 → **779/0** (+5) · CRM 2437/0/5 (dokunulmadı; bir koşuda bilinen PII kararsızı) · mimari 27 · JS `node --check` temiz.

**Kod okuması:**
- Rota sekmeye bırakma = 4D `weeks.js` bileşeni (soru, kurallar, `savePins`): kurum bloğu → `institution`, açık duraktaki doktor kartı → `visit`; `day-pin:moved` olayıyla hedef gün açılıyor.
- Eski `moveBlockToDay` (elle sırayla yaklaşık taşıma) kaldırıldı.
- Gün içi sürükle elle sıra olarak aynen.
- `ROUTE_MOVABLE_WEEK_STATUSES` draft / empty.
- Eczane durağı kliniğiyle birlikte taşınıyor.
- Yeni metin anahtarı yok (4D anahtarları).

**CT sabotajı:** onaylı / geçmiş haftada Rota'da bırakma açıldı → 1 kırmızı (`An_approved_or_past_week_and_a_holiday_weekend_or_gone_day_take_no_drop`). Geri alındı.

**E4:** Faz 4 tek turunda (ajan listesi: sekmeye bırakma, tek doktor sorusu, gün içi sıra, klavye, dolu güne kurum → sığmadı satırı, onaylı / tatil kapalı).
