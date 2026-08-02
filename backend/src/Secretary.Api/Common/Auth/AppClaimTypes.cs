namespace Secretary.Api.Auth;

public static class AppClaimTypes
{
    public const string TenantId = "tenant_id";

    /// <summary>Short claim key ("role"), not the long ClaimTypes.Role URI — keeps the
    /// decoded JWT payload clean for the frontend to read directly.</summary>
    public const string Role = "role";
}
