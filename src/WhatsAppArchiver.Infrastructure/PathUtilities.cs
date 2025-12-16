namespace WhatsAppArchiver.Infrastructure;

/// <summary>
/// Provides utility methods for path manipulation and resolution.
/// </summary>
public static class PathUtilities
{
    /// <summary>
    /// Resolves a path relative to the application's base directory.
    /// </summary>
    /// <param name="path">The path to resolve (can be relative or absolute).</param>
    /// <returns>
    /// The absolute path resolved relative to the application's base directory for relative paths,
    /// or the original path if it's already absolute.
    /// Returns the input unchanged if it's null or whitespace.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method ensures that relative paths are resolved consistently relative to the application's
    /// base directory (<see cref="AppContext.BaseDirectory"/>), making the application portable across
    /// different execution contexts and working directories.
    /// </para>
    /// <para>
    /// Behavior for different path types:
    /// <list type="bullet">
    /// <item><description>Relative paths (e.g., <c>./credentials/file.json</c>) → resolved relative to app base directory</description></item>
    /// <item><description>Absolute paths (e.g., <c>/home/user/file.json</c>) → returned unchanged</description></item>
    /// <item><description>Absolute Windows paths (e.g., <c>C:\Users\file.json</c>) → returned unchanged</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // If app is in /app/bin/
    /// var resolved = PathUtilities.ResolveApplicationPath("./credentials/key.json");
    /// // Returns: "/app/bin/credentials/key.json"
    /// 
    /// var absolute = PathUtilities.ResolveApplicationPath("/etc/config/key.json");
    /// // Returns: "/etc/config/key.json" (unchanged)
    /// </code>
    /// </example>
    public static string? ResolveApplicationPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

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
            return Path.Combine(homeDirectory, path[2..]);
        }

        return path;
    }
}
