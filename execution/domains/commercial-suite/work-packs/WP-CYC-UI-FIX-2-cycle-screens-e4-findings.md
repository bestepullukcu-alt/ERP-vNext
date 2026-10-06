# WORK PACKAGE — WP-CYC-UI-FIX-2 · Dönemler + Dönem Kapasitesi: canlı kontrol (E4) bulguları — Web

> **CT (SoR), 2026-10-06.**
> - **Kaynak:** WP-CYC-UI-1 §37 ek ve WP-CYC-UI-2 §37 ek (canlı E4, 2026-10-06) — 6 küçük bulgu.
> - **Kullanıcı (2026-10-06):** "CYC-UI-FIX-2'yi paketle".
> - **Kapsam:** yalnız `frontend/Diten.Web` (iki modül). CRM DOKUNMA.
>
> **Çalışma yeri:** worktree `C:\tmp\cyc-ui-fix-2`, dal `wp/cyc-ui-fix-2`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Bulgular → NE
1. **"Bugün geçerli dönem" kutusu (Dönemler listesi üstü):** bugün birim vermeden `resolve-active` çağırıyor → yalnız tüm şirket kapsamına bakıyor, ülke kapsamlı aktif dönem varken "aktif dönem yok" diyor.
   - **Düzelt:** kutu "Bugün aktif dönemler" olur — liste verisinden (zaten yüklü; ek CRM isteği yok) **bugünü kapsayan tüm aktif dönemler**, kapsam etiketiyle (ör. "Ülke TR · tr-2026-q4"); hiç yoksa "Bugün aktif dönem yok". Birim bazlı kesin çözüm için "Geçerli dönem bul" aracına bağlantı.
2. **Zaman çizelgesi açılışta bugüne kaydırılmıyor** (eksen başı görünüyor). **Düzelt:** ilk çizimde yatay kaydırma bugün çizgisini görünür alanın ortasına getirir; Arapça (RTL) yönde de doğru.
3. **Dil:** "Yukleniyor..." (Türkçe diakritik eksik) — kaynağı bul, iki modülde ve kullandıkları ortak anahtarda düzelt (7 dil anahtar eşitliği). **Ülke adları İngilizce** ("Turkey") — bulucu, panel ve kapasite takvim ülkesi seçimlerinde ülke adı **arayüz dilinde** gösterilir: projede mevcut bir yerelleştirilmiş ülke adı kaynağı varsa onu kullan; yoksa kod + referans set etiketi kalır ve raporla (DUR değil).
4. **Kapasite ayrıntısı — planlama oturumları tablosu:** durumlar ham kod ("draft", "committed") → yerelleştirilmiş etiketler (7 dil; bilinmeyen değer ham kod yerine "Bilinmiyor").
5. **Eski model kapasitede şelale etiketi:** "Günlük sabit işler (yol, sınav)" eski modelde yanlış — eski modelde rapor da günlük düşülüyor. **Düzelt:** `visitModel = legacy` iken "Günlük sabit işler (yol, rapor, sınav)", `typical` iken "(yol, sınav)"; aynı kural düzenleme ekranının sağ özetinde.
6. **Tarih biçimi tutarsız:** kapasite ayrıntı başlığında "1.10.2026", diğer yerlerde "01 Eki 2026". **Düzelt:** iki modülde tek bir tarih biçimleyicisi (arayüz diline göre, gün + kısa ay adı + yıl).

## KORU / YAPMA
- CRM DOKUNMA; yeni CRM çağrısı yok (madde 1 liste verisinden).
- Ekran yapısı, hesaplar, önizleme akışı, hızlı bakış, bulucu davranışı DEĞİŞMEZ.
- `esc()` / `textContent`.

## Acceptance
- **E2:** Web 0 kırmızı (taban **551/0**), CRM dokunulmadı, build 0 hata; liste doğrulayıcısı iki listede yine yalnız 8 bilinen sapma.
  - **Yeni testler:** "bugün aktif dönemler" liste verisinden (ülke kapsamlı aktif dönem bugünü kapsıyorsa listelenir; taslak / kapalı / bugünü kapsamayan listelenmez); zaman çizelgesi ilk kaydırma hesabı (bugün konumu); "Yükleniyor" anahtarı 7 dilde ve TR diakritikli; oturum durumu etiket eşlemesi; legacy / typical şelale etiketi; tarih biçimleyicisinin iki modülde aynı olması.
  - **Sabotaj:** (1) "bugün aktif" süzgecinde kapsam yerine yalnız tüm şirket kapsamını al → kırmızı; (2) legacy etiketini typical'a eşitle → kırmızı; geri al.
- **E4 (CT):** canlıda kutu "Ülke TR · tr-2026-q4" gösterir; zaman çizelgesi bugünle açılır; etiketler / tarihler düzgün.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-CYC-UI-FIX-2 · Dönemler + Dönem Kapasitesi: canlı kontrol (E4) bulguları — Web
Repository: C:\tmp\cyc-ui-fix-2 (worktree) · Branch: wp/cyc-ui-fix-2 · commit bu dala, push YOK · yalnız frontend/Diten.Web

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-CYC-UI-FIX-2-cycle-screens-e4-findings.md — önce tamamını oku. Ayrıca: …/WP-CYC-UI-1-cycle-periods-screens.md + …/WP-CYC-UI-2-cycle-capacity-screens.md (§37 + §37 ek E4 bulguları) · frontend/Diten.Web/{Controllers/CRM/{CyclePeriods, CycleCapacities}Controller.cs, Views/CRM/{CyclePeriods, CycleCapacities}/**, wwwroot/assets/js/CRM/{CyclePeriods, CycleCapacities}/**, Models/CRM/{CyclePeriod*, CycleCapacity*}.cs, ilgili resx} · memory l10n-bridge-pascalcase-loader.

NE: (1) "Bugün geçerli dönem" kutusu → "Bugün aktif dönemler": liste verisinden bugünü kapsayan TÜM aktif dönemler kapsam etiketiyle (ek CRM isteği yok); yoksa "Bugün aktif dönem yok"; "Geçerli dönem bul" bağlantısı. (2) Zaman çizelgesi ilk çizimde bugünü görünür alanın ortasına kaydırır (RTL dahil). (3) "Yukleniyor..." kaynağını bul, 7 dilde düzelt (TR "Yükleniyor..."); ülke adları arayüz dilinde — mevcut yerelleştirilmiş ülke adı kaynağı varsa kullan, yoksa raporla. (4) Kapasite ayrıntısı oturum durumları yerelleştirilmiş (bilinmeyen → "Bilinmiyor"). (5) Şelale + sağ özet etiketi: legacy "Günlük sabit işler (yol, rapor, sınav)", typical "(yol, sınav)". (6) İki modülde tek tarih biçimleyicisi (gün + kısa ay + yıl, arayüz dili).
KORU/YAPMA: CRM DOKUNMA, yeni CRM çağrısı yok; ekran yapısı/hesap/önizleme/hızlı bakış/bulucu davranışı değişmez; esc()/textContent.
DOĞRULA (E2): dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 551); CRM dokunulmadı; build 0 hata; verify_datatable_page.py iki listede (--area CRM --api-profile proxy --format gaps) → yalnız 8 bilinen sapma. Yeni testler WP Acceptance. Sabotaj: (1) bugün-aktif süzgecini yalnız tüm şirket kapsamına indir → kırmızı; (2) legacy etiketini typical'a eşitle → kırmızı; geri al. Commit ("fix(web): WP-CYC-UI-FIX-2 — cycle screens E4 findings (today's active periods, timeline scroll, l10n, session statuses, legacy label, date format)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
```
