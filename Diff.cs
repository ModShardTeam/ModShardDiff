using Serilog;
using UndertaleModLib;
using UndertaleModLib.Models;
using DiffMatchPatch;
using UndertaleModLib.Decompiler;
using System.Runtime;
using UndertaleModLib.Util;

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
        
        Task<bool> task = FileReader.Diff(name, reference, outputFolder);
        await task;
        if (task.Result)
        {
            Console.WriteLine($"Process failed successfully.");
        }
        else
        {
            Console.WriteLine("Failed exporting differences.");
        }

        await Log.CloseAndFlushAsync();
    }
}

class UndertaleCodeNameComparer : IEqualityComparer<UndertaleCode>
{
    public bool Equals(UndertaleCode? x, UndertaleCode? y)
    {
        if (x == null || y == null) return false;
        return x.Name.Content == y.Name.Content;
    }

    // If Equals() returns true for a pair of objects
    // then GetHashCode() must return the same value for these objects.

    public int GetHashCode(UndertaleCode x)
    {
        //Check whether the object is null
        if (x == null) return 0;
        return x.Name.Content.GetHashCode();
    }
}

class UndertaleSpriteNameComparer : IEqualityComparer<UndertaleSprite>
{
    public bool Equals(UndertaleSprite? x, UndertaleSprite? y)
    {
        if (x == null || y == null) return false;
        return x.Name.Content == y.Name.Content;
    }

    // If Equals() returns true for a pair of objects
    // then GetHashCode() must return the same value for these objects.

    public int GetHashCode(UndertaleSprite x)
    {
        //Check whether the object is null
        if (x == null) return 0;
        return x.Name.Content.GetHashCode();
    }
}

class UnderTaleInstructionComparer : IEqualityComparer<UndertaleInstruction>
{
    public bool Equals(UndertaleInstruction? x, UndertaleInstruction? y)
    {
        if (x == null || y == null) return false;
        return x.Address == y.Address && x.Kind == y.Kind && x.Type1 == y.Type1 && x.Type2 == y.Type2 && x.TypeInst == y.TypeInst && x.ComparisonKind == y.ComparisonKind;
    }

    // If Equals() returns true for a pair of objects
    // then GetHashCode() must return the same value for these objects.

