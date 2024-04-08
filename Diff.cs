using Serilog;
using UndertaleModLib;
using UndertaleModLib.Models;

namespace ModShardDiff;
internal static class MainOperations
{
    public static async Task MainCommand(string name, string reference, string? outputFolder)
    {
        outputFolder ??= Environment.CurrentDirectory;

        LoggerConfiguration logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(string.Format("logs/log_{0}.txt", DateTime.Now.ToString("yyyyMMdd_HHmm")));

        Log.Logger = logger.CreateLogger();

        Console.WriteLine($"Export differences between {name} and {reference} in {outputFolder}.");
        
        Task<bool> task = FileReader.Diff(name, reference, outputFolder);
        await task;
        if (task.Result)
        {
            Console.WriteLine($"Differences in {outputFolder}");
        }
        else
        {
            Console.WriteLine("Failed exporting differences");
        }

        await Log.CloseAndFlushAsync();
    }
}

internal static class FileReader
{
    public static async Task<bool> Diff(string name, string reference, string outputFolder)
    {
        Task<UndertaleData?> taskName =  LoadFile(name);
        await taskName;
        Task<UndertaleData?> taskRef =  LoadFile(reference);
        await taskRef;

        if (taskName.Result == null || taskRef.Result == null) return false;

        IEnumerable<string> diffCode = taskName.Result.Code.Select(x => x.Name.Content).Except(taskRef.Result.Code.Select(x => x.Name.Content));
        Console.WriteLine("Diff in code are: ");
        foreach(string codeName in diffCode)
        {
            Console.WriteLine($"{codeName}");
        }
        return true;
    }

    private static UndertaleData? LoadUmt(string filename)
    {
        UndertaleData? data = null;
        using (FileStream stream = new(filename, FileMode.Open, FileAccess.Read))
        {
            data = UndertaleIO.Read(
                stream, 
                warning =>
                {
                    if (warning.Contains("unserializeCountError.txt") || warning.Contains("object pool size"))
                        return;
                }
            );
        }

        UndertaleEmbeddedTexture.TexData.ClearSharedStream();
        Log.Information(string.Format("Successfully load: {0}.", filename));

        return data;
    }

    public static async Task<UndertaleData?> LoadFile(string filename)
    {
        UndertaleData? data =  null;
        // task load a data.win with umt
        Task taskLoadDataWinWithUmt = Task.Run(() =>
        {
            try
            {
                data = LoadUmt(filename);
            }
            catch (Exception ex)
            {   
                Log.Error(ex, "Something went wrong");
                throw;
            }
        });
        // run
        await taskLoadDataWinWithUmt;
        return data;
    }
}

