# Şablon — kule özet satırı (kule → sahip)

Kim yazar: kule (kule kartı K19). Mesaj sırası her zaman **Biten · Çalışan · Sırada · Sizden istenen (tek iş)**.
Tek bir modülün ya da adımın raporu `modul-blogu.md` biçimindedir (modül adı ve kimliği, "Ne için", ilerleme çubuğu ve tahmini
bitiş, "Bu adım" satırı); sayılar `scripts/ees_run_record.py` aracından alınır, elle yazılmaz. Aşağıdaki satır biçimi yalnız
birden çok hattın tek mesajdaki özetidir: hat başına tek satır.
Araç bir hat için uyarı bildiriyorsa hattın satırı `· uyarı: <kısa neden>` ile biter.
Satırın bilgisi bu turda okunmadıysa saat yerine `eski bilgi, <saat>` yazılır. Sade Türkçe; kod adı tek başına yazılmaz,
yanında ne olduğu söylenir.

```
Biten: <bu turdan beri biten işler, tek cümle> | yok
Çalışan: <hat> · <modül, dilim n/N> · <durum> · <gün> gün, <n> dokunuş, ~<token> bin token (<bilgi saati>)
Sırada: <hat başına bir sonraki adım, tek cümle> | yok
Sizden istenen: <tek iş, bağlantıyla> | yok
```

Örnek (uydurma değerler):

```
Biten: CI'daki gereksiz iptaller düzeldi, birleşti.
Çalışan: İK hattı · zaman çizelgesi 2/4 · teslim onayında · 2,5 gün, 1 dokunuş, ~410 bin token (14:20)
Sırada: İK hattı: onaydan sonra dilim 3, raporlama ekranı.
Sizden istenen: zaman çizelgesi dilim 2'nin PR'ını onaylamak → <bağlantı>
```
