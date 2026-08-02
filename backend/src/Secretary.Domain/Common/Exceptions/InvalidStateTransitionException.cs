namespace Secretary.Domain.Exceptions;

/// <summary>E.g. cancelling an already-cancelled appointment, or accepting an escalation
/// someone else already accepted.</summary>
public sealed class InvalidStateTransitionException : DomainException
{
    public InvalidStateTransitionException(string entityName, object id, string fromState, string attemptedAction)
        : base($"{entityName} '{id}' is {fromState} and cannot be {attemptedAction}.")
    {
    }
}
