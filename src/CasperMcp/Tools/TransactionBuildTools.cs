using System.ComponentModel;
using CasperMcp.Writes;
using ModelContextProtocol.Server;

namespace CasperMcp.Tools;

/// <summary>
/// Local-only tools that BUILD unsigned Casper transactions (no key, no network). Each returns the
/// unsigned transaction JSON plus a human-readable preview; pass the JSON to SignAndSubmitTransaction.
/// Not annotated [McpServerToolType] — registered explicitly in the stdio-write branch (Task 10)
/// and never picked up by the HTTP assembly scan.
/// </summary>
public static class TransactionBuildTools
{
    [McpServerTool(Name = "BuildTransferTransaction"),
     Description("Build an UNSIGNED native CSPR transfer from the local signer's account. Returns the unsigned transaction JSON and a preview (amount in CSPR and motes, recipient, network, fee, hash). Does not sign or submit. Recipient must be a public key hex.")]
    public static string BuildTransferTransaction(
        CasperSigner signer,
        [Description("Recipient public key (hex, e.g. 01... or 02...)")] string recipient,
        [Description("Amount to send, in CSPR (e.g. 12.5)")] decimal amountCspr)
    {
        var (json, preview) = signer.BuildTransfer(recipient, amountCspr);
        return preview + "\n\n```json\n" + json + "\n```";
    }

    [McpServerTool(Name = "BuildDelegateTransaction"),
     Description("Build an UNSIGNED delegation (stake CSPR to a validator) from the local signer's account. Returns unsigned JSON + preview. Validator must be a public key hex.")]
    public static string BuildDelegateTransaction(
        CasperSigner signer,
        [Description("Validator public key (hex)")] string validator,
        [Description("Amount to delegate, in CSPR")] decimal amountCspr)
    {
        var (json, preview) = signer.BuildDelegate(validator, amountCspr);
        return preview + "\n\n```json\n" + json + "\n```";
    }

    [McpServerTool(Name = "BuildUndelegateTransaction"),
     Description("Build an UNSIGNED undelegation (unstake CSPR from a validator). Returns unsigned JSON + preview.")]
    public static string BuildUndelegateTransaction(
        CasperSigner signer,
        [Description("Validator public key (hex)")] string validator,
        [Description("Amount to undelegate, in CSPR")] decimal amountCspr)
    {
        var (json, preview) = signer.BuildUndelegate(validator, amountCspr);
        return preview + "\n\n```json\n" + json + "\n```";
    }

    [McpServerTool(Name = "BuildRedelegateTransaction"),
     Description("Build an UNSIGNED redelegation (move stake from one validator to another). Returns unsigned JSON + preview.")]
    public static string BuildRedelegateTransaction(
        CasperSigner signer,
        [Description("Current validator public key (hex)")] string fromValidator,
        [Description("Destination validator public key (hex)")] string toValidator,
        [Description("Amount to redelegate, in CSPR")] decimal amountCspr)
    {
        var (json, preview) = signer.BuildRedelegate(fromValidator, toValidator, amountCspr);
        return preview + "\n\n```json\n" + json + "\n```";
    }
}
