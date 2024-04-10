using System.Diagnostics;
using Serilog;
using UndertaleModLib;
using UndertaleModLib.Models;

namespace ModShardDiff;
internal static class MainOperations
{
    public static async Task MainCommand(string name, string reference, string? outputFolder)
    {
        outputFolder ??= Path.Join(Environment.CurrentDirectory, Path.DirectorySeparatorChar.ToString(), "results");

        LoggerConfiguration logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(string.Format("logs/log_{0}.txt", DateTime.Now.ToString("yyyyMMdd_HHmm")));

        Log.Logger = logger.CreateLogger();

        Console.WriteLine($"Exporting differences between {name} and {reference} in {outputFolder}.");

        Stopwatch stopWatch = new();
        stopWatch.Start();
        Task<bool> task = FileReader.Diff(name, reference, outputFolder);
        try
        {
            await task;
        }
        catch(Exception ex)
        {
            Log.Error(ex, "Something went wrong");
        }

        if (task.Result)
        {
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Console.WriteLine($"Process failed successfully in {elapsedTime}.");
        }
        else
        {
            Console.WriteLine("Failed exporting differences.");
        }

        await Log.CloseAndFlushAsync();
    }
}
internal static class FileReader
{
    public static async Task<bool> Diff(string name, string reference, string outputFolder)
    {
        DirectoryInfo dir = new(outputFolder);
        if (dir.Exists) dir.Delete(true);
        dir.Create();
        
        if (!File.Exists(name)) throw new FileNotFoundException($"File {name} does not exist.");
        if (!File.Exists(reference)) throw new FileNotFoundException($"File {reference} does not exist.");

        Task<UndertaleData?> taskName =  LoadFile(name);
        await taskName; 
        Task<UndertaleData?> taskRef =  LoadFile(reference);
        await taskRef;

        if (taskName.Result == null || taskRef.Result == null) throw new FormatException($"Cannot load {name} and {outputFolder}.");
        
        DiffUtils.DiffCodes(taskName.Result, taskRef.Result, dir);
        DiffUtils.DiffObjects(taskName.Result, taskRef.Result, dir);
        DiffUtils.DiffRooms(taskName.Result, taskRef.Result, dir);
        DiffUtils.DiffSounds(taskName.Result, taskRef.Result, dir);
        DiffUtils.DiffSprites(taskName.Result, taskRef.Result, dir);
        DiffUtils.DiffTexturePageItems(taskName.Result, taskRef.Result, dir);
        return true;
    }
    private static UndertaleData? LoadUmt(string filename)
    {
        UndertaleData? data = null;
        using (FileStream stream = new(filename, FileMode.Open, FileAccess.Read))
        {
            data = UndertaleIO.Read(stream);
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

