# Şablon — çalıştırma kaydının alanları

Kim üretir: araç (iş 5 ve 6). Biçim: JSON; CI yapıtı olarak saklanır, PR açıklamasına özeti ve sha256'sı yazılır; depoya
girmez (çekirdek Ç21). JSON şeması araçla birlikte yazılır (`calistirma-kaydi.schema.json`, iş 5). Bu kayıt teslim notunun
2, 3 ve 5. satırlarını, modül bloğunun ölçülerini, kule özet satırını ve durum sayfasının hat satırını besler.

| Alan | Anlamı | Rehber şartı |
|---|---|---|
| `run_id`, `hat` (LANE-nnn), `modul`, `dilim`, `is` | kimlik | §7 envanter |
| `dal`, `baslangic_commit`, `bitis_commit` | ne üzerinde çalışıldı | kanıt |
| `kartlar[]` | {yol, commit, sha256} — kullanılan her kart, ajan ve beceri tanımı | §6 kanıt (talimat sürümü) |
| `ajan` | {rol, seviye, araç, model, efor} — seçilen ve gerçekten kullanılan ayrı | §6 durum ve maliyet |
| `adimlar[]` | {ne, komut, çıkış kodu, süre} | kanıt |
| `testler` | {koşulan, geçen, kırmızı, atlanan; kabul ölçütü ↔ test eşlemesi; sabotaj kanıtı} | kanıt |
| `denetim` | {denetçi, seviye, sonuç, tur, bulgu sayısı önem bazında} | §6 kalite kontrolü (üreten ≠ denetleyen) |
| `denemeler` | geçici hata yeniden denemeleri (≤2) ve kalite düzeltmeleri (≤1); seviye yükseltmeleri | §6 hatalar |
| `istisnalar[]` | {tür, karar, karar türü M1/M2/M3, kim onayladı (rol), bitiş tarihi} — istisna kabul değildir; sahibi ve bitişi olmayan istisna kaydedilmez | §6 karar türleri |
| `durdurma` | varsa: kim (rol), ne zaman, ne kaldı | §6 durdurma |
| `sure_dk`, `token_girdi`, `token_cikti`, `maliyet_tahmini` | maliyet | §6 durum ve maliyet |
| `baslangic_onayi` | kişinin başlangıç cümlesi aynen ve zamanı; yoksa "yok" (o durumda her commit, push, PR ayrıca soruldu) | açık onay (kök kural 8) |
| `insan_dokunuslari[]` | {tür: paket onayı / dilim onayı ve birleştirme (tek oturuş) / cevaplanan soru / başlangıç cümlesi, zaman}; sayım hat kartı H2'deki gibi: dilim başına, cümle ve paket onayı ilk dilime | pilot hedefi ≤3 |
| `ci_ilk_deneme_yesil` | evet / hayır | pilot hedefi |
| `ornek_gorevler` | kart değiştiyse: koşulan örnek görevler ve sonuçları | §6 kanıt (örnek görevler) |

Kişi adı yazılmaz; `kim` alanları rol adı taşır. Saklama süresi: CI varsayılanı (GitHub'da 90 gün — doğrulanmalı); IMS
saklama süresi yöneticiye soru.
