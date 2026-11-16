# Şablon — kule özet satırı (kule → sahip)

Kim yazar: kule (kule kartı K19). Mesaj sırası her zaman **Biten · Çalışan · Sizden istenen (tek iş)**. Hat başına tek satır.
Satırın bilgisi bu turda okunmadıysa saat yerine `eski bilgi, <saat>` yazılır. Sade Türkçe; kod adı tek başına yazılmaz,
yanında ne olduğu söylenir.

```
Biten: <bu turdan beri biten işler, tek cümle> | yok
Çalışan: <hat> · <modül, dilim n/N> · <durum> · <gün> gün, <n> dokunuş, ~<token> bin token (<bilgi saati>)
Sizden istenen: <tek iş, bağlantıyla> | yok
```

Örnek (uydurma değerler):

```
Biten: CI'daki gereksiz iptaller düzeldi, birleşti.
Çalışan: İK hattı · zaman çizelgesi 2/4 · teslim onayında · 2,5 gün, 1 dokunuş, ~410 bin token (14:20)
Sizden istenen: zaman çizelgesi dilim 2'nin PR'ını onaylamak → <bağlantı>
```
