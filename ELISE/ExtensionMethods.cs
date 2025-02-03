using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELISE
{
    internal static class ExtensionMethods
    {
        /// <summary>
        /// Converts the given integer to a byte representing a character in Binary-Coded Decimal encoding.
        /// </summary>
        /// <param name="i">An integer representing the index of a character in <see cref="Hollerith"/> encoding.</param>
        /// <returns>A byte representing the offset of the </returns>
        public static byte ToBCD(this int i)
        {
            return (byte)Hollerith.Characters[i];
        }

        /// <summary>
        /// Convert <paramref name="c"/> to its equivalent Hollerith-Encoded value.
        /// </summary>
        /// <remarks>
        /// Since 6 bits can only hold 1/4 as many values as a byte, many inputs will map to <see cref="Hollerith.Undefined"/>.
        /// </remarks>
        public static byte ToHollerith(this char c)
        {
            return Hollerith.Encoding[(byte)c];
        }

        /// <summary>
        /// Convert a sequence of characters to their equivalent Hollerith Encoding.
        /// </summary>
        public static IEnumerable<byte> ToHollerith(this IEnumerable<char> str)
        {
            return str.Select(x => x.ToHollerith());
        }

        /// <summary>
        /// Convert a sequence of bytes to a bitfield, where each byte is interpreted as a field of <paramref name="fieldSize"/> bits.
        /// </summary>
        public static ulong AsBitField(this IEnumerable<byte> bytes, int fieldSize)
        {
            if (bytes.Any())
            {
                ulong result = default;
                result |= bytes.First();

                foreach (byte b in bytes.Skip(1))
                {
                    result <<= 6;
                    result |= b;
                }

                return result;
            }
            else
            {
                return default;
            }
        }

        /// <summary>
        /// Determines whether the given character is defined in the Hollerith-Encoding.
        /// </summary>
        public static bool IsHollerithCompliant(this char c) => c.ToHollerith() != Hollerith.Undefined;

        /// <summary>
        /// Split <paramref name="s"/> into a sequence of character "chunks" of size <paramref name="chunkSize"/>.
        /// </summary>
        public static IEnumerable<string> Chunk(this string s, int chunkSize)
        {
            IEnumerable<char> current = s;

            while (current.Any())
            {
                string output = new string(s.Take(chunkSize).ToArray());
                output = output.PadRight(chunkSize, default);

                yield return output;

                current = current.Skip(chunkSize);
            }
        }
    }
}
