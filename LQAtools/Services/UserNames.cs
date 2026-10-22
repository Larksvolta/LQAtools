namespace LQAtools.Services;

public static class UserNames
{
    public static string Friendly(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return string.Empty;

        var slash = fullName.LastIndexOf('\\');
        return slash >= 0 && slash + 1 < fullName.Length
            ? fullName[(slash + 1)..]
            : fullName;
    }
}
