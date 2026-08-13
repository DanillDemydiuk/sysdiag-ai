using SysDiag.Cli;

// Temporary entry point: it proves that configuration, database and services
// come up. Step 14 replaces it with the real command line application.
using AppServices services = await AppServices.CreateAsync();

Console.WriteLine($"SysDiag-AI 0.1.0");
Console.WriteLine($"Database: {services.Settings.ResolveDatabasePath()}");
Console.WriteLine($"Ollama:   {services.Settings.Ollama.BaseUrl} ({services.Settings.Ollama.Model})");
Console.WriteLine($"Collector: {services.SelectCollector(useDemoData: false).Collector.Name}");
