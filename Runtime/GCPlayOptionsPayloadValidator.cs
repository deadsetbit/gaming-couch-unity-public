using System;
using System.Text;

namespace DSB.GC
{
    /// <summary>
    /// Shape checks run over the raw hosted play payload before JsonUtility parses it. JsonUtility
    /// silently drops fields it does not know, so a legacy roster entry and a malformed one both
    /// arrive as defaults; only reading the JSON text can tell them apart.
    /// </summary>
    internal static class GCPlayOptionsPayloadValidator
    {
        internal static void ValidatePlayersJsonShape(
            string json,
            int playersValueStart,
            int playersValueEnd,
            string paramName
        )
        {
            var cursor = SkipWhitespace(json, playersValueStart, playersValueEnd);
            if (cursor >= playersValueEnd || json[cursor] != '[')
            {
                return;
            }

            cursor++;
            while (cursor < playersValueEnd)
            {
                cursor = SkipWhitespace(json, cursor, playersValueEnd);
                if (cursor >= playersValueEnd || json[cursor] == ']')
                {
                    return;
                }

                var elementStart = cursor;
                var elementEnd = FindJsonValueEnd(json, elementStart, playersValueEnd);
                var elementValueStart = SkipWhitespace(json, elementStart, elementEnd);
                if (elementValueStart < elementEnd && json[elementValueStart] == '{')
                {
                    if (ContainsDirectJsonFieldInObject(json, elementValueStart, elementEnd, "playerId") ||
                        ContainsDirectJsonFieldInObject(json, elementValueStart, elementEnd, "name"))
                    {
                        throw new ArgumentException(GCPlayOptions.LegacyPlayersPayloadErrorMessage, paramName);
                    }

                    if (!ContainsDirectJsonFieldInObject(json, elementValueStart, elementEnd, "playerIndex"))
                    {
                        throw new ArgumentException(GCPlayOptions.MissingPlayerIndexPayloadErrorMessage, paramName);
                    }
                }

                cursor = elementEnd;
                if (cursor < playersValueEnd && json[cursor] == ',')
                {
                    cursor++;
                }
            }
        }

        internal static void ValidateProvidedPlayerIndices(GCPlayerOptions[] players, string paramName)
        {
            var seenPlayerIndices = new bool[players.Length];
            for (var index = 0; index < players.Length; index++)
            {
                var playerIndex = players[index].playerIndex;
                if (playerIndex < 0 || playerIndex >= players.Length)
                {
                    throw new ArgumentException(GCPlayOptions.DensePlayerIndexPayloadErrorMessage, paramName);
                }

                if (seenPlayerIndices[playerIndex])
                {
                    throw new ArgumentException(GCPlayOptions.DuplicatePlayerIndexPayloadErrorMessage, paramName);
                }

                seenPlayerIndices[playerIndex] = true;
            }
        }

        internal static bool TryFindTopLevelJsonFieldValueRange(
            string json,
            string fieldName,
            out int valueStart,
            out int valueEnd
        )
        {
            valueStart = -1;
            valueEnd = -1;

            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            var depth = 0;
            for (var index = 0; index < json.Length;)
            {
                var current = json[index];
                if (current == '"')
                {
                    if (!TryReadJsonStringLiteral(json, index, json.Length, out var value, out var stringEnd))
                    {
                        return false;
                    }

                    var afterString = SkipWhitespace(json, stringEnd, json.Length);
                    if (depth == 1 &&
                        afterString < json.Length &&
                        json[afterString] == ':' &&
                        string.Equals(value, fieldName, StringComparison.Ordinal))
                    {
                        valueStart = SkipWhitespace(json, afterString + 1, json.Length);
                        valueEnd = FindJsonValueEnd(json, valueStart, json.Length);
                        return true;
                    }

                    index = stringEnd;
                    continue;
                }

                if (current == '{' || current == '[')
                {
                    depth++;
                }
                else if (current == '}' || current == ']')
                {
                    depth = Math.Max(0, depth - 1);
                }

                index++;
            }

            return false;
        }

