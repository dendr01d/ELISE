using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELISE.ELIZA
{
    /// <summary>
    /// A script is made up of two types of collections of substitution expressions
    /// The first type are reflections of phrases as used by the user vs the computer
    /// The second type is a dictionary mapping regex search strings to possible responses,
    ///     where the responses may refer to matched subgroups of the regex
    /// </summary>
    public class Script
    {
        public readonly Dictionary<string, string> Reflexives = new();
        public readonly Dictionary<string, List<string>> Responses = new();
    }
}
