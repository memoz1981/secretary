namespace Secretary.Agents.Orders;

/// <summary>What the caller has asked for so far, held for the length of one call.
///
/// A basket rather than a lines array on PlaceOrder, for a reason the appointment line taught
/// us the hard way: a realtime model sent a tool call whose JSON arrived truncated mid-string,
/// and the more structure an argument has the more there is to arrive broken. Two scalars at a
/// time is the shape least likely to be mangled.
///
/// It also matches how people order. "Üç bidon su" then "bir də çörək" is two turns, not one
/// sentence to be assembled before anything can be recorded.
///
/// Scoped to the call, like everything else the tools hold.</summary>
public sealed class OrderDraft
{
    private readonly Dictionary<int, decimal> _quantities = [];
    private readonly Dictionary<int, string> _names = [];

    public bool IsEmpty => _quantities.Count == 0;

    public IReadOnlyList<(int ProductId, decimal Quantity)> Lines
        => _quantities.Select(kv => (kv.Key, kv.Value)).ToList();

    /// <summary>How much of one product is on the order already, so a cap can be judged against
    /// the total rather than one request at a time.</summary>
    public decimal QuantityOf(int productId) => _quantities.GetValueOrDefault(productId);

    /// <summary>Adds to the line rather than replacing it: "two bidons, and one more" is three.
    /// Returns the running quantity so the agent can say it back.</summary>
    public decimal Add(int productId, string productName, decimal quantity)
    {
        _names[productId] = productName;
        _quantities[productId] = _quantities.GetValueOrDefault(productId) + quantity;
        return _quantities[productId];
    }

    /// <summary>Corrects a line to an exact quantity — "yox, iki olsun". Removing it entirely is
    /// a quantity of zero.</summary>
    public void Set(int productId, string productName, decimal quantity)
    {
        if (quantity <= 0)
        {
            _quantities.Remove(productId);
            _names.Remove(productId);
            return;
        }

        _names[productId] = productName;
        _quantities[productId] = quantity;
    }

    public string Describe()
        => IsEmpty
            ? "Sifariş boşdur."
            : string.Join(", ", _quantities.Select(kv => $"{_names[kv.Key]} × {Trim(kv.Value)}"));

    /// <summary>Three, not 3.000 — the agent reads this aloud.</summary>
    private static string Trim(decimal quantity)
        => quantity == decimal.Truncate(quantity) ? ((long)quantity).ToString() : quantity.ToString("0.###");
}
