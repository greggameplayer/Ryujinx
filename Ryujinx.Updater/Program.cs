using ICSharpCode.SharpZipLib.Tar;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;

namespace Ryujinx.Updater
{
    class Program
    {
        public static string localAppPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ryujinx");
        public static string ryuDir = Environment.CurrentDirectory;

        public static string updateSaveLocation;

        public static int lastPercentage;

        private static void MoveAllFilesOver(string root, string dest)
        {
            foreach (var directory in Directory.GetDirectories(root))
            {
                string dirName = Path.GetFileName(directory);

                try
                {
                    if (!Directory.Exists(Path.Combine(dest, dirName)))
                    {
                        Directory.CreateDirectory(Path.Combine(dest, dirName));
                    }
                }
                catch (Exception ex)
                {
                    File.Create(Path.Combine(ryuDir, "UpdaterLog.txt")).Close();
                    File.WriteAllText(Path.Combine(ryuDir, "UpdaterLog.txt"), ex.Message);
                    Environment.Exit(0);
                }

                MoveAllFilesOver(directory, Path.Combine(dest, dirName));
            }

            foreach (var file in Directory.GetFiles(root))
            {
                try
                {
                    File.Move(file, Path.Combine(dest, Path.GetFileName(file)), true);
                }
                catch (Exception ex)
                {
                    File.Create(Path.Combine(ryuDir, "UpdaterLog.txt")).Close();
                    File.WriteAllText(Path.Combine(ryuDir, "UpdaterLog.txt"), ex.Message);
                    Environment.Exit(0);
                }
            }
        }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                return;
            }

            Console.WriteLine($"Updating Ryujinx...");

            // Create temp directory

            if (!Directory.Exists(Path.Combine(localAppPath, "Temp")))
            {
                Directory.CreateDirectory(Path.Combine(localAppPath, "Temp"));
            }

            // Download latest update

            string downloadUrl = args[0];

            updateSaveLocation = Path.Combine(localAppPath, "Temp", "RyujinxPackage.zip");

            Console.WriteLine($"Downloading latest Ryujinx package...");

            WebClient client = new WebClient();

            client.DownloadProgressChanged += (s, e) =>
            {
                if (e.ProgressPercentage != lastPercentage)
                {
                    Console.WriteLine("Package downloading... " + e.ProgressPercentage + "%");
                }

                lastPercentage = e.ProgressPercentage;
            };

            client.DownloadFileTaskAsync(new Uri(downloadUrl), updateSaveLocation).Wait();

            // Extract Update .zip

            Console.WriteLine($"Extracting Ryujinx package...");

            using (FileStream SourceStream = File.Open(updateSaveLocation, FileMode.Open))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    TarArchive tarArchive = TarArchive.CreateInputTarArchive(SourceStream);
                    tarArchive.ExtractContents(localAppPath);
                }
                else
                {
                    ZipArchive zipArchive = new ZipArchive(SourceStream);
                    zipArchive.ExtractToDirectory(localAppPath);
                }
            }

            // Copy new files over to Ryujinx folder

            Console.WriteLine($"Replacing old version...");

            MoveAllFilesOver(Path.Combine(localAppPath, "publish"), ryuDir);

            // Remove temp folders

            Directory.Delete(Path.Combine(localAppPath, "publish"), true);
            Directory.Delete(Path.Combine(localAppPath, "Temp"), true);

            // Start new Ryujinx version and close Updater

            ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(ryuDir, "Ryujinx.exe"));
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
        }
    }
}