using System.Diagnostics;
using Xunit;

namespace TrelloCli.Tests.Packaging;

public sealed class UninstallScriptTests
{
    [Fact]
    public async Task DecliningCredentialRemoval_PreservesCredentialsAndStillUninstallsTheTool()
    {
        if (OperatingSystem.IsWindows()) return;

        using var harness = new UninstallHarness();

        var result = await harness.RunAsync("n\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Credentials preserved", result.StandardOutput);
        Assert.DoesNotContain("trello-cli --clear-auth", harness.CallLog);
        Assert.Contains("dotnet tool uninstall --global TrelloCli", harness.CallLog);
    }

    [Fact]
    public async Task AcceptingCredentialRemoval_ClearsCredentialsBeforeUninstallingTheTool()
    {
        if (OperatingSystem.IsWindows()) return;

        using var harness = new UninstallHarness();

        var result = await harness.RunAsync("y\n");

        Assert.Equal(0, result.ExitCode);
        var clearIndex = harness.CallLog.IndexOf("trello-cli --clear-auth", StringComparison.Ordinal);
        var uninstallIndex = harness.CallLog.IndexOf("dotnet tool uninstall --global TrelloCli", StringComparison.Ordinal);
        Assert.True(clearIndex >= 0, harness.CallLog);
        Assert.True(uninstallIndex > clearIndex, harness.CallLog);
    }

    [Fact]
    public async Task FailedCredentialRemoval_LeavesTheToolInstalledForARetry()
    {
        if (OperatingSystem.IsWindows()) return;

        using var harness = new UninstallHarness(clearSucceeds: false);

        var result = await harness.RunAsync("y\n");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Credential removal failed; the CLI will remain installed", result.StandardOutput);
        Assert.DoesNotContain("dotnet tool uninstall --global TrelloCli", harness.CallLog);
    }

    private sealed class UninstallHarness : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"trello-cli-uninstall-{Guid.NewGuid():N}");
        private readonly string _callLog;
        private readonly string _home;
        private readonly string _bin;

        public UninstallHarness(bool clearSucceeds = true)
        {
            _callLog = Path.Combine(_directory, "calls.log");
            _home = Path.Combine(_directory, "home");
            _bin = Path.Combine(_directory, "bin");
            Directory.CreateDirectory(_home);
            Directory.CreateDirectory(_bin);
            WriteExecutable(
                Path.Combine(_bin, "dotnet"),
                "#!/bin/bash\n" +
                "printf 'dotnet %s\\n' \"$*\" >> \"$TEST_CALL_LOG\"\n" +
                "if [ \"$1 $2\" = \"tool list\" ]; then printf 'trellocli 2.0.0\\n'; fi\n");
            WriteExecutable(
                Path.Combine(_bin, "trello-cli"),
                "#!/bin/bash\n" +
                "printf 'trello-cli %s\\n' \"$*\" >> \"$TEST_CALL_LOG\"\n" +
                $"printf '%s\\n' '{{\"ok\":{clearSucceeds.ToString().ToLowerInvariant()}}}'\n");
        }

        public string CallLog => File.Exists(_callLog) ? File.ReadAllText(_callLog) : string.Empty;

        public async Task<(int ExitCode, string StandardOutput)> RunAsync(string standardInput)
        {
            var script = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../..", "uninstall.sh"));
            var startInfo = new ProcessStartInfo("/bin/bash", script)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.Environment["HOME"] = _home;
            startInfo.Environment["TEST_CALL_LOG"] = _callLog;
            startInfo.Environment["PATH"] = $"{_bin}:{Environment.GetEnvironmentVariable("PATH")}";

            using var process = Process.Start(startInfo)!;
            await process.StandardInput.WriteAsync(standardInput);
            process.StandardInput.Close();
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
