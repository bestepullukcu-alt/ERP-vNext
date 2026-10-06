# WORK PACKAGE — WP-CT-E4-LIVE · Chain Template v2 canlı uçtan uca test (TUTUKON + ALMIBA, tarayıcı)

> **CT (SoR).** MOD-0162 Chain Template v2. Branch `feature/crm-chain-template` (FE-9 `2b8a62a2` / §37 `16e21b61` üstü; tüm WP'ler CT-E2). **Kullanıcı isteği (2026-09-27):** TUTUKON ve ALMIBA için şablonları **tarayıcıdan canlı oluştur** ve v2 akışının tamamını canlı test et. **Kod değişikliği YOK** — bu bir test koşusu; hata bulunursa düzeltme yapılmaz, raporlanır (düzeltme WP'sini CT çıkarır).
>
> **Veri yazma yetkisi (kullanıcı verdi, yalnız bu liste):** aşağıdaki 2 şablonu oluşturma, düzenleme, 1 "Yok say" çözümü, TUTUKON v1 yayınlama, TUTUKON v2 taslak oluşturma. **Başka hiçbir yazma YOK.**

## Ortam
- URL: `http://localhost:5001` · liste: `/CRM/KnowledgeConcepts` (Zincir Şablonları sekmesi) · editör: `/CRM/KnowledgeConcepts/Templates/Create`
- Tenant: **97c59330-dbc4-4665-b29c-0c26dbb5cc93** · DB `DitenERP_Dev` · `concept_chain_templates` = **0** (başlangıç)
- **Fleet restart** gerekli (L10N-LEGACY resx `22182eed`) — testten önce fleet'in güncel çalıştığını doğrula (TR editör başlığı "Zincir Şablonu Oluştur"; FR'de "Créer un modèle de chaîne").

## Canlı veri (CT okudu, salt-okuma)
**ALMIBA** — konu `SUBJ-001` "GP-000000000001 — ALMIBA" (`4b755c94…`)
- Tipler: ATC (Terapötik sınıf) · Indication (Endikasyon) · Profile (Hasta profili) · Need (İhtiyaç) · Benefit (Fayda) · MOA(Mechanism of Action) · Mechanism of Action *(CT-006, düğümü yok)*
- İlişkiler (7): ATC→Indication leads-to · Indication→Benefit addresses · Profile→Benefit requires · Indication→Need leads-to · Need→Benefit addresses · **MOA→Benefit leads-to** · Profile→Need requires

**TUTUKON** — konu `SUBJ-002` "GP-000000000063 — TUTUKON" (`c2d95714…`)
- Tipler: İhtiyaç · Fayda · Bileşen / Etki
- İlişkiler (2): Bileşen/Etki→Fayda leads-to · Fayda→İhtiyaç leads-to

## Şablon tasarımı
**T1 — TUTUKON** (temiz yol: uyumsuz beklenmez)
- Zincir Kodu `TPL-TUTUKON-01` · Ad `Tutukon Sindirim Konforu Zinciri` · Sürüm `v1` · Geçerlilik başlangıç = bugün
- Kimin İçin: ≥1 hedef kitle (listeden uygun ilk profil) · Moderatör: listede varsa bir değer
- DAL A "Ana akış": Bileşen / Etki ×1 → Fayda ×1–2 → İhtiyaç ×1
- DAL B "Kısa akış": Fayda ×1 → İhtiyaç ×1

**T2 — ALMIBA** (uyumsuz yolu: MOA bilerek dışarıda)
- Zincir Kodu `TPL-ALMIBA-01` · Ad `Almiba Hemodiyaliz Karnitin Zinciri` · Sürüm `v1` · Geçerlilik başlangıç = bugün · Kimin İçin ≥1
- DAL A "Klinik akış": ATC ×1 → Indication ×1 → Need ×1 → Benefit ×1–3
- DAL B "Hasta profili": Profile ×1 → Need ×1–2 → Benefit ×1
- **MOA ve Mechanism of Action eklenmez** → MOA→Benefit ilişkisi omurga dışı ("out") olmalı. Uyumsuz sayısını **tahmin etme, ekranda gördüğünü kaydet**; editör ile önizleme aynı sayıyı göstermeli.

