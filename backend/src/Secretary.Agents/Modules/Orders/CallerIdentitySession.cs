namespace Secretary.Agents.Orders;

/// <summary>How far identifying this caller has got, for the length of one call.
///
/// It exists because a tool that answers the same question the same way forever will be asked it
/// forever. On a real call the model looked the same customer up four times, got an identical
/// CONFIRM_NEEDED each time, never once called ConfirmCustomer, and the caller gave up. Nothing
/// in that result said "you already have this — the next step is a different tool".
///
/// Scoped to the call, like the order draft.</summary>
public sealed class CallerIdentitySession
{
    public int? CustomerId { get; private set; }

    public bool IsConfirmed { get; private set; }

    /// <summary>How many times we have been asked to find this same customer.</summary>
    public int Lookups { get; private set; }

    public void Found(int customerId)
    {
        if (CustomerId == customerId)
        {
            Lookups++;
            return;
        }

        CustomerId = customerId;
        Lookups = 1;
        IsConfirmed = false;
    }

    public void Confirmed(int customerId)
    {
        CustomerId = customerId;
        IsConfirmed = true;
    }

    /// <summary>True once looking the same customer up again is plainly not working.</summary>
    public bool IsRepeating(int customerId) => CustomerId == customerId && Lookups > 1;
}
