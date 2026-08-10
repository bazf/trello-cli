using TrelloCli.Credentials;
using Xunit;

namespace TrelloCli.Tests.Credentials;

public class CredentialStoreFactoryTests
{
    [Theory]
    [InlineData(CredentialStorePlatform.Windows, "WindowsCredentialStore")]
    [InlineData(CredentialStorePlatform.MacOS, "MacOsCredentialStore")]
    [InlineData(CredentialStorePlatform.Linux, "LinuxCredentialStore")]
    public void Create_SelectsTheStoreForTheDetectedPlatform(
        CredentialStorePlatform platform,
        string expectedStoreType)
    {
        var store = new CredentialStoreFactory(() => platform).Create();

        Assert.Equal(expectedStoreType, store.GetType().Name);
    }

    [Fact]
    public void Create_RejectsUnsupportedPlatformsWithASafeUnavailableError()
    {
        var exception = Assert.Throws<CredentialStoreException>(
            () => new CredentialStoreFactory(() => CredentialStorePlatform.Unsupported).Create());

        Assert.Equal(CredentialStoreErrorCategory.StoreUnavailable, exception.Category);
        Assert.DoesNotContain("Unsupported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
