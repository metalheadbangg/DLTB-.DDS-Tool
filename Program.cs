using System;
using System.IO;

public class Program
{
    public static void Main(string[] args)
    {
        Console.Title = "DLTB .DDS Tool";

        if (args.Length == 0)
        {
            Console.WriteLine("DLTB .DDS Tool by MetalHeadbangg a.k.a @unsc.odst");
            Console.WriteLine("Usage:");
            Console.WriteLine("  - Drag one or more .rpack files onto the .exe file to unpacking.");
            Console.WriteLine("  - Drag a folder with .dds files for repacking.");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            return;
        }
        string firstInputPath = args[0];

        try
        {
            if (File.Exists(firstInputPath) && Path.GetExtension(firstInputPath).Equals(".rpack", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Found {args.Length} .rpack files.");
                Console.ResetColor();
                int unpackedCount = 0;

                for (int i = 0; i < args.Length; i++)
                {
                    string currentPath = args[i];
                    Console.WriteLine(new string('=', 70));

                    if (File.Exists(currentPath) && Path.GetExtension(currentPath).Equals(".rpack", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write($"Processing file [{i + 1}/{args.Length}]:");
                        Console.ResetColor();
                        Console.WriteLine($" {Path.GetFileName(currentPath)}\n");

                        new Unpacker().Unpack(currentPath);
                        unpackedCount++;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Skipping non-.rpack file [{i + 1}/{args.Length}]: {Path.GetFileName(currentPath)}");
                        Console.ResetColor();
                    }
                }

                Console.WriteLine(new string('=', 70));
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\nUnpack complete. {unpackedCount} file(s) unpacked.");
                Console.ResetColor();
            }
            else if (Directory.Exists(firstInputPath))
            {
                string directoryName = new DirectoryInfo(firstInputPath).Name;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Found Directory:");
                Console.ResetColor();
                Console.WriteLine($" {directoryName}\n");

                string expectedJsonName = directoryName.Replace("_unpack", "") + "_repack.json";
                string jsonPath = Path.Combine(AppContext.BaseDirectory, "jsondata", expectedJsonName);

                if (File.Exists(jsonPath) && directoryName.EndsWith("_unpack", StringComparison.OrdinalIgnoreCase))
                {
                    new Repacker().Repack(firstInputPath);
                }
                else
                {
                    new Repacker().CombineAndRepack(firstInputPath);
                }
            }
            else
            {
                Console.WriteLine("Error: Please drag a valid .rpack file (for unpacking) or a folder (for repacking).");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nAN UNEXPECTED ERROR HAS OCCURRED:");
            Console.WriteLine(ex.ToString());
            Console.ResetColor();
        }

        Console.WriteLine("\nPress any key to exit.");
        Console.ReadKey();
    }
}