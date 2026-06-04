namespace CasperMcp.Writes;

public sealed record PolicyDecision(bool Allowed, string Reason)
{
    public static PolicyDecision Allow() => new(true, "ok");
    public static PolicyDecision Deny(string reason) => new(false, reason);
}
