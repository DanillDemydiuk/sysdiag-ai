using SysDiag.Core.Models;

namespace SysDiag.Core.Abstractions;

/// <summary>
/// Renders a snapshot into a text format that can leave the application: a file,
/// a ticket, an e-mail.
/// </summary>
/// <remarks>
/// The interface returns a string instead of writing a file itself. Deciding
/// where bytes land is the job of the command layer, and a pure text result is
/// what makes every exporter testable with a single assertion.
/// </remarks>
public interface ISnapshotExporter
{
    /// <summary>Name used on the command line, for example "json" or "markdown".</summary>
    string FormatName { get; }

    /// <summary>File extension including the dot, used when no output path is given.</summary>
    string FileExtension { get; }

    /// <summary>Renders the snapshot. Must not throw for snapshots with missing values.</summary>
    string Render(SystemSnapshot snapshot);
}
