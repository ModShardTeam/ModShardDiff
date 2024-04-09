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

        Console.WriteLine($"Exporting differences between {name} and {reference} in {outputFolder}.");
        
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
    private static void ExportDiffs(IEnumerable<string> added, IEnumerable<string> removed, string name, DirectoryInfo outputFolder)
    {
        File.WriteAllLines(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), $"added{name}.txt"), added);
        File.WriteAllLines(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), $"removed{name}.txt"), removed);
    }
    private static void DiffCodes(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        IEnumerable<string> added = name.Code.Select(x => x.Name.Content).Except(reference.Code.Select(x => x.Name.Content));
        IEnumerable<string> removed = reference.Code.Select(x => x.Name.Content).Except(name.Code.Select(x => x.Name.Content));
        ExportDiffs(added, removed, "Codes", outputFolder);
    }
    private static void DiffObjects(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        IEnumerable<string> added = name.GameObjects.Select(x => x.Name.Content).Except(reference.GameObjects.Select(x => x.Name.Content));
        IEnumerable<string> removed = reference.GameObjects.Select(x => x.Name.Content).Except(name.GameObjects.Select(x => x.Name.Content));
        ExportDiffs(added, removed, "GameObjects", outputFolder);
    }
    private static void DiffRooms(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        IEnumerable<string> added = name.Rooms.Select(x => x.Name.Content).Except(reference.Rooms.Select(x => x.Name.Content));
        IEnumerable<string> removed = reference.Rooms.Select(x => x.Name.Content).Except(name.Rooms.Select(x => x.Name.Content));
        ExportDiffs(added, removed, "Rooms", outputFolder);
    }
    private static void DiffSounds(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        IEnumerable<string> added = name.Sounds.Select(x => x.Name.Content).Except(reference.Sounds.Select(x => x.Name.Content));
        IEnumerable<string> removed = reference.Sounds.Select(x => x.Name.Content).Except(name.Sounds.Select(x => x.Name.Content));
        ExportDiffs(added, removed, "Sounds", outputFolder);
    }
    private static void DiffSprites(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        IEnumerable<string> added = name.Sprites.Select(x => x.Name.Content).Except(reference.Sprites.Select(x => x.Name.Content));
        IEnumerable<string> removed = reference.Sprites.Select(x => x.Name.Content).Except(name.Sprites.Select(x => x.Name.Content));
        ExportDiffs(added, removed, "Sprites", outputFolder);
    }
    private static void DiffTexturePageItems(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        IEnumerable<string> added = name.TexturePageItems.Select(x => x.Name.Content).Except(reference.TexturePageItems.Select(x => x.Name.Content));
        IEnumerable<string> removed = reference.TexturePageItems.Select(x => x.Name.Content).Except(name.TexturePageItems.Select(x => x.Name.Content));
        ExportDiffs(added, removed, "TexturePageItems", outputFolder);
    }
    public static async Task<bool> Diff(string name, string reference, string outputFolder)
    {
        DirectoryInfo dir = new(Path.Join(outputFolder, Path.DirectorySeparatorChar.ToString(), "results"));
        if (dir.Exists) dir.Delete(true);
        dir.Create();

        Task<UndertaleData?> taskName =  LoadFile(name);
        await taskName;
        Task<UndertaleData?> taskRef =  LoadFile(reference);
        await taskRef;

        if (taskName.Result == null || taskRef.Result == null) return false;
        
        DiffCodes(taskName.Result, taskRef.Result, dir);
        DiffObjects(taskName.Result, taskRef.Result, dir);
        DiffRooms(taskName.Result, taskRef.Result, dir);
        DiffSounds(taskName.Result, taskRef.Result, dir);
        DiffSprites(taskName.Result, taskRef.Result, dir);
        DiffTexturePageItems(taskName.Result, taskRef.Result, dir);
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

