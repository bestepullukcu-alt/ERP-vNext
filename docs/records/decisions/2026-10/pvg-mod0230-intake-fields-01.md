---
decision_id: PVG-MOD0230-INTAKE-FIELDS-01
status: approved — sahip 7/7 "Evet" (2026-10-22, sohbette "önerilerine katılıyorum"); AL-2 ve AL-6 netleştirmeyle; açık nokta: a. QPPV ve MDM/CRM/Platform onayı bekleyen kısımlar o onaylara kadar uygulanmaz (taslak, commit edilmedi)
prepared_at_local: 2026-10-22
prepared_by: PVG Control Tower (Claude Code oturumu); bu kayıt kendi adına karar vermez
decided_by: depo sahibi (rol) — her satırda "Sahip işareti"; kişi adı ve e-posta yazılmaz
trigger: sahibin soruları (2026-10-22) — "kayıt ne için açılıyor, ürün olmalı mı", "uluslararası sistemlerde nasıl",
  "vakanın geldiği kişi otomatik mi, seçilecek mi, elle mi"; sahip `alim-simdi` dedi
source: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ e1afd174b (sha256 f5ca98e3…),
  "Entity Fields"; REQ-PVG-CRM-01 (CRM ziyaret raporu talebi)
scope: yalnız MOD-0230 alım alanları; başka modüllere gidecek konular yalnız adlandırılır, onlar adına karar verilmez
  (XMC-001); operasyonel runtime kapısı kapalı kalır
---

# MOD-0230 — alım alanlarının tamamlanması (ürün, farkına varma anı, ülke, rapor türü, geçerlilik, bildiren)

> Bu dosya bir **taslaktır**. Sahip işaretlemeden hiçbir satır karar değildir. "Evet" yalnız PVG tarafının kararıdır;
> "Ayrıca gereken onay" yazan satırlar o taraf onaylamadan uygulanmaz. Mevzuata dayanan ifadeler (ICH E2D, ICH E2B(R3),
> EU GVP Modül VI) **genel bilgidir, depoda kaynağı yoktur**; QPPV doğrulamadan kesinleşmez.
>
> **Etkisi:** paket değişir (revizyon 04). İnceleme 03 (UYGUN) ve dalda commit'li onay satırı (`approvals.tsv` satır
> 4, `3bdf52940`) bugünkü sürüme (`f5ca98e3`) bağlıdır; yeni sürüm için inceleme 04 ve yeni bir onay satırı gerekir.
> Eski onay satırının ne olacağı ayrıca karar ister (bkz. "Açık nokta").

## Ölçülen olgular

- Pakette ürün: `SuspectProductText`, isteğe bağlı, en çok 512 karakter serbest metin; ürün ana verisine bağ yok.
- Pakette bildiren: `ReporterType` (PVG seçenek listesi) ve `ReporterContactSummary` (en çok 256 karakter, kişisel veri,
  serbest metin); sistemdeki hiçbir kişi kaydına bağ yok.
- Pakette zaman: yalnız `ReceivedAtUtc` (alınma anı); "farkına varma anı" ve olay ülkesi, rapor türü, geçerlilik sonucu yok.
- Ürün ana verisi: MDM MOD-0290 Product / Item / SKU Master (Global Ürün, GSKU). CRM ziyaret raporu ürünü Global Ürün
  kimliğiyle tutuyor (`execution/domains/commercial-suite/work-packs/WP-VW-W4-report-v2.md` satır 43, 170).
- Doktorlar: CRM kişi kaydı (`services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/Contact.cs`); kimlik kaydında
  MOD-0150 Contact & Relationship Management "reserved / planned" (`execution/registries/module-id-registry.md` satır 268)
  — kod var, kayıt durumu kodla uyuşmuyor (CRM tarafının konusu).
- Çalışanlar / temsilciler: MOD-0288 Organization, Person & Position Directory (Platform, "done").

## Satırlar

### AL-1 — Ürün bağı

- **Öneri:** ürün listesi (bir vakada birden çok ürün olabilir). Her satır: rol (şüpheli / birlikte kullanılan), şirket
  ürünüyse MOD-0290 Global Ürün kimliği (`GlobalProductId`), bildirenin söylediği ad olduğu gibi ("as reported", her
  zaman), isteğe bağlı sunum (GSKU) ve lot numarası. Şirket dışı ya da tanınmayan ürün yalnız metinle girilir.
- **Ne zaman zorunlu:** kayıt açılırken değil; triyajda "geçerli vaka" sonucu için en az bir şüpheli ürün zorunlu.
- **Gerekçe:** serbest metin aynı ürünü eşleştiremez; sinyal (MOD-0234) ve raporlama (MOD-0233) ürün kimliği ister; CRM
  zaten Global Ürün kimliği gönderiyor. Oracle Argus'ta da şirket ürünü ürün sözlüğünden seçilir, bildirenin söylediği ad
  ayrıca saklanır (genel bilgi).
