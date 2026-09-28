namespace Resztka.Core.Tests;

internal static class RepositoryPaths
{
    public static string DataDirectory { get; } = Path.Combine(FindRoot(), "data");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Resztka.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find the repository root.");
    }
}
