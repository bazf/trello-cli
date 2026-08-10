using System.Text;

namespace TrelloCli;

public sealed class ConsoleSecretReader : ISecretReader
{
    private readonly Func<bool> _isInputRedirected;
    private readonly Func<ConsoleKeyInfo> _readKey;

    public ConsoleSecretReader()
        : this(() => Console.IsInputRedirected, () => Console.ReadKey(intercept: true))
    {
    }

    public ConsoleSecretReader(Func<bool> isInputRedirected, Func<ConsoleKeyInfo> readKey)
    {
        _isInputRedirected = isInputRedirected;
        _readKey = readKey;
    }

    public SecretReadResult ReadToken(TextWriter errorWriter)
    {
        if (_isInputRedirected()) return SecretReadResult.InteractiveRequired();

        errorWriter.Write("Trello token: ");
        var token = new StringBuilder();
        while (true)
        {
            var key = _readKey();
            if (key.Key == ConsoleKey.Escape ||
                (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control)))
            {
                errorWriter.WriteLine();
                return SecretReadResult.Cancelled();
            }

            if (key.Key == ConsoleKey.Enter)
            {
                errorWriter.WriteLine();
                return token.Length == 0 ? SecretReadResult.Empty() : SecretReadResult.Success(token.ToString());
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (token.Length > 0) token.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar)) token.Append(key.KeyChar);
        }
    }
}
