using Xunit;

namespace BantuBantu.Tests;

public class CiGateProofTests
{
    [Fact]
    public void IntentionalFailureToProveCiGateBlocks()
    {
        Assert.Fail("Intentional failure: proving the CI Gate turns red when backend tests fail.");
    }
}
