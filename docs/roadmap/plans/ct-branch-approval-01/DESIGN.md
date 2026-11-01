# Dal onayı — onay PR'ı kalkar, ikinci insan modül PR'ına taşınır (tasarım + sabotaj planı)

Durum: **sahip kararı 2026-11-01 ("ok uygun, şimdi yapalım; PR açıp birleşince tüm CT'ler buna geçsin")**, CT-Setup
sohbetinde, düğmeyle onaylandı. Yürürlük: bu tasarımı taşıyan kural PR'ı `origin/main`'e girince; o güne kadar eski kural
(onay satırı PR ile main'e girer) geçerlidir. Kayıt: ledger S-115…S-120 (`docs/records/audits/2026-10/ct-setup-test-01/FINDINGS.md`).

## Sorun (ölçüldü, 22–31 Ekim, 113 birleşen PR)

| PR türü | Sayı | Açılış→birleşme orta | Ortalama | En uzun |
|---|---|---|---|---|
| Kod | 33 | 1,5 sa | 11,5 sa | 71 sa |
| Onay satırı + kayıt | 27 | 3,0 sa | 9,0 sa | 30 sa |
| Yalnız onay satırı | 6 | 0,3 sa | 7,1 sa | 24 sa |

113 PR'ın 66'sını sahibin hesabı açtı; 56'sı modülsüz kural/kayıt PR'ı. Zincir seri: paket → inceleme → onay PR'ı →
birleşme → dağıtım → kod → kod PR'ı. 20 Ekim'den sonra açılan 8 modülün 5'i koda gelmedi (ilk ürün kodu: PVG 3 gün, İş
akışı 8 gün; Yetki, Zaman, İzin, Çalışan kaydı, Toplantılar: yok). Sahip: "eskiden 1 modül 1 PR çalışıyordum, daha hızlıydı."

## Karar (sahip, 2026-11-01)

1. **Dal onayı.** Paket onay satırı ve inceleme satırları CT dalına yazılır; dağıtım kapısı ve kabul kaydı bunları
   **daldan** kabul eder. Onay için PR açılmaz, Beste beklenmez.
2. **İkinci insan modül PR'ında.** Modül (iki haftayı aşarsa dalga) bitince tek PR: kod + kayıtlar + onay ve inceleme
   satırları + kabul kayıtları + kapanış belge paketi. Bu PR'ı listedeki, yüksek riski onaylayabilen (`high = evet`)
   ve satırı yazanın kişisi olmayan **başka bir kişi** birleştirir. Araç bu insan adımını satır main'e girince ölçer
   (bugünkü ölçüm yer değiştirir, kaybolmaz). Onaylayan ile birleştiren aynı kişi olmak zorunda değildir (S-41 kalkar).
3. **Modül başı tek küçük PR kalır:** kuyruk kayıt satırı yalnız `origin/main`'den okunur (iki kulenin aynı modülü
   almasını önler); bu tasarım onu değiştirmez.
