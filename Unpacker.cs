using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ionic.Zlib;
using Newtonsoft.Json;

public class Unpacker
{
    public void Unpack(string rpackPath)
    {
        Console.WriteLine("Unpacking files...");
        string archiveName = Path.GetFileNameWithoutExtension(rpackPath);
        string outputDirectory = Path.Combine(Path.GetDirectoryName(rpackPath), archiveName + "_unpack");
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, true);
        }
        Directory.CreateDirectory(outputDirectory);
        Console.WriteLine($"Files will be extracted to {outputDirectory}.");

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
            for (int i = 0; i < fileNameIndices.Count; i++)
            {
                fileNames[(uint)i] = ReadNullTerminatedString(fileNameBlock, fileNameIndices[i].Offset);
            }

            foreach (var sec in sectionEntries)
            {
                project.Sections.Add(new SectionInfo { FileType = sec.FileType, Type2 = sec.Type2, Type3 = sec.Type3, Type4 = sec.Type4, Unk = sec.Unk });
            }

            var decompressedSections = DecompressSections(reader, sectionEntries);

            var fileInfos = new List<Tuple<FileMapEntry, long>>();
            foreach (var map in fileMapEntries)
            {
                var firstPart = fileParts[(int)map.FirstPartIndex];
                long sortKey = ((long)firstPart.SectionIndex << 32) | firstPart.RawOffset;
                fileInfos.Add(new Tuple<FileMapEntry, long>(map, sortKey));
            }

            var sortedFileInfos = fileInfos.OrderBy(f => f.Item2).ToList();

            for (int i = 0; i < sortedFileInfos.Count; i++)
            {
                var map = sortedFileInfos[i].Item1;
                string fileName = fileNames[map.FileIndex];
                Console.WriteLine($"  [{i + 1}/{sortedFileInfos.Count}] -> {fileName}");

                var fileEntry = new FileEntry
                {
                    OriginalIndex = map.FileIndex,
                    RelativePath = fileName.Replace('\\', '/'),
                    PartsCount = map.PartsCount,
                    FileType = map.FileType,
                    Unk1 = map.Unk1,
                    Unk2 = map.Unk2,
                    Parts = new List<FilePartInfo>()
                };

                string outputPath = Path.Combine(outputDirectory, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                if (map.FileType == 32)
                {
                    if (map.PartsCount == 2)
                    {
                        var metaPart = fileParts[(int)map.FirstPartIndex];
                        var dataPart = fileParts[(int)map.FirstPartIndex + 1];
                        fileEntry.Parts.Add(new FilePartInfo { SectionIndex = metaPart.SectionIndex, Unk1 = metaPart.Unk1 });
                        fileEntry.Parts.Add(new FilePartInfo { SectionIndex = dataPart.SectionIndex, Unk1 = dataPart.Unk1 });
                        byte[] metaData = ReadPartData(reader, metaPart, sectionEntries, decompressedSections);
                        fileEntry.TextureHeader = metaData;
                        ushort width, height; byte depth, format, typeAndMips;
                        using (var metaReader = new BinaryReader(new MemoryStream(metaData))) { metaReader.BaseStream.Seek(64, SeekOrigin.Begin); width = metaReader.ReadUInt16(); height = metaReader.ReadUInt16(); depth = metaReader.ReadByte(); metaReader.ReadByte(); format = metaReader.ReadByte(); typeAndMips = metaReader.ReadByte(); }
                        uint mipCount = (uint)(typeAndMips >> 2); uint texType = (uint)(typeAndMips & 0x03);
                        byte[] pixelData = ReadPartData(reader, dataPart, sectionEntries, decompressedSections);
                        byte[] ddsHeader = DdsHelper.GenerateDdsHeader(width, height, mipCount, format, texType, depth);
                        using (var fs = new MemoryStream()) { fs.Write(ddsHeader, 0, ddsHeader.Length); fs.Write(pixelData, 0, pixelData.Length); File.WriteAllBytes(outputPath + ".dds", fs.ToArray()); }
                    }
                }
                else
                {
                    for (int p = 0; p < map.PartsCount; p++)
                    {
                        var part = fileParts[(int)map.FirstPartIndex + p];
                        fileEntry.Parts.Add(new FilePartInfo { SectionIndex = part.SectionIndex, Unk1 = part.Unk1 });
                        byte[] partData = ReadPartData(reader, part, sectionEntries, decompressedSections);
                        string partOutputPath = map.PartsCount == 1 ? outputPath : Path.Combine(outputDirectory, $"{p}_{fileName}");
                        File.WriteAllBytes(partOutputPath, partData);
                    }
                }
                project.Files.Add(fileEntry);
            }
        }

        string jsonPath = Path.Combine(Path.GetDirectoryName(rpackPath), archiveName + "_repack.json");
        string jsonString = JsonConvert.SerializeObject(project, Formatting.Indented);
        File.WriteAllText(jsonPath, jsonString);
        Console.WriteLine($"Created JSON Data for repacking: {jsonPath}");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nAll files unpacked.");
        Console.ResetColor();
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
        for (int i = 0; i < count; i++) l.Add(new FileNameIndex
        {
            Offset = reader.ReadUInt32()
        });
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
                    {
                        zS.CopyTo(oS);
                        d.Add(oS.ToArray());
                    }
                }
                else
                {
                    d.Add(null);
                }
            }
            else
            {
                d.Add(null);
            }
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