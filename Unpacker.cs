using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ionic.Zlib;
using Newtonsoft.Json;

public class Unpacker
{
    private readonly Dictionary<string, string> _formatMap;

    public Unpacker()
    {
        _formatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "dif", "BC1" }, { "spc", "BC1" }, { "ems", "BC1" }, { "nm1", "R16_UNORM" },
            { "1skybox", "RGBA16F" }, { "2048skybox", "BC6H_UF16" }, { "32skybox", "BC6H_UF16" }, { "4096skybox", "BC6H_UF16" },
            { "64skybox", "BC6H_UF16" }, { "aclg", "RGBA8" }, { "ani", "BC4" }, { "anm", "RGBA16_SNORM" }, { "bbs", "BC4" },
            { "bbt", "RGBA16" }, { "bld", "BC4" }, { "bldopc", "BC4" }, { "bldsnorm", "BC4_SNORM" }, { "cau", "BC4" },
            { "cgd", "BC4" }, { "che", "BC1" }, { "chg", "BC4" }, { "chi", "BC7" }, { "chm", "BC5" }, { "chn", "BC5_SNORM" },
            { "cld", "BC4" }, { "clg", "RGBA8" }, { "clp", "BC4_SNORM" }, { "cou", "BC7" }, { "cpd", "BC4" }, { "crb", "BC4" },
            { "ddf", "BC7" }, { "default", "BC1" }, { "deh", "BC1" }, { "det", "BC4" }, { "difa", "BC7" },
            { "dir", "RGBA8_SNORM" }, { "dirs2d", "RG16_SNORM" }, { "dirs3d", "RGBA8_SNORM" }, { "diru2d", "RG16" },
            { "dit", "BC4" }, { "dml", "BC7" }, { "dn1", "R16_SNORM" }, { "dnr", "R16_SNORM" }, { "dpo", "BC4_SNORM" },
            { "dpt", "BC4" }, { "dtc", "BC1" }, { "dtm", "BC7" }, { "dv1", "RGBA16F" }, { "dvc", "RGBA16F" }, { "dye", "BC7" },
            { "ema", "RGBA8" }, { "end", "BC6H_UF16" }, { "enr", "BC6H_UF16" }, { "env", "BC1" },
            { "exp", "R8" }, { "eym", "BC5" }, { "fam", "RGBA8" }, { "fdc", "BC7" }, { "fdm", "BC4" }, { "flm", "BC5_SNORM" },
            { "flw", "BC4" }, { "fow", "BC7" }, { "frs", "BC4" }, { "frz", "BC4" }, { "fxd", "BC7" }, { "fxe", "BC7" },
            { "fxm", "BC5_SNORM" }, { "fxn", "BC5_SNORM" }, { "fxs", "BC7" }, { "fxt", "RGBA8" }, { "ghn", "BC5_SNORM" },
            { "glr", "BC4" }, { "gra", "RGBA8" }, { "grc", "RGBA16F" }, { "grd", "RGBA8" }, { "gre", "RGBA8" }, { "grf", "RGBA8" },
            { "grm", "RGBA8" }, { "gro", "BC4_SNORM" }, { "grr", "R8" }, { "grs", "RGBA8" }, { "guitmp", "RGBA8" },
            { "guitmpmask", "R8" }, { "hcb", "BC6H_UF16" }, { "hcl", "BC1" }, { "hdc", "BC7" }, { "hgt", "R8" }, { "hil", "R8" },
            { "hil2f", "RG8" }, { "hil3f", "RGBA8" }, { "hld", "RGBA8" }, { "hli", "BC6H_UF16" }, { "hnm", "BC5_SNORM" },
            { "hqrfl", "BC6H_UF16" }, { "hsh", "R8" }, { "hwd", "R8" }, { "idx", "BC4" }, { "loc", "BC1" }, { "lut", "RGBA8" },
            { "m3dc", "BC1" }, { "m3dm", "BC4" }, { "m3dnrm", "BC5_SNORM" }, { "mli", "BC6H_UF16" }, { "msk", "BC4" },
            { "msv", "R8" }, { "mtx0", "RGBA16_SNORM" }, { "mtx1", "RGBA16_SNORM" }, { "mtx2", "RGBA16_SNORM" },
            { "nlt", "BC4" }, { "nrd", "RGBA8_UINT" }, { "nrm", "BC5_SNORM" }, { "ocl", "BC4" },
            { "od1", "BC7" }, { "od2", "BC7" }, { "od3", "BC7" }, { "od4", "BC7" }, { "oe1", "BC7" }, { "oe2", "BC7" },
            { "oe3", "BC7" }, { "oe4", "BC7" }, { "ofc", "BC7" }, { "off", "BC4" }, { "ofo", "BC1" }, { "olm", "BC4_SNORM" },
            { "olmhlp", "RGBA8" }, { "olmsrc", "RGBA8" }, { "opc", "BC4" }, { "ovr", "BC4" }, { "ovr3d", "R8" },
            { "ppc0", "BC7" }, { "ppcm", "BC7" }, { "ppd0", "BC5" }, { "ppm0", "BC4" }, { "ppmm", "BC4" }, { "ppnm", "BC5_SNORM" },
            { "prc", "BC7" }, { "pre", "RG8" }, { "prj", "BC7" }, { "ref", "BC7" }, { "rfm", "BC4" }, { "rgh", "BC4" },
            { "rot", "BC5_SNORM" }, { "satdif", "BC7" }, { "satnrm", "BC5_SNORM" }, { "satrgh", "BC4" }, { "satspc", "BC7" },
            { "sdf", "R8" }, { "sdm", "RGBA8" }, { "skm", "RGBA16F" }, { "skn", "BC4" }, { "sky", "BC6H_UF16" },
            { "srgh", "BC4_SNORM" }, { "thc", "BC4" }, { "tng", "BC5_SNORM" }, { "trn", "BC4" }, { "trs", "R16" }, { "txc", "BC4" },
            { "uic", "BC7" }, { "uics", "BC7" }, { "uif", "BC4" }, { "uifmsdf", "BC7" }, { "uim", "BC4" }, { "uims", "BC4" },
            { "uimu", "R8" }, { "uimus", "R8" }, { "uinrm", "BC5_SNORM" }, { "uistory", "BC7" }, { "uiu", "RGBA8" },
            { "uius", "RGBA8" }, { "usr", "RGBA8" }, { "uva", "BC1" }, { "uvg", "RGBA8" }, { "uvm", "BC4" }, { "va1", "RGBA8" },
            { "vas", "RGBA8" }, { "vgn", "RGBA8" }, { "wflm", "BC5_SNORM" }, { "wmp", "RGBA8" }, { "wri", "BC5_SNORM" },
            { "wscm", "BC4" }, { "wvw", "BC5_SNORM" }, { "wwv", "R8_SNORM" }, { "xuic", "BC7" }, { "xuicna", "BC1" },
            { "xuil8", "R8" }, { "xuiu", "RGBA8" }, { "zet", "BC4" }
        };
    }

    private string GetTextureSuffix(string fileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        var parts = baseName.Split('_');
        if (parts.Length > 1) { for (int i = parts.Length - 1; i >= 0; i--) { string potentialSuffix = string.Join("_", parts, i, parts.Length - i); if (_formatMap.ContainsKey(potentialSuffix)) { return potentialSuffix; } } }
        if (_formatMap.ContainsKey(baseName)) { return baseName; }
        return "fallback";
    }

    private string GetFormatFromLegacyGameFormat(uint gameFormat, uint texType)
    {
        switch (gameFormat)
        {
            case 23: case 89: case 186: return "BC1";
            case 4:
            case 8:
            case 10:
            case 11:
            case 14:
            case 16:
            case 25:
            case 27:
            case 35:
            case 38:
            case 43:
            case 44:
            case 50:
            case 81:
            case 83:
            case 86:
            case 91:
            case 95:
            case 97:
            case 99:
            case 107:
            case 108:
            case 114:
            case 121:
            case 125:
            case 133:
            case 135:
            case 137:
            case 183:
            case 189:
                return "BC4";
            case 12: case 22: case 52: return "BC4_SNORM";
            case 19: case 106: return "BC5";
            case 20:
            case 41:
            case 47:
            case 48:
            case 49:
            case 82:
            case 90:
            case 109:
            case 115:
            case 120:
            case 134:
            case 144:
            case 180:
            case 182:
            case 184:
                return "BC5_SNORM";
            case 1: case 2: case 3: case 5: case 62: return "BC6H_UF16";
            case 17:
            case 18:
            case 24:
            case 28:
            case 29:
            case 30:
            case 45:
            case 51:
            case 53:
            case 54:
            case 55:
            case 63:
            case 92:
            case 93:
            case 94:
            case 104:
            case 105:
            case 110:
            case 112:
            case 122:
            case 128:
            case 145:
                return "BC7";
            case 37:
            case 60:
            case 64:
            case 68:
            case 84:
            case 100:
            case 118:
            case 143:
            case 187:
                return "R8";
            case 185: return "R8_SNORM";
            case 111: return "RG8";
            case 87: return "RG8_SNORM";
            case 56: return "R16_UNORM";
            case 33: case 34: return "R16_SNORM";
            case 36: return "RG16";
            case 32: return "RG16_SNORM";
            case 6:
            case 21:
            case 42:
            case 46:
            case 61:
            case 67:
            case 78:
            case 146:
            case 147:
            case 149:
            case 150:
            case 152:
            case 181:
            case 188:
                return "RGBA8";
            case 31: return "RGBA8_SNORM";
            case 88: return "RGBA8_UINT";
            case 9: case 141: return "RGBA16";
            case 7: case 85: return "RGBA16_SNORM";
            case 0: case 39: case 40: case 124: return "RGBA16F";

            case 59: return (texType == 1) ? "BC7" : "BC1";
            case 65: return (texType == 1) ? "BC6H_UF16" : "R8";
            case 66: return (texType == 1) ? "BC6H_UF16" : "RG8";
            case 80: return (texType == 1) ? "BC6H_UF16" : "BC1";
            case 96: return (texType == 1) ? "BC4_SNORM" : "RGBA8";
            case 113: return (texType == 1) ? "BC7" : "BC4";
            case 119: return "BC7";
            case 139: return (texType == 1) ? "BC7" : "BC1";
            default:
                return "BC1";
        }
    }

    public void Unpack(string rpackPath)
    {
        Console.WriteLine("Starting unpacking...");
        var processedTexturesLog = new List<Tuple<string, string>>();
        string archiveName = Path.GetFileNameWithoutExtension(rpackPath);
        string outputDirectory = Path.Combine(Path.GetDirectoryName(rpackPath), archiveName + "_unpack");
        if (Directory.Exists(outputDirectory)) { Directory.Delete(outputDirectory, true); }
        Directory.CreateDirectory(outputDirectory);
        Console.WriteLine($"Files will be extracted to: {outputDirectory}");
        var project = new RepackProject { Sections = new List<SectionInfo>(), Files = new List<FileEntry>() };
        using (var stream = File.OpenRead(rpackPath))
        using (var reader = new BinaryReader(stream))
        {
            project.RawHeader = reader.ReadBytes(36);
            var header = ReadHeader(project.RawHeader);
            var sectionEntries = ReadSectionEntries(reader, header.SectionsCount);
            var fileParts = ReadFileParts(reader, header.PartsCount);
            var fileMapEntries = ReadFileMapEntries(reader, header.FilesCount);
            var fileNameIndices = ReadFileNameIndices(reader, header.FNamesCount);
            byte[] fileNameBlock = reader.ReadBytes((int)header.FileNamesSize);
            var fileNames = new Dictionary<uint, string>();
            for (int i = 0; i < fileNameIndices.Count; i++) { fileNames[(uint)i] = ReadNullTerminatedString(fileNameBlock, fileNameIndices[i].Offset); }
            foreach (var sec in sectionEntries) { project.Sections.Add(new SectionInfo { FileType = sec.FileType, Type2 = sec.Type2, Type3 = sec.Type3, Type4 = sec.Type4, Unk = sec.Unk }); }
            var decompressedSections = DecompressSections(reader, sectionEntries);
            var fileInfos = new List<Tuple<FileMapEntry, long>>();
            foreach (var map in fileMapEntries) { var firstPart = fileParts[(int)map.FirstPartIndex]; long sortKey = ((long)firstPart.SectionIndex << 32) | firstPart.RawOffset; fileInfos.Add(new Tuple<FileMapEntry, long>(map, sortKey)); }
            var sortedFileInfos = fileInfos.OrderBy(f => f.Item2).ToList();
            Console.WriteLine("Extracting files...");
            for (int i = 0; i < sortedFileInfos.Count; i++)
            {
                var map = sortedFileInfos[i].Item1;
                string internalName = fileNames[map.FileIndex];
                Console.WriteLine($"  [{i + 1}/{sortedFileInfos.Count}] -> {internalName}");

                var fileEntry = new FileEntry { OriginalIndex = map.FileIndex, RelativePath = internalName.Replace('\\', '/'), PartsCount = map.PartsCount, FileType = map.FileType, Unk1 = map.Unk1, Unk2 = map.Unk2, Parts = new List<FilePartInfo>() };

                string outputPath;

                if (map.FileType == 32 && map.PartsCount == 2)
                {
                    string baseName = Path.GetFileNameWithoutExtension(internalName);
                    string cleanExternalName = baseName + ".dds";
                    outputPath = Path.Combine(outputDirectory, cleanExternalName);
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                    var metaPart = fileParts[(int)map.FirstPartIndex]; var dataPart = fileParts[(int)map.FirstPartIndex + 1];
                    fileEntry.Parts.Add(new FilePartInfo { SectionIndex = metaPart.SectionIndex, Unk1 = metaPart.Unk1 });
                    fileEntry.Parts.Add(new FilePartInfo { SectionIndex = dataPart.SectionIndex, Unk1 = dataPart.Unk1 });
                    byte[] metaData = ReadPartData(reader, metaPart, sectionEntries, decompressedSections);
                    fileEntry.TextureHeader = metaData;
                    ushort width = BitConverter.ToUInt16(metaData, 64);
                    ushort height = BitConverter.ToUInt16(metaData, 66);
                    byte depth = metaData[68];
                    byte typeAndMips = metaData[71];
                    uint mipCount = (uint)(typeAndMips >> 2);
                    uint texType = (uint)(typeAndMips & 0x03);

                    string suffix = GetTextureSuffix(internalName);
                    string formatString;

                    if (suffix == "fallback")
                    {
                        byte gameFormat = metaData[70];
                        formatString = GetFormatFromLegacyGameFormat(gameFormat, texType);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.ResetColor();
                    }
                    else
                    {
                        formatString = _formatMap[suffix];
                    }

                    processedTexturesLog.Add(new Tuple<string, string>(cleanExternalName, formatString));
                    byte[] pixelData = ReadPartData(reader, dataPart, sectionEntries, decompressedSections);
                    byte[] ddsHeader = DdsHelper.GenerateDdsHeader(width, height, mipCount, formatString, texType, depth);

                    using (var fs = new MemoryStream())
                    {
                        fs.Write(ddsHeader, 0, ddsHeader.Length);
                        fs.Write(pixelData, 0, pixelData.Length);
                        File.WriteAllBytes(outputPath, fs.ToArray());
                    }
                }
                else
                {
                    outputPath = Path.Combine(outputDirectory, internalName);
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    for (int p = 0; p < map.PartsCount; p++)
                    {
                        var part = fileParts[(int)map.FirstPartIndex + p];
                        fileEntry.Parts.Add(new FilePartInfo { SectionIndex = part.SectionIndex, Unk1 = part.Unk1 });
                        byte[] partData = ReadPartData(reader, part, sectionEntries, decompressedSections);
                        string partOutputPath = map.PartsCount == 1 ? outputPath : Path.Combine(outputDirectory, $"{p}_{internalName}");
                        File.WriteAllBytes(partOutputPath, partData);
                    }
                }
                project.Files.Add(fileEntry);
            }
        }
        string jsonPath = Path.Combine(Path.GetDirectoryName(rpackPath), archiveName + "_repack.json");
        string jsonString = JsonConvert.SerializeObject(project, Formatting.Indented);
        File.WriteAllText(jsonPath, jsonString);
        Console.WriteLine($"\nCreated JSON Data for repacking: {jsonPath}");
        if (processedTexturesLog.Any())
        {
            string texTypesLogPath = Path.Combine(AppContext.BaseDirectory, $"{archiveName}_TexTypes.txt");
            var logBuilder = new StringBuilder();
            foreach (var entry in processedTexturesLog) { logBuilder.AppendLine($"{entry.Item1} ---> {entry.Item2}"); }
            File.WriteAllText(texTypesLogPath, logBuilder.ToString());
            Console.WriteLine($"Texture format log created: {texTypesLogPath}");
        }
        Console.WriteLine("\nAll Files Unpacked.");
    }

    private ArchiveHeader ReadHeader(byte[] headerData)
    {
        using (var r = new BinaryReader(new MemoryStream(headerData)))
        {
            return new ArchiveHeader
            {
                Signature = r.ReadUInt32(),
                Version = r.ReadUInt32(),
                C1 = r.ReadByte(),
                C2 = r.ReadByte(),
                C3 = r.ReadByte(),
                C4 = r.ReadByte(),
                PartsCount = r.ReadUInt32(),
                SectionsCount = r.ReadUInt32(),
                FilesCount = r.ReadUInt32(),
                FileNamesSize = r.ReadUInt32(),
                FNamesCount = r.ReadUInt32(),
                Alignment = r.ReadUInt32()
            };
        }
    }
    private List<SectionEntry> ReadSectionEntries(BinaryReader reader, uint count)
    {
        var l = new List<SectionEntry>();
        for (int i = 0; i < count; i++) l.Add(new SectionEntry
        {
            FileType = reader.ReadByte(),
            Type2 = reader.ReadByte(),
            Type3 = reader.ReadByte(),
            Type4 = reader.ReadByte(),
            RawOffset = reader.ReadUInt32(),
            UnpackedSize = reader.ReadUInt32(),
            PackedSize = reader.ReadUInt32(),
            ResourceCount = reader.ReadUInt16(),
            Unk = reader.ReadUInt16()
        });
        return l;
    }
    private List<FilePart> ReadFileParts(BinaryReader reader, uint count)
    {
        var l = new List<FilePart>();
        for (int i = 0; i < count; i++) l.Add(new FilePart
        {
            SectionIndex = reader.ReadByte(),
            Unk1 = reader.ReadByte(),
            FileIndex = reader.ReadUInt16(),
            RawOffset = reader.ReadUInt32(),
            Size = reader.ReadUInt32(),
            Unk2 = reader.ReadUInt32()
        });
        return l;
    }
    private List<FileMapEntry> ReadFileMapEntries(BinaryReader reader, uint count)
    {
        var l = new List<FileMapEntry>();
        for (int i = 0; i < count; i++) l.Add(new FileMapEntry
        {
            PartsCount = reader.ReadByte(),
            Unk1 = reader.ReadByte(),
            FileType = reader.ReadByte(),
            Unk2 = reader.ReadByte(),
            FileIndex = reader.ReadUInt32(),
            FirstPartIndex = reader.ReadUInt32()
        });
        return l;
    }
    private List<FileNameIndex> ReadFileNameIndices(BinaryReader reader, uint count)
    {
        var l = new List<FileNameIndex>();
        for (int i = 0; i < count; i++) l.Add(new FileNameIndex { Offset = reader.ReadUInt32() });
        return l;
    }
    private string ReadNullTerminatedString(byte[] buffer, uint offset)
    {
        int end = Array.IndexOf(buffer, (byte)0, (int)offset);
        if (end == -1) end = buffer.Length;
        return Encoding.UTF8.GetString(buffer, (int)offset, end - (int)offset);
    }
    private List<byte[]?> DecompressSections(BinaryReader reader, List<SectionEntry> sections)
    {
        var d = new List<byte[]?>();
        foreach (var s in sections)
        {
            if (s.PackedSize > 0)
            {
                reader.BaseStream.Seek(s.CalculatedOffset, SeekOrigin.Begin);
                byte[] c = reader.ReadBytes((int)s.PackedSize);
                if (c.Length > 2 && c[0] == 0x78)
                {
                    using (var iS = new MemoryStream(c)) using (var zS = new ZlibStream(iS, CompressionMode.Decompress)) using (var oS = new MemoryStream())
                    { zS.CopyTo(oS); d.Add(oS.ToArray()); }
                }
                else { d.Add(null); }
            }
            else { d.Add(null); }
        }
        return d;
    }
    private byte[] ReadPartData(BinaryReader reader, FilePart part, List<SectionEntry> sections, List<byte[]?> decompressedSections)
    {
        var sD = decompressedSections[part.SectionIndex];
        if (sD != null)
        {
            long o = (long)part.RawOffset << 4;
            byte[] pD = new byte[part.Size];
            Array.Copy(sD, o, pD, 0, part.Size);
            return pD;
        }
        else
        {
            long aO = sections[part.SectionIndex].CalculatedOffset + ((long)part.RawOffset << 4);
            reader.BaseStream.Seek(aO, SeekOrigin.Begin);
            return reader.ReadBytes((int)part.Size);
        }
    }
}