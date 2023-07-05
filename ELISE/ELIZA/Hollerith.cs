using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELISE.ELIZA
{
    internal class Hollerith
    {
        private static readonly char NullChar = (char)0x0;
        private static readonly byte HollerithUndefined = 0xFF; //64

        private static byte[] bcd = new char[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', NullChar, '=', '\'', NullChar, NullChar, NullChar,
            '+', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', NullChar, '.', ')',  NullChar, NullChar, NullChar,
            '-', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', NullChar, '$', '*',  NullChar, NullChar, NullChar,
            ' ', '/', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', NullChar, ',', '(',  NullChar, NullChar, NullChar
        }.Select(x => (byte)x).ToArray();

        private static readonly byte[] HollerithEncoding = Enumerable.Repeat(HollerithUndefined, 256).ToArray();

        static Hollerith()
        {
            for (byte i = 0; i < 64; ++i)
            {
                if (bcd[i] != NullChar)
                {
                    HollerithEncoding[bcd[i]] = i;
                }
            }
        }

        public static void Initialize() { }

        private static bool IsDefined(char c)
        {
            return HollerithEncoding[(byte)c] != HollerithUndefined;
        }

        /// <summary>
        /// Return an n-bit hash value for the 36-bit d, for n in 0..15
        /// </summary>
        public static int Hash(ulong d, int n)
        {
            if (n < 0 || n > 15)
            {
                throw new ArgumentException("n not in (0, 15)");
            }

            d &= 0x7FFFFFFFFUL;
            d *= d;

            d >>= 35 - (n / 2);

            return (int)(d & ((1UL << n) - 1));
        }

        public static ulong ChunkAsBCD(string s)
        {
            s = s.ToUpper();

            static ulong append(ulong input, char c)
            {
                if (!IsDefined(c))
                {
                    throw new Exception($"Character '{c}' is undefined within the hollerith encoding");
                }

                ulong output = input;
                output <<= 6;
                output |= HollerithEncoding[(byte)c];
                return output;
            }

            ulong result = 0;
            int count = 0;

            for (int c = ((s.Length - 1) / 6) * 6; c < s.Length; ++c, ++count)
            {
                result = append(result, s[c]);
            }

            while (count < 6)
            {
                result = append(result, ' ');
                count++;
            }

            return result;
        }

    }
}
