# Şablon — iş süreci (tek sayfa, süreç başına bir kez)

Yer: `execution/domains/<alan>/is-surecleri/SUR-<ALAN>-<nn>-<kısa-ad>.md`. Klasör ilk süreç dosyasıyla birlikte açılır
(önceden boş klasör açılmaz). Alan kısa adı `AGENTS.md` §9'dadır; orada olmayan alan için önerilen kısa ad PR'da sorulur.
Kimlik `SUR-<ALAN>-<nn>`: `<nn>` alanın klasöründeki sıradır (klasörde süreç yoksa 01); klasöre bakmadan numara uydurulmaz (Ç27).
Alan kısa adı bilinmiyorsa önerilen ad yazılır ve PR'da sorulur; `SUR-TBD` kullanılmaz (paket künyesi onu uyarır).

## Kullanım notu
- **Kim yazar:** `ees-is-analisti` taslağı çıkarır; hat açılınca, modül kimliği ön kontrolüyle aynı anda ve paket taslağıyla aynı
  dalda. Karar kişinindir.
- **Ne zaman:** yalnız o süreç için henüz dosya yoksa — süreç başına bir kez, modül ya da takip paketi başına değil.
  **Süreç varsa yazılmaz:** paket künyesinde `business_process: SUR-… adım a-b` ile atıf yapar. **Teknik modül** (iş süreci olmayan
  altyapı: kayıt kodu, ağ geçidi, kimlik) `yok — teknik: <gerekçe>`; **takip paketi** üst paketin sürecine atıf yapar ya da
  `yok — takip paketi: üst paketin süreci` yazar; ikisi süreç yazmaz. Var olan paketlere geriye dönük süreç yazılmaz.
- **Onay:** ayrı kapı yok. Süreç, ona ilk atıf yapan paketle **aynı PR'da** gelir ve paketi onaylayan kişinin onayıyla geçer.
  İlk dönemde eksik süreç yalnız uyarıdır (`scripts/pack_contract_check.py`, U1).
- **Boyut:** en çok 90 satır ve 9 KB; en çok 12 adım, 12 istisna, 4 uçtan uca senaryo. Daha uzunsa iki süreçtir; ayrı
  tetikleyicisi olan akış (zamanlanmış hatırlatma, dönem raporu) ayrı süreçtir.
- **İçerik:** ne yapılacağı, nasıl yapılacağı değil: ekran, uç nokta, tablo, sınıf adı yazılmaz. Kullanıcının gördüğü sonucu
  değiştiren kural (hangi saat diliminin günü, hafta nasıl sayılır, süre nasıl yuvarlanır) iş kuralıdır, buraya yazılır. Süreçte
  yalnız kullanıcının ya da işin gördüğü istisna durur; teknik istisna (eşzamanlı yazma, kanca hatası) paketin kabul ölçütüdür.
- **Senaryo → ölçüt:** her `E2E-k` satırı, senaryonun bütün adımlarının teslim edildiği ilk dilimin paketinde bir zorunlu
  kabul ölçütü olur: `AC-n (zorunlu) — SUR-…/E2E-k: … · dilim: DL-n · test: uçtan uca`. Birden çok modüllü süreçte senaryo
  testi ancak hepsi ana dala girince "geçti" sayılır (H17).
- **Değişirse:** paket kendiliğinden değişmez; fark yazılır, süreci kullanan paketlerin sahiplerine sorulur (onaylı sürüm
  değişmeden kod onu izler). Süreç değişince `surum` artar; kalkınca `durum: emekli`.
- **Takip paketi:** üst paketin süreci varsa atıf yapar, yeni süreç yazmaz. Üst paketin süreci yoksa takip paketi o sürecin ilk
  paketidir: süreç bu paketle aynı PR'da yazılır (geriye dönük yazım değil, bu işin sürecidir).
- **Açık sorular:** süreçteki açık soru, o süreci anan paketin bölüm 7'sinde bir açık karar satırıdır.
- **Rol adı** kullanılır, kişi adı değil. Blueprint'te olmayan modül uydurulmaz (aday olarak yazılır).

## Şablon (kopyala-doldur; köşeli parantezler doldurulur, açıklama satırları silinir)

```markdown
---
id: SUR-<ALAN>-<nn>
ad: <süreç adı, iş dilinde>
alan: <alan klasör adı>
sahip: <rol adı — kişi adı değil>
durum: taslak | onaylı | emekli
surum: 1
blueprint: <Blueprint_Data "Capability Group" · Module Pages sayfa adları>   # bilinmiyorsa TBD
moduller: [<MOD-nnnn | CAND-… | adlandırılmış yüzey>, ...]   # sahip modül ilk sırada
---

# SUR-<ALAN>-<nn> — <süreç adı>

**Amaç.** <Bir cümle: süreç bittiğinde iş için ne doğru olur.>
**Tetikleyici.** <Süreci ne başlatır: bir kişinin isteği, bir tarih, başka bir sürecin sonucu.>
**Bitiş.** <Süreç hangi durumda biter; olası bütün son durumlar.>

## Roller
| Rol | Ne yapar | Hangi izinle (iş dilinde) |
|---|---|---|

## Adımlar
| # | Rol | Ne yapar | Girdi | Çıktı / oluşan kayıt | Modül |
|---|---|---|---|---|---|
| 1 | | | | | |

## İş kuralları
- K1. <Adımlardan bağımsız her zaman doğru kalan kural.>

## İstisnalar
| # | Hangi adımda | Durum | Ne olur |
|---|---|---|---|
| X1 | | | |

## Uçtan uca senaryolar
| # | Senaryo | Başlangıç verisi | Beklenen son durum |
|---|---|---|---|
| E2E-1 | <mutlu yol> | | |

## Açık sorular
- <yalnız süreçle ilgili; teknik soru pakete gider>
```

Uzunluk kılavuzu: amaç-tetikleyici-bitiş 3 satır, roller 2–5, adımlar 4–12, kural 2–8, istisna 2–12, senaryo 1–4.
