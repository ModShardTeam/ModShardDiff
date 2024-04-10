using Serilog;
using UndertaleModLib;
using UndertaleModLib.Models;
using DiffMatchPatch;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Util;
using System.Runtime.Serialization.Formatters.Binary;
using Polenter.Serialization;

namespace ModShardDiff;

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
static public class DiffUtils
{
    // thanks to Pong to acknowledge me that possibility
    private static unsafe bool UnsafeCompare(byte[] a1, byte[] a2)
    {
        if (a1 == null || a2 == null || a1.Length != a2.Length) return false;
        fixed (byte* p1 = a1, p2 = a2)
        {
            byte* x1 = p1, x2 = p2;
            int len = a1.Length;
            for (int i = 0; i < len / 8; i++, x1 += 8, x2 += 8)
            {
                if (*(long*) x1 != *(long*) x2) return false; // classic type casting in C, testing 8 bits by 8 bits
            }
            if ((len & 4) != 0) // testing last 4 bits
            {
                if (*(int*) x1 != *(int*) x2) return false;
                x1 += 4;
                x2 += 4;
            }
            if ((len & 2) != 0) // 2 bits
            {
                if (*(short*) x1 != *(short*) x2) return false;
                x1 += 2;
                x2 += 2;
            }
            if ((len & 1) != 0 && *x1 != *x2) return false;
            return true;
        }
    }
    private static bool CompareUndertaleCode(MemoryStream ms, SharpSerializer burstSerializer, UndertaleCode codeRef, UndertaleCode code)
    {
        ms.SetLength(0);
        burstSerializer.Serialize(code, ms);
        byte[] bytes = ms.ToArray();
        ms.SetLength(0);
        
        burstSerializer.Serialize(codeRef, ms);
        byte[] bytesRef = ms.ToArray();
        return UnsafeCompare(bytes, bytesRef);
    }
    public static void ExportDiffs(IEnumerable<string> added, IEnumerable<string> removed, string name, DirectoryInfo outputFolder)
    {
        File.WriteAllLines(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), $"added{name}.txt"), added);
        File.WriteAllLines(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), $"removed{name}.txt"), removed);
    }
    private static void AddedRemovedCodes(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        GlobalDecompileContext contextName = new(name, false);
        DirectoryInfo dirAddedCode = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), "AddedCodes"));
        dirAddedCode.Create();

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
    }
    private static void ModifiedCodes(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        using MemoryStream ms = new();
        SharpSerializerBinarySettings settings = new(BinarySerializationMode.Burst);
        SharpSerializer burstSerializer = new(settings);

        GlobalDecompileContext contextName = new(name, false);
        GlobalDecompileContext contextRef = new(reference, false);
        DirectoryInfo dirModifiedCode = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), "ModifiedCodes"));
        dirModifiedCode.Create();

        string strName = "";
        string strRef = "";
        UndertaleCode codeRef;

        IEnumerable<UndertaleCode> common = name.Code.Intersect(reference.Code, new UndertaleCodeNameComparer());
        diff_match_patch dmp = new();

        foreach(UndertaleCode code in common)
        {
            codeRef = reference.Code.First(t => t.Name.Content == code.Name.Content);
            if (CompareUndertaleCode(ms, burstSerializer, code, codeRef)) continue;
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
            if (diff.Count == 0 || (diff.Count == 1 && diff[0].operation == Operation.EQUAL)) continue;

            string report = dmp.diff_prettyHtml(diff);
            File.WriteAllText(Path.Join(dirModifiedCode.FullName, Path.DirectorySeparatorChar.ToString(), $"{code.Name.Content}.html"), report);
        }
    }
    public static void DiffCodes(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        AddedRemovedCodes(name, reference, outputFolder);
        ModifiedCodes(name, reference, outputFolder);
    }
    public static void DiffObjects(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        IEnumerable<string> added = name.GameObjects.Select(x => x.Name.Content).Except(reference.GameObjects.Select(x => x.Name.Content));
        IEnumerable<string> removed = reference.GameObjects.Select(x => x.Name.Content).Except(name.GameObjects.Select(x => x.Name.Content));
        ExportDiffs(added, removed, "GameObjects", outputFolder);
    }
    public static void DiffRooms(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        IEnumerable<string> added = name.Rooms.Select(x => x.Name.Content).Except(reference.Rooms.Select(x => x.Name.Content));
        IEnumerable<string> removed = reference.Rooms.Select(x => x.Name.Content).Except(name.Rooms.Select(x => x.Name.Content));
        ExportDiffs(added, removed, "Rooms", outputFolder);
    }
    public static void DiffSounds(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        IEnumerable<string> added = name.Sounds.Select(x => x.Name.Content).Except(reference.Sounds.Select(x => x.Name.Content));
        IEnumerable<string> removed = reference.Sounds.Select(x => x.Name.Content).Except(name.Sounds.Select(x => x.Name.Content));
        ExportDiffs(added, removed, "Sounds", outputFolder);
    }
    private static void AddedRemovedSprites(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder, TextureWorker worker)
    {
        DirectoryInfo dirAddedSprite = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), "AddedSprites"));
        dirAddedSprite.Create(); 

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
    }
    private static void ModifiedSprites(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder, TextureWorker worker)
    {
        DirectoryInfo dirModifiedSprite = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), "ModifiedSprites"));
        dirModifiedSprite.Create();

        IEnumerable<UndertaleSprite> common = name.Sprites.Intersect(reference.Sprites, new UndertaleSpriteNameComparer());
        int minCount = 0;
        foreach(UndertaleSprite sprite in common)
        {
            UndertaleSprite spriteRef = reference.Sprites.First(t => t.Name.Content == sprite.Name.Content);
            minCount = sprite.Textures.Count < spriteRef.Textures.Count ? sprite.Textures.Count : spriteRef.Textures.Count;

            for (int i = 0; i < minCount; i++)
            {
                if (sprite.Textures[i]?.Texture is not null)
                {
                    if(UnsafeCompare(sprite.Textures[i].Texture.TexturePage.TextureData.TextureBlob, spriteRef.Textures[i].Texture.TexturePage.TextureData.TextureBlob)) continue;
                    worker.ExportAsPNG(sprite.Textures[i].Texture, Path.Combine(dirModifiedSprite.FullName , sprite.Name.Content + "_" + i + ".png"), null, true);
                }
            }

            for (int i = minCount; i < sprite.Textures.Count; i++)
            {
                if (sprite.Textures[i]?.Texture is not null)
                {
                    worker.ExportAsPNG(sprite.Textures[i].Texture, Path.Combine(dirModifiedSprite.FullName , sprite.Name.Content + "_" + i + ".png"), null, true);
                }
            }
        }
    }
    public static void DiffSprites(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        TextureWorker worker = new();
        
        AddedRemovedSprites(name, reference, outputFolder, worker);
        ModifiedSprites(name, reference, outputFolder, worker);
    }
    public static void DiffTexturePageItems(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        IEnumerable<string> added = name.TexturePageItems.Select(x => x.Name.Content).Except(reference.TexturePageItems.Select(x => x.Name.Content));
        IEnumerable<string> removed = reference.TexturePageItems.Select(x => x.Name.Content).Except(name.TexturePageItems.Select(x => x.Name.Content));
        ExportDiffs(added, removed, "TexturePageItems", outputFolder);
    }
}