4. **Resmî inceleme** PC başındayken ayrı bir sohbette, uzaktayken kule içinde alt ajanla yapılır; inceleyen kuleyle
   konuşup düzeltme yapabilir; "temiz" deyince inceleme satırı dala yazılır. Sonra sahibe tek düğme: direkt kabul ·
   bağımsız son bakış (geçmişsiz ajan, yalnız inceleme ZIP'i) · Codex. Araç riski HIGH ölçüyorsa bağımsız son bakış
   kendiliğinden koşar, sahip "atla" derse atlanır; LOW'da varsayılan direkt kabul.
5. **Sahip sözü modül başına tek: "onayla".** Paketi onaylar ve tüm dilimleri sırayla başlatır. Kule bir sonraki dilim
   için yeni söz beklemez. Zorunlu duraklar yalnız: başka modülün dosyası (XMC-001), kapsam değişikliği, yıkıcı git
   işlemi, kural / `.antigravity` değişikliği, dışarıya mesaj, kulenin kendi çözemediği engel. Takip sayfası kule
   açılışında şablondan açılır ("sayfa-ac" sözü kalkar); modül sonu PR'ını kule açar (açmak geri alınabilir).
6. **Kapanış belge paketi** (docs-organization §3.1): kullanıcı kılavuzu, API anlatısı, servis README, ADR, sürüm notu,
   mimari denetim raporu; kule alt ajanlarla üretir, modül PR'ına koyar.
7. **Tür kelimesi:** her iş satırı (kısa haber, tam rapor Parçalar, takip sayfası) şu sekizden biriyle başlar: Belge ·
   Arka uç · Ekran · API · Geçit · Test · Kural · Karar. İlerleme satırı "kod N · belge M" sayısını taşır.

Kabul edilen bedel (sahip kararı): onay satırındaki "onaylayan" alanı, satır main'e girene kadar GitHub'da doğrulanmaz;
sahibin "onayla" sözü kule sohbetinde ve satırın `requested_by` / oturum alanlarındadır. Kod yine ikinci kişi olmadan
main'e giremez (dal koruması). Kule öncesi dönemle aynı sınır; üstüne paket, inceleme ve sabotaj testi kalır.

## Araç değişikliği

### `scripts/pack_approval.py`

- `Ctx(root, commit, path, deferred=False)`; `evaluate(root, ref, path, deferred=False)`.
- `deferred=True` ("dal onayı" okuması; dağıtım ve kabul anı): `judge` etkili HIGH için `main_merge` "henüz ana
  çizgide değil" diyorsa **hata yerine not** yazar: `insan adımı: beklemede — modül PR'ı başka bir kişi tarafından
  birleştirilince ölçülür`. Yine de ölçülür: onaylayan `approvers.tsv`'de (ana dal sürümü, `approvers_at(root, ctx.main)`)
  ve `high = evet`; onaylayan kişisi ≠ yazan kişisi; ENGEL kabulü onaylayana ait; kart özeti; paket özeti; inceleme
  onaydan önce; dilim tablosu. Satır **zaten main'deyse** sıkı yol aynen uygulanır (merge'ün PR merge'ü olması, açan ≠
  birleştiren, birleştiren listede ve `high = evet`, birleştiren ≠ satır yazarı). Doğrudan main'e yazılmış satır her iki
  modda da geçersiz.
- `review_problems(..., deferred=False)`: `deferred=True` ve satır henüz main'de değilse `human_step` hatası nota döner;
  diğer her denetim (sonuç, inceleyen, oturumlar farklı, bulgu dosyası özeti, açık ENGEL, UYGUN DEĞİL, yalnız ekleme)
  aynen kalır. Satır main'de ama insan adımı başka nedenle yoksa (doğrudan push, aynı kişi) yine hata.
- `pr_person_check(root, merge, approver=None, author_key=None)`: "onaylayan birleştirmeli" koşulu (S-41) kalkar.
  Yerine `high: bool` parametresi: HIGH satırda birleştiren `high = evet` olmalı. Açan ≠ birleştiren ve birleştiren ≠
  satır yazarı (`author_key`) kalır. `judge` onay satırı için `author_key = person(row["author"])` geçirir.
- `check <pack> [--ref REF] [--deferred]`: komut satırı bayrağı; varsayılan sıkı (ana dal) okuma. `history` değişmez.
- Belge: modül docstring'ine "Dal onayı (S-115, sahip kararı 2026-11-01)" paragrafı; CONTRACT.md'ye tarihli ek bölüm
  (eski metin silinmez).

### `scripts/prompt_gate.py`

- Dağıtım ve `--prepare`: `pa.evaluate(where, commit, authority, deferred=True)`.
- `review_negatives(pa, root, main, commit, path, data, deferred=False)`: `deferred=True` iken inceleme satırları
  **`commit`'te** (CT'nin ref'i; main'i birleştirmişse main satırları da oradadır) okunur, `review_problems(...,
  deferred=True)`; yalnız-ekleme izi `append_only_problems(root, commit, REVIEWS, main)` ile dal–main arasında ölçülür.
  Mesaj "main'de geçerli inceleme yok" → "dalda ya da main'de geçerli inceleme yok". Q2/Q4 mantığı değişmez.
- Kuyruk kayıt satırı (`ct_queue.load_for_dispatch`) yine yalnız `origin/main`'den okunur.

### `scripts/acceptance_record.py`

- Q3: paketin bu baytları **CT ref'inde** (`at_full`) ve `pa.evaluate(root, at_full, path, deferred=True)` çıkış 0; inceleme
  örtüsü `review_negatives(..., deferred=True)`. Bugünkü "main'de" okuması, satır main'e girince aynı sonucu verir.

## Sabotaj testleri (öz testler; hepsi kırmızı olmalı, guard kaldırılınca yeşile dönmeli)

| # | Sabotaj | Beklenen |
|---|---|---|
| 1 | Dalda HIGH onay satırı, onaylayan listede yok | deferred: ENGEL |
| 2 | Dalda HIGH onay satırı, onaylayan = yazan (başka hesabı) | deferred: ENGEL |
| 3 | Dalda onay satırı, paket sonradan değişti | deferred: "paket onaydan sonra değişti" |
| 4 | Dalda onay satırı, inceleme satırı yok | deferred: "inceleme yok" ENGEL |
| 5 | Dalda inceleme satırı UYGUN DEĞİL ya da açık ENGEL | deferred: ENGEL |
| 6 | Dalda inceleme satırı `reviewer_session == author_session` | deferred: ENGEL |
| 7 | Dalda onay/inceleme satırı silinmiş ya da değiştirilmiş (yalnız ekleme ihlali) | deferred: ENGEL |
| 8 | Satır doğrudan main'e push edilmiş (merge yok) | sıkı ve deferred: ENGEL |
| 9 | Satır main'e satır yazarının başka hesabının birleştirdiği PR ile geldi | sıkı: ENGEL |
| 10 | Satır main'e `high = hayır` kişinin birleştirdiği PR ile geldi (HIGH satır) | sıkı: ENGEL |
| 11 | Satır main'e açan = birleştiren PR ile geldi | sıkı: ENGEL |
| 12 | Sıkı okuma (deferred=False) dalda kalan HIGH satır | ENGEL (bugünkü davranış korunur) |
| 13 | Yeşil: dalda HIGH onay + temiz inceleme, onaylayan listede/high, ≠ yazan | deferred: çıkış 0, "insan adımı: beklemede" notu |
| 14 | Yeşil: satır main'e, onaylayan ≠ birleştiren, birleştiren listede/high/≠ yazar | sıkı: çıkış 0 (S-41 kalktı) |
| 15 | prompt_gate: inceleme yalnız dalda, UYGUN | dağıtım geçer |
| 16 | prompt_gate: inceleme yalnız dalda, UYGUN DEĞİL | dağıtım durur |
| 17 | acceptance_record: onay ve inceleme yalnız dalda | kabul geçer; satır main'e girince de geçer |

Yeniden oynatma: `origin/main`'deki bütün onay satırları sıkı okumayla bugünkü sonucu birebir vermeli (S-41 kalktığı için
yalnız "onaylayan merge etmedi" ENGEL'leri geçebilir; sayısı kayda yazılır).

## Yürürlük ve geçiş

Araç + öz testler → geçmişsiz Claude alt ajanıyla bağımsız inceleme (araç farkı + kart) → tek kural PR'ı (sahip
isteğiyle haftalık pencere beklenmez; S-100/3 istisnası: "sahip açıkça istedi") → Beste birleştirir → Yönetici
üzerinden 8 kuleye duyuru: yürüyen modüllerde açık onay PR'ları eski yolla biter, yeni dilimler dal onayına geçer.
Ölçüm 2026-11-14: modül başına PR sayısı, onaydan ilk koda geçen süre, inceleme turu sayısı.
