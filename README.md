# Dotnet.Dmg

Pure .NET tooling to craft macOS DMG images without external macOS tooling.

## Highlights
- Build ISO9660 layouts with Rock Ridge entries in memory and wrap them into UDIF DMGs
- **Dual compression support**: UDBZ (bzip2) and UDZO (zlib) - 100% managed code via SharpCompress
- Minimal Mach-O ad-hoc signer to stamp executables and dylibs during packaging
- CLI helper to turn a published folder into a `.app` bundle inside a DMG, including `Info.plist` and `PkgInfo`
- Targets .NET 10; no native dependencies or platform-specific tooling required
- **Structurally compatible** with industry-standard DMGs (verified against Parcel output)

## Quick start
1) Publish your app for macOS:
   ```bash
   dotnet publish -c Release -r osx-arm64 -p:PublishSingleFile=true -p:SelfContained=true -o ./publish
   ```
2) Build the DMG (app name is optional; defaults to the input folder name with `.app` appended):
   ```bash
   dotnet run --project Dotnet.Dmg.App -- ./publish ./MyApp.dmg MyGreatApp
   ```
   The tool uses **bzip2 compression by default** for optimal file size.

3) Mount `MyApp.dmg` on macOS to verify the bundle layout and ad-hoc signatures.

## Projects
- `Dotnet.Dmg.App`: Console entry point that wires the builders together and emits the final DMG from a publish folder.
- `Dotnet.Dmg.Iso`: ISO9660 + Rock Ridge builder with directory, file, and symlink support.
- `Dotnet.Dmg.Udif`: UDIF writer that produces compressed DMG streams from an ISO image.
- `Dotnet.Dmg.MachO`: Lightweight Mach-O parser and ad-hoc code signer used by the CLI.
- `Dotnet.Dmg.Tests`: xUnit test suite covering the core libraries.

## NuGet packaging
- Shared package metadata (authors, license, repo URL, tags, README) lives in `Directory.Build.props` and is picked up by every packable project.
- The root `README.md` is packed into the generated `.nupkg` files via `PackageReadmeFile`.
- Build packages locally with `dotnet pack -c Release`; `Dotnet.Dmg.Tests` is excluded via `IsPackable=false`.

## Features
### Compression
- **UDBZ (bzip2)**: Default compression, better ratios (~4% smaller than zlib)
- **UDZO (zlib)**: Alternative compression, faster but larger output
- Both implemented as 100% managed code (no P/Invoke or native dependencies)

### Programmatic Usage
```csharp
using Dotnet.Dmg.Udif;

var writer = new UdifWriter 
{ 
    CompressionType = CompressionType.Bzip2  // or CompressionType.Zlib
};
writer.Create(isoStream, dmgStream);
```

## Known gaps
- Large file support is limited by the ISO builder using 32-bit lengths (roughly 2 GB per entry)
- Only Rock Ridge extensions are emitted; Joliet and El Torito are not implemented
- DMG signing of the container itself is not implemented; only ad-hoc signing of Mach-O binaries occurs
- Optional metadata (checksums, size info) not yet implemented

## Contributing
- Run `dotnet test` to validate changes.
- Open issues or PRs with ideas for better compression, real code signing, or broader ISO support.
