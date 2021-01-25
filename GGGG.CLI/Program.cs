using System;
using System.IO;

namespace GGGG.CLI
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 4)
            {
                var codes = args[0].Split('+');
                var mode = 0;
                var inputFile = args[2];
                var outputFile = args[3];

                if (!Int32.TryParse(args[1], out mode))
                {
                    Console.WriteLine($"Invalid mode [1-5]");

                    return 2;
                }

                if (mode < 1 || mode > 5)
                {
                    Console.WriteLine($"Invalid mode [1-5], {mode} given");

                    return 3;
                }

                if (inputFile == outputFile)
                {
                    Console.WriteLine("Input and output file can't be the same");

                    return 4;
                }

                var patcher = new Patcher();
                patcher.OnLog += Console.WriteLine;

                using (var inputStream = File.OpenRead(inputFile))
                using (var outputStream = File.Create(outputFile))
                {
                    inputStream.CopyTo(outputStream);
                    inputStream.Close();

                    outputStream.Seek(0, SeekOrigin.Begin);
                    patcher.Patch(outputStream, (Patcher.RomType)mode, codes);
                }

                return 0;
            }
            else
            {
                Console.WriteLine("Usage:");
                Console.WriteLine($"  {Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName)} \"[Codes, combine with +]\" [mode] [originalrom] [newrom]");
                Console.WriteLine("");
                Console.WriteLine("Valid modes:");
                Console.WriteLine("  1: Game boy / Game Gear / Master System");
                Console.WriteLine("  2: Genesis / Mega Drive (No SMD Roms)");
                Console.WriteLine("  3: Nintendo");
                Console.WriteLine("  4: Super Nintendo");
                Console.WriteLine("  5: PC Engine");
            }
            return 1;
        }
    }
}