- **Ayrıca gereken onay:** MDM (MOD-0290) sahibi — PVG'nin Global Ürün ve GSKU okuma yetkisi ve okuma sözleşmesi. PVG
  MDM'ye yazmaz.
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: Evet (sahip, 2026-10-22). MDM (MOD-0290) okuma yetkisi ve sözleşmesi bekleniyor.

### AL-2 — Farkına varma anı ve şirket alıcısı

- **Öneri:** iki ayrı alan. (a) Farkına varma anı (`AwarenessAtUtc`, "day 0"): şirketten birinin asgari bilgiyi ilk
  öğrendiği an. (b) Şirket alıcısı: bunu öğrenen çalışan (MOD-0288 kişi kimliği). `ReceivedAtUtc` (PV'nin aldığı an)
  kalır. Kural: farkına varma anı ≤ alınma anı.
- **Gerekçe:** yasal saat şirketin herhangi bir çalışanının öğrendiği andan başlar (EU GVP VI, genel bilgi); CRM
  temsilcisinin ziyaret anı buraya yazılır. CRM talebinin 2. sorusunun cevabı bu alandır.
- **Ayrıca gereken onay:** QPPV — hangi zaman damgasının güvenilir sayılacağı (REQ-PVG-CRM-01, QPPV-02).
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: Evet (sahip, 2026-10-22), netleştirmeyle: vaka PV ekranında elle açılıyorsa şirket alıcısı varsayılan olarak kaydı açan PV kullanıcısıdır ve değiştirilebilir; bilgi önce başka bir çalışana (ör. temsilciye) geldiyse farkına varma anı onun öğrendiği andır. Zaman damgasının güvenilirliği QPPV doğrulamasını bekliyor.

### AL-3 — Olay ülkesi

- **Öneri:** olayın gerçekleştiği ülke, Platform referans verisindeki ülke listesinden seçilir; zorunlu, bilinmiyorsa
  "bilinmiyor" değeri.
- **Gerekçe:** hangi otoriteye, hangi sürede raporlanacağını belirler (E2B'de zorunlu alan, genel bilgi).
- **Ayrıca gereken onay:** yok (referans veri okunur).
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: Evet (sahip, 2026-10-22). "Bilinmiyor" seçeneği bulunur.

### AL-4 — Rapor türü

- **Öneri:** PVG seçenek listesi: kendiliğinden (spontaneous), çalışmadan (study), literatür, istenmiş (solicited — ör.
  hasta destek programı), diğer. Zorunlu.
- **Gerekçe:** süreler ve otoriteye gönderim kuralları türe göre değişir (E2B "type of report", genel bilgi). Saha
  temsilcisinin ziyaretinde duyduğu bildirim genellikle "kendiliğinden" sayılır — QPPV doğrulamalı.
- **Ayrıca gereken onay:** QPPV (liste değerleri).
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: Evet (sahip, 2026-10-22). Liste değerleri QPPV doğrulamasını bekliyor.

### AL-5 — Geçerlilik sonucu

- **Öneri:** triyajda dört asgari ölçüt işaretlenir: tanımlanabilir bildiren, tanımlanabilir hasta, şüpheli ürün, advers
  olay. Dördü de varsa "geçerli vaka"; eksikse "geçersiz — takip gerekli" ve hangi ölçütün eksik olduğu. Geçerli olmayan
  bildirim silinmez, kayıt olarak kalır.
- **Gerekçe:** ICH E2D'deki geçerli vaka tanımı (genel bilgi); AL-1'in "ürün triyajda zorunlu" kuralı buradan çıkar.
- **Ayrıca gereken onay:** QPPV.
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: Evet (sahip, 2026-10-22). QPPV doğrulamasını bekliyor.

### AL-6 — Bildiren kişi: otomatik mi, seçilir mi, elle mi?

- **Öneri: kaynağa göre üç yol, hepsi aynı kayıt biçiminde.**
  1. **CRM'den sistemle gelirse — otomatik.** Bildiren = CRM kişi kaydındaki hekim (kişi kimliği); şirket alıcısı =
     raporu gönderen temsilci (oturum açmış kullanıcının MOD-0288 kişi kimliği); kanal "saha ziyareti", kaynak "CRM",
     kaynak numarası = CRM rapor kimliği. Kullanıcı bunları elle girmez.
  2. **PV ekranında elle açılırsa — önce seçilir.** Bildiren alanı arama kutusudur: CRM kişileri (hekim / eczacı) ve
     kişi dizini (çalışan) içinde aranır. Bulunursa seçilir ve bağlanır.
  3. **Sistemde yoksa — elle girilir.** Hasta, hasta yakını, sistemde kaydı olmayan hekim: bildiren türü + en az bir
     tanımlayıcı (ad ya da baş harf, meslek, ülke, iletişim özeti) elle yazılır. Seçim zorunlu değildir; PV hiçbir
     bildirimi "kişi sistemde yok" diye reddetmez.
- **Kayıtta ne tutulur:** bağ varsa kaynak kişi kimliği (CRM kişi ya da MOD-0288 kişi) **ve** o anki bilgilerin anlık
  kopyası (ad / baş harf, meslek, ülke, iletişim özeti). Kaynak kayıt sonradan değişse ya da silinse vaka değişmez
  (GxP kaydı); takip için güncel iletişim gerekirse kaynak kayıt okunur. Kopya kişisel veridir: maskeleme (MOD-0019) ve
  denetim kuralları aynen uygulanır.
- **İki rolü karıştırma:** bildiren (olayı anlatan kişi: hekim, hasta) ile şirket alıcısı (öğrenen çalışan: temsilci)
  ayrı alanlardır. Temsilci bildiren değildir; yalnız kendi yan etkisini bildiren çalışan bildiren olur (tür:
  tüketici / çalışan).
- **Gerekçe:** otomatik doldurma yazım hatasını ve süreyi azaltır; seçim aynı hekimin vakalarını birleştirir (takip ve
  mükerrer kontrolü); elle giriş PV'nin hiçbir bildirimi kaçırmamasını sağlar. Veeva ve Argus'ta da bildiren alımda
  aranır, bulunamazsa elle girilir (genel bilgi).
- **Ayrıca gereken onay:** CRM (MOD-0150) sahibi — PVG'nin kişi okuma yetkisi ve sözleşmesi; Platform (MOD-0288) sahibi —
  kişi dizini okuma. PVG ikisine de yazmaz (XMC-001). CRM'den otomatik alım, REQ-PVG-CRM-01'deki sistemden sisteme alım
  sözleşmesinin parçasıdır ve operasyonel runtime onayına bağlıdır.
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: Evet (sahip, 2026-10-22), netleştirmeyle: elle girilen bildirende kayıt tamamlanınca kişi CRM'de bir kez aranır; bulunursa bağlanır (bağ + anlık kopya). CRM (MOD-0150) ve Platform (MOD-0288) okuma yetkileri bekleniyor.

### AL-7 — Sonraki modüllere bırakılanlar (yalnız adlandırma)

- **Öneri:** aşağıdakiler MOD-0230'a girmez; ilgili modülün paketine "gelecek gereksinim" olarak yazılır, onlar adına
  karar verilmez:
  - altı ciddiyet ölçütü (ölüm, hayatı tehdit, hastaneye yatış, kalıcı sakatlık, doğumsal anomali, tıbben önemli) —
    MOD-0231 Case Processing; MOD-0230'daki tek "ciddiyet" değeri triyaj içindir;
  - mükerrer vaka kontrolü — MOD-0231 (AL-6'daki kişi bağı buna temel olur);
  - yasal son tarih ve raporlama saati — MOD-0233 Reporting & Submissions;
  - E2B(R3) ile vaka alımı — MOD-0230'un ileride açılacak bir takip işi (FU), sahibin ayrı kararıyla;
  - takip (follow-up) — MOD-0231.
- **Ayrıca gereken onay:** ilgili paket sahipleri (MOD-0231/0233 sahipleri bugün TBD).
- **Sahip işareti:** [x] Evet  [ ] Hayır — not: Evet (sahip, 2026-10-22). Yalnız adlandırma; ilgili paket sahiplerinin kararı değildir.

## Açık nokta (ayrı karar)

- Dalda commit'li onay satırı (`approvals.tsv` satır 4, `3bdf52940`) bugünkü paket sürümünü onaylıyor. Paket değişirse
  bu satır ara bir sürümün onayı olarak main'e girer. Seçenekler: (a) satırı geri alan bir commit (`git revert`; tarih
  yeniden yazılmaz); (b) satır kalır, yeni sürüm için ikinci satır eklenir. Control Tower önerisi (a). **Sahip kararı
  (2026-10-22): a** — satır geri alan bir commit ile kaldırılır.

## İşaretten sonra (Control Tower yapar, her adımı ayrıca sorar)

1. İşaretli kaydı commit etmek.
2. Paketi `module-pack-author` ile güncellemek (revizyon 04).
3. İnceleme isteği 04, inceleme 04, yeni onay satırı; ardından tek PR.
4. "Ayrıca gereken onay" satırları için MDM, CRM ve Platform sahiplerine soru taslakları (gönderilmez).
