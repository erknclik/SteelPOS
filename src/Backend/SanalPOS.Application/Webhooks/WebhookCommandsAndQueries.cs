using FluentValidation;
using MediatR;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Contracts;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Webhooks;

public sealed record WebhookSubscriptionDto(Guid Id, Guid MerchantId, string EventType, string TargetUrl, bool IsActive)
{
    public static WebhookSubscriptionDto FromEntity(WebhookSubscription w) =>
        new(w.Id, w.MerchantId, w.EventType, w.TargetUrl, w.IsActive);
}

public static class WebhookEventTypes
{
    public const string PaymentCompleted = "PaymentCompleted";
    public const string PaymentFailed = "PaymentFailed";
    public const string RefundCompleted = "RefundCompleted";

    public static readonly string[] All = [PaymentCompleted, PaymentFailed, RefundCompleted];
}

// ---- Abonelik oluşturma ----

public sealed record CreateWebhookSubscriptionCommand(Guid MerchantId, string EventType, string TargetUrl, string Secret)
    : IRequest<WebhookSubscriptionDto>;

public class CreateWebhookSubscriptionCommandValidator : AbstractValidator<CreateWebhookSubscriptionCommand>
{
    public CreateWebhookSubscriptionCommandValidator()
    {
        RuleFor(x => x.MerchantId).NotEmpty();
        RuleFor(x => x.EventType)
            .NotEmpty()
            .Must(t => WebhookEventTypes.All.Contains(t))
            .WithMessage($"Event tipi şunlardan biri olmalıdır: {string.Join(", ", WebhookEventTypes.All)}");
        RuleFor(x => x.TargetUrl).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Secret).NotEmpty().MinimumLength(16).MaximumLength(200)
            .WithMessage("Webhook secret'ı en az 16 karakter olmalıdır.");
    }
}

public class CreateWebhookSubscriptionCommandHandler : IRequestHandler<CreateWebhookSubscriptionCommand, WebhookSubscriptionDto>
{
    private readonly IWebhookSubscriptionRepository _repository;
    private readonly IMerchantRepository _merchantRepository;
    private readonly ISecretProtector _secretProtector;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWebhookSubscriptionCommandHandler(
        IWebhookSubscriptionRepository repository,
        IMerchantRepository merchantRepository,
        ISecretProtector secretProtector,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _merchantRepository = merchantRepository;
        _secretProtector = secretProtector;
        _unitOfWork = unitOfWork;
    }

    public async Task<WebhookSubscriptionDto> Handle(CreateWebhookSubscriptionCommand request, CancellationToken ct)
    {
        _ = await _merchantRepository.GetByIdAsync(request.MerchantId, ct)
            ?? throw new NotFoundException(nameof(Merchant), request.MerchantId);

        // Secret şifrelenerek saklanır (bkz. docs/11-guvenlik.md §3).
        var subscription = new WebhookSubscription(
            request.MerchantId, request.EventType, request.TargetUrl, _secretProtector.Protect(request.Secret));

        await _repository.AddAsync(subscription, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return WebhookSubscriptionDto.FromEntity(subscription);
    }
}

// ---- Abonelik silme ----

public sealed record DeleteWebhookSubscriptionCommand(Guid SubscriptionId) : IRequest<Unit>;

public class DeleteWebhookSubscriptionCommandHandler : IRequestHandler<DeleteWebhookSubscriptionCommand, Unit>
{
    private readonly IWebhookSubscriptionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteWebhookSubscriptionCommandHandler(IWebhookSubscriptionRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(DeleteWebhookSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct)
                           ?? throw new NotFoundException(nameof(WebhookSubscription), request.SubscriptionId);

        _repository.Remove(subscription);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ---- Test event'i gönderme ----

public sealed record TestWebhookCommand(Guid SubscriptionId) : IRequest<Unit>;

public class TestWebhookCommandHandler : IRequestHandler<TestWebhookCommand, Unit>
{
    private readonly IWebhookSubscriptionRepository _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICorrelationIdAccessor _correlationId;

    public TestWebhookCommandHandler(
        IWebhookSubscriptionRepository repository,
        IEventPublisher eventPublisher,
        ICorrelationIdAccessor correlationId)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _correlationId = correlationId;
    }

    public async Task<Unit> Handle(TestWebhookCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct)
                           ?? throw new NotFoundException(nameof(WebhookSubscription), request.SubscriptionId);

        await _eventPublisher.PublishAsync(new WebhookTestRequestedEvent(
            subscription.Id, subscription.MerchantId, DateTime.UtcNow, _correlationId.CorrelationId), ct);
        return Unit.Value;
    }
}

// ---- Abonelik listesi ----

public sealed record GetWebhookSubscriptionsQuery(Guid MerchantId) : IRequest<IReadOnlyList<WebhookSubscriptionDto>>;

public class GetWebhookSubscriptionsQueryHandler : IRequestHandler<GetWebhookSubscriptionsQuery, IReadOnlyList<WebhookSubscriptionDto>>
{
    private readonly IWebhookSubscriptionRepository _repository;

    public GetWebhookSubscriptionsQueryHandler(IWebhookSubscriptionRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<WebhookSubscriptionDto>> Handle(GetWebhookSubscriptionsQuery request, CancellationToken ct)
    {
        var subscriptions = await _repository.GetByMerchantAsync(request.MerchantId, ct);
        return subscriptions.Select(WebhookSubscriptionDto.FromEntity).ToList();
    }
}
