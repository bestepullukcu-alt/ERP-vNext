# MOD-0188 Demand v2 — gerçek RabbitMQ kanıtı

Durum: **CI gerçek broker koşusu bekleniyor.** Bu belge tek başına teslimi kanıtlamaz. Demand v2 DRAFT ve PR #228 taslak kalır.

- Test: services/Diten.PlanningService/tests/Diten.PlanningService.Cycles.Tests/DemandRevisionRabbitMqIntegrationTests.cs
- CI: .github/workflows/service-tests.yml içindeki planning-tests matrisi. RABBITMQ_IMAGE mevcut sabit sürüm/digest; hazır olma kontrolü mevcut Start RabbitMQ (Eventing) adımı. Tek üyeli test Mongo scripts/test-env/mod0188-ci-mongo.sh ile ayrı başlar.
- CI komutu (filtresiz): dotnet test "$PROJECT" -c Debug --blame-hang --blame-hang-timeout 20m --logger "trx;LogFileName=$SUITE.trx" --results-directory "$RUNNER_TEMP/test-results"
- Kanıt ölçütü: MOD0188_TEST_RABBITMQ_ENABLED=true iken tüketici EventTransportMessage nesnesini gerçek broker kuyruğundan alır. Test eventId, correlationId, tenant ve olay adını karşılaştırır; aynı eventId için Mongo outbox DeliveryState=Published ve DispatchedAt kaydını doğrular. Published bu store'un teslim edilmiş durum adıdır.
- Yerel derleme: 0 uyarı / 0 hata. Main acfdf3fe2 merge sonrası geçici Mongo ile filtresiz PlanningService: **407 geçti, 0 başarısız, 0 atlandı**; broker opt-in yerel koşuda kapalı olduğundan bu sayı gerçek broker kanıtı değildir.
- Yerel sabotaj komutu (PowerShell, test Mongo açıkken): $env:MOD0188_TEST_RABBITMQ_ENABLED='true'; $env:MOD0188_TEST_RABBITMQ_HOST='127.0.0.1'; $env:MOD0188_TEST_RABBITMQ_PORT='1'; dotnet test services/Diten.PlanningService/tests/Diten.PlanningService.Cycles.Tests/Diten.PlanningService.Cycles.Tests.csproj --no-restore --filter 'FullyQualifiedName~RabbitMqOptIn_ConsumesCommittedOutboxEventAndMatchesMongoReceipt' --logger 'console;verbosity=normal'
- Sabotaj sonucu: **1 kırmızı**, çıkış kodu 1, MOD0188_RABBITMQ_CONNECTION_FAILED; alt neden Broker unreachable: guest@127.0.0.1:1/. Kapalı port testin yeşil kalmasına izin vermedi. Bu, broker kapalıyken gerçek teslimin gerçekleştiği iddiası değildir.
- Main acfdf3fe2 merge sonrası mimari takım: **16 kırmızı, 211 geçti, 0 atlandı**. Yeni broker koleksiyonu ve CI guard uyarlaması kendi odaklı muhafızlarını geçti; 16 kırmızının main ile birebir karşılaştırması CI koşusunda ayrıca kaydedilecek.
- CI iş bağlantısı ve gerçek broker sonucu: **PR #228 push koşusu tamamlanınca eklenecek.**
