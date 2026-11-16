# MOD-0018-FU31 — sahip kararları 02

Bu bir ek kayıttır. Kayıt 01 değiştirilmez.

- Kaynak: CT · Auth (Yetki kulesi).
- Söz, sahibin bu sohbette doğrudan yazdığı sözdür.
- Kişi adı yazılmaz; sahip = MOD-0018 sahibi.
- Paket sürümü: `2d3e2922d`, sha256 `9ea166b4462a1107e8a7cd7a4953154e4fc0261ae1e7fa10f290db2c0c95e59f`.
- İnceleme: üçüncü tur, KOŞULLU, engel yok (`reviews.tsv` satırı `09b4e0344`).

Sahibin önüne konan öneriler, paketin "Açık kararlar" tablosundaki önerilerdir. Söz bunların hepsini kabul eder.

| # | Karar | Söz | Nerede | Zaman |
|---|---|---|---|---|
| 2 | K-2: rol adları ve açıklamaları paketin Authorization Convention tablolarındaki değerlerle 7 dilde yazılır. tr adları başlık harflidir: "Depo Sorumlusu", "Sevkiyat Planlayıcısı", "Depo Operatörü". | "Hepsini kabul et" | CT · Auth sohbeti, doğrudan | 2026-11-11 11:05 |
| 3 | K-3 (b): bilinen küme 20 çekirdek + 3 S3 anahtarıdır. Beyan bu kümenin alt kümesi olmalı ve en az bir anahtar taşımalıdır. Eksik anahtar hata sayılmaz; roller kesişimi alır. Bilinmeyen anahtar reddedilir. | aynı | aynı | aynı |
| 4 | K-4 (b): Viewer'da şablondan gelen `System` satırı DL-2 koşucusuyla geri alınır: soft delete, önce denetim satırı, Off / DryRun / Apply; varsayılan Off. | aynı | aynı | aynı |
| 5 | K-5 (c): DL-2 koşucusunun Viewer tanımı, `Module` + `putaway-picking-packing` kaynaklı satırı da aday sayar. | aynı | aynı | aynı |
| 6 | K-14 (a): üç yeni rolün açıklaması `RoleDescription_{RoleName}` olarak 7 dilde yazılır, köprüsüyle birlikte. Saklı değer İngilizcedir. | aynı | aynı | aynı |
| 7 | K-15 (b), yalnız depo yolunda: depo eşitlemesinin daraltma ve plan silmeleri `IRoleAuditRecorder` ile denetim satırı yazar. Silme fiziksel kalır. | aynı | aynı | aynı |
| 8 | K-18 (a): denetim düzeyi bugünkü `IRoleAuditRecorder.RecordAsync` yoludur. Yerel satır ve merkezi iletim en iyi çabayla yapılır, kuyruk yoktur. FU29'dan bağımsızdır. | aynı | aynı | aynı |
| 9 | K-17: DL-2 geri alma tanımının karar dosyası bu kayıttır (aşağıdaki bölüm). | aynı | aynı | aynı |

## K-17 — DL-2 geri alma tanımı (`DecisionRef`)

- **Rol:** Viewer.
- **Anahtar:** yalnız `warehouse.outbound-shipments.read`.
- **Aday satırlar:** (i) `System` kaynaklı satır; (ii) `Module` kaynaklı ve `SourceModuleCode = putaway-picking-packing` olan satır.
  - `Manual` satır ve başka kaynak modüllü satır aday değildir.
- **Yöntem:** önce denetim satırı yazılır, sonra soft delete yapılır (`IsDeleted`, `DeletedAt`). Kiracı başına çalışır ve idempotenttir.
- **Kip:** varsayılan Off.
  - Her ortamda (dev, staging, prod) önce DryRun koşulur ve sayım kayda yazılır.
  - `Apply` her ortam için ayrı bir sahip sözüyle açılır. Bu kayıt hiçbir ortamda `Apply` izni vermez.

## Açık kalan

- **K-18 / şart 4 kabulü.** Bu kayıt, K-1 şart 4'ü ("her kurulum merkezi denetim kaydına düşsün") "en iyi çaba, kuyruk yok" düzeyine çeker. Bu daraltmayı şartın sahibi olan MOD-0178 sahibi ayrıca kabul etmelidir. Bu kabul, onay satırından önce kayda geçer. Kaynak: üçüncü tur inceleme F-2.
- **Paket onayı.** Bu kayıt paket onayı değildir. Onay, MOD-0018 sahibinin «onayla» sözüyle verilir.

Bu kayıt commit'ten sonra değiştirilmez.
