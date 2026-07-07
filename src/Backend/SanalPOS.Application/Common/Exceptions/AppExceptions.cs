using FluentValidation.Results;

namespace SanalPOS.Application.Common.Exceptions;

/// <summary>Validasyon hataları; API'da RFC 7807 formatında 400 döner.</summary>
public class SanalPosValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public SanalPosValidationException(IEnumerable<ValidationFailure> failures)
        : base("Bir veya daha fazla validasyon hatası oluştu.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());
    }
}

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} bulunamadı. (Anahtar: {key})")
    {
    }
}

/// <summary>Aynı idempotency key ile farklı gövde gibi çakışmalarda 409 döner.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
