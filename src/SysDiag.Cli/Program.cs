using Spectre.Console;
using Spectre.Console.Cli;
using SysDiag.Cli;
using SysDiag.Cli.Commands;
using SysDiag.Cli.Infrastructure;
using SysDiag.Cli.Output;

// Startup happens before the command framework exists, so it needs its own
// guard: a missing write permission for the database must not print a stack
// trace either.
AppServices services;

try
{
    services = await AppServices.CreateAsync();
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
    ErrorPresenter.Render(AnsiConsole.Console, exception);
    return ExitCodes.UnexpectedError;
}

using (services)
{
    // Ctrl+C cancels the running command instead of killing the process, which
    // matters while waiting for a slow model.
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        services.Cancel();
    };

    var registrar = new TypeRegistrar();
    registrar.RegisterInstance(typeof(AppServices), services);
    registrar.RegisterInstance(typeof(IAnsiConsole), AnsiConsole.Console);

    var app = new CommandApp(registrar);

    app.Configure(config =>
    {
        config.SetApplicationName("sysdiag");

        // Anything a command throws ends up here, so no exception can reach the
        // console unformatted.
        config.SetExceptionHandler((exception, _) =>
        {
            ErrorPresenter.Render(AnsiConsole.Console, exception);
            return exception is OperationCanceledException
                ? ExitCodes.UserError
                : ExitCodes.UnexpectedError;
        });

        config.AddCommand<ScanCommand>("scan")
            .WithDescription("Collect the current configuration and store it as a snapshot.")
            .WithExample("scan")
            .WithExample("scan", "--demo");

        config.AddCommand<ListCommand>("list")
            .WithDescription("List stored snapshots, newest first.")
            .WithExample("list", "--limit", "5");

        config.AddCommand<CompareCommand>("compare")
            .WithDescription("Show the differences between two snapshots.")
            .WithExample("compare", "1", "2");

        config.AddCommand<ExportCommand>("export")
        .WithDescription("Write a snapshot as JSON or Markdown.")
        .WithExample("export", "--format", "markdown")
        .WithExample("export", "2", "--format", "json", "--output", "report.json");

    config.AddCommand<ExplainCommand>("explain")
            .WithDescription("Let the local model explain a snapshot in plain language.")
            .WithExample("explain")
            .WithExample("explain", "2");
    });

    return await app.RunAsync(args);
}
