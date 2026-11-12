using CasperMcp.Tools;
using CSPR.Cloud.Net.Objects.Validator;

namespace CasperMcp.Tests;

public class ValidatorTotalsTests
{
    private static ValidatorData V(ulong? delegators) => new() { DelegatorsNumber = delegators };

    [Fact]
    public void DelegatorsSummary_Sums_Every_Validator_When_The_Page_Holds_Them_All()
    {
        var page = new List<ValidatorData> { V(10_000), V(6_000), V(773), V(null) };

        var line = ValidatorTools.DelegatorsSummary(page, itemCount: 4);

        Assert.Equal(
            "**Delegators summed over all 4 validators:** 16,773 (an account delegating to several validators is counted once for each)",
            line);
    }

    [Fact]
    public void DelegatorsSummary_Says_Nothing_For_A_Partial_Page()
    {
        // A sum over the first 10 of 69 validators would read as the network's total.
        var page = new List<ValidatorData> { V(10), V(20) };

        Assert.Null(ValidatorTools.DelegatorsSummary(page, itemCount: 69));
        Assert.Null(ValidatorTools.DelegatorsSummary(new List<ValidatorData>(), itemCount: 0));
    }
}
