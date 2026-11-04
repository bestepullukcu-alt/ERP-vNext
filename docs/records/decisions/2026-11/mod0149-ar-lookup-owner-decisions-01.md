---
status: taslak — sahip (Beste) okuması bekliyor; bu belge yeni bir karar vermez, alınmış kararları kayda geçirir
decided: 2026-10-30 (K-1…K-6), 2026-11-04 (K-8)
module: MOD-0149
work_packages: MOD-0149 Treasury AR eki (pack §30–§31), CRM-ACCOUNT-LOOKUP 1.0.0
decision_ids: K-1, K-2, K-3, K-4, K-5, K-6, K-8 (K-7 ayrı kayıtta, aşağıda)
---

# MOD-0149 Treasury AR eki — sahip kararları K-1…K-8

> **Karar sahibi:** commercial-suite sahibi (Beste, K2); K-7 MOD-0120 sahibinindir.
> **Neden bu kayıt:** Bağımsız inceleme (eski sürüm paket `4e6c020d`, sözleşme `ec4aaa16`) kararların pakette ve sözleşmede yalnız ad ve
> tarihle anıldığını, dosya:satır kaynağı olmadığını buldu. Bu dosya o kaynaktır; paket §30.11 ve sözleşme §11 buraya atıf verir.
> **Sınır:** Bu kayıt push, PR, birleştirme, kod ya da dağıtım izni değildir.

## Kaynaklar

- **K-1…K-6 (2026-10-30):** CT · CRM oturumunda sahibe sorularak alındı; ilk yazılı hâli paket §30.11 ve sözleşme §11,
  commit `5531a6c57` ("owner decisions K-1..K-6"). O oturumun soru-cevap kaydı repoda yok; bu kayıt paket metnini kaynak
  alır ve sahibin okumasıyla kesinleşir.
- **K-7 (2026-10-30, MOD-0120 sahibi):** `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` D112/2
  ("MOD-0149 `external-references/resolve` ucu O-10'a ertelendi; v1'de çağıran yok, `lookup-validation` kalır"). Pakete
  commit `c2c9a4a9f` ile işlendi.
- **K-8 (2026-11-04):** CT · CRM oturumu (780dee5f), ilk bağımsız incelemenin F-2 bulgusu
  (`docs/records/reviews/MOD-0149/2026-11-04-6df3626c.md`) üzerine sorulan üç seçenekten sahibin cevabı: "1 olsun".

## Kararlar

| # | Konu | Karar |
|---|---|---|
| K-1 | İzin anahtarı | `crm.account.lookup-validation` (PKS-001, Tier-3 `lookup-validation`). AR kullanıcısı `crm.account.read` almaz. Auth katalog kaydı ve AR rol ataması ayrı iş (MOD-0018-FU27). §16 satırı #360 ile eklendi. |
| K-2 | `archived` sözleşmede yayımlanır mı | Evet; `isBillable = false`. Küme beş değer: `active` (tek faturalanabilir), `inactive`, `draft`, `suspended`, `archived`. |
| K-3 | U-2 409 (çoklu eşleşme) karşısında tüketici | Üretici kuralı: tüketici tahmin etmez, hesap seçmez, işlemi durdurur; hata kalıcıdır → öneri 422 ayrı kod, 503 değil. Kod adı MOD-0120 sahibinindir. |
| K-4 | Gateway | Mevcut `/api/crm/accounts/{everything}` joker rotası yeterli; `ocelot.json` değişmez; integration-agent teyidi yalnız not. |
| K-5 | `runtime_code_scope` künyesi | Değişmez: "external-ref surface" U-2'yi, "Account/WorkPlace master + 360" okuma yüzeyi U-1'i (ve K-8 ile U-3'ü) kapsar. |
| K-6 | Teslim dilimleri | Tablo yalnız bu ek için (§31); §1–§29'un teslim edilmiş işi geriye dönük dilimlenmez. |
| K-7 | U-2'nin MOD-0120'deki yeri | (MOD-0120 sahibi, D112/2) Bu sürümde U-2'yi çağıran akış yok; DL-2 MOD-0120 O-10'a ertelendi; DL-1 ve sözleşme bu sürümün önkoşulu. |
| K-8 | MOD-0120 müşteri seçicisinin veri kaynağı | Seçenek 1: dar arama ucu U-3 `GET /api/crm/accounts/lookup`, aynı `crm.account.lookup-validation` anahtarıyla, DL-1 içinde; yalnız `accountId`, `code`, `name`, `status`, `isBillable` döner. Seçicide görünmek uygunluk kararı değildir; seçilen kimlik U-1 ile doğrulanır. K-1 korunur. MOD-0120:1146-1147 ve :1349-1350'nin hizalanması Finans'ın işidir. |

## Açık kalanlar (karar değil)

- **K-9:** MVP-6 seçici + adres için sözleşmeli yüzey (öneri: DL-1 sonrası 1.x eki) — sahip commercial-suite.
- **K-10:** gelen `X-Correlation-Id`'nin CRM günlüğüne bağlanması — sahip commercial-suite (K2); sözleşme 1.0 garanti etmez.
