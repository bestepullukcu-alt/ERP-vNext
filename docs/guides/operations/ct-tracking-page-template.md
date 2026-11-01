# Kule iş takip sayfası — ortak şablon (S-119)

Her Control Tower'ın sahibe verdiği "iş takibi" sayfasının tek kaynağı:
`docs/guides/operations/ct-tracking-page-template.html`. Sahip kararı 2026-11-01 (S-119): kuleler kendi sayfasını
yazmaz; bu şablonu olduğu gibi yayınlar, yalnız depodaki satırları doldurur. Kaynak tasarım: Çalışan kaydı kulesinin
sayfası (MOD-0251); modülden bağımsız hali + tür etiketi + kodsuz satır + depodan gelen başlık.

## 1. Amaç

- Sahip tek sayfada görür: sırada ne var, hangi karar kimde, ne bitti, ne duraklatıldı; üstte dilim cetveli ve
  `kod N · belge M · karar K` sayımı.
- Sayfa **salt okunurdur**: kule yazar, sahip okur. Sahip değişiklik isteğini kule sohbetine yazar (cümle sayfada da
  durur: "Bir değişiklik isterseniz `<kule>` sohbetine yazın").
- Sahibe giden her mesajdaki **Biten / Çalışan** satırları sayfadaki satırlarla aynı **türü** taşır (örnek:
  `Biten · Ekran: çalışan listesi filtreleri`); mesaj ile sayfa birbirini tutmazsa sayfa güncellenmemiştir.

## 2. Veri modeli

### `items` koleksiyonu — her belge bir iş satırı

Belge kimliği (`id`) kalıcı ve kısadır: kod varsa kodun küçük harfi (`hcm-emp-07`), yoksa `k-03` gibi.

| Alan | Zorunlu | Değer |
|---|---|---|
| `id` | evet | belge kimliğiyle aynı; değişmez |
| `code` | hayır | görünen kod (`HCM-EMP-07`); varsa başlığın yanında küçük mono etiket, yoksa etiket yok |
| `title` | evet | sade başlık; sahip dili, kod adı değil |
| `detail` | evet | bir iki cümle: ne, neden, ne bekleniyor |
| `type` | evet | şu sekizden biri, aynen: `Belge` · `Arka uç` · `Ekran` · `API` · `Geçit` · `Test` · `Kural` · `Karar`; boşsa satırda "tür yok" görünür |
| `status` | evet | `acik` · `bekliyor` · `bitti` · `duraklatildi` |
| `section` | evet | `simdi` · `kararlar` · `fazlar` · `takipler` · `baskasi` · `ortak` · `duraklatilan` (sırası bu; başka değer görünmez) |
| `order` | evet | bölüm içi sıra (sayı); bitenler kendiliğinden sona gider |
| `slice` | hayır | dilim kodu (`S3`); yan sütunda küçük etiket |
| `waitingOn` | hayır | kimi/neyi bekliyor (`Sahip kararı`, `Codex incelemesi`, `PR #312`) |
| `evidence` | hayır | kanıt: dosya yolu`@`commit, PR, kayıt dosyası; mono yazılır |
| `doneAt` | hayır | `YYYY-AA-GG`; yalnız `bitti` satırında, pil üstünde tarih olarak görünür |

Sayım: **kod** = Arka uç + Ekran + API + Geçit + Test; **belge** = Belge + Kural; **karar** = Karar.

### `meta/info` belgesi — başlık, künye, cetvel

| Alan | Zorunlu | Değer |
|---|---|---|
| `title` | evet | sayfa başlığı ve sekme adı (`Çalışan Kaydı İş Takibi`); gelmeden önce "İş Takibi" yazar |
| `block` | evet | modül adı, başlığın altında (`MOD-0251 Çekirdek İK / Çalışan Ana Kaydı`) |
| `tower` | evet | kule sohbetinin adı (`Çalışan kaydı`); "… `<kule>` sohbetine yazın" cümlesine girer |
| `branch` | evet | kule dalı |
| `head` | evet | kule dalının son commit'i (kısa sha) |
| `mainAt` | evet | ana daldan alınan son commit (kısa sha) |
| `updatedAt` | evet | `YYYY-AA-GG`; künyede "Son güncelleme" |
| `accepted` | evet | kabul sayısı (sayı); cetvel başlığında "N kabul" |
| `slices` | evet | `[{code, name, state, done}]`; `done: true` yeşil, `state` "Sırada" ile başlıyorsa vurgulu; `state` serbest metin (`Bitti 10-28`, `Sırada · prompt hazır`) |

