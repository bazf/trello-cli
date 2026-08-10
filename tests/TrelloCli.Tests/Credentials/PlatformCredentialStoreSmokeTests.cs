using TrelloCli.Credentials;
using Xunit;

namespace TrelloCli.Tests.Credentials;

public sealed class PlatformCredentialStoreSmokeTests
{
    private const string OptInEnvironmentVariable = "TRELLO_CREDENTIAL_STORE_SMOKE_TESTS";

    [Fact]
    public async Task CurrentPlatform_RoundTripsAndCleansUpAnIsolatedCredential()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInEnvironmentVariable), "1", StringComparison.Ordinal))
            return;

        var suffix = Guid.NewGuid().ToString("N");
        var service = $"trello-cli-smoke-{suffix}";
        var account = $"trello-token-smoke-{suffix}";
        var token = $"trello-secret-smoke-{suffix}";
        var store = CreateStore(service, account);

        try
        {
            await store.DeleteTokenAsync();
            await store.SetTokenAsync(token);
            Assert.Equal(token, await store.GetTokenAsync());
        }
        finally
        {
            await store.DeleteTokenAsync();
        }

        Assert.Null(await store.GetTokenAsync());
    }

    private static ICredentialStore CreateStore(string service, string account)
    {
        if (OperatingSystem.IsWindows())
            return new WindowsCredentialStore(new WindowsCredentialManager(), service, account);
        if (OperatingSystem.IsMacOS())
            return new MacOsCredentialStore(new ProcessRunner(), service, account);
        if (OperatingSystem.IsLinux())
            return new LinuxCredentialStore(new ProcessRunner(), service, account);

        throw new PlatformNotSupportedException();
    }
}
