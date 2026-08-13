namespace SysDiag.Cli.Commands;

/// <summary>
/// Process exit codes. Scripts and CI check these, so they are part of the
/// public behaviour of the tool and deserve names instead of bare numbers.
/// </summary>
internal static class ExitCodes
{
    /// <summary>The command did what it was asked to do.</summary>
    public const int Success = 0;

    /// <summary>The input was wrong, for example an id that does not exist.</summary>
    public const int UserError = 1;

    /// <summary>The program hit an unexpected error.</summary>
    public const int UnexpectedError = 2;
}
