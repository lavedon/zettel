using System.Text.RegularExpressions;

namespace Zettel;

public static partial class TagParser
{
    // Matches #TagName — must start with a letter, allows letters, digits, and hyphens
    // Does NOT match #Word: (SuperMemo metadata like #Author:, #Link:, etc.)
    [GeneratedRegex(@"(?:^|(?<=\s))#([A-Za-z][A-Za-z0-9-]*)(?!:)(?=\s|$|[.,;!?\)])", RegexOptions.Multiline)]
    private static partial Regex HashTagPattern();

    // Matches $TagName (SuperMemo-style tags)
    [GeneratedRegex(@"(?:^|(?<=\s))\$([A-Za-z][A-Za-z0-9-]*)(?=\s|$|[.,;!?\)])", RegexOptions.Multiline)]
    private static partial Regex DollarTagPattern();

    public static HashSet<string> Extract(string content)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in HashTagPattern().Matches(content))
        {
            var tag = match.Groups[1].Value;
            if (IsValidTag(tag))
                tags.Add(tag);
        }

        foreach (Match match in DollarTagPattern().Matches(content))
        {
            var tag = match.Groups[1].Value;
            if (IsValidTag(tag))
                tags.Add(tag);
        }

        return tags;
    }

    // SuperMemo metadata key prefixes that look like tags but aren't
    private static readonly HashSet<string> IgnoredTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "SuperMemo", "Title", "Author", "Link", "Source", "Parent"
    };

    private static bool IsValidTag(string tag)
    {
        // Skip single-character tags
        if (tag.Length <= 1)
            return false;

        // Skip tags that are just numbers with a prefix letter
        if (tag.Length >= 2 && char.IsLetter(tag[0]) && tag[1..].All(char.IsDigit))
            return false;

        // Skip known SuperMemo metadata prefixes
        if (IgnoredTags.Contains(tag))
            return false;

        return true;
    }
}
