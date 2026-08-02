namespace Secretary.Domain.Exceptions;

/// <summary>Deliberately doesn't say whether the email or the password was wrong — that
/// distinction shouldn't be observable from outside.</summary>
public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException() : base("Email or password is incorrect.")
    {
    }
}
