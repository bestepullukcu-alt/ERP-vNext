# Commercial Suite — System-of-Record (SoR) Boundary

> CRM neyi sahiplenir, neyi yalnız tüketir? Bu matris domain boundary leak'lerini ve müşteri-truth dublikasyonunu önler.
> Otorite: bu domain-config + Blueprint `SoR_Map`. Çakışma EA refinement'e gider.

## CRM sahiplenir (owns)

| Object | Owner Module | Not |
|---|---|---|
| Account / Customer / WorkPlace (CRM view) | MOD-0149 | Blueprint SoR: accounts/customers (CRM view), hierarchies, account attributes |
| Account hierarchy | MOD-0149 | Hierarchy trace + evidence pack |
| Contact / Relationship / Affiliation | MOD-0150 | Consent buraya **gömülmez** |
| Territory / Zone / MicroZone tanımı + rep/account assignment | MOD-0151 | MicroZone burada **tanımlanır** |
| Lead | MOD-0152 | Generic CRM çekirdeği |
| Opportunity / Pipeline | MOD-0153 | Generic CRM çekirdeği |
| Forecast / Quota | MOD-0154 | — |
| Visit Plan / MicroTarget / Visit / Visit Report / route plan | MOD-0155 | MicroZone'u **tüketir**, tanımlamaz |
| Consent / Preference / consent history | MOD-0164 | Contact değil, **Consent** sahiplenir |
| Campaign / CyclePeriod / campaign execution & results | MOD-0165 | Segment sahiplenmez |
| Journey / Automation | MOD-0166 | Campaign execution ≠ Journey automation |
| Segment / TargetCustomer / UCLN / SubjectList / StrategyTemplate | MOD-0167 | Segment burada **sahiplenilir** |

## CRM sahiplenmez (does NOT own — yalnız tüketir)

| Object | Correct Owner | CRM Relationship | Legacy Risk | Decision |
|---|---|---|---|---|
| Country / City / District | MOD-0048 Reference Data | read-only consume | Legacy CRM'de gömülü coğrafya kopyası | CRM kopya tutmaz; reference lookup |
| Generic lookup/reference set | MOD-0048 | read-only consume | Hardcoded fallback | Fallback list yasak |
| Employee / Sales Rep master | MOD-0288 / HR-Org | read-only consume | Legacy MR = employee kopyası | Rep = Org Person referansı |
| Business Unit master | MOD-0288 / Platform-Org | read-only consume | — | reference |
| Brand / Product / SKU | MDM / Product | read-only consume | Legacy Property/PropertyList ürün kopyası | MDM SoR; CRM referans |
| Auth / Role / Permission engine | MOD-0018 / AuthService | consume (yeni engine yok) | Legacy kendi rol sistemi | CRM MOD-0018'e entegre olur |
| Navigation engine | tenant shell / ModulePageDescriptor loader | consume | — | CRM menü loader yazmaz |
| Gateway global routing policy | integration-agent (ocelot.json) | consume | — | CRM route ekleme integration-agent |
| Legacy Archive layout / controllers / views | FROZEN | yok | Kod taşıma cazibesi | Taşınmaz; yalnız business-rule hafızası |

## Kritik sınır kuralları (özet)

1. **Consent** MOD-0150'de değil, **MOD-0164**'te.
2. **Segment / TargetCustomer** MOD-0165'te değil, **MOD-0167**'de.
3. **Visit / MicroTarget** MOD-0149'a gömülmez, **MOD-0155**'te.
4. **MicroZone** MOD-0151'de tanımlanır; MOD-0155 rota planlama için tüketir.
5. **Campaign execution** MOD-0165; **Journey automation** MOD-0166 — ayrı SoR.
6. Lead / Opportunity generic CRM çekirdeğidir; pharma field-sales içine gömülmez.

## EA-TBD (açık SoR sınırları)

- **HCP identity SoR:** doktor/eczacı/hastane kimliği CRM Account (MOD-0149) mi yoksa MDM master mı? Legacy pharma'da
  CRM içindeydi; kurumsal CRM + MDM stratejisinde bu **ayrıştırılmalı**. EA kararı gerekli.
- **O2C bridge SoR:** MOD-0169 Billing & Invoicing, MOD-0170/0171/0172 — Finance / Order-Management domain'i ile
  paylaşımlı SoR olabilir. Blueprint bunları "Order-to-Cash (Bridge)" olarak işaretler. Kesin sahiplik EA refinement.
- **Time/Effort overlap:** Field visit efor kaydı ↔ MOD-0280 Time Entry SoR (PPM'deki DCP-003 ASSUMPTION rejimine benzer).
