using ame.WadReader;

namespace WADUtil;

public class FileRecognizer
{
    public static FileType RecognizeFile(Wad wad, Lump lump)
    {
        // maps
        // map markers are detected by few lumps occuring after that lump
        // it's really basic now, it doesn't check whenever sublumps under that map marker are actual data lumpa
        // i.e. just checks their names
        if (wad.Directory[lump.PositionInWad + 1].Name == "THINGS" &&
            wad.Directory[lump.PositionInWad + 2].Name == "LINEDEFS" &&
            wad.Directory[lump.PositionInWad + 3].Name == "SIDEDEFS" &&
            wad.Directory[lump.PositionInWad + 4].Name == "VERTEXES" &&
            wad.Directory[lump.PositionInWad + 5].Name == "SEGS" &&
            wad.Directory[lump.PositionInWad + 6].Name == "SSECTORS" &&
            wad.Directory[lump.PositionInWad + 7].Name == "NODES" &&
            wad.Directory[lump.PositionInWad + 8].Name == "SECTORS" &&
            wad.Directory[lump.PositionInWad + 10].Name == "BLOCKMAP")
        {
            return FileType.Map;
        }
        
        // we don't know what file it is
        return FileType.Marker;
    }
}

public enum FileType
{
    Sound,
    Music,
    Playpal,
    Colormap,
    Endoom,
    Demo,
    Sprite,
    Map,
    Flat,
    Marker
}