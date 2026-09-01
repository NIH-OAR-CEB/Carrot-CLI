using System.Reflection;
using System.Text.RegularExpressions;
using Carrot.Cli.Cli.Commands;
using Carrot.Cli.Cli.Settings;
using Carrot.Cli.Cli.UI;
using Carrot.Cli.Common;
using Carrot.Cli.Configuration;
using Carrot.Cli.Processing;
using Xunit;

namespace Carrot.Cli.Tests.Architecture;

/**************************************************************/
/// <summary>
/// Enforces durable implemented-surface and production-source documentation boundaries.
/// </summary>
public sealed class DocumentationConventionTests
{
    #region implementation

    /**************************************************************/
    /// <summary>Verifies required command, workflow, and settings types exist and remain internal.</summary>
    [Fact]
    public void EveryPlannedTypeAndSignatureExists()
    {
        #region implementation

        var requiredTypes = new[]
        {
            typeof(ProcessCommand), typeof(PreviewCommand), typeof(ServerInfoCommand),
            typeof(ProcessSettings), typeof(PreviewSettings), typeof(EndpointSettings),
            typeof(DocumentProcessingWorkflow), typeof(PreparedDocumentProcessor),
            typeof(InteractivePreviewFlow), typeof(IDocumentProcessingWorkflow), typeof(OperationResult<>)
        };
        Assert.All(requiredTypes, type => Assert.False(type.IsPublic, $"{type.FullName} must remain internal."));
        Assert.NotNull(typeof(IDocumentProcessingWorkflow).GetMethod(nameof(IDocumentProcessingWorkflow.ProcessAsync)));
        Assert.NotNull(typeof(IDocumentProcessingWorkflow).GetMethod(nameof(IDocumentProcessingWorkflow.PreviewAsync)));
        Assert.True(typeof(ProcessSettings).IsSubclassOf(typeof(ClusteringSettings)));
        Assert.True(typeof(PreviewSettings).IsSubclassOf(typeof(ClusteringSettings)));

        #endregion
    }

    /**************************************************************/
    /// <summary>Verifies source conventions and rejects obsolete production layout-only stubs.</summary>
    [Fact]
    public void ProductionSourceFollowsDocumentationAndDeferredStubRules()
    {
        #region implementation

        var sourceRoot = findSourceRoot();
        var failures = new List<string>();
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(sourceRoot, file);
            if (text.Contains("NotImplementedException(\"Layout stub only.\")", StringComparison.Ordinal)
                || text.Contains("layout-only scaffold", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{relativePath}: obsolete layout-only production marker.");
            }

            if (Regex.IsMatch(text, @"\b(internal|public)\s+(sealed\s+)?(class|record|interface|enum)\s+", RegexOptions.CultureInvariant)
                && (!text.Contains("/**************************************************************/", StringComparison.Ordinal)
                    || !text.Contains("/// <summary>", StringComparison.Ordinal)))
            {
                failures.Add($"{relativePath}: production type lacks the established separator-header or XML-summary convention.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));

        #endregion
    }

    /**************************************************************/
    /// <summary>Finds the repository production source directory without relying on an absolute checkout path.</summary>
    /// <returns>The full path to the production C# source directory.</returns>
    private static string findSourceRoot()
    {
        #region implementation

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Carrot.Cli");
            if (Directory.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate src/Carrot.Cli from the test assembly base directory.");

        #endregion
    }

    #endregion
}
