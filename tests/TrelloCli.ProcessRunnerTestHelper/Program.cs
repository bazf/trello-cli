using System.Diagnostics;
using System.Globalization;

if (args.Length == 0)
{
    await Console.OpenStandardInput().CopyToAsync(Console.OpenStandardOutput());
    return;
}

if (args is ["--wait-without-reading-stdin"])
{
    await Task.Delay(TimeSpan.FromSeconds(2));
    return;
}

if (args is ["--write-output-and-error"])
{
    await Console.Out.WriteAsync("captured-output");
    await Console.Error.WriteAsync("captured-error");
    return;
}

if (args is ["--exit-with-descendant-holding-output", var pidFile])
{
    var startInfo = new ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add("--hold-output-open");
    using var descendant = Process.Start(startInfo) ?? throw new InvalidOperationException();
    await File.WriteAllTextAsync(pidFile, descendant.Id.ToString(CultureInfo.InvariantCulture));
    return;
}

if (args is ["--hold-output-open"])
{
    await Task.Delay(TimeSpan.FromSeconds(10));
    return;
}

Environment.ExitCode = 2;
