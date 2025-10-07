using System.Collections.Generic;
using Newtonsoft.Json;

public class RepackProject
{
    public byte[] RawHeader { get; set; }
    public List<SectionInfo> Sections { get; set; }
    public List<FileEntry> Files { get; set; }
}

public class SectionInfo
{
    public byte FileType { get; set; }
    public byte Type2 { get; set; }
    public byte Type3 { get; set; }
    public byte Type4 { get; set; }
    public ushort Unk { get; set; }
}

public class FileEntry
{
    [JsonIgnore]
    public int PhysicalOrder { get; set; }
    public uint OriginalIndex { get; set; }
    public string RelativePath { get; set; }
    public byte PartsCount { get; set; }
    public byte FileType { get; set; }
    public byte Unk1 { get; set; }
    public byte Unk2 { get; set; }
    public byte[]? TextureHeader { get; set; }
    public List<FilePartInfo> Parts { get; set; }
}

public class FilePartInfo
{
    public byte SectionIndex { get; set; }
    public byte Unk1 { get; set; }
}

public class ArchiveHeader
{ public uint Signature { get; set; }
    public uint Version { get; set; } 
    public byte C1 { get; set; } 
    public byte C2 { get; set; } 
    public byte C3 { get; set; } 
    public byte C4 { get; set; } 
    public uint PartsCount { get; set; } 
    public uint SectionsCount { get; set; } 
    public uint FilesCount { get; set; } 
    public uint FileNamesSize { get; set; } 
    public uint FNamesCount { get; set; } 
    public uint Alignment { get; set; } 
}
public class SectionEntry
{
    public byte FileType { get; set; } 
    public byte Type2 { get; set; } 
    public byte Type3 { get; set; } 
    public byte Type4 { get; set; } 
    public uint RawOffset { get; set; } 
    public uint UnpackedSize { get; set; } 
    public uint PackedSize { get; set; } 
    public ushort ResourceCount { get; set; } 
    public ushort Unk { get; set; } 
    public long CalculatedOffset => (long)RawOffset << 4; 
}
public class FilePart 
{
    public byte SectionIndex { get; set; } 
    public byte Unk1 { get; set; } 
    public ushort FileIndex { get; set; } 
    public uint RawOffset { get; set; } 
    public uint Size { get; set; } 
    public uint Unk2 { get; set; } 
}
public class FileMapEntry 
{ 
    public byte PartsCount { get; set; } 
    public byte Unk1 { get; set; } 
    public byte FileType { get; set; } 
    public byte Unk2 { get; set; } 
    public uint FileIndex { get; set; } 
    public uint FirstPartIndex { get; set; } 
}
public class FileNameIndex { public uint Offset { get; set; } }