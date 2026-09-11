namespace GameGarage.Utilities;

internal static class TextUtilities
{
    // Levenshtein distance with two rows of storage, used only to suggest an image edition.
    public static int ComputeLevenshteinDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (int row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            for (int column = 1; column <= right.Length; column++)
                current[column] = Math.Min(Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + (left[row - 1] == right[column - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}
