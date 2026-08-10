using TrelloCli;
using Xunit;

namespace TrelloCli.Tests.Cli;

public class ConsoleSecretReaderTests
{
    [Fact]
    public void ReadToken_AcceptsEnterAndBackspaceWithoutEchoingCharacters()
    {
        var keys = new Queue<ConsoleKeyInfo>([
            Key('a', ConsoleKey.A),
            Key('b', ConsoleKey.B),
            Key('\b', ConsoleKey.Backspace),
            Key('c', ConsoleKey.C),
            Key('\r', ConsoleKey.Enter)
        ]);
        using var error = new StringWriter();
        var reader = new ConsoleSecretReader(() => false, () => keys.Dequeue());

        var result = reader.ReadToken(error);

        Assert.True(result.IsSuccess);
        Assert.Equal("ac", result.Token);
        Assert.Equal($"Trello token: {Environment.NewLine}", error.ToString());
        Assert.DoesNotContain("ac", error.ToString());
    }

    [Fact]
    public void ReadToken_RequiresEnvironmentVariablesWhenInputIsRedirected()
    {
        using var error = new StringWriter();
        var reader = new ConsoleSecretReader(
            () => true,
            () => throw new InvalidOperationException("ReadKey must not run for redirected input."));

        var result = reader.ReadToken(error);

        Assert.False(result.IsSuccess);
        Assert.Equal("INTERACTIVE_REQUIRED", result.ErrorCode);
        Assert.Contains("TRELLO_API_KEY", result.Error);
        Assert.Contains("TRELLO_TOKEN", result.Error);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void ReadToken_RejectsEmptyInput()
    {
        var keys = new Queue<ConsoleKeyInfo>([Key('\r', ConsoleKey.Enter)]);
        using var error = new StringWriter();
        var reader = new ConsoleSecretReader(() => false, () => keys.Dequeue());

        var result = reader.ReadToken(error);

        Assert.False(result.IsSuccess);
        Assert.Equal("TOKEN_REQUIRED", result.ErrorCode);
        Assert.Equal($"Trello token: {Environment.NewLine}", error.ToString());
    }

    [Fact]
    public void ReadToken_CancelsWhenControlCIsPressed()
    {
        var keys = new Queue<ConsoleKeyInfo>([new ConsoleKeyInfo('c', ConsoleKey.C, shift: false, alt: false, control: true)]);
        using var error = new StringWriter();
        var reader = new ConsoleSecretReader(() => false, () => keys.Dequeue());

        var result = reader.ReadToken(error);

        Assert.False(result.IsSuccess);
        Assert.Equal("TOKEN_INPUT_CANCELLED", result.ErrorCode);
        Assert.Equal($"Trello token: {Environment.NewLine}", error.ToString());
    }

    [Fact]
    public void ReadToken_CancelsWhenEscapeIsPressed()
    {
        var keys = new Queue<ConsoleKeyInfo>([Key('\u001b', ConsoleKey.Escape)]);
        using var error = new StringWriter();
        var reader = new ConsoleSecretReader(() => false, () => keys.Dequeue());

        var result = reader.ReadToken(error);

        Assert.False(result.IsSuccess);
        Assert.Equal("TOKEN_INPUT_CANCELLED", result.ErrorCode);
        Assert.Equal($"Trello token: {Environment.NewLine}", error.ToString());
    }

    private static ConsoleKeyInfo Key(char character, ConsoleKey key) =>
        new(character, key, shift: false, alt: false, control: false);
}
