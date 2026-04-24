# MDM — Ownership Decisions

## Owned Modules
MDM domain bootstrap kapsamında parser çıktısıyla eşleşen modüller domain sahipliğine alındı.

## Ownership Rules
- Her module pack yalnızca kendi hedef modülünün iş kapsamını tanımlar.
- Domain dışı sahiplik gerektiren durumlar ilgili domain'e delege edilir.
- Cross-domain bağımlılıklar module pack `Dependencies` bölümünde açıkça listelenir.

## Cross-Domain Contracts
- PSS domain'i ile kimlik/yetki ve platform entegrasyon sınırları korunur.
- ESBP domain'i ile analitik/performans kullanımında MDM verisi tüketim sınırları korunur.
