namespace RepoLens.IntegrationTests;

public class AssemblySanityCheck
{
    [Fact]
    public void IntegrationTestAssembly_LoadsSuccessfully()
    {
        Assert.NotNull(typeof(AssemblySanityCheck).Assembly);
    }
}
