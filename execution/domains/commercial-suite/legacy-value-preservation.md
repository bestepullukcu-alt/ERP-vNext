# Commercial Suite — Legacy Value Preservation

> **İlke:** Eski CRM kodu doğrudan taşınmaz. Değerli olan **business-rule hafızasıdır** — davranış, kısıt, lifecycle,
> hesaplama kuralları. Legacy Archive (controllers/views/layout) FROZEN'dır; kod değil, kural çıkarılır.

## Preservation method sözlüğü

- **Rule capture:** İş kuralı module pack'in "Validation Rules" / "Failure Path" bölümüne yazılır (greenfield implement).
- **Reference schema:** Legacy entity alanları yeni entity tasarımında referans alınır; şema kopyalanmaz.
- **Do-not-migrate:** Kod/controller/view taşınmaz; yalnız kavram korunur.

## Legacy Value Matrix

| Legacy Asset | Source | Target Module | Preservation Method | Do-not-migrate Notes |
|---|---|---|---|---|
| MicroTarget | Legacy pharma field-force | MOD-0155 | Rule capture (targeting cadence, atama) | Controller/view taşınmaz |
| Activity / Visit | Legacy pharma | MOD-0155 | Reference schema + rule capture | Legacy status kolonları birebir kopyalanmaz |
| ActivityReport | Legacy pharma | MOD-0155 | Rule capture (rapor zorunluluk kuralları) | Legacy rapor formu taşınmaz |
| Visit status lifecycle | Legacy pharma | MOD-0155 | Rule capture (state machine) | Yeni lifecycle greenfield modellenir |
| Ziyaret çakışma kontrolü | Legacy pharma | MOD-0155 | Rule capture (overlap validation) | — |
| Aynı gün aynı activity type engeli | Legacy pharma | MOD-0155 | Rule capture (dedup rule) | — |
| Frequency / cadence | Legacy pharma | MOD-0155 (+ MOD-0167 hedefleme) | Rule capture | Frequency veri kaynağı EA-TBD (bkz. open q.) |
| MR zone / micro-zone yetkisi | Legacy pharma | MOD-0151 (tanım) + MOD-0155 (tüketim) | Rule capture + ABAC (FU15) | Legacy yetki tablosu taşınmaz |
| Schedule engine | Legacy pharma | MOD-0155 | Rule capture (planlama algoritması) | Kod taşınmaz; algoritma yeniden yazılır |
| Hastane doktorları → yakın eczane rota önerisi | Legacy pharma | MOD-0155 route-plan | Rule capture (geo-proximity rota) | Geo veri MOD-0048/MDM'den; legacy kopya değil |
| Client / WorkPlace / Property / PropertyList / ClientCategory | DitenCRM | MOD-0149 / MOD-0150 | Reference schema | Ürün (Property) SoR = MDM, kopyalanmaz |
| Campaign / PromoCampaign / CyclePeriod | Legacy Campaign | MOD-0165 | Rule capture (cycle period kuralları) | Execution greenfield |
| TargetCustomer / UCLN / StrategyTemplate / SubjectList / ForWhom | CrmV2 | MOD-0167 | Reference schema + rule capture | Segment eval greenfield |

## Greenfield (legacy referansı düşük/yok)

MOD-0164 Consent · MOD-0152 Lead · MOD-0153 Opportunity · MOD-0154 Forecast · MOD-0166 Journey — büyük ölçüde
greenfield. Legacy'den yalnız domain terminolojisi alınır.

## MOD-0155 en yüksek değerli legacy alan

Field Sales / Visit Planning, legacy pharma sisteminin en olgun ve kurala en zengin alanıdır. Öneri: **implementation
Blueprint W-4 sırasına göre sonra**, fakat **legacy preservation design pack erken** hazırlanır (kurallar unutulmadan
çıkarılır). Bu, `crm-field-sales-extension` lane'inin "pack erken / impl geç" önceliğiyle uyumludur
(bkz. [crm-build-lanes.md](crm-build-lanes.md)).

## Open questions (EA-TBD)

- **Frequency verisi** nereden beslenecek (legacy tablo mu, yeni cadence config mi)?
- **Daywork / VisitMix** kaynakları nerede (legacy'de mevcut mu, greenfield mi)?
- **HCP identity SoR** CRM mi MDM mi (doktor/eczacı kimliği)?
