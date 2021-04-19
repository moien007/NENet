using System.Text;

using ENetDotNet.Checksums;

using NUnit.Framework;

namespace ENetDotNet.Tests.UnitTests.Checksums
{
    [TestFixture]
    public class CRC32Tests
    {
        [TestCase("", ExpectedResult = 0)]
        [TestCase("Hello World!", ExpectedResult = 2736531740)]
        public uint CalculateTest(string utf8)
        {
            var bytes = Encoding.UTF8.GetBytes(utf8);
            var crc32 = new ENetCRC32();
            crc32.Begin();
            crc32.Sum(bytes);
            return crc32.End();
        }
    }
}