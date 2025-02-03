using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELISE
{
    /// <summary>
    /// Defines functions relating to the Hollerith character encoding used by the original ELIZA.
    /// </summary>
    /// <remarks>
    /// Older IBM machines used 6-bit fields to encode characters instead of whole bytes.
    /// Rather than fiddling with bit fields though I'm just translating the bytes to their equivalents.
    /// </remarks>
    internal static class Hollerith
    {
        private const int _8BITSPACE = 0x1 << 8;

        private static readonly char _nil = (char)0x0;

        /// <summary>
        /// Placeholder for a byte value that can't be represented in 6 bits.
        /// </summary>
        public static readonly byte Undefined = 0x1 << 6;

        /// <summary>
        /// The characters representable via Hollerith-encoding, arranged via BCD.
        /// </summary>
        public static readonly char[] Characters = new char[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', _nil, '=', '\'', _nil, _nil, _nil,
            '+', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', _nil, '.', ')',  _nil, _nil, _nil,
            '-', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', _nil, '$', '*',  _nil, _nil, _nil,
            ' ', '/', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', _nil, ',', '(',  _nil, _nil, _nil
        };

        /// <summary>
        /// Hollerith-Encoded characters indexed according to the byte values of the characters they represent.
        /// </summary>
        public static readonly byte[] Encoding = CreateHollerithMapping();

        /// <summary>
        /// Iterate through and map each 8-bit value to its corresponding Hollerith-Encoded value.
        /// </summary>
        private static byte[] CreateHollerithMapping()
        {
            byte[] byteMap = Enumerable.Repeat(Undefined, _8BITSPACE).ToArray();

            // Iterate through the 6-bit value space
            for (byte i = 0; i < Undefined; ++i)
            {
                if (Characters[i] != _nil)
                {
                    byteMap[Characters[i]] = i;
                }
            }

            return byteMap;
        }

        /// <summary>
        /// Return an n-bit hash value for the 36-bit d, for n in 0..15
        /// </summary>
        public static int Hash(ulong d, int n)
        {
            if (n < 0 || n > 15)
            {
                throw new ArgumentException("n not in [0, 15]");
            }

            d &= 0x7FFFFFFFFUL;
            d *= d;

            d >>= 35 - n / 2;

            return (int)(d & (1UL << n) - 1);
        }

        /// <summary>
        /// Return an n-bit hash for the last x-character chunk of <paramref name="s"/> in
        /// terms of its Hollerith-Encoding.
        /// </summary>
        /// <param name="s">The string to be hashed.</param>
        /// <param name="n">The desired number of bits in the resulting hash. Must be in the range 0 to 15.</param>
        public static int Hash(string s, int n, int x)
        {
            ulong input = s.Chunk(x).Last().ToHollerith().AsBitField(6);
            return Hash(input, n);
        }
    }
}
