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

Environment.ExitCode = 2;
