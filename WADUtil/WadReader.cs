using System.Text;

namespace ame.WadReader;

public class Wad
{
    public WadType Type { get; internal set; }
    public List<Lump> Directory { get; internal set; } = new List<Lump>();
    public string Path { get; internal set; } = "";

    public Wad(string path)
    {
        using(FileStream fs = File.Open(path, FileMode.Open))
        {
            byte[] header = new Byte[4];
            fs.ReadExactly(header, 0, 4);
            if (Encoding.ASCII.GetString(header) != "IWAD" && Encoding.ASCII.GetString(header) != "PWAD")
                throw new NonWadFileException();

            byte[] numLumps = new byte[4];
            fs.ReadExactly(numLumps, 0, 4);
            byte[] dirOffset = new byte[4];
            fs.ReadExactly(dirOffset, 0, 4);
            List<Lump> dir = new List<Lump>();
            Path = path;
            Type = Encoding.ASCII.GetString(header) == "PWAD" ? WadType.Pwad : WadType.Iwad;

            for(int index = 0; index < BitConverter.ToInt32(numLumps); index++)
            {
                int offset = index * 16 + BitConverter.ToInt32(dirOffset);
                byte[] filesize, fileoffset;
                filesize = new byte[4]; 
                fileoffset = new byte[4];
                byte[] filename = new byte[8];
                
                fs.Seek(offset, SeekOrigin.Begin);
                fs.ReadExactly(fileoffset, 0, 4);
                fs.ReadExactly(filesize, 0, 4);
                fs.ReadExactly(filename, 0, 8);
                
                Lump file = new Lump();
                file.Offset = BitConverter.ToInt32(fileoffset);
                file.Size = BitConverter.ToInt32(filesize);
                file.SourceWad = path;
                file.Name = Encoding.ASCII.GetString(filename).Trim('\0');
                file.PositionInWad = index;
                Directory.Add(file);
            }
        }
    }

    public byte[] ReadFile(Lump file)
    {
        using (FileStream fs = File.Open(Path, FileMode.Open))
        {
            fs.Seek(file.Offset, SeekOrigin.Begin);
            byte[] contents = new byte[file.Size];
            fs.ReadExactly(contents, 0, file.Size);
            return contents;
        }
    }
    
    public async Task<Byte[]> ReadFileAsync(Lump file)
    {
        using (FileStream fs = File.Open(Path, FileMode.Open)) 
        {
            fs.Seek(file.Offset, SeekOrigin.Begin);
            byte[] contents = new byte[file.Size];
            await fs.ReadExactlyAsync(contents, 0, file.Size);
            return contents;
        }
    }
    
    public Lump? FindLump(string name)
    {
        foreach (Lump lump in Directory)
        {
            if (lump.Name == name)
                return lump;
        }

        return null;
    }
}

public class Lump
{
    public int Offset { get; internal set; }
    public int Size { get; internal set; }
    public string Name { get; internal set; } = "";
    public int PositionInWad { get; internal set; }
    public string SourceWad { get; internal set; } = "";
}

public enum WadType
{
    Iwad,
    Pwad
}

[Serializable]
class NonWadFileException : Exception
{
    public NonWadFileException() : base("This file is not a valid WAD.")
    {
            
    }
}