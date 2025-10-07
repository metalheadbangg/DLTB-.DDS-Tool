using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

public class Repacker
{
    public void Repack(string unpackDirectory)
    {
        Console.WriteLine("Repacking...");

        string archiveName = new DirectoryInfo(unpackDirectory).Name.Replace("_unpack", "");
        string outputRpackName = $"custom_{archiveName}_pc.rpack"; // For Synsteric's Loader
        string jsonPath = Path.Combine(Path.GetDirectoryName(unpackDirectory), archiveName + "_repack.json");

        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"No JSON file found for repacking: {jsonPath}");

        Console.WriteLine($"Reading JSON Data: {Path.GetFileName(jsonPath)}");
        string jsonString = File.ReadAllText(jsonPath);
        RepackProject project = JsonConvert.DeserializeObject<RepackProject>(jsonString);

        if (project == null || project.Files == null || project.Sections == null || project.RawHeader == null)
            throw new Exception("JSON file is invalid or doesn't contain required data.");

        var filesToInclude = new List<FileEntry>();
        foreach (var fileFromJson in project.Files)
        {
            string expectedFilePath;
            if (fileFromJson.FileType == 32) // for texture, maybe we can add more file types in future
            {
                expectedFilePath = Path.Combine(unpackDirectory, fileFromJson.RelativePath.Replace('/', '\\') + ".dds");
            }
            else
            {
                string partFileName = fileFromJson.PartsCount == 1 ? fileFromJson.RelativePath : $"0_{fileFromJson.RelativePath}";
                expectedFilePath = Path.Combine(unpackDirectory, partFileName.Replace('/', '\\'));
            }

            if (File.Exists(expectedFilePath))
            {
                filesToInclude.Add(fileFromJson);
            }
        }

