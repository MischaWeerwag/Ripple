using FsCheck;
using FsCheck.Xunit;
using System;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class Rfc1751Tests
    {
        [Theory]
        [InlineData(0x53403DE77EF733EBul, "TIDE ITCH SLOW REIN RULE MOT")]
        [InlineData(0x3636363636363636ul, "RAM LOIS GOAD CREW CARE HIT")]
        [InlineData(0x8E18F62AB4EFCD01ul, "AMY DARK TOIL BELL BUST YOU")]
        [InlineData(0xE7102A373F06A09Bul, "HOYT ABE GRAY CUTS JERK DISC")]
        [InlineData(0x14DA591A0DE86522ul, "LA OLD ARK POP HYMN CAB")]
        public void TestEncode(ulong bits, string expected)
        {
            var words = Rfc1751.Encode(bits);
            Assert.Equal(expected, words);
        }

        [Fact]
        public void TestEncodeBytes()
        {
            var bytes = Base16.Decode("CCAC2AED591056BE4F90FD441C534766");
            var words = Rfc1751.Encode(bytes);
            Assert.Equal("RASH BUSH MILK LOOK BAD BRIM AVID GAFF BAIT ROT POD LOVE", words);
        }

        [Theory]
        [InlineData(0x53403DE77EF733EBul, "TIDE ITCH SLOW REIN RULE MOT")]
        [InlineData(0x3636363636363636ul, "RAM LOIS GOAD CREW CARE HIT")]
        [InlineData(0x8E18F62AB4EFCD01ul, "AMY DARK TOIL BELL BUST YOU")]
        [InlineData(0xE7102A373F06A09Bul, "HOYT ABE GRAY CUTS JERK DISC")]
        [InlineData(0x14DA591A0DE86522ul, "LA OLD ARK POP HYMN CAB")]
        public void TestDecode(ulong expected, string words)
        {
            var split = words.Split(" ");
            var bits = Rfc1751.Decode(split[0], split[1], split[2], split[3], split[4], split[5], out var parityError);
            Assert.Equal(expected, bits);
            Assert.False(parityError);
        }

        [Fact]
        public void TestDecodeBytes()
        {
            var bytes = Rfc1751.Decode("TROD MUTE TAIL WARM CHAR KONG HAAG CITY BORE O TEAL AWL", out var parityError);
            Assert.Equal("EFF81F9BFBC65350920CDD7416DE8009", Base16.Encode(bytes.Span));
            Assert.False(parityError);
        }

        [Property]
        public Property TestRoundTrip()
        {
            return Prop.ForAll(
                Arb.From<ulong>(),
                bits =>
                {
                    var words = Rfc1751.Encode(bits);
                    var split = words.Split(" ");
                    var result = Rfc1751.Decode(split[0], split[1], split[2], split[3], split[4], split[5], out var parityError);
                    Assert.Equal(bits, result);
                    Assert.False(parityError);
                });
        }

        [Property]
        public Property TestRoundTripBytes()
        {
            var arb =
                Arb.From<byte[]>().Filter(bytes => bytes.Length % 8 == 0);

            return Prop.ForAll(
                arb,
                bytes =>
                {
                    var words = Rfc1751.Encode(bytes);
                    var result = Rfc1751.Decode(words, out var parityError);
                    Assert.Equal(Base16.Encode(bytes), Base16.Encode(result.Span));
                    Assert.False(parityError);
                });
        }
    }
}
