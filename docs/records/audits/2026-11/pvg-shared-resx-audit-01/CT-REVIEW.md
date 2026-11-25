# PVG ortak kaynakları — gerçek FAIL ve kapsam ayrımı

2026-11-21 (Asia/Baku). Bağımsız ajan mevcut resx-check skill'inin değişmemiş doğrulayıcısını tenant bağlamında çalıştırdı. CT kaynak HEADdddaea5544d142cc1288dd50a5b6b70e71c6b3ae; karşılaştırma yerel main58eb6fa901683296a0049a06910dc4b12e970950 Gitbloblarından çıkarılan yalnız yedi kaynak dosyasının minimal aynasıdır. Bu ikinci koşum main CI veya tam checkout testi değildir.

Her iki gerçek koşum **exit1 / FAIL / 11 [X] grubu** verdi. fr/es/zh/ar/ru dillerinin her birinde35, toplam175 eksik anahtar örneği main ile aynıdır. İngilizceyle eşit değer sayısı CT174/main173; Title Case bulgusu yok. Bunlar otomatik aracın sonuçlarıdır, PASS'a çevrilmez. 52 dış kanıtın byte/hash doğrulaması0uyuşmazlık; dokuz orijinal rapor/ölçüm/CLI dosyası RAW-AUDIT-ARTIFACTS.json içinde base64/orijinal SHA ile korunur. CRCRLF içeren raporun ham baytları da değiştirilmedi.

PVG'nin bu dalda eklenen iki ortak anahtarı Nav.Domain.PHARMACOVIGILANCE ve Nav.Module.CASEINTAKETRIAGE, ikisi de7/7 dilde dolu ve mevcuttur; yeni eksik anahtar yok. Tek yeni literal eşitlik fr/Nav.Domain.PHARMACOVIGILANCE=Pharmacovigilance'dır. Ham checker grup listesi yalnız ilk20adı basar; bu anahtar ilişkisi bütün Gitblob girdilerinden ayrıca ölçülmüştür, basılmayan satır basılmış gibi sunulmaz.

CT dilsel değerlendirmesi (araç ölçümünden ayrı çıkarım): aynı yazım tek başına çevrilmemiş değer demek değildir. Fransız ilaç otoritesinin resmî Fransızca kılavuzu da başlığında bu sözcüğü kullanır: [ANSM — Bonnes pratiques de pharmacovigilance](https://ansm.sante.fr/uploads/2021/01/06/bppv-fevrier-2018.pdf). Bu nedenle bu Fransızca modül adı için metin düzeltmesi gerektiren bir anlam kusuru saptanmadı. Aracın literal bulgusu ve genel FAIL sonucu aynen korunur; kural/allowlist veya çeviri dosyası değiştirilmez.

Skill anlam doğrulamaz, PAGE.RESX/Razor/JS köprüsünü kontrol etmez ve yayın kapısı değildir. Yedi yeni sayfa çevirisinin ayrı bağımsız XML/içerik UYGUN ve CT entegrasyon kanıtı pvg-dl12-l10n-01 kaydındadır. Genel ortak kaynak borcu bu PVG işi içinde düzeltilmez; başka modül/onay kapsamına yazılmaz. No-loss, BE/FE, runtime, Linux veya operasyon başarısı bu kayıttan çıkarılmaz.

Son git fetch origin main gerçek0: main aynı58eb6fa… kaldı; kaynak/ref ölçümü MAIN-REFRESH.json içindedir. Önceki denetimin veri sürümü değişmedi; yeni koşum veya ana dal testi diye yeniden etiketlenmedi.
