using Financial.Robot.Worker.Extract;
using Microsoft.Extensions.Configuration;

var configuration = BuildConfiguration(args);

if (string.IsNullOrWhiteSpace(configuration["mode"]))
{
    configuration["mode"] = "extract";
}

Environment.ExitCode = await ExtractModeRunner.RunAsync(configuration);

static IConfigurationRoot BuildConfiguration(string[] args)
{
    var basePath = AppContext.BaseDirectory;

    return new ConfigurationBuilder()
        .SetBasePath(basePath)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .AddCommandLine(args)
        .Build();
}
