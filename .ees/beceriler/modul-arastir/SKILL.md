---
seviye: standart
---

# Modül araştırması — adı verilen yeni modül ya da kapsam için salt-okunur keşif (asıl tanım)

Yeni akışın kulesinin modül talebi adımlarıdır. Salt-okunur: hiçbir dosya yazmaz (geçici dosya dahil), dal değiştirmez, kod ya da paket izni
vermez. Eski `control-tower` becerisinin araştırma bölümünden sadeleştirildi; eski beceri eski hatlar için yerinde durur.

## Adımlar
1. **Kimlik ve mükerrer:** `python3 .antigravity/scripts/verify_module_id.py . --discover "<ad>"`; ad bir alan adıysa keşfi o
   alanın modülleriyle tek tek koş. `--check-report` yalnız bütünlüğü kanıtlar, güncelliği değil.
2. **Blueprint:** modülün satırı, alanı, süiti; yoksa "Blueprint'te yok" yaz.
3. **Paket ve alan ayarı:** `execution/domains/<alan>/module-packs/` ve `domain-config.md`; var olan paketin onay durumu.
4. **Geçmiş kararlar:** `git log --all --grep "<ad>"` ve `docs/records/decisions/`; her kararı `yol@commit` ile yaz ve "hâlâ
   geçerli mi?" diye sor.
5. **Yoksa ayrım:** ana modül, ek (FU), yeni modül ya da aday — gerekçesiyle.

## Yanıt (dört parça)
1. Okunan kaynaklar (çıkarım ayrı).
2. Koşmayan ya da eksik keşif ve nedeni ("kısmi inceleme").
3. Adaylar ve mükerrerler; gerekçeli **tek** ilk kapsam önerisi, "öneri — onay değil" diye işaretli.
4. Açık onay ve sahiplik kararları (TBD dahil).
