using System.Text.Json;
using Casper.Network.SDK.Types;
using CasperMcp.Writes;

namespace CasperMcp.Tests;

public class CasperTransactionBuilderTests
{
    private static CasperTransactionBuilder Builder(out PublicKey signer)
    {
        var kp = KeyPair.CreateNew(KeyAlgo.ED25519);
        signer = kp.PublicKey;
        return new CasperTransactionBuilder(kp.PublicKey, chainName: "casper-test");
    }

    [Fact]
    public void BuildTransfer_Produces_Decodable_Transfer()
    {
        var b = Builder(out var signer);
        var recipient = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey.ToString();
        var (json, preview) = b.BuildTransfer(recipient, 12.5m);

        var intent = TransactionIntrospector.FromUnsignedJson(json);
        Assert.Equal(WriteKind.Transfer, intent.Kind);
        Assert.Equal(signer.ToString().ToLowerInvariant(), intent.SenderPublicKeyHex);
        Assert.Equal(recipient.ToLowerInvariant(), intent.TargetPublicKeyHex);
        Assert.Equal(new System.Numerics.BigInteger(12_500_000_000UL), intent.AmountMotes); // 12.5 CSPR
        Assert.Contains("12.5", preview);
        Assert.Contains("12500000000", preview); // motes shown too
        Assert.Contains("casper-test", preview);
    }

    [Fact]
    public void BuildDelegate_Produces_Decodable_Delegate()
    {
        var b = Builder(out _);
        var validator = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey.ToString();
        var (json, _) = b.BuildDelegate(validator, 500m);
        var intent = TransactionIntrospector.FromUnsignedJson(json);
        Assert.Equal(WriteKind.Delegate, intent.Kind);
        Assert.Equal(validator.ToLowerInvariant(), intent.TargetPublicKeyHex);
    }

    [Fact]
    public void BuildRedelegate_Has_Both_Validators()
    {
        var b = Builder(out _);
        var from = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey.ToString();
        var to = KeyPair.CreateNew(KeyAlgo.ED25519).PublicKey.ToString();
        var (json, _) = b.BuildRedelegate(from, to, 50m);
        var intent = TransactionIntrospector.FromUnsignedJson(json);
        Assert.Equal(WriteKind.Redelegate, intent.Kind);
        Assert.Equal(from.ToLowerInvariant(), intent.TargetPublicKeyHex);
        Assert.Equal(to.ToLowerInvariant(), intent.NewValidatorPublicKeyHex);
    }
}
