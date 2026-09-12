using System.Xml.Linq;
using Enzo.Diagrams.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enzo.Diagrams.Api.Tests;

public sealed class ArchitectureDependencyTests
{
    [Fact]
    public void ProjectReferences_FollowCleanArchitectureDirection()
    {
        var root = FindRepositoryRoot();

        Assert.Empty(ReadProjectReferences(root, "src/Enzo.Diagrams.Domain/Enzo.Diagrams.Domain.csproj"));
        Assert.Equal(
            ["src/Enzo.Diagrams.Domain/Enzo.Diagrams.Domain.csproj"],
            ReadProjectReferences(root, "src/Enzo.Diagrams.Application/Enzo.Diagrams.Application.csproj"));
        Assert.Equal(
            [
                "src/Enzo.Diagrams.Application/Enzo.Diagrams.Application.csproj",
                "src/Enzo.Diagrams.Domain/Enzo.Diagrams.Domain.csproj"
            ],
            ReadProjectReferences(root, "src/Enzo.Diagrams.Infrastructure/Enzo.Diagrams.Infrastructure.csproj"));
        Assert.Equal(
            [
                "src/Enzo.Diagrams.Application/Enzo.Diagrams.Application.csproj",
                "src/Enzo.Diagrams.Infrastructure/Enzo.Diagrams.Infrastructure.csproj"
            ],
            ReadProjectReferences(root, "src/Enzo.Diagrams.Api/Enzo.Diagrams.Api.csproj"));
        Assert.Equal(
            [
                "src/Enzo.Diagrams.Application/Enzo.Diagrams.Application.csproj",
                "src/Enzo.Diagrams.Infrastructure/Enzo.Diagrams.Infrastructure.csproj"
            ],
            ReadProjectReferences(root, "src/Enzo.Diagrams.Cli/Enzo.Diagrams.Cli.csproj"));
    }

    [Fact]
    public void ApiComposition_ResolvesApplicationAndInfrastructureServices()
    {
        using var factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Enzo:ApiKey", "test-api-key"));
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DiagramService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDiagramRenderer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRenderResultStore>());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Enzo.Diagrams.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }

    private static IReadOnlyList<string> ReadProjectReferences(string root, string projectPath)
    {
        var fullPath = Path.Combine(root, projectPath.Replace('/', Path.DirectorySeparatorChar));
        var projectDirectory = Path.GetDirectoryName(fullPath)!;
        var document = XDocument.Load(fullPath);

        return document.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetRelativePath(root, Path.GetFullPath(Path.Combine(projectDirectory, value!))).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
