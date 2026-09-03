using System.Xml.Linq;
using Xunit;

namespace Enzo.Diagrams.Cli.Tests;

public sealed class CliPackagingMetadataTests
{
    [Fact]
    public void CliProject_ConfiguresDotnetToolPackage()
    {
        var projectPath = Path.Combine(FindRepositoryRoot(), "src", "Enzo.Diagrams.Cli", "Enzo.Diagrams.Cli.csproj");
        var project = XDocument.Load(projectPath);

        Assert.Equal("Enzo.Diagrams.Cli", ReadProperty(project, "PackageId"));
        Assert.Equal("0.1.0", ReadProperty(project, "Version"));
        Assert.Equal("true", ReadProperty(project, "PackAsTool"));
        Assert.Equal("enzo-diagram", ReadProperty(project, "ToolCommandName"));
        Assert.Equal("https://github.com/solakpetri/Enzo.DiagramApp", ReadProperty(project, "RepositoryUrl"));
    }

    private static string ReadProperty(XContainer project, string name)
    {
        return project.Descendants(name).Single().Value;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Enzo.Diagrams.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
