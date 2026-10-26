# PVG-MOD0230-AC-ESLEME-01 — MOD-0230 kabul ölçütleri BG-n'den AC-n'ye (paket revizyonu 08)

> Karar sahibi: sahip.
> - "1 paket ac", 2026-10-23, PVG Control Tower sohbetinde.
> - Aynı karar CT-Setup üzerinden de iletildi: CT-ACC-BG-01 için "paket yolu"; araç değişmeyecek.
> - Kurulum defteri S-44: `docs/records/audits/2026-10/ct-setup-test-01/FINDINGS.md`.
>
> Modül: MOD-0230 Case Intake & Triage. Kayıt: PVG Control Tower.

## Durum

- `scripts/acceptance_record.py` paketteki kabul ölçütlerini yalnız `- [ ] AC-<n> (zorunlu|isteğe bağlı) — <ölçüt>` biçiminde tanıyor.
- MOD-0230 paketinin 07. revizyonu (sha256 `10fdbe5c…`) 33 ölçütü `- [ ] BG-<n> (<dilim; koşul>) - <metin>` biçiminde taşıyordu. Araç bu paketi "eski biçim" saydığı için kabul kaydı yazılamadı. Ölçüm: `docs/records/audits/2026-10/pvg-wave1-integration-01/INTEGRATION.md`.
- BG maddelerini 2026-10-21'de PVG'nin paket yeniden yazımı ekledi (`8b66d283b`). Bu tarih, 2026-10-16 kuralından sonra: `.antigravity/rules/module-pack-standard.md:294` yeni Control Tower'ların yeni maddelerinde AC biçimini istiyor. Yani sorun PVG tarafında. Araç değişikliği yalnız bu bir pakete yarayacaktı.

## Karar

1. Paket revizyon 08'e geçer ve yalnız biçim değişir:
   - Her `BG-<n>` ölçütü aynı numarayla `AC-<n> (zorunlu)` olur.
   - Dilim ve koşul bilgisi ölçüt metninin başında kalır.
   - Bölümdeki numarasız listeler, değiştirilmeden yeni "Gate State and Acceptance History" bölümüne taşınır: özgün planlama paketi, build/test kapısı durumu ve operasyonel runtime kapısı. Bu bölüm DL-1 hücresine eklenir.
   - Devam satırlarındaki beş çapraz atıf "criterion <n>" olarak yazılır: `criterion 15`, `criterion 30` (üç kez) ve `criterion 33`.
2. **Eşleme:** `BG-<n> ≡ AC-<n>`, numaralar aynı.
   - Revizyon 08'den önce yazılmış ve `BG-<n>`'ye atıf yapan her kayıt ve prompt, aynı numaralı `AC-<n>`'yi kasteder.
   - Bu dosyalara dokunulmaz (K4). Kapsam: `docs/records/**` altında 39 PVG dosyası ve dağıtılmış ya da geçmiş prompt'lar (WP-0230-DL2A, DL3, DL3A, DL8).
3. Kuyruk: Yalnız PLANNED satırlarının metni çevrilir; 11 atıf.
   - `WP-0230-DL2B`: AC-21…AC-30.
   - `WP-0230-DL3B`: AC-9.
   - INTEGRATED ve SUPERSEDED satırlar olduğu gibi kalır; eşleme bu kayıttır.
4. Yeni prompt'lar (DL2B, DL3B ve sonrası) `AC-<n>` yazar.

## Ölçüm (CT, 2026-10-23)

- **Ayrıştırıcı:** Aracın kendi ayrıştırıcısı (`parse_pack(text, "MOD-0230")`) çalışma ağacındaki revizyon 08'de şunu verdi:
  - 33 ölçüt; kimlikler 1…33.
  - Hepsi zorunlu.
  - Sorun listesi boş, `old=False`.
- **İçerik korunması:** Kelime çoklu kümesi karşılaştırıldı (önce `BG-n → AC-n` normalize edildi). Önceki sürüme göre silinen sözcükler yalnız şunlar:
  - 33 tane ` - ` ayracı (yerine ` — ` geldi).
  - Yukarıdaki beş çapraz atıf (yerine `criterion` geldi).
  - Eklenenler yalnız `(zorunlu)` işaretleri, revizyon 08 notu ve yeni bölümün başlığı ile tek cümlesi.
- **Künye:** Ön bilgi bloğu bayt bayt aynı.
- **Dilim tablosu:** `pack_approval.slices()` DL-1…DL-9'u okudu.
- **Kuyruk:** `ct_queue.py validate --queue pvg` 0 hata, 0 uyarı verdi.
- Pakette `BG-<rakam>` geçen satır kalmadı. Revizyon notundaki `BG-<n>` yazısı bir desendir; arkasından rakam gelmiyor.

## Kural notu

`module-pack-standard.md:295` "Mevcut paketler dönüştürülmez" diyor. Bu dönüşüm sahip kararına dayanıyor ve sorun paketin kendi yeni maddelerinden çıktı. CT-Setup, kurala "paketi baştan yazan ya da yeni madde ekleyen yeni CT `AC` biçimini kullanır" cümlesini ayrıca önerdi (S-44). O değişiklik `.antigravity`'de olduğu için ayrı sahip onayına bağlı ve bu kaydın kapsamında değil.

## Sonraki adımlar

1. Revizyon 08 için bağımsız inceleme (CT-APR-02).
2. Bu sürüme bağlı onay satırı (CT-APR-01). Tek PR'da gider ve PR'ı onaylayan birleştirir (kart §7a/7).
3. Onaydan sonra WP-0230-DL2A, DL3A ve DL8 için künyeli kabul kayıtları (CT-OPS-03) yazılır.
