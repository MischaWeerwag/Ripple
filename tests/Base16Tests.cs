using System;
using System.Buffers;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class Base16Tests
    {
        [Theory]
        [InlineData("")]
        [InlineData("0123456789ABCDEF")]
        [InlineData("6578616D706C652E636F6D")]
        [InlineData("69626173612e636f2e756b")]
        public void TestRoundTrip(string data)
        {
            var utf8 = System.Text.Encoding.UTF8.GetBytes(data);

            var bytes = new byte[Base16.GetMaxDecodedFromUtf8Length(utf8.Length)];
            var status = Base16.DecodeFromUtf8(utf8, bytes, out var bytesConsumed, out var bytesWritten);

            Assert.Equal(OperationStatus.Done, status);
            Assert.Equal(utf8.Length, bytesConsumed);
            Assert.Equal(bytes.Length, bytesWritten);

            utf8 = new byte[Base16.GetMaxEncodedToUtf8Length(bytes.Length)];
            status = Base16.EncodeToUtf8(bytes, utf8, out bytesConsumed, out bytesWritten);

            Assert.Equal(OperationStatus.Done, status);
            Assert.Equal(bytes.Length, bytesConsumed);
            Assert.Equal(utf8.Length, bytesWritten);

            Assert.Equal(data, System.Text.Encoding.UTF8.GetString(utf8));
        }

        [Theory]
        [InlineData("x")]
        [InlineData("00x")]
        public void TestInvalidData(string data)
        {
            var utf8 = System.Text.Encoding.UTF8.GetBytes(data);

            var bytes = new byte[Base16.GetMaxDecodedFromUtf8Length(utf8.Length)];
            var status = Base16.DecodeFromUtf8(utf8, bytes, out var bytesConsumed, out var bytesWritten);

            Assert.Equal(OperationStatus.InvalidData, status);
        }

        [Theory]
        [InlineData("00")]
        [InlineData("00FF")]
        public void TestDestinationTooSmall(string data)
        {
            var utf8 = System.Text.Encoding.UTF8.GetBytes(data);

            var bytes = new byte[Base16.GetMaxDecodedFromUtf8Length(utf8.Length) - 1];
            var status = Base16.DecodeFromUtf8(utf8, bytes, out var bytesConsumed, out var bytesWritten);

            Assert.Equal(OperationStatus.DestinationTooSmall, status);
        }
    }
}