## Test adımları (her adımda: beklenen / gözlenen / PASS-FAIL + ekran görüntüsü)
**A. T1 TUTUKON — oluştur**
1. Editör: TR başlık/etiketler; 2 sütun düzen (sol Kimlik+Dallar, sağ Palet→Sekmeler→Referans).
2. Konu TUTUKON seç → palette 3 tip gelir.
3. DAL A: paletten **sürükle-bırak** ile 3 tip; başlık "DAL A · Ana akış · 3 adım".
4. DAL B'yi **"+ Tip ekle" (klavye yolu)** ile doldur; boş dal dropzone "Kavram tipi sürükleyin" önce görünür.
5. Fayda (DAL A) min/max 1–2 → çip `×1–2`.
6. Sağ sekmeler: Bağlantılar dolu; Uyumsuz sekmesi sayısını kaydet (beklenen 0).
7. Durum = taslak → **Kaydet** → başarı mesajı, listeye/edit'e dönüş.

**B. T1 — liste + hızlı önizleme**
8. Liste satırı: Omurga, dal sayısı, durum/sürüm kolonları doğru.
9. Göz ikonu → önizleme: "Omurga" başlığı, DAL A/DAL B rozetleri + ad + "N adım", çipler `×1` / `×1–2`, Uyumsuz = editördeki sayı (0 → yeşil rozet).

**C. T1 — Edit + yayınla**
10. Edit: tüm alanlar/dallar/min-max kaydedildiği gibi geliyor; Zincir Kodu salt-okunur. Açıklamaya bir satır ekle → Kaydet.
11. **Guard testi:** Kimin İçin'i geçici boşalt → "Yayınla ve dondur" diyaloğunda kitle kontrolü kırmızı, onay **pasif** → iptal, kitleyi geri koy (kaydetmeden önce).
12. Diyalog: donacak omurga etiketleri, engelleyen kontroller hepsi yeşil → **Yayınla ve dondur**.
13. Yayınlanmış görünüm: kilit bandı + "Yeni sürüm oluştur"; palet/drag/min-max/kaydet kapalı; önizlemede durum "published".

**D. T1 — yeni sürüm**
14. "Yeni sürüm oluştur" → taslak; sürüm `v2` gir → Kaydet.
15. Sürümler sekmesi: v1 published + v2 draft görünür; liste iki kaydı doğru gösterir.

**E. T2 ALMIBA — oluştur + uyumsuz yolu**
16. Konu ALMIBA → palette 7 tip; T2 dallarını kur (A sürükle-bırak, B "+ Tip ekle").
17. Omurga dışı özet (diyagram altı) MOA'yı eksik tip olarak gösteriyor mu; Uyumsuz sekmesi sayısı + satırları (sebep: omurga dışı / ters sıra) kaydet.
18. Kaydet (taslak) → Edit'te aç.
19. Uyumsuz sekmesinde **1 ilişki "Yok say"** → rozet sayısı 1 azalır; sayfayı yenile → yok sayma kalıcı.
20. Liste → T2 önizleme: Uyumsuz sayısı = 19'daki son sayı (turuncu rozet; 0 ise yeşil).
21. T2 **taslak kalır** (yayınlama).

**F. Dil**
22. Arayüzü **FR** yap → editör (T2 Edit) + önizleme: başlık, kimlik alanları, adım düğmeleri, mesajlar Fransızca. (İsteğe bağlı AR: RTL kırılma var mı.) → **TR'ye geri al.**

**G. Konsol/ağ**
23. Tüm akış boyunca konsol hataları ve 4xx/5xx istekler (beklenen 409/400 dışı) kaydedilir.

