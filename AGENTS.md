# Future Improvements & Known Limitations

This document outlines areas of the `Dotnet.Dmg` project that could be improved or expanded in the future.

## ISO Builder
- **Large File Support**: Currently, `IsoNode.DataLength` uses `int`, limiting file sizes to 2GB. Support for ISO 9660 Level 3 (allowing >4GB) or UDF bridge would be needed for larger applications.
- **Joliet Support**: Only Rock Ridge extensions are currently implemented. Adding Joliet would improve compatibility with Windows (though not strictly necessary for macOS DMG).
- **El Torito**: Bootable ISO support is not implemented (not needed for DMGs).

## UDIF (DMG) Generator
- **Checksums**: The Koly block checksums (DataFork and Master) are currently placeholders or minimal. Implementing full CRC32/MD5 calculation for the entire image would improve verification reliability.
- **DMG Signing**: The current implementation signs the inner Mach-O binaries, but does not sign the DMG container itself. The Parcel log confirms it signs the DMG (`rcodesign sign ... EvaluacionesApp.Desktop.dmg`). Implementing this is required for full parity.
- **Compression Algorithms**: Only `UDZO` (zlib) is supported. `UDBZ` (bzip2) or `ULFO` (lzfse) could be added for better compression ratios. Note: The reference Parcel DMG uses Bzip2 (`UDBZ`).

## Mach-O Signer
- **Architecture Support**: The signer currently hardcodes a 4KB page size for Code Directory hashing. Apple Silicon (arm64) binaries typically use 16KB pages. This needs to be dynamic based on the binary's target architecture.
- **Universal Binaries (Fat)**: The current parser expects a single-arch Mach-O (`MH_MAGIC_64`). It does not handle Fat headers (`CAFEBABE`) containing multiple slices (e.g., x64 and arm64).
- **CMS Blob**: The signer generates an ad-hoc signature (Code Directory only). It does not support cryptographic signing with X.509 certificates (CMS/PKCS#7). This is sufficient for local development and some distribution methods, but not for notarization.
- **Entitlements**: The current implementation does not embed an Entitlements blob. The Parcel log shows that .NET apps require specific entitlements (e.g., `com.apple.security.cs.allow-jit`) to run correctly. This is a critical missing feature.

## General
- **Memory Usage**: The `IsoBuilder` builds the filesystem structure in memory. For very large applications, a streaming approach or temporary file backing would be more efficient.
- **Icon Generation**: The tool expects a pre-made `.icns` file. Integrating a PNG-to-ICNS converter would streamline the workflow.
- **Recursive Signing**: Parcel explicitly discovers and signs all `.dylib` files and the main executable. Our CLI does this, but ensuring we catch all native dependencies is crucial.
