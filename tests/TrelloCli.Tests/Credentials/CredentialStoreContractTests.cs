using TrelloCli.Services;
using Xunit;

namespace TrelloCli.Tests.Credentials;

public class CredentialStoreContractTests
{
    [Fact]
    public void CredentialStoreContract_ExposesAsynchronousTokenOperations()
    {
        var credentialStoreType = typeof(ConfigService).Assembly.GetType("TrelloCli.Credentials.ICredentialStore");

        Assert.NotNull(credentialStoreType);
        Assert.Contains(credentialStoreType!.GetMethods(), method => method.Name == "GetTokenAsync");
        Assert.Contains(credentialStoreType.GetMethods(), method => method.Name == "SetTokenAsync");
        Assert.Contains(credentialStoreType.GetMethods(), method => method.Name == "DeleteTokenAsync");
    }

    [Fact]
    public void CredentialStoreSubsystem_ExposesFactoryAndInjectableProcessRunner()
    {
        var assembly = typeof(ConfigService).Assembly;

        Assert.NotNull(assembly.GetType("TrelloCli.Credentials.CredentialStoreFactory"));
        Assert.NotNull(assembly.GetType("TrelloCli.Credentials.IProcessRunner"));
        Assert.NotNull(assembly.GetType("TrelloCli.Credentials.ProcessRunRequest"));
        Assert.NotNull(assembly.GetType("TrelloCli.Credentials.ProcessRunResult"));
    }

    [Fact]
    public void CredentialStoreFactory_ExposesRuntimeCreation()
    {
        var createMethod = typeof(ConfigService).Assembly
            .GetType("TrelloCli.Credentials.CredentialStoreFactory")!
            .GetMethod("Create");

        Assert.NotNull(createMethod);
    }
}
