# CLAUDE.md — giriş kapısı

> Bu dosya **kısadır ve öyle kalmalıdır.** Her oturumda otomatik yüklenir; uzarsa her
> oturumdan bağlam çalar. Sözleşmenin kendisi `AGENTS.md`'dedir.

## İLK İŞ: `AGENTS.md`'yi oku

```
Read AGENTS.md
```

⚠ Claude Code **yalnız bu dosyayı** otomatik yükler. `AGENTS.md`, `.antigravity/rules/`
(41 kural), `.antigravity/agents/` (20 ajan) ve `.antigravity/workflows/` (18 akış)
**otomatik yüklenmez** — 2026-09-08'de canlı oturumda ölçüldü: bağlamdaki tek proje
dosyası `MEMORY.md`'ydi.

Yani bu dosyayı okuyup `AGENTS.md`'yi açmazsan, 1.3 MB'lık mühendislik standardının
hiçbirini görmemiş olursun.

*(Windows'ta sembolik bağ güvenilir olmadığı için bu dosya bir kopya değil, bir
yönlendirmedir. `AGENTS.md` tek kaynak olarak kalır; buradaki hiçbir madde onun yerine
geçmez, çakışma halinde `AGENTS.md` kazanır.)*

## Okumadan önce bilinmesi gerekenler

Aşağıdakiler `AGENTS.md`'nin özeti değil, **onu açana kadar hata yapmanı önleyecek asgari
set**. Tamamı için `AGENTS.md` §6.1 Kural Haritası.

| konu | kural |
|---|---|
| **Yetki sırası** | Module Pack > Domain Config > `AGENTS.md` > `.antigravity/` > Archive |
| **Kiracı izolasyonu** | Her sorgu `TenantId` ile sınırlanır. İhlali sessizdir ve veri sızdırır. |
| **Dil** | Platform modülleri **2 dil** (en, tr) · Tenant modülleri **7 dil** (en, tr, fr, es, zh, ar, ru). Bir dil eksikse iş bitmemiştir. |
| **Git** | `main`'e asla commit edilmez. Modül başına bir dal. |
| **Ekran** | Yetkisiz kullanıcıya sayfa iskeleti çizilmez (UAS-001), yönlendirme yapılmaz. |
| **Belge** | `docs/` köküne dosya konmaz; yeri `.antigravity/rules/docs-organization.md` beş soruyla belirlenir. |
| **Test** | Kural, kendi kopyasıyla değil üretim koduyla ölçülür. Sabotaj kanıtı olmayan guard, guard değildir. |

## Portlar

```
5000 Gateway · 5001 Web · 5056 Auth · 5057 Platform · 5058 DevEnablement
5059 MDM · 5060 HCM      ⚠ 5060, mikroservis bandının (5011–5060) son portu
```

## Neden bu dosya var

`AGENTS.md` yıllardır "Claude Code tarafından otomatik yüklenir" diyordu ve bu doğru
değildi. Ekip Antigravity'den Claude Code'a geçtiğinde `.antigravity/` altındaki her şey
sessizce görünmez oldu — dosyalar duruyordu, kimse okumuyordu.

Bu dosya o boşluğu kapatır. Tek işi seni `AGENTS.md`'ye götürmek; büyümeye başlarsa
amacını kaybeder.
