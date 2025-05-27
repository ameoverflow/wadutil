using System.Text;
using ame.WadReader;

namespace WADUtil;

class Program
{
    private static Wad file;
    private static string[] commandArgs;
    private static string path;
    
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: wadutil <path> <command>");
            Console.WriteLine("Available commands:");
            Console.WriteLine(" ls");
            return 1;
        }
        
        string command = args[1];
        commandArgs = args.Skip(2).ToArray();
        path = args[0];
        
        // if it's new file command, skip reading wad entirely
        if (command == "new")
        {
            CreateNewWad();
            return 0;
        }
        
        file = new Wad(args[0]);

        switch (command)
        {
            case "ls":
                PrintDirectory();
                break;
            case "wad":
                PrintWadInfo();
                break;
            case "lump":
                PrintLumpInfo();
                break;
            case "extract":
                ExtractFile();
                break;
            case "add":
                AddLump();
                break;
            case "rm":
                DeleteLump();
                break;
            
        }

        return 0;
    }

    #region information commands

    private static void PrintDirectory()
    {
        Console.Write("total ");
        if (commandArgs.Contains("-h"))
        {
            Console.WriteLine(ConvertSizeUnits(file.Directory.Sum(lump => lump.Size)));    
        }
        else
        {
            Console.WriteLine(file.Directory.Sum(lump => lump.Size));
        }
                    
        foreach (var lump in file.Directory)
        {
            StringBuilder fileEntry = new StringBuilder();
            fileEntry.Append($"{lump.PositionInWad}");
            int characterFill = 5 - lump.PositionInWad.ToString().Length;
            fileEntry.Append(String.Concat(Enumerable.Repeat(" ", characterFill + 2)));
                        
            fileEntry.Append(lump.Name);
        
            characterFill = 8 - lump.Name.Length;
            fileEntry.Append(String.Concat(Enumerable.Repeat(" ", characterFill)));
        
            if (commandArgs.Contains("-h"))
            {
                characterFill = 11 - ConvertSizeUnits(lump.Size).Length;
                fileEntry.Append($"  {String.Concat(Enumerable.Repeat(" ", characterFill))}");
                fileEntry.Append($"{ConvertSizeUnits(lump.Size)}");
            }
            else
            {
                fileEntry.Append($"  {lump.Size}");
            }
                        
            Console.WriteLine(fileEntry);
        }
    }

    private static void PrintWadInfo()
    {
        Console.WriteLine($"type: {file.Type}");
        Console.WriteLine($"directory size: {file.Directory.Count}");
    }

    private static void PrintLumpInfo()
    {
        // determine if arg is name or position
        Lump lump;
        if (int.TryParse(commandArgs[0], out _))
        {
            // it's file position
            if (int.Parse(commandArgs[0]) == file.Directory.Count)
            {
                Console.WriteLine("outside of directory");
                return;
            }
            lump = file.Directory[int.Parse(commandArgs[0])];
        }
        else
        {
            // it's a file name
            lump = file.FindLump(commandArgs[0].ToUpper());
            if (lump == null)
            {
                Console.WriteLine("no lump found");
            }
        }
        
        Console.WriteLine($"name: {lump.Name}");
        Console.WriteLine($"size: {(commandArgs.Contains("-h") ? ConvertSizeUnits(lump.Size) : lump.Size)}");
        Console.WriteLine($"type: {FileRecognizer.RecognizeFile(file, lump)}");
        Console.WriteLine($"pointer: {(commandArgs.Contains("-h") ? "0x" + lump.Offset.ToString("x8").ToUpper() : lump.Offset)}");
    }
    
    #endregion
    
    #region file operations
    
    private static void CreateNewWad()
    {
        using (var fileStream =
               new FileStream(path, FileMode.Create))
        {
            fileStream.Write(Encoding.UTF8.GetBytes(commandArgs.Contains("-i") ? "IWAD" : "PWAD"), 0, 4);
            fileStream.Write(BitConverter.GetBytes((uint)0), 0, 4);
            fileStream.Write(BitConverter.GetBytes((uint)fileStream.Length + 4), 0, 4);
            
            fileStream.Close();
        }
        Console.WriteLine("created new wad");
    }

    private static void ExtractFile()
    {
        Lump lump;
        if (int.TryParse(commandArgs[0], out _))
        {
            // it's file position
            if (int.Parse(commandArgs[0]) >= file.Directory.Count)
            {
                Console.WriteLine("outside of directory");
                return;
            }
            lump = file.Directory[int.Parse(commandArgs[0])];
        }
        else
        {
            Console.WriteLine("exact position required");
            return;
        }
        
        byte[] lumpData = file.ReadFile(lump);
        File.WriteAllBytes(lump.Name, lumpData);
        Console.WriteLine($"{lump.Name} extracted");
    }

    private static void AddLump()
    {
        byte[] srcFile = File.ReadAllBytes(commandArgs[0]);
        string filename = string.Concat(Path.GetFileNameWithoutExtension(commandArgs[0]).ToUpper().Take(8));
        Console.WriteLine($"adding lump {filename}");
        Console.WriteLine($"{srcFile.Length} bytes");
        
        Console.WriteLine("dumping all lumps to memory");
        List<byte[]> lumpData = new List<byte[]>();
        foreach (Lump lump in file.Directory)
        {
            lumpData.Add(file.ReadFile(lump));
        }
        
        Console.WriteLine("rebuilding wad, this may take a while...");
        using (var fileStream =
               new FileStream(path, FileMode.Create))
        {
            fileStream.Write(Encoding.UTF8.GetBytes(file.Type == WadType.Iwad ? "IWAD" : "PWAD"), 0, 4);
            fileStream.Write(BitConverter.GetBytes((uint)file.Directory.Count + 1), 0, 4);
            
            // write nonexistent directory offset, this will get rewritten later
            fileStream.Write(BitConverter.GetBytes((uint)0), 0, 4);
            
            foreach (var lump in file.Directory)
            {
                // write lump data to wad
                fileStream.Write(lumpData[lump.PositionInWad], 0, lumpData[lump.PositionInWad].Length);
            }
            
            // write the new file
            int newOffset = (int)fileStream.Position;
            fileStream.Write(srcFile, 0, srcFile.Length);
            
            // jump to start of wad file to write directory offset
            fileStream.Seek(8, SeekOrigin.Begin);
            fileStream.Write(BitConverter.GetBytes((uint)fileStream.Length), 0, 4);
            
            // seek to end and start writing directory data
            fileStream.Seek(0,  SeekOrigin.End);
            foreach (var lump in file.Directory)
            {
                fileStream.Write(BitConverter.GetBytes(lump.Offset), 0, 4);
                fileStream.Write(BitConverter.GetBytes(lump.Size), 0, 4);
                fileStream.Write(Encoding.UTF8.GetBytes(lump.Name + String.Concat(Enumerable.Repeat("\0", 8 - lump.Name.Length))), 0, 8);
            }
            
            // write new lump directory entry
            fileStream.Write(BitConverter.GetBytes(newOffset), 0, 4);
            fileStream.Write(BitConverter.GetBytes(srcFile.Length), 0, 4);
            fileStream.Write(Encoding.UTF8.GetBytes(filename + String.Concat(Enumerable.Repeat("\0", 8 - filename.Length))), 0, 8);
            
            fileStream.Close();
        }
    }

    private static void DeleteLump()
    {
        int position;
        if (int.TryParse(commandArgs[0], out _))
        {
            // it's file position
            if (int.Parse(commandArgs[0]) >= file.Directory.Count)
            {
                Console.WriteLine("outside of directory");
                return;
            }

            position = int.Parse(commandArgs[0]);
        }
        else
        {
            Console.WriteLine("exact position required");
            return;
        }
        
        Console.WriteLine("dumping all lumps to memory");
        List<byte[]> lumpData = new List<byte[]>();
        foreach (Lump lump in file.Directory)
        {
            lumpData.Add(file.ReadFile(lump));
        }
        
        Console.WriteLine("rebuilding wad, this may take a while...");
        using (var fileStream =
               new FileStream(path, FileMode.Create))
        {
            fileStream.Write(Encoding.UTF8.GetBytes(file.Type == WadType.Iwad ? "IWAD" : "PWAD"), 0, 4);
            fileStream.Write(BitConverter.GetBytes((uint)file.Directory.Count - 1), 0, 4);
            
            // write 0 for now, this will get rewritten later
            fileStream.Write(BitConverter.GetBytes((uint)0), 0, 4);
            
            foreach (var lump in file.Directory)
            {
                // skip the deleted file when writing lump data
                if (lump.PositionInWad == position)
                    continue;
                
                fileStream.Write(lumpData[lump.PositionInWad], 0, lumpData[lump.PositionInWad].Length);
            }
            
            // jump to start of wad file to write directory location
            fileStream.Seek(8, SeekOrigin.Begin);
            fileStream.Write(BitConverter.GetBytes((uint)fileStream.Length), 0, 4);
            
            // seek to end and start writing directory data
            fileStream.Seek(0,  SeekOrigin.End);
            foreach (var lump in file.Directory)
            {
                // skip deleted lump directory entry
                if (lump.PositionInWad == position)
                    continue;
                
                fileStream.Write(BitConverter.GetBytes(lump.Offset), 0, 4);
                fileStream.Write(BitConverter.GetBytes(lump.Size), 0, 4);
                fileStream.Write(Encoding.UTF8.GetBytes(lump.Name + String.Concat(Enumerable.Repeat("\0", 8 - lump.Name.Length))), 0, 8);
            }
            
            fileStream.Close();
        }
    }
        
    #endregion

    static string ConvertSizeUnits(float size)
    {
        if (size < 1024)
        {
            return $"{size} B";
        }
        
        size /= 1024f;
        
        if (size < 1024)
        {
            return $"{Math.Round(size, 2)} KiB";
        }
        
        size /= 1024f;
        
        if (size < 1024)
        {
            return $"{Math.Round(size, 2)} MiB";
        }
        
        size /= 1024f;
        
        return $"{Math.Round(size, 2)} GiB";
    }
}