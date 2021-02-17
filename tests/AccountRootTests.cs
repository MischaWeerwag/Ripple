using System;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class AccountRootTests
    {
        [Fact]
        public void TestExample()
        {
            var utf8 = System.Text.Encoding.UTF8.GetBytes("11006122000000002400000001250062FEA42D0000000055C204A65CF2542946289A3358C67D991B5E135FABFA89F271DBA7A150C08CA0466240000000354540208114C909F42250CFE8F12A7A1A0DFBD3CBD20F32CD79");
            Span<byte> data = new byte[Base16.GetDecodedFromUtf8Length(utf8.Length)];
            Assert.Equal(System.Buffers.OperationStatus.Done, Base16.DecodeFromUtf8(utf8, data, out var _, out var _));

            var expectedHash = new Hash256("00001A2969BE1FC85F1D7A55282FA2E6D95C71D2E4B9C0FDD3D9994F3C00FF8F");

            var reader = new St.StReader(data);

            reader.TryReadFieldId(out var type, out var field);
            Assert.Equal(St.StTypeCode.UInt16, type);
            Assert.Equal(1u, field);
            Assert.Equal(St.StLedgerEntryTypes.AccountRoot, (St.StLedgerEntryTypes)reader.ReadUInt16());

            var accountRoot = new AccountRoot(reader);
            Assert.Equal(new AccountId("rKKzk9ghA2iuy3imqMXUHJqdRPMtNDGf4c"), accountRoot.Account);
            Assert.Equal(XrpAmount.FromDrops(893730848), accountRoot.Balance);
            Assert.Equal(AccountRootFlags.None, accountRoot.Flags);
            Assert.Equal(0u, accountRoot.OwnerCount);
            Assert.Equal(new Hash256("C204A65CF2542946289A3358C67D991B5E135FABFA89F271DBA7A150C08CA046"), accountRoot.PreviousTxnID);
            Assert.Equal(6487716u, accountRoot.PreviousTxnLgrSeq);
            Assert.Equal(1u, accountRoot.Sequence);
            Assert.Equal(expectedHash, accountRoot.ID);
        }
    }
}