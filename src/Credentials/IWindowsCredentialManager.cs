using Meziantou.Framework.Win32;

namespace TrelloCli.Credentials;

public interface IWindowsCredentialManager
{
    string? Read(string target, string username);

    void Write(string target, string username, string token, bool localMachinePersistence);

    bool Delete(string target, string username);
}

public sealed class WindowsCredentialManager : IWindowsCredentialManager
{
#pragma warning disable CA1416 // The calls are guarded by IsWindowsVersionAtLeast.
    public string? Read(string target, string username)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600)) throw new PlatformNotSupportedException();
        var credential = CredentialManager.ReadCredential(target);
        return credential?.UserName == username ? credential.Password : null;
    }

    public void Write(string target, string username, string token, bool localMachinePersistence)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600)) throw new PlatformNotSupportedException();
        CredentialManager.WriteCredential(
            target,
            username,
            token,
            localMachinePersistence ? CredentialPersistence.LocalMachine : CredentialPersistence.Session);
    }

    public bool Delete(string target, string username)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600)) throw new PlatformNotSupportedException();
        try
        {
            CredentialManager.DeleteCredential(target);
            return true;
        }
        catch (System.ComponentModel.Win32Exception exception) when (exception.NativeErrorCode == 1168)
        {
            return false;
        }
    }
#pragma warning restore CA1416

}
