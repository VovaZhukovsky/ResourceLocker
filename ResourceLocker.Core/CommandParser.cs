namespace ResourceLocker.Core;

public static class CommandParser
{
    public static CommandParserResponse Parse(ReadOnlySpan<char> input)
    {
        input = input.Trim();
        int index0 = input.IndexOf(' ');

        if (index0 == -1)
            return new CommandParserResponse();

        var command = input.Slice(0, index0);

        input = input.Slice(index0 + 1).Trim();

        if (input.IsEmpty)
            return new CommandParserResponse();

        int index1 = input.IndexOf(' ');

        if (index1 == -1)
        {
            if (input.IsEmpty)
                return new CommandParserResponse();

            return new CommandParserResponse(command, input);
        }

        var key = input.Slice(0, index1);

        if (key.IsEmpty)
            return new CommandParserResponse();

        input = input.Slice(index1 + 1).Trim();

        if (input.IsEmpty)
            return new CommandParserResponse(command, key);

        int index2 = input.IndexOf(' ');

        if (index2 == -1)
            return new CommandParserResponse(command, key, input);

        return new CommandParserResponse(command, key);
    }
}

public readonly ref struct CommandParserResponse
{
    public readonly ReadOnlySpan<char> Command { get; } = ReadOnlySpan<char>.Empty;
    public readonly ReadOnlySpan<char> Key { get; } = ReadOnlySpan<char>.Empty;
    public readonly ReadOnlySpan<char> Value { get; } = ReadOnlySpan<char>.Empty;

    public CommandParserResponse(
        ReadOnlySpan<char> command,
        ReadOnlySpan<char> key,
        ReadOnlySpan<char> value = default)
    {
        Command = command;
        Key = key;
        Value = value;
    }
}
