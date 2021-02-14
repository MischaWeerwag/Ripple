using System;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class LedgerHeaderTests
    {
        [Fact]
        public void TestExample()
        {
            var utf8 = System.Text.Encoding.UTF8.GetBytes("005B67A60163457208228EB931635D1A225FF646885BFA8A33A2A3424BFE3290E493C6C6CEB9A51D84D5457BD96702F77340ED9C5294B88A13A0F42BE487D7921F18A2916E6BCA9AC3F9D3F1EEBDADA11F85DA6AB50E29754BEBF4276C327BA05E48004AE574C8F04B4493FD261A7568261A75690A00");
            var data = new byte[Base16.GetDecodedFromUtf8Length(utf8.Length)];
            Assert.Equal(System.Buffers.OperationStatus.Done, Base16.DecodeFromUtf8(utf8, data, out var _, out var _));

            var header = new LedgerHeader(data);
            Assert.Equal(5990310u, header.Sequence);
            Assert.Equal(99999972797353657ul, header.TotalCoins);
            Assert.Equal(new Hash256("31635D1A225FF646885BFA8A33A2A3424BFE3290E493C6C6CEB9A51D84D5457B"), header.ParentHash);
            Assert.Equal(new Hash256("D96702F77340ED9C5294B88A13A0F42BE487D7921F18A2916E6BCA9AC3F9D3F1"), header.TransactionHash);
            Assert.Equal(new Hash256("EEBDADA11F85DA6AB50E29754BEBF4276C327BA05E48004AE574C8F04B4493FD"), header.AccountHash);
            Assert.Equal(Epoch.ToDateTimeOffset(639268200), header.ParentCloseTime);
            Assert.Equal(Epoch.ToDateTimeOffset(639268201), header.CloseTime);
            Assert.Equal(10, header.CloseTimeResolution);
            Assert.Equal(0, header.CloseFlags);
        }
    }
}