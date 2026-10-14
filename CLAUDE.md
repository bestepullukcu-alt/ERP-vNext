# CLAUDE.md — Claude Code giriş kapısı

Proje sözleşmesi `AGENTS.md`'dedir. Claude Code, `CLAUDE.md` varken `AGENTS.md`'yi kendiliğinden yüklemez; bu yüzden
`AGENTS.md`'nin başındaki **Çekirdek** bloğu `.claude/rules/agents-core.md` olarak üretilir ve her oturumda (alt ajanlar dahil) yüklenir.

- Çekirdek kurallar her zaman bağlayıcıdır. Ayrıntı gerektiğinde `AGENTS.md`'nin ilgili bölümünü (§N), §6.1 kural haritasındaki
  kural dosyalarını ve `.antigravity/AGENTS-REFERENCE.md`'yi aç.
- Çakışmada `AGENTS.md` kazanır. Bu dosyaya kural yazılmaz: kural `AGENTS.md`'ye yazılır, ardından
  `python3 .antigravity/scripts/sync_adapters.py` çalıştırılır.
- `AGENTS.md`'yi bu dosyaya içe aktarmayın (`@AGENTS.md`): her oturuma ve alt ajana ~9k token ekler (İ5-B ölçümü).
