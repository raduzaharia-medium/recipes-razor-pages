namespace Recipes.Services;

public static class Search
{
    extension(string? text)
    {
        /// <summary>LIKE pattern matching anywhere in the text (escapes the wildcards; use with escape character '\').</summary>
        public string ContainsPattern() => $"%{(text ?? "").Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
    }
}
