using System.ComponentModel;
using CasperMcp.Writes;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

/// <summary>
/// The only key-touching tool. Re-decodes the supplied unsigned transaction, enforces the local
/// policy (allowlist, caps, network, sender) on the REAL bytes, then signs in-process and submits.
/// The signature is never returned — only a transaction hash and status. A policy denial is returned
/// as a readable refusal, not an error.
/// Not annotated [McpServerToolType] — registered explicitly in the stdio-write branch (Task 10)
/// and never picked up by the HTTP assembly scan.
/// </summary>
public static class TransactionSignTools
{
    [McpServerTool(Name = "SignAndSubmitTransaction"),
     Description("Sign (with the LOCAL key) and submit an unsigned Casper transaction produced by a Build* tool. The signer re-validates the transaction against local policy (recipient/validator allowlist, per-tx and daily caps, network) before signing. Returns the transaction hash and submission status, or a refusal reason if policy blocks it. Never returns the signature or the private key.")]
    public static Task<string> SignAndSubmitTransaction(
        CasperSigner signer,
        [Description("The unsigned transaction JSON returned by a Build* tool")] string unsignedTransactionJson)
        => signer.SignAndSubmit(unsignedTransactionJson, Guid.NewGuid().ToString("n"));
}
