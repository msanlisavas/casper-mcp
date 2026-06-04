using System.Numerics;
using System.Text.Json;
using Casper.Network.SDK.Types;
using CasperMcp.Writes;

namespace CasperMcp.Tests;

public class TransactionIntrospectorTests
{
    private static string TransferJson(PublicKey from, PublicKey to, ulong motes, string chain = "casper-test") =>
        JsonSerializer.Serialize(new Transaction.NativeTransferBuilder()
            .From(from).Target(to).Amount(motes).ChainName(chain).Payment(100_000_000UL, (byte)1).Build());

    private static string DelegateJson(PublicKey from, PublicKey validator, ulong motes) =>
        JsonSerializer.Serialize(new Transaction.NativeDelegateBuilder()
            .From(from).Validator(validator).Amount(motes).ChainName("casper-test").Payment(2_500_000_000UL, (byte)1).Build());

    [Fact]
    public void Decodes_Transfer()
    {
        var from = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey;
        var to = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey;
        var intent = TransactionIntrospector.FromUnsignedJson(TransferJson(from, to, 2_500_000_000UL));
        Assert.Equal(WriteKind.Transfer, intent.Kind);
        Assert.Equal(from.ToString().ToLowerInvariant(), intent.SenderPublicKeyHex);
        Assert.Equal(to.ToString().ToLowerInvariant(), intent.TargetPublicKeyHex);
        Assert.Equal(new BigInteger(2_500_000_000UL), intent.AmountMotes);
        Assert.Equal("casper-test", intent.ChainName);
    }

    [Fact]
    public void Decodes_Delegate_With_Validator()
    {
        var from = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey;
        var validator = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey;
        var intent = TransactionIntrospector.FromUnsignedJson(DelegateJson(from, validator, 500_000_000_000UL));
        Assert.Equal(WriteKind.Delegate, intent.Kind);
        Assert.Equal(validator.ToString().ToLowerInvariant(), intent.TargetPublicKeyHex);
        Assert.Equal(new BigInteger(500_000_000_000UL), intent.AmountMotes);
    }

    [Fact]
    public void Rejects_Legacy_Deploy_Envelope()
    {
        var ex = Assert.Throws<TransactionDecodeException>(() =>
            TransactionIntrospector.FromUnsignedJson("""{ "Deploy": { "hash": "00" }, "Version1": null }"""));
        Assert.Contains("deploy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_NonNative_Target()
    {
        var json = """
            { "Deploy": null, "Version1": { "payload": { "chain_name":"casper-test",
            "initiator_addr": { "PublicKey": "01aa" },
            "fields": { "entry_point":"Call", "target":"Stored", "args": { "Named": [] } } } } }
            """;
        Assert.Throws<TransactionDecodeException>(() => TransactionIntrospector.FromUnsignedJson(json));
    }

    [Fact]
    public void Rejects_Garbage()
    {
        Assert.Throws<TransactionDecodeException>(() => TransactionIntrospector.FromUnsignedJson("not json"));
    }
}
