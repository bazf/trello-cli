using TrelloCli.Credentials;
using TrelloCli.Services;
using Xunit;

namespace TrelloCli.Tests.Credentials;

public class WindowsCredentialStoreContractTests
{
    [Fact]
    public void WindowsCredentialStore_ExposesAnInjectableCredentialManagerBoundary()
    {
        var type = typeof(ConfigService).Assembly.GetType("TrelloCli.Credentials.IWindowsCredentialManager");

        Assert.NotNull(type);
    }

    [Fact]
    public void WindowsCredentialStore_AcceptsTheCredentialManagerBoundary()
    {
        var constructor = typeof(WindowsCredentialStore).GetConstructor([typeof(IWindowsCredentialManager)]);

        Assert.NotNull(constructor);
    }
}