    public int GetHashCode(UndertaleInstruction x)
    {
        //Check whether the object is null
        if (x == null) return 0;
        return x.Address.GetHashCode() ^ x.Kind.GetHashCode() ^ x.Type1.GetHashCode() ^ x.Type2.GetHashCode() ^ x.TypeInst.GetHashCode() ^ x.ComparisonKind.GetHashCode();
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
        GlobalDecompileContext contextName = new(name, false);
        GlobalDecompileContext contextRef = new(reference, false);
        DirectoryInfo dirAddedCode = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), "AddedCodes"));
        dirAddedCode.Create();
        DirectoryInfo dirModifiedCode = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), "ModifiedCodes"));
        dirModifiedCode.Create();

        IEnumerable<UndertaleCode> added = name.Code.Except(reference.Code, new UndertaleCodeNameComparer());
        IEnumerable<UndertaleCode> removed = reference.Code.Except(name.Code, new UndertaleCodeNameComparer());

        using (StreamWriter sw = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), $"addedCodes.txt")))
        {
            foreach(UndertaleCode code in added)
            {
                sw.WriteLine(code.Name.Content);
                string strCode = "";
                try
                {
                    strCode = Decompiler.Decompile(code, contextName);
                    File.WriteAllText(Path.Join(dirAddedCode.FullName, Path.DirectorySeparatorChar.ToString(), $"{code.Name.Content}.gml"), strCode);
                }
                catch
                { 
                    // pass if failed
                }
            }
        }
        File.WriteAllLines(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), $"removedCodes.txt"), removed.Select(x => x.Name.Content));

        IEnumerable<UndertaleCode> common = name.Code.Intersect(reference.Code, new UndertaleCodeNameComparer());
        diff_match_patch dmp = new();

        foreach(UndertaleCode code in common)
        {
            UndertaleCode codeRef = reference.Code.First(t => t.Name.Content == code.Name.Content);
            if (codeRef.Length == code.Length && codeRef.Instructions.SequenceEqual(code.Instructions, new UnderTaleInstructionComparer())) continue;
            Console.WriteLine($"{code.Name.Content} modified.");

            string strName = "";
            string strRef = "";
            try
            {
                strName = Decompiler.Decompile(code, contextName);
                strRef = Decompiler.Decompile(codeRef, contextRef);
            }
            catch(Exception ex)
            {
                if (!ex.Message.Contains("This code block represents a function nested inside"))
                {
                    try
                    {
                        strName = code.Disassemble(name.Variables, name.CodeLocals.For(code));
                        strRef = codeRef.Disassemble(reference.Variables, reference.CodeLocals.For(codeRef));
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($@"{ex2.GetType()}: {ex2.Message}");
                    }
                }
            }
            List<Diff> diff = dmp.diff_main(strRef, strName);
            if (diff.Count <= 1) continue;

            string report = dmp.diff_prettyHtml(diff);
            File.WriteAllText(Path.Join(dirModifiedCode.FullName, Path.DirectorySeparatorChar.ToString(), $"{code.Name.Content}.html"), report);
        }
        
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
        TextureWorker worker = new();
        DirectoryInfo dirAddedSprite = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), "AddedSprites"));
        dirAddedSprite.Create(); 
        DirectoryInfo dirModifiedSprite = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), "ModifiedSprites"));
        dirModifiedSprite.Create();

        IEnumerable<UndertaleSprite> added = name.Sprites.Except(reference.Sprites, new UndertaleSpriteNameComparer());
        IEnumerable<UndertaleSprite> removed = reference.Sprites.Except(name.Sprites, new UndertaleSpriteNameComparer());
        using (StreamWriter sw = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), $"addedSprites.txt")))
        {
            foreach(UndertaleSprite sprite in added)
            {
                sw.WriteLine(sprite.Name.Content);
                for (int i = 0; i < sprite.Textures.Count; i++)
                {
                    if (sprite.Textures[i]?.Texture is not null)
                    {
                        worker.ExportAsPNG(sprite.Textures[i].Texture, Path.Combine(dirAddedSprite.FullName , sprite.Name.Content + "_" + i + ".png"), null, true);
                    }
                }
            }
        }
        File.WriteAllLines(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), $"removedSprites.txt"), removed.Select(x => x.Name.Content));

        IEnumerable<UndertaleSprite> common = name.Sprites.Intersect(reference.Sprites, new UndertaleSpriteNameComparer());
        bool equalTexture = true;
        foreach(UndertaleSprite sprite in common)
        {
            UndertaleSprite spriteRef = reference.Sprites.First(t => t.Name.Content == sprite.Name.Content);
            for (int i = 0; i < sprite.Textures.Count; i++)
            {
                if (sprite.Textures[i]?.Texture is not null)
                {
                    equalTexture = sprite.Textures[i].Texture.TexturePage.TextureData.TextureBlob.SequenceEqual(spriteRef.Textures[i].Texture.TexturePage.TextureData.TextureBlob);
                    if(equalTexture) continue;
                    worker.ExportAsPNG(sprite.Textures[i].Texture, Path.Combine(dirModifiedSprite.FullName , sprite.Name.Content + "_" + i + ".png"), null, true);
                }
            }
        }
    }
    private static void DiffTexturePageItems(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        IEnumerable<string> added = name.TexturePageItems.Select(x => x.Name.Content).Except(reference.TexturePageItems.Select(x => x.Name.Content));
        IEnumerable<string> removed = reference.TexturePageItems.Select(x => x.Name.Content).Except(name.TexturePageItems.Select(x => x.Name.Content));
        ExportDiffs(added, removed, "TexturePageItems", outputFolder);
    }
    public static async Task<bool> Diff(string name, string reference, string outputFolder)
    {
        DirectoryInfo dir = new(outputFolder);
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