        private static bool ContainsDirectJsonFieldInObject(string json, int objectStart, int objectEnd, string fieldName)
        {
            var cursor = SkipWhitespace(json, objectStart, objectEnd);
            if (cursor >= objectEnd || json[cursor] != '{')
            {
                return false;
            }

            var nestedDepth = 0;
            for (var index = cursor + 1; index < objectEnd;)
            {
                var current = json[index];
                if (current == '"')
                {
                    if (!TryReadJsonStringLiteral(json, index, objectEnd, out var value, out var stringEnd))
                    {
                        return false;
                    }

                    var afterString = SkipWhitespace(json, stringEnd, objectEnd);
                    if (nestedDepth == 0 &&
                        afterString < objectEnd &&
                        json[afterString] == ':' &&
                        string.Equals(value, fieldName, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    index = stringEnd;
                    continue;
                }

                if (current == '{' || current == '[')
                {
                    nestedDepth++;
                }
                else if (current == '}' || current == ']')
                {
                    if (nestedDepth == 0)
                    {
                        return false;
                    }

                    nestedDepth--;
                }

                index++;
            }

            return false;
        }

        private static int FindJsonValueEnd(string json, int start, int limit)
        {
            var depth = 0;
            for (var index = start; index < limit;)
            {
                var current = json[index];
                if (current == '"')
                {
                    if (!TryReadJsonStringLiteral(json, index, limit, out _, out var stringEnd))
                    {
                        return limit;
                    }

                    index = stringEnd;
                    continue;
                }

                if (current == '{' || current == '[')
                {
                    depth++;
                }
                else if (current == '}' || current == ']')
                {
                    if (depth == 0)
                    {
                        return index;
                    }

                    depth--;
                }
                else if (current == ',' && depth == 0)
                {
                    return index;
                }

                index++;
            }

            return limit;
        }

        private static bool TryReadJsonStringLiteral(
            string json,
            int start,
            int limit,
            out string value,
            out int end
        )
        {
            value = null;
            end = start;

            if (start >= limit || json[start] != '"')
            {
                return false;
            }

            StringBuilder builder = null;
            for (var index = start + 1; index < limit; index++)
            {
                var current = json[index];
                if (current == '\\')
                {
                    if (builder == null)
                    {
                        builder = new StringBuilder(json.Substring(start + 1, index - start - 1));
                    }

                    index++;
                    if (index >= limit)
                    {
                        return false;
                    }

                    var escaped = json[index];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            if (!TryReadJsonHexQuad(json, index + 1, limit, out var codePoint))
                            {
                                return false;
                            }

                            builder.Append((char)codePoint);
                            index += 4;
                            break;
                        default:
                            return false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    value = builder != null
                        ? builder.ToString()
                        : json.Substring(start + 1, index - start - 1);
                    end = index + 1;
                    return true;
                }

                builder?.Append(current);
            }

            return false;
        }

        private static bool TryReadJsonHexQuad(string value, int start, int limit, out int codePoint)
        {
            codePoint = 0;
            if (start + 4 > limit)
            {
                return false;
            }

            for (var index = start; index < start + 4; index++)
            {
                var digit = HexDigitValue(value[index]);
                if (digit < 0)
                {
                    return false;
                }

                codePoint = (codePoint << 4) + digit;
            }

            return true;
        }

        private static int HexDigitValue(char value)
        {
            if (value >= '0' && value <= '9')
            {
                return value - '0';
            }

            if (value >= 'a' && value <= 'f')
            {
                return value - 'a' + 10;
            }

            if (value >= 'A' && value <= 'F')
            {
                return value - 'A' + 10;
            }

            return -1;
        }

        private static int SkipWhitespace(string value, int start, int limit)
        {
            var index = start;
            while (index < limit && char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            return index;
        }
    }
}
