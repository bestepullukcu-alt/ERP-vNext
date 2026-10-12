# MVP6 yeniden ölçüm — 2026-09-24

**Ölçü: ağırlıklı teslimat kilometre taşı endeksi; full-finish/efor yüzdesi değildir.**

| Modül | Pack | Contract | Bounded backend | Frontend | Entegrasyon | Test/VER | Endeks |
|---|---:|---:|---:|---:|---:|---:|---:|
| 0183 Shipment/POD | %75 | %75 | %100 | %0 | %25 | %75 | **%60.00** |
| 0184 Carrier | %100 | %75 | %100 | %75 | %75 | %75 | **%85.00** |
| 0185 Loads | %75 | %75 | %100 | %0 | %25 | %75 | **%60.00** |
| 0186 Returns | %75 | %75 | %100 | %0 | %50 | %75 | **%63.75** |
| 0187 Claims | %75 | %75 | %100 | %0 | %50 | %75 | **%63.75** |
| 0190 S&OP | %75 | %75 | %100 | %0 | %25 | %75 | **%60.00** |
| 0192 Capacity | %75 | %75 | %100 | %0 | %25 | %75 | **%60.00** |
| 0147 Supplier Performance | %50 | %50 | %0 | %0 | %0 | %0 | **%10.00** |
| 0148 Supplier Portal | %50 | %50 | %0 | %0 | %0 | %0 | **%10.00** |
| **MVP6** | %72.2 | %69.4 | %77.8 | %8.3 | %30.6 | %58.3 | **%52.50** |

Full E5/G5 kapanışı: **0/9**. Eski ≈48 ile delta hesaplanmaz: yöntem değişti. Bu yeni başlangıç ölçümüdür.

## Somut öncelikler
1. Auth F-01/F-02 bağımsız Phase2; ardından gerçek Carrier Auth→MVC→Gateway E2E.
2. Carrier browser final acceptance ve açık shared-search/PNG disposition.
3. Diğer altı backend modülünün UI scope/acceptance paketlerini ayrı sahiplikte tanımlamak; otomatik runtime yetkisi yok.
4. Supplier DC-01…05 gerçek kararları; tekrar spec/approval-search döngüsü yok.
5. Mock/fixture kullanılan Loads ve Planning seam’lerini live integration olarak işaretlememek; full golden flow için ayrı iş.

## Audit bulgusu
DEV token rework raporu Auth build için 0 warning, Platform için 32 ve MDM için 5 pre-existing warning bildiriyor. Önceki kısa özetlerdeki toplu 0-warning iddiası bu rapora taşınmadı. İlk 27017 fallback denemesi etki-yok kabul edilmedi.

## Kanıt ve hesap
[Yöntem](METHOD.md) · [216 checkpoint](DELIVERABLES.tsv) · [Input hashleri](INPUT-HASHES.tsv)
