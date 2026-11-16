# Şablon — PR açıklaması (ajanın açtığı PR)

Kim yazar: hat (hat kartı H25). Her dilim kendi PR'ıyla gelir; aynı anda hazır iki küçük dilim tek PR'da gelebilir. Paket
taslağı da onaydan önce taslak PR olarak açılır; onun açıklamasında yalnız "Bu modül ne işe yarar", "Neden" ve "Kabul" dolar. `<!-- araç -->` işaretli bölümleri çalıştırma kaydı gelince araç doldurur; o güne dek hat
kayıttaki değerleri yazar. Bugünkü `.github/pull_request_template.md`'nin beş başlığı korunur; ajan işi için "Neden",
"Hangi testler", "Açık noktalar", "Ajan çalıştırması" ve "Kabul" eklenir. Uygulamada bu metin o dosyaya taşınır (iş 6).
Okuyan: kodu görmeden PR'ın ne olduğunu anlaması gereken geliştirici. Kişi adı yazılmaz; rol adı kullanılır.

```markdown
## Bu modül ne işe yarar
<!-- Modül ve dilim (ör. MOD-0230 DL-3); hangi iş sürecinin hangi adımı. Bir iki cümle. -->

## Ne yapıldı
<!-- 3–5 madde; kullanıcının göreceği değişiklik önce. -->

## Neden
<!-- Onaylı paket: <yol>@<commit>; karşılanan kabul ölçütleri (AC-n). Paketten sapma varsa burada ve gerekçesiyle. -->

## Hangi testler  <!-- araç -->
| Kabul ölçütü | Test | Sonuç |
|---|---|---|
<!-- Koşulan komutlar ve CI bağlantısı; sabotajla kırmızıya dönen koruma testleri ayrıca işaretlenir. -->

## Nasıl denenir
<!-- Ekran yolu ve örnek veri ya da çalıştırılacak test komutu. -->

## Kullandığı ana veri
<!-- Okunan ya da yazılan ana veriler ve sahibi olan modül; yoksa "yok". -->

## Açık noktalar ve dokunulmayanlar
<!-- Neyi bozabilir; neye bilerek dokunulmadı; sonraya kalan ve ertelemenin gelecekteki maliyeti (Ç22);
     denetçinin kapanmamış küçük bulguları ve hattın itirazları (H19, H20). -->

## Ajan çalıştırması  <!-- araç -->
- Hat: LANE-nnn · işler: <n> · seviye: <üst|standart|hafif> (araç: <…>, model: <…>, efor: <…>)
- Başlangıç onayı: kişinin başlangıç cümlesi verildi mi (evet: çalıştırma kaydında aynen / hayır: her adım ayrıca soruldu)
- Kart sürümleri: `.ees/kartlar/<kart>.md@<commit>` sha256:<…> (her kullanılan kart için)
- Süre · token · deneme · denetçi sonucu (UYGUN / KOŞULLU) ve tur sayısı
- Çalıştırma kaydı: <CI yapıtı bağlantısı> sha256:<…>

## Kabul
<!-- Hattın listelenen onaylayıcısı (asıl ya da yedek) tek oturuşta inceler, PR onayında "kabulü onaylıyorum" yazar, onaylar
     ve birleştirir (D-12; sahip kararı 16 Kasım). Paket PR'ında cümle "paketi onaylıyorum"dur. Ajan onay yazmaz, birleştirmez. -->
```
