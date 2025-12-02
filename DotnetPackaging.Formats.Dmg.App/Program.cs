using System;
using System.IO;
using System.Linq;
using DotnetPackaging.Formats.Dmg.Iso;
using DotnetPackaging.Formats.Dmg.MachO;
using DotnetPackaging.Formats.Dmg.Udif;
using Zafiro.DivineBytes;
using Path = System.IO.Path;

namespace DotnetPackaging.Formats.Dmg.App
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Dotnet.Dmg <input-directory> <output-dmg> [app-name]");
                return;
            }

            string inputDir = Path.GetFullPath(args[0]);
            string outputDmg = Path.GetFullPath(args[1]);
            string appName = args.Length > 2 ? args[2] : new DirectoryInfo(inputDir).Name;
            if (!appName.EndsWith(".app")) appName += ".app";

            Console.WriteLine($"Packaging {inputDir} to {outputDmg}...");

            try
            {
                // 1. Prepare Layout
                // We need to create a temporary directory structure for the ISO
                // Root
                //   - .DS_Store
                //   - .background/
                //   - Applications (Symlink)
                //   - MyApp.app/
                //       - Contents/
                //           - MacOS/
                //           - Resources/
                //           - Info.plist
                //           - PkgInfo

                // Since we are building ISO in-memory (IsoBuilder), we don't need to copy files to disk!
                // We just map them in IsoBuilder.

                var builder = new IsoBuilder(appName.Replace(".app", "").ToUpper());

                // Add Applications symlink
                builder.Root.AddChild(new IsoSymlink("Applications", "/Applications"));

                // Add .app directory
                var appDir = new IsoDirectory(appName);
                builder.Root.AddChild(appDir);

                var contentsDir = new IsoDirectory("Contents");
                appDir.AddChild(contentsDir);

                var macOsDir = new IsoDirectory("MacOS");
                contentsDir.AddChild(macOsDir);

                var resourcesDir = new IsoDirectory("Resources");
                contentsDir.AddChild(resourcesDir);

                // Add files from inputDir to MacOS
                // We assume inputDir contains the published output (single file or self-contained)
                // We need to identify the main executable.
                // Usually it's the file with the same name as the project, without extension.

                var files = Directory.GetFiles(inputDir, "*", SearchOption.AllDirectories);
                string mainExe = null;

                foreach (var file in files)
                {
                    string relPath = Path.GetRelativePath(inputDir, file);
                    string fileName = Path.GetFileName(file);

                    // Simple heuristic for main exe: no extension, executable permission?
                    // Or matches app name?
                    // Let's assume the user provides a clean publish folder.
                    // We copy everything to MacOS folder.
                    // Resources should go to Resources? .NET publish puts everything in one folder usually.
                    // Standard .NET macOS bundle structure:
                    // Contents/MacOS/ <dlls, dylibs, exe>
                    // Contents/Resources/ <icons>

                    // We'll put everything in MacOS for now, except .icns

                    if (fileName.EndsWith(".icns"))
                    {
                        resourcesDir.AddChild(new IsoFile(fileName) { ContentSource = () => ByteSource.FromStreamFactory(() => File.OpenRead(file)) });
                    }
                    else
                    {
                        var isoFile = new IsoFile(fileName) { ContentSource = () => ByteSource.FromStreamFactory(() => File.OpenRead(file)) };
                        // Check if executable (no extension or specific extensions)
                        if (!fileName.Contains("."))
                        {
                            isoFile.Mode = 0x81ED; // 755
                            mainExe = fileName;

                            // Sign it!
                            // We need to sign in-place or copy to temp?
                            // CodeSigner modifies file in-place.
                            // We should copy to temp first.
                            string tempFile = Path.GetTempFileName();
                            File.Copy(file, tempFile, true);

                            Console.WriteLine($"Signing {fileName}...");
                            try
                            {
                                new CodeSigner().Sign(tempFile, "com.example." + appName);
                                isoFile.ContentSource = () => ByteSource.FromStreamFactory(() => File.OpenRead(tempFile));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Warning: Failed to sign {fileName}: {ex.Message}. Using original file.");
                                isoFile.ContentSource = () => ByteSource.FromStreamFactory(() => File.OpenRead(file));
                            }
                        }
                        else if (fileName.EndsWith(".dylib"))
                        {
                            // Sign dylibs too?
                            // Yes, usually.
                            string tempFile = Path.GetTempFileName();
                            File.Copy(file, tempFile, true);
                            try
                            {
                                new CodeSigner().Sign(tempFile, "com.example." + appName);
                                isoFile.ContentSource = () => ByteSource.FromStreamFactory(() => File.OpenRead(tempFile));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Warning: Failed to sign {fileName}: {ex.Message}. Using original file.");
                                isoFile.ContentSource = () => ByteSource.FromStreamFactory(() => File.OpenRead(file));
                            }
                        }

                        macOsDir.AddChild(isoFile);
                    }
                }

                // Generate Info.plist
                string plist = GenerateInfoPlist(appName.Replace(".app", ""), mainExe ?? "App");
                var plistFile = new IsoFile("Info.plist")
                {
                    ContentSource = () => ByteSource.FromBytes(System.Text.Encoding.UTF8.GetBytes(plist))
                };
                contentsDir.AddChild(plistFile);

                // Generate PkgInfo
                var pkgInfoFile = new IsoFile("PkgInfo")
                {
                    ContentSource = () => ByteSource.FromBytes(System.Text.Encoding.ASCII.GetBytes("APPL????"))
                };
                contentsDir.AddChild(pkgInfoFile);

                // 2. Build ISO
                Console.WriteLine("Building ISO...");
                using (var isoStream = new MemoryStream())
                {
                    builder.Build(isoStream);
                    isoStream.Position = 0;

                    // 3. Create DMG with bzip2 compression
                    Console.WriteLine("Creating DMG (bzip2 compression)...");
                    using (var dmgStream = File.Create(outputDmg))
                    {
                        var writer = new UdifWriter { CompressionType = CompressionType.Bzip2 };
                        writer.Create(isoStream, dmgStream);
                    }
                }

                Console.WriteLine("Done!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static string GenerateInfoPlist(string name, string exe)
        {
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>CFBundleName</key>
    <string>{name}</string>
    <key>CFBundleDisplayName</key>
    <string>{name}</string>
    <key>CFBundleIdentifier</key>
    <string>com.example.{name.ToLower()}</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleExecutable</key>
    <string>{exe}</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.12</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>";
        }
    }
}
