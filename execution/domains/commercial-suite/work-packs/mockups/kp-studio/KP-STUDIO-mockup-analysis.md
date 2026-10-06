# Bilgi Yolu Stüdyosu — mockup analizi (CT, 2026-09-30)

- **Kaynak:** `kp-studio-prototype.html` (kullanıcının yaptırdığı mockup).
- **Çözülmüş dosyalar:** `kp-studio-screens.decoded.html` (ekranlar + mantık), `kp-studio-page-component.decoded.html` (sayfa çizim bileşeni), `kp-studio-screen-texts.txt` (ekran metinleri).
- **Brief:** `../content-studio-v2/BRIEF-content-studio-v2.md` (v2).
- **Kararlar:** `SCMM-studio-knowledge-bridge-decision.md` §7, §8.

## Sonuç
**Uygun — temel olarak kabul.**
- Kapsam: brief'in 12 bölümünün 6'sı tam, 5'i kısmen karşılanmış; 1'i (çok dil / tema / erişilebilirlik) eksik. Bu eksik bir prototipte beklenen bir şey, uygulamada ürünün L10n / tema altyapısıyla gelir.
- Kararlarla uyum: set yok, yol türü yok, legal entity sahiplik ekseni değil, MLR sıralı, SoD var.
- **Düzeltilmesi gereken 8 nokta** ve **karar gereken 4 nokta** var.

## Ekranlar
Liste · çalışma alanı (8 sekme):
- Kurgu (dal kanbanı);
- Sayfa tasarımı (sayfa şeridi, blok paleti, tuval, özellikler, sayfa uyumu);
- Uyum kontrolü;
- MLR onayı (zaman çizelgesi, inceleyici görünümü: iğneli not + kanıt paneli);
- Çıktı ve yayın (HTML zip + PDF, sha256, ön koşullar, geri çek);
- Revizyonlar (sahada / onayda / çalışılan + fark);
- Saha önizleme (onaylı / taslak filigranı);
- Kullanım (yolculuklar, şablonlar, sayfa gösterim / süre).

Ek modallar: onaya gönder kontrol listesi, eski yol sihirbazı, ülkeye uyarla, adıma ekle, revizyon farkı, yeni yol, öneriler.

## Kapsama (brief §)
| § | Durum | Not |
|---|---|---|
| 1 Liste | ✅ | Ülke filtresinde TM, dil filtresinde tk / be eksik |
| 2 Üst bilgi | ✅ | |
| 3 Kurgu | ◐ | İddia kartında iddia **metni** yok (yalnız kod / ad / sürüm) |
| 4 Tasarımcı | ◐ | Etkileşimde yalnız açılır bölüm; şablon uygulama sahte; hizalama yok; kilitli bölge yalnız altbilgi |
| 5 Kontroller | ◐ | Logo ve font kuralı yok; görsel kütüphane seçici yok |
| 6 MLR / yayın | ✅ | MLR adım tarihleri eksik |
| 7 Önizleme | ✅ | |
| 8 Sahiplik | ◐ | Marka kiti yönetim yeri görünmüyor |
| 9 Dil / tema / erişilebilirlik | ✗ | Yalnız TR; koyu tema yok; RTL yarım; aria yok; kanban yalnız fare |
| 10 Yetki | ◐ | Yetkisize liste çiziliyor (UAS-001); marka kiti / şablon yetkisi yok |
| 11 Örnek veri | ✅ | |
| 12 Öneriler | ✅ | 7 öneri |

