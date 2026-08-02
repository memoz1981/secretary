namespace Secretary.Domain.Exceptions;

/// <summary>Base type for exceptions representing a business-rule violation, as opposed to
/// an unexpected/infrastructure failure. The Api layer's exception middleware maps these to
/// specific HTTP status codes instead of a generic 500.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}