## KORU / YAPMA
- **Kod/commit YOK.** Hata bulunursa düzeltme yapma → adım + ekran görüntüsü + konsol/ağ kaydı ile raporla, sonraki bağımsız adıma geç.
- **Şifre/kimlik bilgisi GİRME.** Oturum yoksa dur, kullanıcıdan tarayıcıda giriş yapmasını iste (97c5 tenant'ı).
- **Yazma yalnız izinli listede:** T1 oluştur/düzenle/yayınla/v2 taslak, T2 oluştur/düzenle/1 yok say. **Silme/arşivleme YOK.** Konu, kavram tipi, düğüm, ilişki, hedef kitle, RBAC, başka modül verisine **dokunma**. DB'ye doğrudan yazma YOK (okuma serbest).
- **Tarayıcı:** kullanıcının başka oturumlu sekmelerine mock/harness enjekte etme; bu test için ayrı sekme kullan. Gerçek UI üstünden yürü (fetch stub/harness YOK — bu canlı test).
- Adım 11'deki guard testinde geçici boşaltılan alanı **kaydetmeden** geri koy.
- **DUR:** konu/tip listesi yukarıdaki canlı veriyle uyuşmuyorsa; kaydet 5xx dönüyorsa (tekrar deneme → rapor); yayınla sonrası salt-okunur görünüm gelmiyorsa v2 adımını atla.

## Rapor formatı
Tablo: `# | adım | beklenen | gözlenen | PASS/FAIL | kanıt (ekran görüntüsü/istek)`; sonunda: oluşturulan kayıtlar (templateId, chainCode, sürüm, durum), editör vs önizleme uyumsuz sayıları (T1, T2 önce/sonra yok say), konsol/ağ hataları, bulunan hatalar (önem sırasıyla).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-E4-LIVE · Chain Template v2 canlı uçtan uca test — TUTUKON + ALMIBA (MOD-0162, tarayıcı, KOD YOK)
Repository: C:\Users\user\Desktop\ERP-vNext · Branch: feature/crm-chain-template (tüm CT WP'leri E2; son 16e21b61)

Amaç: http://localhost:5001 üzerinde TUTUKON ve ALMIBA için zincir şablonlarını TARAYICIDAN canlı oluştur ve v2 akışının tamamını canlı test et. Bu bir test koşusu: kod değişikliği ve commit YOK; hata bulursan düzeltme, raporla.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-E4-LIVE-tutukon-almiba.md (canlı veri, şablon tasarımı T1/T2, 23 test adımı, rapor formatı).

Ön koşul: fleet güncel mi (resx 22182eed için restart gerekli) — TR editör başlığı "Zincir Şablonu Oluştur". Oturum yoksa DUR ve kullanıcıdan 97c5 tenant'ına tarayıcıda giriş yapmasını iste; şifre/kimlik bilgisi GİRME.

YAP (WP'deki sırayla, her adımda beklenen/gözlenen/PASS-FAIL + ekran görüntüsü):
 A) T1 TPL-TUTUKON-01 "Tutukon Sindirim Konforu Zinciri": DAL A Bileşen/Etki ×1 → Fayda ×1–2 → İhtiyaç ×1 (sürükle-bırak), DAL B Fayda → İhtiyaç ("+ Tip ekle" klavye yolu), Kimin İçin ≥1, taslak kaydet.
 B) Liste + göz ikonu önizleme: Omurga, DAL A/B rozetleri, ×1/×1–2 çipleri, Uyumsuz sayısı = editördeki.
 C) Edit: alanlar kaydedildiği gibi; açıklama ekle+kaydet; guard testi (Kimin İçin geçici boş → yayınla onayı pasif → KAYDETMEDEN geri koy); Yayınla ve dondur; kilit bandı + salt-okunur.
 D) Yeni sürüm oluştur → v2 taslak kaydet; Sürümler sekmesi v1 published + v2 draft.
 E) T2 TPL-ALMIBA-01 "Almiba Hemodiyaliz Karnitin Zinciri": DAL A ATC → Indication → Need → Benefit ×1–3, DAL B Profile → Need ×1–2 → Benefit; MOA BİLEREK dışarıda; omurga dışı özet + Uyumsuz sayısını kaydet (tahmin etme); taslak kaydet; Edit'te 1 ilişki "Yok say" → sayı 1 azalır, yenilemede kalıcı; önizleme sayısı = editör. T2 taslak kalır.
 F) Arayüz FR: editör + önizleme Fransızca; (ops. AR RTL); sonra TR'ye geri al.
 G) Akış boyunca konsol hataları + beklenmeyen 4xx/5xx kaydet.
