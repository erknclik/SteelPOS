# SanalPOS - Test Stratejisi

## 1. Test Piramidi

```
        /\
       /E2E\          <- az sayıda, kritik akışlar (Playwright)
      /------\
     /Integr. \       <- API + DB + Redis + MQ (Testcontainers)
    /----------\
   /   Unit     \     <- çok sayıda, hızlı, izole (xUnit)
  /--------------\
```

## 2. Backend Unit Testler

- **Framework**: xUnit + FluentAssertions + NSubstitute (veya Moq)
- Kapsam: Domain entity davranışları, Application handler'lar (repository/servisler mock'lanır), FluentValidation validator'ları
- Kod kapsamı hedefi: Domain ve Application katmanlarında **%80+**

```csharp
public class CreatePaymentCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnApprovedTransaction()
    {
        // Arrange
        var repository = Substitute.For<IPaymentTransactionRepository>();
        var bankAdapter = Substitute.For<IBankProviderAdapter>();
        bankAdapter.ChargeAsync(Arg.Any<ChargeRequest>())
            .Returns(new ChargeResult { IsApproved = true, AuthCode = "482910" });

        var handler = new CreatePaymentCommandHandler(repository, bankAdapter, /* ... */);
        var command = new CreatePaymentCommand(/* ... */);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(TransactionStatus.Approved);
        await repository.Received(1).AddAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
    }
}
```

## 3. Backend Integration Testler

- **Testcontainers** ile gerçek PostgreSQL, Redis, RabbitMQ konteynerleri ayağa kaldırılır (test ortamında mock değil gerçek davranış doğrulanır)
- `WebApplicationFactory<Program>` ile API uçtan uca (in-memory server) test edilir
- Her iki ORM provider'ı için de (EF Core ve NHibernate) **aynı test seti** ayrı ayrı çalıştırılır (parametrik/`[Theory]` ile provider bazlı çalıştırma), tutarlılık garanti altına alınır

```csharp
public class PaymentsApiTests : IClassFixture<SanalPosApiFactory>
{
    [Theory]
    [InlineData("EfCore")]
    [InlineData("NHibernate")]
    public async Task CreatePayment_WithValidRequest_Returns201(string persistenceProvider)
    {
        var client = _factory.WithPersistenceProvider(persistenceProvider).CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/payments", validRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

## 4. Mimari Testler (Architecture Tests)

`NetArchTest.Rules` ile katman bağımlılık kurallarının otomatik doğrulanması:

```csharp
[Fact]
public void Domain_ShouldNotHaveDependencyOnInfrastructure()
{
    var result = Types.InAssembly(typeof(PaymentTransaction).Assembly)
        .Should()
        .NotHaveDependencyOn("SanalPOS.Infrastructure")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}
```

## 5. Sözleşme Testleri (Contract Tests)

Mesaj kuyruğu event'leri için (`SanalPOS.Contracts` içindeki record'lar) publisher/consumer arasında şema uyumu, serileştirme testleri ile doğrulanır; breaking change durumunda CI kırılır.

## 6. Frontend Testler

- **Vitest + Testing Library**: Bileşen/hook unit testleri
- **MSW**: API mock'lama ile form gönderim/hata senaryoları test edilir
- **Playwright** (e2e): Giriş, ödeme oluşturma, iade, rapor indirme kritik akışları

## 7. Performans ve Yük Testleri

- **k6** veya **NBomber** ile ödeme oluşturma endpoint'i için yük testi (ör. 500 eşzamanlı istek, p95 gecikme hedefi < 300ms)
- Redis ve mesaj kuyruğu darboğaz analizleri yük testi sırasında izlenir (Grafana dashboard)

## 8. Güvenlik Testleri

- OWASP ZAP baseline scan (CI pipeline'ında otomatik)
- Bağımlılık zafiyet taraması (`dotnet list package --vulnerable`, `npm audit`)

## 9. Test Verisi Yönetimi

- **Bogus** kütüphanesi ile gerçekçi ama gerçek olmayan test verisi üretimi (özellikle kart numaraları için Luhn algoritmasına uygun ama gerçek olmayan test kartları kullanılır — banka test ortamlarının sağladığı resmi test kartları tercih edilir)
- Her integration test kendi izole veri setiyle çalışır (test bitiminde temizlik / transaction rollback)

## 10. CI Pipeline'da Test Kapıları

```
1. dotnet restore
2. dotnet build
3. dotnet test (Unit) - kapsam raporu üretir
4. dotnet test (Integration) - Testcontainers ile
5. npm ci && npm run test (Frontend)
6. Architecture tests
7. Security scan (dependency + OWASP ZAP baseline)
8. Kod kapsamı eşiği kontrolü (%80 altına düşerse pipeline kırılır)
```

Detaylar için bkz. [13-devops-dagitim.md](./13-devops-dagitim.md)
