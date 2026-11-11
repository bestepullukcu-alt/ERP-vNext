# PVG dördüncü dalga — DoR (kart §1) kanıtı: WP-0230-DL4B

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır; dağıtım izni değildir. Dağıtım için iki koşul gerekir: `prompt_gate.py check` dağıtım kipinde çıkış 0 vermeli ve sahip işçi oturumunun açılmasını bu sohbette onaylamalı.
> Tarih 2026-10-27. Plan: `docs/records/decisions/2026-10/pvg-mod0230-dalga4-plani-01.md` (D4-3, D4-4). Kararlar: ODPVG2-01 ve ODPVG2-02 (kiracı claim'i `tenant_id`, sahip "claim-duzelt").

| DoR maddesi | WP-0230-DL4B |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | Revizyon 11 (`918dd660…`), `main`'de onaylı (#261, HIGH). Satır 1871'deki "TenantId", ODPVG2-02 ile düzeltildi. Paket metni bir sonraki revizyonda düzelecek. |
| Sözleşme | AC-5. Prompt'taki 15 alıntı Expected HEAD'de birebir. |
| Bağımlılık | WP-0230-DL3D CT dalında (VERIFIED). D4-4'teki "DL-3 önce" koşulu sağlandı. |
| Kapsam dışı | AC-6 hata zarfı (OD-PVG-3) → WP-0230-DL4C. Gateway kodu. MVC/JS (DL-7). MOD-0018 seed ve grant. |
| Kapsam | `PvgCaseIntakeTriageEndpoints.cs`: bağlam çözümü ve `RequireAuthorization`. `PvgServiceApiHost.cs`: JWT kaydı ile `UseAuthentication` / `UseAuthorization`. Api csproj: +2 satır. İki mevcut test dosyasında yalnız bağlam satırları. Yeni dosyalar `PvgTestIdentity.cs` ve `PvgCaseIntakeAuthenticationTests.cs` (N1–N9). |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl4b-r1`, dal `feature/pvg/wp-0230-dl4b-r1`, Expected HEAD `c212053c2` (CT dalı ucu), temiz. |
| Yürütme / şerit | Risk HIGH (kiracı, yetki, sözleşme). Aynı anda başka bir PVG işçisi yok. |
| Teslim (S-63, S-76) | İşçi yalnız kendi dalında commit eder, bildirir ve durur. `origin/main` birleştirmesi yapmaz. İşçi dalını CT dalına CT birleştirir. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL4B-v1.2.md`, sha256 `03c76ef651f0889d085c9670f07140afad96eb76daa8b07ba8924bd4b7cf0125` @ 5417547bc. |
| Bağımsız inceleme (HIGH) | Üç tur: v1.0 KOŞULLU, v1.1 KOŞULLU, v1.2 UYGUN. ENGEL 0, YÜKSEK 0 (v1.0'daki F-1 YÜKSEK, ODPVG2-02 ile kapandı). |
| Kabul | AC-5: N1–N9 ve sabotajlar. **Kanıt sınırı:** DL-4 için gerekli seviye E4; bu iş E2 üretir. |

## CT okumaları

1. **Kimlik doğrulama kaydının yeri.** Paket DL-4 "Yollar" alanında "`Program.cs` içindeki kimlik doğrulama kaydı" yazıyor, ancak kayıt `PvgServiceApiHost.cs`'e yapılıyor. Nedeni şu: DL3A'dan beri `Program` yalnız host'u çağırıyor ve DL3A'nın `Program.cs` koruma testi orada `AddPvgServiceApiHost` / `ConfigureServices` kullanılmasını yasaklıyor. Kayıt `Program.cs`'e yazılsaydı bu koruma kırılırdı. İnceleme bu okumayı yeterli buldu.
2. **Api csproj.** İki satır eklenir: JwtBearer 8.0.11 paketi ve Security.Secrets proje başvurusu. Her ikisi de gerekli: JwtBearer paylaşılan çerçevede yok, ortak saat payı sabiti de Building.Blocks'ta. Building.Blocks değişmez.
3. **Yeni test dosyaları ve iki mevcut test dosyası.** Bunlar DL-4 "Yollar" alanındaki "API testleri" kapsamında okunur.
4. **Eksik `JwtSettings`.** Açılış reddedilmez; her istek 401 alır, yani istekler varsayılan olarak reddedilir. Bu fail-closed davranış CRM'den farklıdır. Açılışın da reddedilmesi gerekirse DL3A ve DL3D test yapılandırmalarına `JwtSettings` eklenmesi gerekir; bu, bu işin kapsamı dışında bırakıldı (Known gaps).
5. **Kiracı uyuşmazlığı 409 döner (G-8).** Doğru durum kodu AC-6'nın işidir (DL4C).

## Dağıtım notu

Bozma denemelerinden biri (F-6) kütüphanenin davranışına dayanıyor. İnceleme bu davranışı yalnız dolaylı kanıtla doğrulayabildi. Kırmızı sonuç görülmezse işçi önce 401'in IDX kodunu raporlar. Bu durumda yanılgı güvenli yönde kalır.
