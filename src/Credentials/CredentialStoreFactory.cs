namespace TrelloCli.Credentials;

public sealed class CredentialStoreFactory
{
    private readonly Func<CredentialStorePlatform> _platformDetector;
    private readonly Func<IProcessRunner> _processRunnerFactory;

    public CredentialStoreFactory(
        Func<CredentialStorePlatform>? platformDetector = null,
        Func<IProcessRunner>? processRunnerFactory = null)
    {
        _platformDetector = platformDetector ?? DetectCurrentPlatform;
        _processRunnerFactory = processRunnerFactory ?? (() => new ProcessRunner());
    }

    public ICredentialStore Create()
    {
        return _platformDetector() switch
        {
            CredentialStorePlatform.Windows => new WindowsCredentialStore(),
            CredentialStorePlatform.MacOS => new MacOsCredentialStore(_processRunnerFactory()),
            CredentialStorePlatform.Linux => new LinuxCredentialStore(_processRunnerFactory()),
            _ => throw new CredentialStoreException(
                CredentialStoreErrorCategory.StoreUnavailable,
                "The credential store is unavailable.")
        };
    }

    public static CredentialStorePlatform DetectCurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) return CredentialStorePlatform.Windows;
        if (OperatingSystem.IsMacOS()) return CredentialStorePlatform.MacOS;
        if (OperatingSystem.IsLinux()) return CredentialStorePlatform.Linux;
        return CredentialStorePlatform.Unsupported;
    }
}

public enum CredentialStorePlatform
{
    Windows,
    MacOS,
    Linux,
    Unsupported
}
