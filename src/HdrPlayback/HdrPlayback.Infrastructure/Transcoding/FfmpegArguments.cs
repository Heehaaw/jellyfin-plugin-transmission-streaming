namespace HdrPlayback.Infrastructure.Transcoding;

using System.Text;

/// <summary>Reads ProcessStartInfo-style arguments without applying shell expansion or changing filter escapes.</summary>
internal static class FfmpegArguments
{
    public static List<string> Parse(string command)
    {
        var arguments = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        var started = false;
        for (var index = 0; index < command.Length; index++)
        {
            var character = command[index];
            if (char.IsWhiteSpace(character) && !quoted)
            {
                if (started)
                {
                    arguments.Add(value.ToString());
                    value.Clear();
                    started = false;
                }

                continue;
            }

            started = true;
            if (character == '\\')
            {
                var count = 1;
                while (index + 1 < command.Length && command[index + 1] == '\\')
                {
                    count++;
                    index++;
                }

                if (index + 1 < command.Length && command[index + 1] == '"')
                {
                    value.Append('\\', count / 2);
                    index++;
                    if (count % 2 == 0)
                    {
                        quoted = !quoted;
                    }
                    else
                    {
                        value.Append('"');
                    }
                }
                else
                {
                    value.Append('\\', count);
                }
            }
            else if (character == '"')
            {
                quoted = !quoted;
            }
            else
            {
                value.Append(character);
            }
        }

        if (quoted)
        {
            throw new InvalidOperationException("The native FFmpeg command has an unterminated argument.");
        }

        if (started)
        {
            arguments.Add(value.ToString());
        }

        return arguments;
    }

    public static string Format(IEnumerable<string> arguments)
    {
        return string.Join(' ', arguments.Select(Quote));
    }

    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            result.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes);
            result.Append(character);
            backslashes = 0;
        }

        result.Append('\\', backslashes * 2);
        return result.Append('"').ToString();
    }
}
