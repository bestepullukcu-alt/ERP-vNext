# MOD-0286 — ölçüm kaydı dizininin bugünkü durumu (`README-p1-03.md`)

`README.md` bu dizinde commit'lidir ve düzeltilmez (docs/records kuralı). Yazıldığı gün (2026-10-23) doğruydu; bugün bazı satırları eskidi. Bu dosya PSS-UI-P1-03 işinin yeni dosyasıdır: dizindeki kayıtların bugünkü durumunu ve `README.md`'nin hangi satırlarının eskidiğini yazar. Çelişkide bu dosya `README.md`'nin eski satırlarının yerine okunur; paket (rev 12, sha256 `89333f90`) her ikisinin üstündedir.

## Kayıtların bugünkü durumu

| Kayıt | Durum | Bağlı olduğu kod commit'i | Kaydın commit'i |
|---|---|---|---|
| `uic-auto.sh`, `uic-sabotage.sh`, `uic-resx-baseline.sh`, `resx-baseline/` | commit'li, düzeltilmedi | — | `a0a4050b0` |
| `auto-01/` | var; `run_record.py verify` çıkış 0; ham çıkış 0 | `a0a4050b0` (`run-record-v1.json` `commit` alanı) | `3fbf891bb` |
| `sabotage-01/` | var; `run_record.py verify` çıkış 0; baseline 97 `[PASS]` / 1 `[FAIL]`, sabotaj 7 `[PASS]` / 2 `[FAIL]` | `3fbf891bb` (`run-record-v1.json` `commit` alanı) | `bb3464954` |
| `manual.md` | var; 10 elle madde (1 `uygun`, 2 `uygun değil`, 2 `kısmi`, 5 `ölçülmedi`) | — | `d3105a332` |
| `p1-03-taban-01/`, `uic-p1-03.sh` | Control Tower'ın kanıt betiği sürüm 01 ve kırmızı taban koşusu; sürüm 01 prompt incelemesinde reddedildi, kullanılmaz | — | `d3a32783a`, `0b68a8f00` |
| `uic-p1-03-02.sh`, `p1-03-taban-02/` | Control Tower'ın kanıt betiği sürüm 02 (`c01b7f2a2`) ve kırmızı taban koşusu (`zorunlu geçti 12 / 17`; koşu `c01b7f2a2`'ye bağlı, kaydı `8b9a45ba9`) | `c01b7f2a2` | `c01b7f2a2` (betik), `8b9a45ba9` (kayıt) |
| `auto-02/` | PSS-UI-P1-03 işinin kayıtlı koşusu (`uic-p1-03-02.sh`, RC-1…RC-24 ve AC-1…AC-16 + CT-1…CT-3). Bağlı olduğu kod commit'i, bu dosyayı ekleyen işin tek belge commit'idir (`run-record-v1.json` `commit` alanından okunur); koşu o commit'ten sonra yapılır. Kayıt commit'ini Control Tower yapar | `run-record-v1.json` `commit` alanı | Control Tower (bu iş commit etmez) |

## `README.md`'nin eskiyen satırları

| `README.md` satırı | Eski ifade | Bugünkü durum |
|---|---|---|
| 3 | "TASLAK / hazırlık"; paket "revizyon 06 (sha256 `7f6868d9`)" | Paket rev 12 (sha256 `89333f90`, PR #245 merge `c51568aa8`) main'de onaylıdır; belgeler main'dedir; kabul PSS-UI-P1-03 ile bekliyor (kabul kaydı Control Tower'ındır) |
| 13 | `auto-01/`, `sabotage-01/` "henüz yok" | İkisi de var (tablo yukarıda) |
| 14 | `manual.md` "henüz yok" | Var (`d3105a332`); kendi "Durum" satırı da hazırlık notudur, canlı ölçüm gerektiren 5 madde `ölçülmedi` kalır |
| 16-28 ("Koşu sırası") | Gelecek zaman: önce commit, sonra `auto-01`, sonra `sabotage-01` | Sıra uygulandı (`a0a4050b0` → `3fbf891bb` → `bb3464954`) |
| 30 ("Kuru koşu özeti") | "ağaç kirli", `run_record.py` koşulmadı, HEAD `005a0cc58` | O tarihli kuru koşu; kayıtlı koşular artık `auto-01/` ve `sabotage-01/` |
| 70 | "`run-record-v1.json` yok, kabul bağlaması yok. `manual.md` yazılmadı." | İki koşu dizini (`auto-01`, `sabotage-01`) `run-record-v1.json` taşır; `manual.md` yazıldı. Kabul bağlaması Control Tower'ın tek kabul kaydındadır |

`README.md`'nin öteki satırları (dosya tablosu, "Sınırlar" içindeki AC-13 / RC-24 sınırları) değişmedi.