## 3. Yayınlama — kule açılışında, bir kez

1. `artifact-capabilities` becerisini yükle; şablonu Artifact aracıyla **değiştirmeden** yayınla:
   `file_path` = bu HTML, `icon: "list"`, `description: "<MOD> <modül adı> iş takibi"` (galeride ayırt etmek için),
   `capabilities: {db: {rules: [{path:"items", read:"view", write:"owner"}, {path:"meta", read:"view", write:"owner"}]}}`.
2. Sayfa özel başlar. Bağlantıyı kule sohbetinde sahibe ver; paylaşımı sahip açar.
3. İlk satırları ArtifactData `batch` ile yaz: önce `meta/info`, sonra `items` belgeleri (bir çağrı).
4. Sayfa kaynağı bir daha yayınlanmaz; yalnız satırlar güncellenir. Şablon değişirse bu dosyaya PR açılır ve kuleler
   aynı `url`'ye yeniden yayınlar (satırlar depoda kalır, silinmez).

```json
{"action":"batch","url":"<sayfanın claude.ai bağlantısı>","writes":[
  {"op":"set","collection":"meta","doc_id":"info","data":{"title":"Çalışan Kaydı İş Takibi","block":"MOD-0251 Çekirdek İK / Çalışan Ana Kaydı","tower":"Çalışan kaydı","branch":"chore/ct/hcm-emp-01","head":"3a607547e","mainAt":"231d840d0","updatedAt":"2026-11-01","accepted":2,"slices":[{"code":"S1","name":"Kayıt","state":"Bitti 10-28","done":true},{"code":"S2","name":"Liste","state":"Sırada · prompt hazır","done":false}]}},
  {"op":"set","collection":"items","doc_id":"hcm-emp-07","data":{"id":"hcm-emp-07","code":"HCM-EMP-07","title":"Çalışan listesi filtreleri","detail":"Birim ve durum filtresi; sayfalama ortak liste bileşeninden.","type":"Ekran","status":"acik","section":"simdi","order":1,"slice":"S2","waitingOn":"","evidence":"","doneAt":""}}
]}
```

## 4. Güncelleme anları

- **Her karar, prompt, kabul ve PR'dan sonra**, sahibe mesaj gitmeden önce: ilgili satırın `status` / `waitingOn` /
  `evidence` / `doneAt` alanı ve `meta/info` (`updatedAt`, `head`, `mainAt`, `slices`, `accepted`). Tek `batch`; var olan
  her belge son okunan `version` ile (`if_version`) yazılır, yalnız yeni belgede atlanır. Belge o arada değiştiyse yazma
  düşer; yeniden okunur, üstüne yazılmaz.
- Yeni iş sayfaya girerken `type` ve `section` seçilir; "tür yok" bırakılmaz.
- Sahip kararı bekleyen iş `kararlar` bölümünde `bekliyor` + `waitingOn: "Sahip kararı"`; karar gelince satır kalır,
  durumu değişir.
- Başka sahibin işi (XMC-001) `baskasi` bölümünde; kule onu bitirmez, yalnız izler.

## 5. Yapılmaz

- Modül, kule ya da başlık adını HTML'e yazmak; bunlar yalnız `meta/info`'dan gelir.
- `write` kuralını `owner` dışına açmak; sayfadan yazma yoktur.
- Satır silmek: biten `bitti`, vazgeçilen `duraklatildi` + `detail`'de neden.
- Satır güncellemek için sayfayı yeniden yayınlamak.
- Sahibe mesajda sayfada olmayan bir iş ya da sayfadakinden farklı bir tür yazmak.
