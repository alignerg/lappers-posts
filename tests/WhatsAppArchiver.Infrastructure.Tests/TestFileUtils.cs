namespace WhatsAppArchiver.Infrastructure.Tests;

/// <summary>
/// Shared utilities for accessing test files and directories.
/// </summary>
internal static class TestFileUtils
{
    /// <summary>
    /// Gets the path to the SampleData directory.
    /// </summary>
    /// <returns>The full path to the SampleData directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the SampleData directory cannot be found.</exception>
    public static string GetSampleDataPath()
    {
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (currentDir is not null)
        {
            var testsDir = Path.Combine(currentDir.FullName, "tests", "SampleData");
            if (Directory.Exists(testsDir))
            {
                return testsDir;
            }

            var sampleDataDir = Path.Combine(currentDir.FullName, "SampleData");
            if (Directory.Exists(sampleDataDir))
            {
                return sampleDataDir;
            }

            currentDir = currentDir.Parent;
        }

        var outputDir = Directory.GetCurrentDirectory();
        var relativePaths = new[]
        {
            Path.Combine(outputDir, "..", "..", "..", "..", "..", "tests", "SampleData"),
            Path.Combine(outputDir, "..", "..", "..", "..", "SampleData")
        };

        var foundPath = relativePaths.FirstOrDefault(path => Directory.Exists(path));
        if (foundPath is not null)
        {
            return Path.GetFullPath(foundPath);
        }

        throw new DirectoryNotFoundException(
            "Could not find SampleData directory. Searched from: " + Directory.GetCurrentDirectory());
    }
}
