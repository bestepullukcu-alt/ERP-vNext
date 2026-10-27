# MOD-0280-FU02 — paket öncesi tasarım kararları 01

> Control Tower kaydıdır; paket onayı, kod ya da dağıtım izni değildir. Paket taslağı (HCM-LEAVE-06) bu kararları
> kaynak gösterir. Kaynak bulgular: bağımsız tasarım denetimi 01
> (`docs/records/reviews/MOD-0280-FU02/2026-10-27-design-01.md`, KOŞULLU). F-02 kararı ADR-005 "Ek karar 1"dedir.

## Künye

- Kuyruk: `hcm-leave` · iş: `HCM-LEAVE-06` · dal: `chore/ct/hcm-leave-ct-01`
- Kule: CT · İzin ve devamsızlık · modül: MOD-0280-FU02 Leave & Absence Management (öneri kimlik, `planned`)

## D-1 — Kiracı içi erişim ve rol kapsamı (F-03)

- **Sahip kararı:** `erisim-kiraci` (2026-10-27, CT · İzin ve devamsızlık sohbeti).
- **Karar (ilk dilim):**
  1. **Çalışan:** yalnız kendi izin talepleri ve kendi bakiyesi; başka kişinin kaydı görünmez.
  2. **Yönetici:** yalnız kendisine MOD-0023 iş akışında atanmış onay görevleri ve o talebin özeti. Yetki, iş akışının
     atama anlık görüntüsünden hesaplanır (FU01 emsali `TimesheetApprovalService.cs:391–435`). Ekip görünümü yok.
  3. **İK:** izin türlerini ve izin haklarını yönetir; kiracıdaki bütün izin kayıtlarını görür (kiracı geneli kapsam).
  4. **Ayrı anahtarlar:** okuma ve yazma izinleri ayrıdır (çalışanın kendi okuması, talep oluşturma/iptal, İK okuma,
     tür yönetimi, hak yönetimi). Anahtar adları PKS-001'e göre paket taslağında verilir; `platform.*` ve
     `hcm.time-attendance-leave.*` kullanılmaz.
  5. **Onaylayan bulma:** FU01 deseni (`ApproverResolver.cs`): pozisyon → bağlı olunan pozisyon → yönetici; zincir yoksa
     havuz. Kişi kendi talebini onaylayamaz (havuzda dahil). Onaylayan bulunamazsa talep gönderilemez; otomatik onay yok.
  6. **Kiracı sınırı:** kiracı yalnız sunucuda çözülür; başka kiracının kaydı 404 döner (varlık sızdırılmaz).
- **Sonraya:** şirket (tüzel kişi) bazlı İK kapsamı sonraki dilime yazılır; çok şirketli kurulumda İK'nın bütün
  şirketleri görmesi ilk dilimin bilinen sınırıdır ve pakette yazılır.
