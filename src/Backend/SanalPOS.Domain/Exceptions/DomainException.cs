namespace SanalPOS.Domain.Exceptions;

/// <summary>İş kuralı ihlallerinde fırlatılır; API katmanında 422'ye çevrilir.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
