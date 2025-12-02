using System.Text;
using DotnetPackaging.Formats.Dmg.Udif;

namespace DotnetPackaging.Formats.Dmg.Tests
{
    public class UdifTests
    {
        [Fact]
        public void KolyBlock_IsGeneratedCorrectly()
        {
            var writer = new UdifWriter();
            byte[] inputData = new byte[1024 * 1024]; // 1MB
            new Random().NextBytes(inputData);

            using var input = new MemoryStream(inputData);
            using var output = new MemoryStream();

            writer.Create(input, output);

            byte[] dmg = output.ToArray();

            // Koly block is last 512 bytes
            byte[] kolyBytes = new byte[512];
            Array.Copy(dmg, dmg.Length - 512, kolyBytes, 0, 512);

            // Check signature 'koly' (0x6B6F6C79)
            // It's Big Endian in file.
            Assert.Equal(0x6B, kolyBytes[0]);
            Assert.Equal(0x6F, kolyBytes[1]);
            Assert.Equal(0x6C, kolyBytes[2]);
            Assert.Equal(0x79, kolyBytes[3]);

            // Check version (4)
            Assert.Equal(0, kolyBytes[4]);
            Assert.Equal(0, kolyBytes[5]);
            Assert.Equal(0, kolyBytes[6]);
            Assert.Equal(4, kolyBytes[7]);
        }

        [Fact]
        public void Plist_Contains_Blkx()
        {
            var writer = new UdifWriter();
            using var input = new MemoryStream(new byte[100]);
            using var output = new MemoryStream();

            writer.Create(input, output);

            byte[] dmg = output.ToArray();
            string content = Encoding.UTF8.GetString(dmg);

            Assert.Contains("<key>blkx</key>", content);
            Assert.Contains("<key>resource-fork</key>", content);
        }

        [Fact]
        public void Koly_Flags_BitZero_IsSet()
        {
            var writer = new UdifWriter();
            using var input = new MemoryStream(new byte[1024]);
            using var output = new MemoryStream();
            
            writer.Create(input, output);
            
            byte[] dmg = output.ToArray();
            byte[] kolyBytes = new byte[512];
            Array.Copy(dmg, dmg.Length - 512, kolyBytes, 0, 512);
            
            uint flags = ReadBigEndianUInt32(kolyBytes, 12);
            Assert.Equal(1u, flags & 1); // Bit 0 should be set
        }

        [Fact]
        public void Mish_BuffersNeeded_IsCalculated()
        {
            var writer = new UdifWriter();
            byte[] inputData = new byte[5 * 1024 * 1024]; // 5 MB
            using var input = new MemoryStream(inputData);
            using var output = new MemoryStream();
            
            writer.Create(input, output);
            
            // Extract mish block from plist
            byte[] dmg = output.ToArray();
            byte[] mishData = ExtractMishBlock(dmg);
            
            uint buffersNeeded = ReadBigEndianUInt32(mishData, 0x20);
            Assert.True(buffersNeeded > 0, "Buffers needed should be calculated");
        }

        private uint ReadBigEndianUInt32(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | 
                   ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private ulong ReadBigEndianUInt64(byte[] data, int offset)
        {
            return ((ulong)data[offset] << 56) | ((ulong)data[offset + 1] << 48) | 
                   ((ulong)data[offset + 2] << 40) | ((ulong)data[offset + 3] << 32) |
                   ((ulong)data[offset + 4] << 24) | ((ulong)data[offset + 5] << 16) | 
                   ((ulong)data[offset + 6] << 8) | data[offset + 7];
        }

        private byte[] ExtractMishBlock(byte[] dmg)
        {
            // Read Koly block to get XML offset/length
            byte[] kolyBytes = new byte[512];
            Array.Copy(dmg, dmg.Length - 512, kolyBytes, 0, 512);
            
            ulong xmlOffset = ReadBigEndianUInt64(kolyBytes, 0xD8);
            ulong xmlLength = ReadBigEndianUInt64(kolyBytes, 0xE0);
            
            // Extract plist
            byte[] plistBytes = new byte[xmlLength];
            Array.Copy(dmg, (long)xmlOffset, plistBytes, 0, (long)xmlLength);
            string plistText = Encoding.UTF8.GetString(plistBytes);
            
            // Find <data> tag after <key>Data</key>
            int dataKeyIndex = plistText.IndexOf("<key>Data</key>");
            if (dataKeyIndex < 0) throw new Exception("No Data key found");
            
            int dataTagStart = plistText.IndexOf("<data>", dataKeyIndex);
            int dataTagEnd = plistText.IndexOf("</data>", dataTagStart);
            
            string base64Data = plistText.Substring(dataTagStart + 6, dataTagEnd - dataTagStart - 6)
                .Replace("\n", "").Replace("\t", "").Replace(" ", "");
            
            return Convert.FromBase64String(base64Data);
        }

        [Fact]
        public void UdifWriter_SupportsBzip2Compression()
        {
            var writer = new UdifWriter { CompressionType = CompressionType.Bzip2 };
            byte[] inputData = new byte[1024 * 1024]; // 1 MB
            for (int i = 0; i < inputData.Length; i++) inputData[i] = (byte)(i % 256);

            using var input = new MemoryStream(inputData);
            using var output = new MemoryStream();
            
            writer.Create(input, output);
            
            // Extract mish and verify compression type is 0x80000006
            byte[] dmg = output.ToArray();
            byte[] mishData = ExtractMishBlock(dmg);
            
            // First block descriptor starts at 0xC8 (after block run count at 0xC4)
            // Block descriptor format: Type (4 bytes) + Reserved (4 bytes) + ...
            uint compressionType = ReadBigEndianUInt32(mishData, 0xC8);
            Assert.Equal(0x80000006u, compressionType);
        }

        [Fact]
        public void Bzip2_ProducesSmallerOutput_ThanZlib()
        {
            byte[] inputData = new byte[5 * 1024 * 1024]; // 5 MB of compressible data
            // Create somewhat compressible data (repeating pattern)
            for (int i = 0; i < inputData.Length; i++)
            {
                inputData[i] = (byte)((i / 1024) % 256);
            }
            
            var zlibWriter = new UdifWriter { CompressionType = CompressionType.Zlib };
            using var zlibOutput = new MemoryStream();
            zlibWriter.Create(new MemoryStream(inputData), zlibOutput);
            
            var bzip2Writer = new UdifWriter { CompressionType = CompressionType.Bzip2 };
            using var bzip2Output = new MemoryStream();
            bzip2Writer.Create(new MemoryStream(inputData), bzip2Output);
            
            Assert.True(bzip2Output.Length < zlibOutput.Length, 
                        $"Bzip2 ({bzip2Output.Length}) should produce smaller output than zlib ({zlibOutput.Length})");
        }
    }
}
