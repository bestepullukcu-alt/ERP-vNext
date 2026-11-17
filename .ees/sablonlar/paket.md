---
id: MOD-<nnnn>[-FU<nn>]
name: <modülün adı>
domain: <alan klasör adı>
service: <servis>
shell: tenant | platform
golden_reference: slim | compact
status: draft
# bugünkü öteki alanlar aynen: data_mode, entity_base, owner (rol adı), branch, started, target, form_field_count, parent, adr …
pack_standard: kisa-v1
plan_row: <plan sürümü> · <MVP kimliği> · MUST|SHOULD|COULD
business_process: SUR-<ALAN>-<nn> adım <a-b>
conditional: []
---
> **Şablon notu (sil).** Standart: `.ees/standartlar/paket-standardi.md`. Başlıklar ve sıra değişmez; `<…>` alanları doldurulur;
> kullanılmayan satır silinir. `plan_row` / `business_process` için değer uydurma: `plan satırı yok — <neden>`,
> `yok — teknik: <gerekçe>`, `yok — takip paketi: üst paketin süreci`. Geçmiş ve kararlar `kararlar/<ID>-<kısa-ad>.karar.md`'ye gider.

## 1. Ne ve neden
<Amaç, bir cümle.> Roller: <rol, rol>. Süreç SUR-<ALAN>-<nn> adım <a-b>. <Servis sapması varsa bir cümle + adr.>

## 2. Kapsam
- Kapsam: <…>
- Kapsam dışı: <…> → <takip paketi | başka modül | gelecek MVP>

## 3. Bağlar
| Kaynak | Atıf | Giren | Girmeyen |
|---|---|---|---|
| Süreç | SUR-<ALAN>-<nn> | <adımlar, kurallar K, istisnalar X, senaryolar E2E> | <paket dışı adımlar → dilim ya da paket> |
| Plan | <sürüm · MVP kimliği · öncelik> · "<yetenek, tek cümle>" | <…> | <…> |
| Üst paket / var olan kod | <MOD-nnnn | yol ya da yüzey adı> | <yeniden kullanılan, atıf yapılan> | <değişmeyen> |
| Başka modül | <MOD-nnnn> (<okur | çağırır | ekler>); sahip onayı: <bağlantı> | <yalnız okuma / ekleme> | <o modülün kodu; modüle özgü dokunmama sınırı> |

## 4. Sözleşme
Nesneler ve uç noktalar

| Nesne | Tür | Açıklama | Dilim |
|---|---|---|---|
| `<Ad>` | varlık / komut / sorgu / uç nokta / arka plan işi / dışa açık arayüz / sağlayıcı | <kısa açıklama> | DL-1 |

Alanlar

| Nesne.alan | Tip | Zorunlu | Kural | Dilim |
|---|---|---|---|---|
| `<Ad>.<alan>` | <tip> | evet / hayır | <biçim, aralık, benzersizlik> | DL-1 |

İzinler

| Anahtar | Tür | Kim | Uç nokta | Dilim |
|---|---|---|---|---|
| `<izin.anahtarı>` | standart / açık izin / yüksek düzey | <rol> | <uç nokta> | DL-1 |

Denetlenen olaylar

| Komut | İşlem adı | Yol ya da istisna sınıfı + gerekçe | Dilim |
|---|---|---|---|
| <komut (kullanıcı "sistem" ise sistem)> | <işlem adı> | <a/b/c> | DL-1 |

## 5. Acceptance Criteria

- [ ] AC-1 (zorunlu) — <gözlenebilir davranış> · dilim: DL-1 · test: <birim | entegrasyon | sözleşme | uçtan uca | görünüm | canlı tur>
- [ ] AC-2 (zorunlu) — <hata yolu: 404 başka kiracı | 403 yetkisiz | 409 çakışma | 503 bağımlılık yok> · dilim: DL-1 · test: entegrasyon

## 6. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | süreç adımı | plan | ölçütler | bağımlı | kanıt |
|---|---|---|---|---|---|---|---|
| DL-1 | <çekirdek dilim adı> | Sözleşme, Acceptance Criteria | <a–b ya da teknik> | <MVP kimliği ya da plan satırı yok> | 1–2 | bağımsız | otomatik test |

## 7. Açık kararlar
| # | Karar | Kim | Hangi dilimi bekletir |
|---|---|---|---|
| 1 | <karar> | <rol> | <DL-n> |

> Koşullu bölümler (varsa, künyedeki `conditional:` sırasıyla): `## K-<ad>. <Ad>` — biçim ve sorular
> `.ees/standartlar/kosullu-bolumler.md`. Yoksa bu not silinir.
