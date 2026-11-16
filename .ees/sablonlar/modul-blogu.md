# Şablon — hat modül bloğu (hat → kişi, her dilim sonunda)

Kim yazar: hat (hat kartı H27). Ölçüler çalıştırma kaydından gelir; elle yazılan yalnız "Sizden istenen" satırıdır.
Tek istek: kişiden aynı anda birden çok şey istenmez.

```
<Modülün sade adı> (<modül kimliği>) · dilim <n>/<N> · <durum: kodda | kontrolde | teslim onayında | birleşti | durduruldu>
Sizden istenen: <tek iş, tek bağlantı> | yok
PR: <bağlantı> · CI ilk denemede <yeşil | kırmızı>
Testler: kabul ölçütü <bağlı>/<toplam> teste bağlı · test <geçen>/<toplam>
Belgeler: <var>/<zorunlu> (modül sonunda tamamlanacaklar: <liste> | yok)
Ölçüler: <geçen> iş günü (hedef ≤5) · <n> dokunuş (hedef ≤3) · ~<token> bin token
Denetim: <UYGUN | KOŞULLU | UYGUN DEĞİL> · tur <1|2> · açık bulgu: <sayı ve önem> | yok
```

Örnek (uydurma değerler):

```
Haftalık zaman çizelgesi (MOD-0280-FU01) · dilim 2/4 · teslim onayında
Sizden istenen: PR'ı inceleyip onayınıza "kabulü onaylıyorum" yazın → <PR bağlantısı>
PR: <bağlantı> · CI ilk denemede yeşil
Testler: kabul ölçütü 7/7 teste bağlı · test 112/112
Belgeler: 4/6 (modül sonunda tamamlanacaklar: kullanıcı kılavuzu, sürüm notu)
Ölçüler: 2,5 iş günü (hedef ≤5) · 1 dokunuş (hedef ≤3) · ~410 bin token
Denetim: UYGUN · tur 1 · açık bulgu: 2 küçük (PR'da)
```
