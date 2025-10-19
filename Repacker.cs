using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

public class ChunkedMemoryStream : Stream
{
    private readonly List<MemoryStream> _chunks = new List<MemoryStream>();
    private const long _maxChunkSize = 1_900_000_000;
    private long _position;
    private long _length;

    public ChunkedMemoryStream()
    {
        _chunks.Add(new MemoryStream());
        _position = 0;
        _length = 0;
    }

    public override bool CanRead => false;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("This stream is write-only.");
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (offset == 0 && origin == SeekOrigin.Begin)
        {
            _position = 0;
            return 0;
        }
        throw new NotSupportedException("Only seeking to the beginning is supported.");
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        int bytesRemaining = count;
        int currentOffset = offset;

        while (bytesRemaining > 0)
        {
            var currentChunk = _chunks[_chunks.Count - 1];
            long remainingInChunk = _maxChunkSize - currentChunk.Position;

            int bytesToWrite = (int)Math.Min(bytesRemaining, remainingInChunk);

            if (bytesToWrite > 0)
            {
                currentChunk.Write(buffer, currentOffset, bytesToWrite);
                _position += bytesToWrite;
                currentOffset += bytesToWrite;
                bytesRemaining -= bytesToWrite;
            }

            if (bytesRemaining > 0)
            {
                _chunks.Add(new MemoryStream());
            }
        }

        if (_position > _length)
        {
            _length = _position;
        }
    }
    public override void CopyTo(Stream destination, int bufferSize)
    {
        foreach (var chunk in _chunks)
        {
            chunk.Seek(0, SeekOrigin.Begin);
            chunk.CopyTo(destination, bufferSize);
        }
    }

    public void CopyTo(Stream destination)
    {
        CopyTo(destination, 81920);
    }


    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var chunk in _chunks)
            {
                chunk.Dispose();
            }
            _chunks.Clear();
        }
        base.Dispose(disposing);
    }
}


public class Repacker
{
    private class FileLogEntry
    {
        public string RelativePath { get; set; }
        public bool IsUpdated { get; set; }
        public string OldResolution { get; set; }
        public string NewResolution { get; set; }
    }

    public void Repack(string unpackDirectory)
    {
        string archiveName = new DirectoryInfo(unpackDirectory).Name.Replace("_unpack", "");
        string jsonPath = Path.Combine(AppContext.BaseDirectory, "jsondata", archiveName + "_repack.json");

        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"No JSON file found for repacking: {jsonPath}");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("Reading JSON Data:");
        Console.ResetColor();
        Console.WriteLine($" {Path.GetFileName(jsonPath)}");

        string jsonString = File.ReadAllText(jsonPath);
        RepackProject project = JsonConvert.DeserializeObject<RepackProject>(jsonString);

