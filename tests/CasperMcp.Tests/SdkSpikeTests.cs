using System.Text.Json;
using Casper.Network.SDK.Types;

namespace CasperMcp.Tests;

public class SdkSpikeTests
{
    [Fact]
    public void Build_Serialize_Sign_RoundTrips()
    {
        var kp = KeyPair.CreateNew(KeyAlgo.ED25519);
        var recipient = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey;

        Transaction txn = new Transaction.NativeTransferBuilder()
            .From(kp.PublicKey).Target(recipient).Amount(2_500_000_000UL)
            .ChainName("casper-test").Payment(100_000_000UL, (byte)1).Build();

        var json = JsonSerializer.Serialize(txn);
        Assert.Contains("\"Version1\"", json);
        Assert.Contains("\"entry_point\":\"Transfer\"", json);

        var back = JsonSerializer.Deserialize<Transaction>(json)!;
        Assert.Equal("casper-test", back.ChainName);
        Assert.Equal(System.Numerics.BigInteger.Parse("2500000000"),
            back.Invocation.GetRuntimeArgValue("amount")!.ToBigInteger());

        txn.Sign(kp);
        Assert.Single(txn.Approvals);
    }
}
