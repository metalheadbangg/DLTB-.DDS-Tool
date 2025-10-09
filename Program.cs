using System;
using System.IO;

public class Program
{
    public static void Main(string[] args)
    {
        Console.Title = "DLTB .DDS Tool";

        if (args.Length == 0)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("DLTB .DDS Tool by MetalHeadbangg a.k.a @unsc.odst");
            Console.WriteLine("  - Drag a .rpack file onto an .exe file to extract the files.");
            Console.WriteLine("  - Drag an _unpack folder onto the .exe file to repack the archive.");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            return;
        }

        string inputPath = args[0];

        try
        {
            if (File.Exists(inputPath) && Path.GetExtension(inputPath).Equals(".rpack", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($" Found: {Path.GetFileName(inputPath)}");
                new Unpacker().Unpack(inputPath);
            }
            else if (Directory.Exists(inputPath) && new DirectoryInfo(inputPath).Name.EndsWith("_unpack", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($" Found: {new DirectoryInfo(inputPath).Name}");
                new Repacker().Repack(inputPath);
            }
            else
            {
                Console.WriteLine("Error: Please drag a valid .rpack file or a folder with the _unpack extension.");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nAN UNEXPECTED ERROR HAS OCCURRED:");
            Console.WriteLine(ex.ToString());
            Console.ResetColor();
        }

        Console.WriteLine("\nComplete. Press any key to exit.");
        Console.ReadKey();
    }
}