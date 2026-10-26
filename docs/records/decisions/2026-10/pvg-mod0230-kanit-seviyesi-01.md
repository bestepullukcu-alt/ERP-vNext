# PVG-MOD0230-KANIT-SEVIYESI-01 — MOD-0230 dilimlerinin gerekli kanıt seviyesi (paket revizyonu 10)

> **Karar sahibi:** sahip. Kayıt: PVG Control Tower.
> **Dayanaklar:**
> - CT-Setup'ın cevabı, sahip onayıyla. Sahip bunu 2026-10-26'da PVG Control Tower sohbetine yapıştırdı.
> - SOP §23.1, `docs/guides/operations/control-tower-sop.md` satır 1600-1606.
> - Kabul kaydı kuralı, `.antigravity/rules/status-reporting-and-evidence.md:131`.
>
> **Modül:** MOD-0230 Case Intake & Triage.

## Durum

- Revizyon 09 onaylandı (#220). Ancak WP-0230-DL2A, DL3A ve DL8 için kabul kaydı yazılamadı.
- Sebep: kayıt, `required_evidence_source` alanında onaylı bir paket ya da WP satırını göstermek zorunda ve o satırın gerekli kanıt seviyesini **tek başına** söylemesi gerekiyor. Pakette böyle bir satır yoktu. Aday satırlar ya "E2/E3" gibi iki seviye söylüyor ya da geçmiş ölçümleri anlatıyordu.
- Depoda henüz hiç `rep-001-v2` kabul kaydı yoktu; bu ölçüldü.

## CT-Setup cevabı (sahip onayıyla, özet)

1. SOP §23.1 alt sınırları, yalnız build/test kapısı açık bir pakette de geçerlidir; seviye düşürülmez. Buna göre DL2A E4 (durum değiştiriyor), DL3A E3 (açılış ve hazır olma), DL8 E3 (Gateway yönlendirmeleri).
2. Seviye, teslim dilimleri tablosunda her dilim satırına eklenen tek seviyeli bir "gerekli kanıt" sütunuyla yazılır. AC satırları değişmez. Bu, revizyon 10'da (inceleme ve onay) bir kez yapılır.
3. DL8 revizyon 10'dan sonra `accepted` alabilir: E3 kanıtı ve birleşmiş commit üzerinde `run_record` koşu kaydıyla. DL2A ve DL3A bugün `accepted` alamaz. `conditional` da yazılamaz, çünkü kanıt gerekli seviyenin altındaysa araç koşullu kaydı da reddediyor (`acceptance_record.py:549-553`). Canlı kapı açılıp E3/E4 ölçülene kadar ikisi `INTEGRATED` kalır.
4. Bekletecek bir kart, SOP ya da araç değişikliği yok. Koşullu kaydın kanıt kuralı (S-46) sahibe ayrıca soruluyor.

## Karar: revizyon 10'daki seviyeler

| Dilim | Kapsam | Seviye | Dayanak |
|---|---|---|---|
| DL-1 | ortak çerçeve ve kapsam; yalnız paket dosyası, kod yok | E1 | SOP §23.1 "Profile C inspection: E1". CT önerisi |
| DL-2 | sözleşme alanları ve doğrulama; depoda durum değiştirir | E4 | CT-Setup cevabı 1 |
| DL-3 | host açılışı ve yapılandırma | E3 | CT-Setup cevabı 1 |
| DL-4 | API: kimlik doğrulama, kiracı bağlamı, hata modeli; create, update, triage ve route durum değiştirir | E4 | SOP §23.1 "state-changing ise E4". CT önerisi |
| DL-5 | denetim yazımı; MOD-0021 forwarder üzerinden düzenlenmiş kalıcı kayıt (modüller arası) | E5 | SOP §23.1 "Compliance … persistent record: L3 + E4/E5" ve "Cross-module capability seam: E5". CT önerisi |
| DL-6 | izin anahtarı sözleşmesi; MOD-0018 / AuthService catalog-grant bağlantısı | E5 | SOP §23.1 "Cross-module capability seam: E5". CT önerisi |
| DL-7 | tenant UI ve MVC proxy | E4 | SOP §23.1 "UI/state-changing: E4". CT önerisi |
| DL-8 | Gateway route ailesi | E3 | CT-Setup cevabı 1 |
| DL-9 | operasyonel runtime kapısı | E5 | uçtan uca operasyonel çalışma; modüller arası. CT önerisi |

"CT önerisi" satırları SOP §23.1'den türetildi. Bağımsız inceleme 10 bu satırları ayrıca denetler; sahibin onay satırı bu sürümü onaylar.

## Sonuçlar

- **DL8:** Revizyon 10 onaylanınca bir kabul kaydı alabilir. Ölçüt AC-16, kanıt E3, koşu kaydı birleşmiş commit üzerinde alınır.
- **DL2A ve DL3A:** `INTEGRATED` kalır. Kabul için canlı kapıdan sonra DL2A'nın E4, DL3A'nın E3 kanıtı gerekir.
- **Kuyruk:** Bu kayıt kuyruğu değiştirmez.
- **Önceki kayıtlar:** `pvg-mod0230-dl3-bolme-01.md` DB-2'de "DL3B, DL3A kabul edildikten sonra" yazıyor. Bu koşul artık canlı kapıya bağlı. DL3B'nin dağıtımı sahibe ayrıca sorulur.

## Ölçüm (CT, 2026-10-26)

- Her DL satırında tam bir seviye belirteci var ve değerler yukarıdaki tabloyla aynı.
- Revizyon notunda ve tablonun altındaki cümlede seviye belirteci yok.
- `pack_approval.slices`: DL-1…DL-9.
- `parse_pack`: 33 zorunlu ölçüt, sorun yok.
- Paket sha256 `c236cb87d47f5e821d8a67b168656656d2af55502ac48861ccc26344332c7232`.