KORU/YAPMA: kod/commit YOK; yazma YALNIZ: T1 oluştur/düzenle/yayınla/v2 taslak, T2 oluştur/düzenle/1 yok say — silme/arşivleme YOK; konu/kavram tipi/düğüm/ilişki/hedef kitle/RBAC/başka modül verisine dokunma; DB'ye doğrudan yazma YOK (okuma serbest); gerçek UI üstünden yürü (fetch stub/harness YOK); kullanıcının diğer oturumlu sekmelerine dokunma, ayrı sekme kullan.
Durma: konu/tip listesi WP'deki canlı veriyle uyuşmuyorsa; kaydet 5xx dönerse (tekrar deneme, raporla); yayınla sonrası salt-okunur görünüm gelmezse D adımını atla.
RAPOR (§22 TÜRKÇE): tablo # | adım | beklenen | gözlenen | PASS/FAIL | kanıt; oluşturulan kayıtlar (templateId, chainCode, sürüm, durum); editör vs önizleme uyumsuz sayıları (T1; T2 yok say öncesi/sonrası); konsol/ağ hataları; bulunan hatalar önem sırasıyla. K13.
```

## §37 CT bağımsız doğrulama (2026-09-27) → **ACCEPTED (E4 canlı, bulgulu)**
```
Agent: 23/23 PASS (5 notlu) · kod/commit yok · CT: DB salt-okuma ile kayıtlar doğrulandı
```
- ✅ **Kayıtlar (DitenERP_Dev.concept_chain_templates, 3):**
  - `ff42ec4a…` TPL-TUTUKON-01 **v1 published** · omurga Bileşen/Etki→Fayda→İhtiyaç · DAL "Ana akış" (1-1, 1-2, 1-1) + "Kısa akış" (1-1, 1-1) · kitle 1 · moderatör position
  - `221def9e…` TPL-TUTUKON-01 **v2 draft** · aynı yapı (yeni sürüm kopyası doğru)
  - `4f7d4ffb…` TPL-ALMIBA-01 **v1 draft** · omurga ATC→Indication→Need→Benefit→**Profile** · ignored = [`70d48d5c…`] = MOA→Benefit leads-to (doğru ilişki)
- ✅ **İzinsiz yazma yok:** concept_types 10 / nodes 9 / relationships 9 — test öncesiyle aynı.
- ✅ **Bulgu kök nedenleri CT tarafından kodda teyit edildi:**
  - #1 `template-form.js:86` `spineFromBranches` = dallar arası **ilk görünüş** sırası → Profile (yalnız DAL B başında) omurga sonuna düşüyor; DAL B içi ileri `requires` ilişkileri "sıra ters".
  - #3 `concept-slim.js:610` `versionLabel` = `v${v}` + resx `PublishedSiblingHint` "v{0} yayında" → "v1" girilince "vv1".
  - TR resx: 266 değerin ~83'ünde Türkçe karakter yok ("Olustur", "Sablon", "Iptal"…) — kullanıcının dili TR olduğu için görünür borç.
- ⚠ Sürükle-bırak CDP sınırı nedeniyle gerçek `DragEvent` dispatch ile yapıldı (uygulama kodu + backend gerçek); klavye yolu tamamen gerçek.

**Sonuç:** Chain Template v2 canlıda uçtan uca çalışıyor. Açık: #1 omurga türetme kararı, #2 ALMIBA `addresses` yön verisi, düzeltme WP'leri (#3–#6, #8) + TR/L10n WP'si.
