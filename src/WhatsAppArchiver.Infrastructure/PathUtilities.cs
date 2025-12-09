namespace WhatsAppArchiver.Infrastructure;

/// <summary>
/// Provides utility methods for path manipulation and resolution.
/// </summary>
public static class PathUtilities
{
    /// <summary>
    /// Expands tilde (~) in paths to the user's home directory.
    /// </summary>
    /// <param name="path">The path that may contain a tilde prefix.</param>
    /// <returns>
    /// The path with tilde expanded to the user's home directory, or the original path if it doesn't start with tilde.
    /// Returns the input unchanged if it's null or whitespace.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method handles both forward slash (Unix-style) and backslash (Windows-style) tilde paths:
    /// <list type="bullet">
    /// <item><description><c>~</c> → user's home directory</description></item>
    /// <item><description><c>~/docs/file.txt</c> → <c>/home/user/docs/file.txt</c> (on Unix)</description></item>
    /// <item><description><c>~\docs\file.txt</c> → <c>C:\Users\user\docs\file.txt</c> (on Windows)</description></item>
    /// <item><description><c>/absolute/path</c> → unchanged</description></item>
    /// <item><description><c>relative/path</c> → unchanged</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var expanded = PathUtilities.ExpandTildePath("~/docs/file.txt");
    /// // Returns: "/home/username/docs/file.txt" on Linux
    /// // Returns: "C:\Users\username\docs\file.txt" on Windows
    /// </code>
    /// </example>
    public static string? ExpandTildePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        // Check if path starts with ~/ or ~\ or is exactly ~
        if (path.StartsWith("~/") || path.StartsWith("~\\") || path == "~")
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            // If path is just ~, return home directory
            if (path.Length == 1)
            {
                return homeDirectory;
            }
            
            // Replace ~ with home directory, skipping the ~/ or ~\ prefix
            return Path.Combine(homeDirectory, path.Substring(2));
        }

        return path;
    }
}
