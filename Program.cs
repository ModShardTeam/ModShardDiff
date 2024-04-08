using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

namespace ModShardDiff;
internal static class Program
{
    static async Task Main(string[] args)
    {
        Option<string> nameOption = new("--name")
        {
            Description = "Name of the file to be compared to.",
            IsRequired = true
        };
        nameOption.AddAlias("-n");
        nameOption.SetDefaultValue(null);
        
        Option<string> refOption = new("--ref")
        {
            Description = "Name of the reference to compare to.",
            IsRequired = true
        };
        refOption.AddAlias("-r");
        
        Option<string?> outputOption = new("--output")
        {
            Description = "Output folder for the diff files."
        };
        outputOption.AddAlias("-o");
        outputOption.SetDefaultValue(null);

        RootCommand rootCommand = new("A CLI tool to export diff files from two data.win.")
        {
            nameOption,
            refOption,
            outputOption
        };

        rootCommand.SetHandler(MainOperations.MainCommand, nameOption, refOption, outputOption);

        CommandLineBuilder commandLineBuilder = new(rootCommand);

        commandLineBuilder.AddMiddleware(async (context, next) =>
        {
            await next(context);
        });

        commandLineBuilder.UseDefaults();
        Parser parser = commandLineBuilder.Build();

        await parser.InvokeAsync(args);
    }
}