## Düzeltilecekler (uygulamada zorunlu)
1. **UAS-001:** yetkisiz kullanıcıya liste de çizilmez.
2. **Zincir uyumu:** adım başka dala sürüklenemez, zincir adımı "çıkarılamaz". Kurgu zincirin iskeletini değiştirmez, yalnız adımlara öğe yerleştirir. Çıkarılan adımın sahipsiz sayfası oluşmamalı.
3. **Ülke değişimi:** ülke değişince iddialar o ülkenin sürümüne göre yeniden denetlenir. Kilit metni sabit "Türkiye / Türkçe" olmaz.
4. **Ham kodlar:** "TR · Slayt", "KA çeviri yok", "GE v1.0", "VRL-0112" gibi kodlar yerine ad ve etiket gösterilir (FE-2'nin ülke / dil adları).
5. **Yol kodu öneki `BY-`:** Belarus ISO koduyla çakışıyor. Kod öneki `KP-` kalır.
6. **SoD kişiye göre** (role göre değil): gönderen, aynı kişi olarak hiçbir MLR adımını onaylayamaz.
7. **Marka kiti:** logo ve font kuralları eklenir; görseller onaylı kütüphane seçicisinden gelir.
8. **Arayüz:** renkler tema değişkeniyle, 7 dil, RTL (sayfa bileşeni dahil, mantıksal CSS), klavye erişimi (ürün altyapısı).

## Karar gerekenler
- **K1 · Onay kararı nerede verilir?**
  - Mockup Onayla / Reddet'i yolun inceleyici görünümüne koymuş; brief kararı Görev Merkezi'ne vermişti.
  - Öneri: **tek kanal = iş akışı.** İnceleyici görünümündeki düğmeler aynı MOD-0023 onay çağrısını yapar; Görev Merkezi iş öğesi bu görünümü açar.
  - Bu, REQ-WCN-01'deki "onay yorumu yazılamıyor" ve "adım adı görünmüyor" eksiklerini de bizim tarafta çözer.
- **K2 · Güvenlilik bilgisi bloğu:** mockup güvenlilik bilgisini bir iddia gibi modellemiş (`CLM-ALMIBA-S01`).
  - Öneri: iddia değil, **ülke bazında onaylı güvenlilik metni** (KÜB / KT'den).
  - Yasal blok gibi yönetilir, sayfaya kilitli gelir.
- **K3 · Yeni sürümde ülke / dil:** v1.0 TR yayındayken v2.0 taslağında ülke değişebiliyor.
  - Öneri: **ülke ve dil yolun kimliğidir, sürümler arasında değişmez.** Başka ülke için "Ülkeye uyarla" yeni bir yol üretir.
- **K4 · Eski yol sihirbazı:** serbest metni otomatik olarak iddiaya bağlıyor.
  - Öneri: otomatik bağlama yalnız **öneri** olsun; kullanıcı her birini onaylasın. Riskli.

## "Öneri"ler (tasarımcının)
| # | Öneri | Değerlendirme |
|---|---|---|
| 1 | İddia arama | ✅ Al (ülke filtresiyle) |
| 2 | AI yerleşim önerisi | ⏸ Sonraya (kapsam dışı) |
| 3 | Sayfa bazlı yorum akışı (çözüldü / taşınır) | ✅ Al (K1 ile birlikte) |
| 4 | Erişilebilirlik kontrolü | ✅ Al (uyarı düzeyinde) |
| 5 | Ülke / dile uyarlama | ⏸ Faz 2 (K3 ile uyumlu) |
| 6 | Gönderimde değişiklik özeti | ✅ Al (fark zaten var) |
| 7 | Saha etkisi uyarısı (<10 sn, bölge müdürüne rapor) | ⏸ Sonraya (yeni politika) |

## Korunacak UX fikirleri
- Sürüm ile revizyon ayrımının açıklanması ve üçlü durum kartı.
- Uyum satırından bloğa gitme.
- "Adıma ekle"de gerekçeli "Eklenemez".
- Engelleyici varken gönderimi kapatan kontrol listesi.
- İğneli not + kanıt paneli.
- Çıktılarda aynı parmak izi.
- Taslak filigranı.
- Yayın ön koşulları.

## Sadeleştirme
- Sekme + ana düğme + ⋮ + "sıradaki adım" bandı + tanıtım metni aynı anda çok yoğun. Tanıtım metni ilk kullanımda gösterilsin, sonra gizlenebilsin.
- Başlıktaki üç durum (Taslak / Rev 3 / Sahada v1.0) tek bir durum çizgisinde birleşsin.

---

## v2 kontrolü (2026-09-30) — `kp-studio-prototype-v2.html`
Sayfa bileşeni değişmedi; ekran ve mantık şablonu +3,9 KB. CT, v1 ↔ v2 farkını çıkardı.

**Karşılananlar ✅**
- **K1 tek kanal:** İnceleyici görünümünde "Tek kanal: kararınız Görev Merkezi'ndeki onay iş akışına (MOD-0023) yazılır…" notu var. Gönderen için karar alanı kapalı. Açık bir tur varken yeni revizyon gönderilemiyor (`openRound` engeli).
- **K2 güvenlilik:** Artık iddia değil. Ayrı `SAFETY` kaydı ("Onaylı güvenlilik metni · ALMIBA × Türkiye × Türkçe", `GBM-ALMIBA-TR-TR v1.1`), kilitli blok. Uyarlama tablosunda ülke bazında ayrı satır.
- **K3 ülke / dil kimlik:** Bağlam şeridindeki düzenleme kalemi kilit ikonu oldu ("oluşturulurken seçilir, sürümler arasında değişmez. Başka ülke için Uyarla."). Ülke / dil seçimi ve dil çatışma bandı kaldırıldı. "Ülkeye uyarla" artık **yeni kodlu ayrı yol** üretiyor.
- **K4 sihirbaz:** Otomatik bağlama kaldırıldı. Her iddia önerisi için Kabul / Reddet var. Karar verilmeden "İleri" pasif ("Her iddia önerisi için karar verin").
- **#2 zincir iskeleti:** Adım sürükleme, adım çıkarma ve en az / en çok değiştirme kaldırıldı ("Dal ve adımlar zincirden gelir; eklenip çıkarılamaz, taşınamaz"). Öğeler yalnız **kendi adımı içinde** sürüklenerek sıralanıyor.
- **#3 ülke değişimi:** K3 ile gereksizleşti (ülke değişmiyor).
- **Ek:**
  - Geri çekmede `path_in_use` hatası, kullanan aşamaların adlarıyla.
  - Uyum satırlarına CRM hata kodu eklendi.

**Karşılanmayanlar → uygulamada yapılacak (yeni mockup turu gerekmez)**
| # | Kalan | Nerede ele alınır |
|---|---|---|
| 1 | Yetkisiz rol listeyi görüyor (UAS-001) | KP-UI-1 (sayfa izin kapısı, mevcut desen) |
| 4 | Ham kodlar ("TR", "GE v1.0", "VRL-…") | KP-UI-1 / 2 (FE-2 ülke / dil adları, resx etiketleri) |
| 5 | Yol kodu öneki `BY-` | KP-1 (kod `KP-`) |
| 6 | SoD role göre (`isSubmitter = role==='editor'`) | KP-2 (kişi bazında; MOD-0023 SoD + CRM kontrolü) |
| 7 | Logo / yazı tipi kuralı yok | KP-5 marka kiti + KP-UI-3 |
| 8 | Kurgu kartında iddia metni yok | KP-UI-1 |
| — | Filtrelerde TM / tk / be eksik; RTL / tema / aria | KP-UI-1 (ürün L10n / tema altyapısı) |

**Yeni not:** Uyum satırlarında `component_not_published` gibi **ham hata kodu** görünüyor. Kural: kullanıcıya kendi dilinde metin; kod yalnız ikincil bilgi (tooltip ya da destek ayrıntısı) olarak gösterilebilir. KP-UI-2'de uygulanır.

**Sonuç:** v2 **kabul**. Kararlar (K1–K4) ve iskelet kilidi mockup'a işlendi. Kalan maddeler uygulama paketlerinde.
