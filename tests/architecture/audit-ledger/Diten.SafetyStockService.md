# Denetim defteri - Diten.SafetyStockService

AUD-001 gecici `c` yolu, Ali'nin MOD-0191 bes komut karari. Bu yerel iz Platform merkezi denetim gunlugu degildir. `BL-501 bekliyor` (merkezi ekip, MVP'ler sonrasi); baglandiginda yerel iz korunur. Iz gorunurlugu bekliyor (dilim: policy ekrani).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| safety-stock-policy-izi | c | yazıcı | IPolicyMutationStore.ExecuteAsync |
| safety-stock-manuel-miktar-izi | c | yazıcı | IManualQuantityStore.ExecuteAsync |

`PolicyMutationCommand` islemleri: Create, Revise, Submit, Approve, Reject. Her biri `MongoPolicyMutationStore.ExecuteAsync` icinde policy/revision ve `PolicyAuditAppender.AppendAsync` kaydini ayni Mongo transaction'ina yazar. Audit ekleme hatasinda transaction geri alinir (`PolicyMutationMongoTests.AuditWriteFailure_RollsBackEachCommand`). Yerel audit guncelleme veya silme arayuzu sunmaz.

`ManualQuantityCommand` islemleri: Create, Submit, Approve. `ManualQuantityMongoStore.ExecuteAsync` miktar ve audit kaydini ayni Mongo transaction'ina yazar. Audit hatasinda miktar, durum ve policy surum kilidi geri alinir (`ManualQuantityMongoTests.AuditFailure_RollsBackQuantityAndPolicyLock`). Gerekceli yerel iz gorunurlugu bekliyor (dilim: manuel miktar ekrani). BL-501 bekliyor; merkezi audit gelince bu yerel iz korunur.

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

BL-501 baglantisi bekliyor; kabul edilmis yerel iz silinmez.

## K2 borcu

Yok.

## Yazan sorgular

Yok.
