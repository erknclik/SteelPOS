using MassTransit;
using SanalPOS.BackgroundJobs;

var builder = Host.CreateApplicationBuilder(args);

var messagingProvider = builder.Configuration["Messaging:Provider"] ?? "RabbitMq";
builder.Services.AddMassTransit(x =>
{
    if (messagingProvider == "Kafka")
    {
        x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        x.AddRider(rider =>
        {
            rider.AddProducer<SanalPOS.Contracts.DailyReconciliationRequestedEvent>("daily-reconciliation-requested");
            rider.UsingKafka((context, k) =>
                k.Host(builder.Configuration["Messaging:Kafka:BootstrapServers"] ?? "localhost:9092"));
        });
    }
    else
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(
                builder.Configuration["Messaging:RabbitMq:Host"] ?? "localhost",
                builder.Configuration["Messaging:RabbitMq:VirtualHost"] ?? "/",
                h =>
                {
                    h.Username(builder.Configuration["Messaging:RabbitMq:Username"] ?? "guest");
                    h.Password(builder.Configuration["Messaging:RabbitMq:Password"] ?? "guest");
                });
            cfg.ConfigureEndpoints(context);
        });
    }
});

builder.Services.AddHostedService<DailyReconciliationJob>();

var host = builder.Build();
host.Run();