        ProcessRepack(unpackDirectory, project);
    }
    public void CombineAndRepack(string sourceDirectory)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Searching for JSON data...");
        Console.ResetColor();

        string jsonDataPath = Path.Combine(AppContext.BaseDirectory, "jsondata");
        if (!Directory.Exists(jsonDataPath))
            throw new DirectoryNotFoundException("'jsondata' directory couldn't found. Please unpack at least one .rpack file first.");

        var metadataPool = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
        RepackProject templateProject = null;

        foreach (var jsonFile in Directory.GetFiles(jsonDataPath, "*.json"))
        {
            var project = JsonConvert.DeserializeObject<RepackProject>(File.ReadAllText(jsonFile));
            if (project?.Files == null) continue;

            if (templateProject == null) templateProject = project;

            foreach (var fileEntry in project.Files)
            {
                string ddsFileName = Path.GetFileNameWithoutExtension(fileEntry.RelativePath) + ".dds";
                if (!metadataPool.ContainsKey(ddsFileName))
                {
                    metadataPool.Add(ddsFileName, fileEntry);
                }
            }
        }

        if (templateProject == null)
            throw new Exception("No valid JSON data found in the 'jsondata' directory.");

        Console.WriteLine($"Found data for {metadataPool.Count} files across all JSONs.");

        var filesInSourceDir = Directory.GetFiles(sourceDirectory, "*.dds", SearchOption.AllDirectories)
                                        .Select(path => Path.GetFileName(path))
                                        .ToList();

        if (filesInSourceDir.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nWarning: No .dds files found in the source directory. An empty .rpack file will be created.");
            Console.ResetColor();
        }

        var combinedProject = new RepackProject
        {
            RawHeader = templateProject.RawHeader,
            Sections = templateProject.Sections,
            Files = new List<FileEntry>()
        };

        foreach (var fileName in filesInSourceDir)
        {
            if (metadataPool.TryGetValue(fileName, out var entry))
            {
                combinedProject.Files.Add(entry);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Couldn't find data for '{fileName}'. This file will be skipped.");
                Console.ResetColor();
            }
        }

        Console.WriteLine($"\nFound {combinedProject.Files.Count} files.");

        ProcessRepack(sourceDirectory, combinedProject);
    }

    private void ProcessRepack(string unpackDirectory, RepackProject project)
    {
        Stream[] sectionStreams = null;

        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Repacking...");
            Console.ResetColor();

            string baseName = new DirectoryInfo(unpackDirectory).Name.EndsWith("_unpack")
                ? new DirectoryInfo(unpackDirectory).Name.Replace("_unpack", "")
                : new DirectoryInfo(unpackDirectory).Name;
            string outputRpackName = $"custom_{baseName}_pc.rpack";

            if (project == null || project.Files == null || project.Sections == null || project.RawHeader == null)
                throw new Exception("Data is invalid or doesn't contain required data.");

            var filesToInclude = new List<FileEntry>();
            foreach (var fileFromJson in project.Files)
            {
                string expectedFilePath;
                if (fileFromJson.FileType == 32)
                {
                    string baseFileName = Path.GetFileNameWithoutExtension(fileFromJson.RelativePath);
                    string cleanExternalName = baseFileName + ".dds";
                    expectedFilePath = Path.Combine(unpackDirectory, cleanExternalName.Replace('/', '\\'));
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
                Console.WriteLine("\nWarning: No files found in the target folder. An empty .rpack file will be created.");
                Console.ResetColor();
            }

            var fileLogs = new List<FileLogEntry>();
            int sectionsCount = project.Sections.Count;

            sectionStreams = new Stream[sectionsCount];
            for (int i = 0; i < sectionsCount; i++)
                sectionStreams[i] = new ChunkedMemoryStream();


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
                var logEntry = new FileLogEntry { RelativePath = currentFile.RelativePath };

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

                if (map.FileType == 32 && map.PartsCount == 2)
                {
                    var metaPartInfo = currentFile.Parts[0]; var dataPartInfo = currentFile.Parts[1];

                    string baseFileName = Path.GetFileNameWithoutExtension(currentFile.RelativePath);
                    string cleanExternalName = baseFileName + ".dds";
                    string ddsFilePath = Path.Combine(unpackDirectory, cleanExternalName.Replace('/', '\\'));
                    byte[] ddsData = File.ReadAllBytes(ddsFilePath);

                    byte[] headerData = currentFile.TextureHeader;
                    if (headerData == null) throw new Exception($"TextureHeader information for {currentFile.RelativePath} couldn't found in JSON Data.");

                    ushort newWidth = (ushort)BitConverter.ToUInt32(ddsData, 16);
                    ushort newHeight = (ushort)BitConverter.ToUInt32(ddsData, 12);
                    ushort originalWidth = BitConverter.ToUInt16(headerData, 64);
                    ushort originalHeight = BitConverter.ToUInt16(headerData, 66);

                    if (newWidth != originalWidth || newHeight != originalHeight)
                    {
                        logEntry.IsUpdated = true;
                        logEntry.OldResolution = $"{originalWidth}x{originalHeight}";
                        logEntry.NewResolution = $"{newWidth}x{newHeight}";
                        Array.Copy(BitConverter.GetBytes(newWidth), 0, headerData, 64, 2);
                        Array.Copy(BitConverter.GetBytes(newHeight), 0, headerData, 66, 2);
                    }

                    var metaPart = new FilePart { SectionIndex = metaPartInfo.SectionIndex, Unk1 = metaPartInfo.Unk1, FileIndex = (ushort)i, Unk2 = 0 };
                    var metaSectionStream = sectionStreams[metaPart.SectionIndex];
                    metaPart.RawOffset = (uint)(metaSectionStream.Position >> 4);
                    metaPart.Size = (uint)headerData.Length;
                    metaSectionStream.Write(headerData, 0, headerData.Length);
                    newFileParts.Add(metaPart);
                    resourceCounts[metaPart.SectionIndex]++;
                    long metaPartPadding = (16 - (metaSectionStream.Position % 16)) % 16;
                    metaSectionStream.Write(new byte[metaPartPadding], 0, (int)metaPartPadding);

                    var dataPart = new FilePart { SectionIndex = dataPartInfo.SectionIndex, Unk1 = dataPartInfo.Unk1, FileIndex = (ushort)i, Unk2 = 0 };
                    var dataSectionStream = sectionStreams[dataPart.SectionIndex];
                    dataPart.RawOffset = (uint)(dataSectionStream.Position >> 4);
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
                        long partPadding = (16 - (targetSectionStream.Position % 16)) % 16;
                        targetSectionStream.Write(new byte[partPadding], 0, (int)partPadding);
                    }
                }
                accumPartCount += map.PartsCount;
                fileLogs.Add(logEntry);
            }

            Console.WriteLine("\n\n");
            int boxWidth = 90;
            string title = $"{filesToProcess.Count} Files Found";

            int totalPadding = boxWidth - 2 - title.Length;
            int leftPadding = totalPadding / 2;
            int rightPadding = totalPadding - leftPadding;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("╔" + new string('═', boxWidth - 2) + "╗");
            Console.Write("║" + new string(' ', leftPadding));
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(title);
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(new string(' ', rightPadding) + "║");
            Console.WriteLine("╠" + new string('═', boxWidth - 2) + "╣");

            foreach (var log in fileLogs)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("╠ ");
                Console.ResetColor();

                string baseText = $" {log.RelativePath}";
                string updateText = "";
                if (log.IsUpdated)
                {
                    updateText = $" - Updated {log.OldResolution} to {log.NewResolution}";
                }

                Console.Write(baseText);
                if (log.IsUpdated)
                {
                    Console.Write(" - Updated ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write(log.OldResolution);
                    Console.ResetColor();
                    Console.Write(" to ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(log.NewResolution);
                    Console.ResetColor();
                }

                int currentLength = 2 + baseText.Length + updateText.Length;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(new string(' ', Math.Max(0, boxWidth - currentLength - 2)) + " ╣");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("╚" + new string('═', boxWidth - 2) + "╝");
            Console.ResetColor();

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
            foreach (var map in newFileMaps) { map.FileIndex = nameRemap[map.FileIndex]; }
            newFileMaps = newFileMaps.OrderBy(m => m.FileIndex).ToList();

            uint filesCount = (uint)filesToProcess.Count; uint partsCount = (uint)newFileParts.Count; uint fnames_size = (uint)namesStream.Length;
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
                var secInfo = project.Sections[i]; long unpackedSize = sectionStreams[i].Length;
                var entry = new SectionEntry { FileType = secInfo.FileType, Type2 = secInfo.Type2, Type3 = secInfo.Type3, Type4 = secInfo.Type4, Unk = secInfo.Unk, PackedSize = 0, UnpackedSize = (uint)unpackedSize, RawOffset = (uint)(currentDataOffset >> 4) };
                newSectionEntries.Add(entry);
                currentDataOffset += unpackedSize;
            }

            Console.WriteLine("\n\n");

            string outputRpackPath = Path.Combine(Path.GetDirectoryName(unpackDirectory), outputRpackName);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Creating new .rpack file: ");
            Console.ResetColor();
            Console.WriteLine(Path.GetFileName(outputRpackPath));

            using (var writer = new BinaryWriter(File.Create(outputRpackPath)))
            {
                writer.Write(finalHeader);
                for (int i = 0; i < newSectionEntries.Count; i++)
                {
                    var e = newSectionEntries[i];
                    writer.Write(e.FileType); writer.Write(e.Type2); writer.Write(e.Type3); writer.Write(e.Type4); writer.Write(e.RawOffset); writer.Write(e.UnpackedSize); writer.Write(e.PackedSize); writer.Write(resourceCounts[i]); writer.Write(e.Unk);
                }
                foreach (var p in newFileParts) { writer.Write(p.SectionIndex); writer.Write(p.Unk1); writer.Write(p.FileIndex); writer.Write(p.RawOffset); writer.Write(p.Size); writer.Write(p.Unk2); }
                foreach (var m in newFileMaps) { writer.Write(m.PartsCount); writer.Write(m.Unk1); writer.Write(m.FileType); writer.Write(m.Unk2); writer.Write(m.FileIndex); writer.Write(m.FirstPartIndex); }
                foreach (var ix in newFileNameIndices) { writer.Write(ix.Offset); }
                writer.Write(namesStream.ToArray());
                if (metaPadding > 0) writer.Write(new byte[metaPadding]);

                for (int i = 0; i < sectionsCount; i++)
                {
                    sectionStreams[i].Seek(0, SeekOrigin.Begin);
                    sectionStreams[i].CopyTo(writer.BaseStream);
                }
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("All Files Repacked.");
            Console.ResetColor();
        }
        finally
        {
            if (sectionStreams != null)
            {
                foreach (var stream in sectionStreams)
                {
                    stream?.Close();
                    stream?.Dispose();
                }
            }
        }
    }
}