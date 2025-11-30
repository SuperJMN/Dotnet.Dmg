using System;
using System.IO;
using System.Text;
using Dotnet.Dmg.Udif;
using Xunit;

namespace Dotnet.Dmg.Tests
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
    }
}
