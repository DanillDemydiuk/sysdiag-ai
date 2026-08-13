using Spectre.Console;
using Spectre.Console.Cli;
using SysDiag.Cli;
using SysDiag.Cli.Commands;
using SysDiag.Cli.Infrastructure;

// Services are built once and handed to the command framework, so every command
// works on the same database connection settings and the same HttpClient.
using AppServices services = await AppServices.CreateAsync();

var registrar = new TypeRegistrar();
registrar.RegisterInstance(typeof(AppServices), services);
registrar.RegisterInstance(typeof(IAnsiConsole), AnsiConsole.Console);

var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("sysdiag");

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

    config.AddCommand<ExplainCommand>("explain")
        .WithDescription("Let the local model explain a snapshot in plain language.")
        .WithExample("explain")
        .WithExample("explain", "2");
});

return await app.RunAsync(args);