        if (filesToInclude.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nWarning: No modified files listed in the JSON file were found in the _unpack folder. An empty .rpack file will be created.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"{filesToInclude.Count} files found to be repacked.");
            Console.ResetColor();
        }


        int sectionsCount = project.Sections.Count;
        var sectionStreams = new MemoryStream[sectionsCount];
        for (int i = 0; i < sectionsCount; i++)
            sectionStreams[i] = new MemoryStream();

        var resourceCounts = new ushort[sectionsCount];
        var newFileParts = new List<FilePart>();
        var newFileMaps = new List<FileMapEntry>();

        var namesStream = new MemoryStream();
        uint accumPartCount = 0;

        var filesToProcess = filesToInclude;
        var fileNamesPhysicallyOrdered = filesToProcess.Select(f => f.RelativePath).ToList();

        for (int i = 0; i < filesToProcess.Count; i++)
        {
            var currentFile = filesToProcess[i];
            var map = new FileMapEntry
            {
                FileIndex = (uint)i,
                FirstPartIndex = accumPartCount,
                PartsCount = currentFile.PartsCount,
                FileType = currentFile.FileType,
                Unk1 = currentFile.Unk1,
                Unk2 = currentFile.Unk2
            };
            newFileMaps.Add(map);

            if (map.FileType == 32)
            {
                var metaPartInfo = currentFile.Parts[0];
                var dataPartInfo = currentFile.Parts[1];

                var metaPart = new FilePart { SectionIndex = metaPartInfo.SectionIndex, Unk1 = metaPartInfo.Unk1, FileIndex = (ushort)i, Unk2 = 0 };
                var metaSectionStream = sectionStreams[metaPart.SectionIndex];
                metaPart.RawOffset = (uint)(metaSectionStream.Position >> 4);
                byte[] headerData = currentFile.TextureHeader;
                if (headerData == null) throw new Exception($"TextureHeader information for {currentFile.RelativePath} couldn't found in JSON Data.");
                metaPart.Size = (uint)headerData.Length;
                metaSectionStream.Write(headerData, 0, headerData.Length);
                newFileParts.Add(metaPart);
                resourceCounts[metaPart.SectionIndex]++;
                long metaPartPadding = (16 - (metaSectionStream.Position % 16)) % 16;
                metaSectionStream.Write(new byte[metaPartPadding], 0, (int)metaPartPadding);

                var dataPart = new FilePart { SectionIndex = dataPartInfo.SectionIndex, Unk1 = dataPartInfo.Unk1, FileIndex = (ushort)i, Unk2 = 0 };
                var dataSectionStream = sectionStreams[dataPart.SectionIndex];
                dataPart.RawOffset = (uint)(dataSectionStream.Position >> 4);
                string ddsFilePath = Path.Combine(unpackDirectory, currentFile.RelativePath.Replace('/', '\\') + ".dds");
                byte[] ddsData = File.ReadAllBytes(ddsFilePath);
                uint ddsHeaderSize = (ddsData.Length > 84 && BitConverter.ToUInt32(ddsData, 84) == 0x30315844) ? 148u : 128u;
                byte[] pixelData = ddsData.Skip((int)ddsHeaderSize).ToArray();
                dataPart.Size = (uint)pixelData.Length;
                dataSectionStream.Write(pixelData, 0, pixelData.Length);
                newFileParts.Add(dataPart);
                resourceCounts[dataPart.SectionIndex]++;
                long dataPadding = (16 - (dataSectionStream.Position % 16)) % 16;
                dataSectionStream.Write(new byte[dataPadding], 0, (int)dataPadding);
            }
            else
            {
                for (int p = 0; p < map.PartsCount; p++)
                {
                    var partInfo = currentFile.Parts[p];
                    var part = new FilePart { SectionIndex = partInfo.SectionIndex, Unk1 = partInfo.Unk1, FileIndex = (ushort)i, Unk2 = 0 };
                    var targetSectionStream = sectionStreams[part.SectionIndex];
                    part.RawOffset = (uint)(targetSectionStream.Position >> 4);
                    string partFileName = map.PartsCount == 1 ? currentFile.RelativePath : $"{p}_{currentFile.RelativePath}";
                    byte[] fileData = File.ReadAllBytes(Path.Combine(unpackDirectory, partFileName.Replace('/', '\\')));
                    part.Size = (uint)fileData.Length;
                    targetSectionStream.Write(fileData, 0, fileData.Length);
                    newFileParts.Add(part);
                    resourceCounts[part.SectionIndex]++;
                    long padding = (16 - (targetSectionStream.Position % 16)) % 16;
                    targetSectionStream.Write(new byte[padding], 0, (int)padding);
                }
            }
            accumPartCount += map.PartsCount;
        }

        var fileNamesAlphabetical = fileNamesPhysicallyOrdered.OrderBy(name => name).ToList();
        var newFileNameIndices = new List<FileNameIndex>();
        var nameRemap = new Dictionary<uint, uint>();

        foreach (string name in fileNamesAlphabetical)
        {
            newFileNameIndices.Add(new FileNameIndex { Offset = (uint)namesStream.Position });
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            namesStream.Write(nameBytes, 0, nameBytes.Length);
            namesStream.WriteByte(0);

            uint oldIndex = (uint)fileNamesPhysicallyOrdered.IndexOf(name);
            uint newIndex = (uint)fileNamesAlphabetical.IndexOf(name);
            nameRemap[oldIndex] = newIndex;
        }

        foreach (var map in newFileMaps)
        {
            map.FileIndex = nameRemap[map.FileIndex];
        }

        newFileMaps = newFileMaps.OrderBy(m => m.FileIndex).ToList();

        uint filesCount = (uint)filesToProcess.Count;
        uint partsCount = (uint)newFileParts.Count;
        uint fnames_size = (uint)namesStream.Length;

        byte[] finalHeader = new byte[36];
        project.RawHeader.CopyTo(finalHeader, 0);
        using (var ms = new MemoryStream(finalHeader))
        using (var writer = new BinaryWriter(ms))
        {
            writer.BaseStream.Seek(12, SeekOrigin.Begin); writer.Write(partsCount);
            writer.BaseStream.Seek(20, SeekOrigin.Begin); writer.Write(filesCount);
            writer.Write(fnames_size); writer.Write((uint)newFileNameIndices.Count);
        }

        long totalMetadataSize = 36 + (sectionsCount * 20) + (newFileParts.Count * 16) + (newFileMaps.Count * 12) + (newFileNameIndices.Count * 4) + namesStream.Length;
        long dataStartOffset = totalMetadataSize;
        long metaPadding = (4096 - (dataStartOffset % 4096)) % 4096;
        dataStartOffset += metaPadding;

        var newSectionEntries = new List<SectionEntry>();
        long currentDataOffset = dataStartOffset;
        for (int i = 0; i < sectionsCount; i++)
        {
            var secInfo = project.Sections[i];
            long unpackedSize = sectionStreams[i].Length;
            var entry = new SectionEntry { FileType = secInfo.FileType, Type2 = secInfo.Type2, Type3 = secInfo.Type3, Type4 = secInfo.Type4, Unk = secInfo.Unk, PackedSize = 0, UnpackedSize = (uint)unpackedSize, RawOffset = (uint)(currentDataOffset >> 4) };
            newSectionEntries.Add(entry);
            currentDataOffset += unpackedSize;
        }

        string outputRpackPath = Path.Combine(Path.GetDirectoryName(unpackDirectory), outputRpackName);
        Console.WriteLine($"Creating new .rpack file: {outputRpackPath}");
        using (var writer = new BinaryWriter(File.Create(outputRpackPath)))
        {
            writer.Write(finalHeader);
            for (int i = 0; i < newSectionEntries.Count; i++) { var e = newSectionEntries[i]; writer.Write(e.FileType); writer.Write(e.Type2); writer.Write(e.Type3); writer.Write(e.Type4); writer.Write(e.RawOffset); writer.Write(e.UnpackedSize); writer.Write(e.PackedSize); writer.Write(resourceCounts[i]); writer.Write(e.Unk); }
            foreach (var p in newFileParts) { writer.Write(p.SectionIndex); writer.Write(p.Unk1); writer.Write(p.FileIndex); writer.Write(p.RawOffset); writer.Write(p.Size); writer.Write(p.Unk2); }
            foreach (var m in newFileMaps) { writer.Write(m.PartsCount); writer.Write(m.Unk1); writer.Write(m.FileType); writer.Write(m.Unk2); writer.Write(m.FileIndex); writer.Write(m.FirstPartIndex); }
            foreach (var ix in newFileNameIndices) { writer.Write(ix.Offset); }
            writer.Write(namesStream.ToArray());
            if (metaPadding > 0) writer.Write(new byte[metaPadding]);
            for (int i = 0; i < sectionsCount; i++) { writer.Write(sectionStreams[i].ToArray()); }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nAll files repacked.");
        Console.ResetColor();
    }
}