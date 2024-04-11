using UndertaleModLib;
using UndertaleModLib.Models;
using DiffMatchPatch;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Util;
using Polenter.Serialization;
using Newtonsoft.Json;

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
class UndertaleGameObjectComparer : IEqualityComparer<UndertaleGameObject>
{
    public bool Equals(UndertaleGameObject? x, UndertaleGameObject? y)
    {
        if (x == null || y == null) return false;
        return x.Name.Content == y.Name.Content && 
            (x.Sprite?.Name.Content ?? "") == (y.Sprite?.Name.Content ?? "") &&
            (x.ParentId?.Name.Content ?? "") == (y.ParentId?.Name.Content ?? "") &&
            x.Visible == y.Visible &&
            x.Persistent == y.Persistent &&
            x.Awake == y.Awake &&
            x.CollisionShape == y.CollisionShape && 
            x.Events.Count == y.Events.Count;
    }

    // If Equals() returns true for a pair of objects
    // then GetHashCode() must return the same value for these objects.

    public int GetHashCode(UndertaleGameObject x)
    {
        //Check whether the object is null
        if (x == null) return 0;
        return x.Name.Content.GetHashCode() ^ 
            (x.Sprite?.Name.Content ?? "").GetHashCode() ^ 
            (x.ParentId?.Name.Content ?? "").GetHashCode() ^
            x.Visible.GetHashCode() ^
            x.Persistent.GetHashCode() ^
            x.Awake.GetHashCode() ^
            x.CollisionShape.GetHashCode() ^
            x.Events.Count.GetHashCode();
    }
}
class UndertaleGameObjectNameComparer : IEqualityComparer<UndertaleGameObject>
{
    public bool Equals(UndertaleGameObject? x, UndertaleGameObject? y)
    {
        if (x == null || y == null) return false;
        return x.Name.Content == y.Name.Content;
    }

    // If Equals() returns true for a pair of objects
    // then GetHashCode() must return the same value for these objects.

    public int GetHashCode(UndertaleGameObject x)
    {
        //Check whether the object is null
        if (x == null) return 0;
        return x.Name.Content.GetHashCode();
    }
}
public class GameObjectSummary
{
    public string name {get; set;} = "";
    public string spriteName {get; set;} = "";
    public string parentName {get; set;} = "";
    public bool isVisible {get; set;}
    public bool isPersistent {get; set;}
    public bool isAwake {get; set;}
    public string collisionShapeFlags {get; set;} = "";
    public List<(string, int)> Events {get; set;} = new List<(string, int)>();
}

static public class DiffUtils
{
    public static IEnumerable<(int, T)> Enumerate<T>(this IEnumerable<T> ienumerable) 
    {
        int ind = 0;
        foreach(T element in ienumerable) {
            yield return (ind++, element);
        }
    }
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
    private static bool CompareUndertaleCode(MemoryStream ms, SharpSerializer burstSerializer, UndertaleCode code, UndertaleCode codeRef)
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
        SharpSerializer burstSerializer = new(new SharpSerializerBinarySettings(BinarySerializationMode.Burst));

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
    private static void AddedRemovedObjects(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        DirectoryInfo dirAddedObject = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), "AddedGameObjects"));
        dirAddedObject.Create();

        IEnumerable<UndertaleGameObject> added = name.GameObjects.Except(reference.GameObjects, new UndertaleGameObjectNameComparer());
        IEnumerable<UndertaleGameObject> removed = reference.GameObjects.Except(name.GameObjects, new UndertaleGameObjectNameComparer());
        using (StreamWriter sw = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), $"addedGameObjects.txt")))
        {
            foreach(UndertaleGameObject ob in added)
            {
                sw.WriteLine(ob.Name.Content);
                GameObjectSummary gameObjectSummary = new()
                {
                    name = ob.Name.Content,
                    spriteName = ob.Sprite?.Name.Content ?? "",
                    parentName = ob.ParentId?.Name.Content ?? "",
                    isVisible = ob.Visible,
                    isPersistent = ob.Persistent,
                    isAwake = ob.Awake,
                    collisionShapeFlags = ob.CollisionShape.ToString(),
                    Events = ob.Events.Enumerate().SelectMany(x => x.Item2.Select(y => (((EventType)x.Item1).ToString(), (int)y.EventSubtype))).ToList(),
                };

                File.WriteAllText(Path.Join(dirAddedObject.FullName, Path.DirectorySeparatorChar.ToString(), $"{ob.Name.Content}.json"), 
                    JsonConvert.SerializeObject(gameObjectSummary)
                );
            }
        }
        File.WriteAllLines(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), $"removedGameObjects.txt"), removed.Select(x => x.Name.Content));
    }
    private static void ModifiedObjects(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        diff_match_patch dmp = new();
        UndertaleGameObjectComparer comparer = new();

        DirectoryInfo dirModifiedObject = new(Path.Join(outputFolder.FullName, Path.DirectorySeparatorChar.ToString(), "ModifiedObjects"));
        dirModifiedObject.Create();

        UndertaleGameObject obRef;
        string strName = "";
        string strRef = "";

        IEnumerable<UndertaleGameObject> common = name.GameObjects.Intersect(reference.GameObjects, new UndertaleGameObjectNameComparer());

        foreach(UndertaleGameObject ob in common)
        {
            obRef = reference.GameObjects.First(t => t.Name.Content == ob.Name.Content);
            if (comparer.Equals(ob, obRef)) continue;

            strName = JsonConvert.SerializeObject(
                new GameObjectSummary()
                {
                    name = ob.Name.Content,
                    spriteName = ob.Sprite?.Name.Content ?? "",
                    parentName = ob.ParentId?.Name.Content ?? "",
                    isVisible = ob.Visible,
                    isPersistent = ob.Persistent,
                    isAwake = ob.Awake,
                    collisionShapeFlags = ob.CollisionShape.ToString(),
                    Events = ob.Events.Enumerate().SelectMany(x => x.Item2.Select(y => (((EventType)x.Item1).ToString(), (int)y.EventSubtype))).ToList(),
                }
            );
            strRef = JsonConvert.SerializeObject(
                new GameObjectSummary()
                {
                    name = obRef.Name.Content,
                    spriteName = obRef.Sprite?.Name.Content ?? "",
                    parentName = obRef.ParentId?.Name.Content ?? "",
                    isVisible = obRef.Visible,
                    isPersistent = obRef.Persistent,
                    isAwake = obRef.Awake,
                    collisionShapeFlags = obRef.CollisionShape.ToString(),
                    Events = ob.Events.Enumerate().SelectMany(x => x.Item2.Select(y => (((EventType)x.Item1).ToString(), (int)y.EventSubtype))).ToList(),
                }
            );

            List<Diff> diff = dmp.diff_main(strRef, strName);
            if (diff.Count == 0 || (diff.Count == 1 && diff[0].operation == Operation.EQUAL)) continue;

            string report = dmp.diff_prettyHtml(diff);
            File.WriteAllText(Path.Join(dirModifiedObject.FullName, Path.DirectorySeparatorChar.ToString(), $"{ob.Name.Content}.html"), report);
        }
    }
    public static void DiffObjects(UndertaleData name, UndertaleData reference, DirectoryInfo outputFolder)
    {
        AddedRemovedObjects(name, reference, outputFolder);
        ModifiedObjects(name, reference, outputFolder);
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