using System.Diagnostics;
using Xunit;

namespace TrelloCli.Tests.Packaging;

public sealed class InstallerScriptTests
{
    [Theory]
    [InlineData("Darwin", "macOS Keychain")]
    [InlineData("Linux", "Linux Secret Service")]
    [InlineData("MINGW64_NT-10.0", "Windows Credential Manager")]
    public async Task InstallOutput_IdentifiesTheCurrentPlatformCredentialBackendAndLinuxRequirements(
        string uname,
        string expectedBackend)
    {
        if (OperatingSystem.IsWindows()) return;

        using var harness = new InstallerHarness(uname);

        var result = await harness.RunAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(expectedBackend, result.Output);
        if (uname == "Linux")
        {
            Assert.Contains("libsecret-tools", result.Output);
            Assert.Contains("D-Bus", result.Output);
            Assert.Contains("unlocked Secret Service", result.Output);
        }

        foreach (var backend in new[] { "Windows Credential Manager", "macOS Keychain", "Linux Secret Service" })
        {
            if (backend != expectedBackend)
                Assert.DoesNotContain(backend, result.Output);
        }
    }

    private sealed class InstallerHarness : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"trello-cli-install-{Guid.NewGuid():N}");
        private readonly string _home;
        private readonly string _bin;

        public InstallerHarness(string uname)
        {
            _home = Path.Combine(_directory, "home");
            _bin = Path.Combine(_directory, "bin");
            Directory.CreateDirectory(_home);
            Directory.CreateDirectory(_bin);
            WriteExecutable(Path.Combine(_bin, "brew"), "#!/bin/bash\nexit 0\n");
            WriteExecutable(Path.Combine(_bin, "uname"), $"#!/bin/bash\nprintf '%s\\n' '{uname}'\n");
            WriteExecutable(
                Path.Combine(_bin, "dotnet"),
                "#!/bin/bash\n" +
                "if [ \"$1\" = \"--version\" ]; then printf '10.0.100\\n'; fi\n" +
                "exit 0\n");
        }

        public async Task<(int ExitCode, string Output)> RunAsync()
        {
            var script = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../..", "install.sh"));
            var startInfo = new ProcessStartInfo("/bin/bash", script)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.Environment["HOME"] = _home;
            startInfo.Environment["PATH"] = $"{_bin}:{Environment.GetEnvironmentVariable("PATH")}";

            using var process = Process.Start(startInfo)!;
            var output = await process.StandardOutput.ReadToEndAsync();
            output += await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, output);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }

        private static void WriteExecutable(string path, string contents)
        {
            File.WriteAllText(path, contents);
            if (OperatingSystem.IsWindows()) return;
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
