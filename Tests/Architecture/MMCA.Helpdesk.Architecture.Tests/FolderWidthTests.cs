namespace MMCA.Helpdesk.Architecture.Tests;

/// <summary>
/// Folder-width rule (rubric §5), driven by the shared <see cref="FolderWidthTestsBase"/>: no folder under
/// this repo's <c>Source/</c> or <c>Tests/</c> tree holds more than the allowed number of direct code
/// files, so the layout stays feature by folder (the shape the <c>mmca-module</c> template is cut from).
/// </summary>
public sealed class FolderWidthTests : FolderWidthTestsBase
{
    protected override string RepoRoot { get; } = ArchitectureMapBase.FindRepoRoot("MMCA.Helpdesk.slnx");
}
