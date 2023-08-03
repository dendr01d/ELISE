using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

using TagMap = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>;

namespace ELISE
{
    /// <summary>
    /// Represents a fully-parsed script that can be used to transform input text into the appropriate responses
    /// </summary>
    public class Script
    {
        /// <summary>
        /// Contains each of the scripts rewrite rules, indexed according to their associated keyword(s). Multiple keys
        /// may refer to the same rule object
        /// </summary>
        internal Dictionary<string, Rule> Rules { get; set; } = new();

        /// <summary>
        /// The special rule used when recalling memorized user input
        /// </summary>
        internal Rule? MemoryRule { get; set; } = null;
        /// <summary>
        /// The special rule used when no other rules can be applied
        /// </summary>
        internal Rule? NoneRule { get; set; } = null;

        /// <summary>
        /// Taggged categories of words, indexed according to their tag value
        /// </summary>
        internal TagMap Tags { get; set; } = new();

        internal Dictionary<string, string> KeywordSubstitutions { get; set; } = new();

        internal IEnumerable<KeyValuePair<string, Rule>> GetKeywordMatches(string input)
        {
            return Rules.Where(x => Regex.IsMatch(input, $@"\b{x.Key}\b"));
        }

        internal string? GetSubstitutionMatch(string input)
        {
            return KeywordSubstitutions.FirstOrDefault(x => Regex.IsMatch(input, $@"\b{x.Key}\b")).Value;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Script with {Rules.Count + 2} rules, {Tags.Count} tags. Memory rule {(MemoryRule != null ? "valid" : "invalid")}. None rule {(NoneRule != null ? "valid" : "invalid")}.";
            //, {Pairs.Count} pairs
        }
    }

    /// <summary>
    /// Represents a singular, scripted response to some input
    /// </summary>
    internal class Response
    {
        public readonly string RawInput;
        public readonly Script ParentScript;
        public readonly StringBuilder LogicLog;

        public string[] SplitWords = Array.Empty<string>();

        public string Text
        {
            get => string.Join(' ', SplitWords);
            set => SplitWords = value.Split(' ');
        }

        public string Keyword { get; set; } = string.Empty;

        //public string Output { get; set; } = String.Empty;
        //public string? NewKeyword { get; set; } = null;

        public Response(string input, Script parent, StringBuilder log)
        {
            RawInput = input;
            ParentScript = parent;
            LogicLog = log;

            Text = input;
        }

        public void UpdateKeyword(string? kw = null)
        {
            if (kw == null)
            {
                Keyword = "*";
            }
            else
            {
                Keyword = kw;
            }
        }

        /// <summary>
        /// Given a collection of grouped, decomposed text, applies the parent script's list of keyword substitutions
        /// </summary>
        public string[] ApplySubsToGroups(string[] groupValues)
        {
            string[][] words = groupValues.Select(x => x.Split(' ')).ToArray();

            for (int i = 0; i < groupValues.Length; ++i)
            {
                for (int j = 0; j < words[i].Length; ++j)
                {
                    string word = words[i][j];

                    if (ParentScript.KeywordSubstitutions.TryGetValue(word, out string? sub) && sub != null)
                    {
                        words[i][j] = sub;
                    }
                }
            }

            return words.Select(x => string.Join(' ', x)).ToArray();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"<{Text}>";
        }
    }

    /// <summary>
    /// A data structure encapsulating several values used to determine the order of precedence of a particular rewrite rule
    /// </summary>
    public struct Rank
    {
        /// <summary>
        /// The stated priority of the associated rule. Higher priority means higher precedence
        /// </summary>
        public readonly int Priority;
        /// <summary>
        /// The script line index where the rule is defined. Rules with lower indices (and thus defined earlier) are given higher precedence
        /// </summary>
        public readonly int ScriptIndex;
        /// <summary>
        /// The rule to which this rank applies
        /// </summary>
        internal readonly Rule Owner;

        internal Rank(int prio, int index, Rule rl)
        {
            Priority = prio;
            ScriptIndex = index;
            Owner = rl;
        }

        /// <summary>
        /// Returns a new rank object guaranteed to have higher precedence than the one provided. Throws an exception
        /// if the provided rank is already maxed
        /// </summary>
        public static Rank MoreThan(Rank r)
        {
            if (r.Priority == int.MaxValue) throw new InvalidOperationException("Can't out-prioritize a max-ranking.");

            return new Rank(r.Priority + 1, r.ScriptIndex, r.Owner);
        }

        /// <summary>
        /// Returns a rank with the highest theoretical precedence
        /// </summary>
        public static Rank Max => new Rank(int.MaxValue, int.MinValue, new Rule());
    }

    /// <summary>
    /// Represents a single rewrite-rule as defined by a script
    /// </summary>
    internal class Rule
    {
        /// <summary>
        /// A list of keywords that can be used to identify whether this rule applies to a given text input
        /// </summary>
        public List<string> Keywords { get; set; } = new();

        /// <summary>
        /// A score used to determine precedence relative to other rules
        /// </summary>
        public Rank Ranking { get; set; } = new();

        /// <summary>
        /// A list of transformation procedures that can be used to rewrite text to which this rule applies
        /// </summary>
        public List<Transformation> Transforms { get; set; } = new();

        /// <summary>
        /// Sequentially attempts to apply every transformation procedure defined within this rule. Returns
        /// failure if none of them succeed.
        /// </summary>
        public Transformation.Status TryApply(ref Response resp)
        {
            foreach (var trans in Transforms)
            {
                Transformation.Status result = trans.TryApply(ref resp);

                if (result != Transformation.Status.Fail)
                {
                    return result;
                }
            }

            return Transformation.Status.Fail;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Rule on [{string.Join(", ", Keywords)}], prio {Ranking.Priority}, index {Ranking.ScriptIndex}, {Transforms.Count} transform{(Transforms.Count != 1 ? "s" : "ation")}";
        }
    }

    /// <summary>
    /// Represents a procedure for transforming input text into output text.
    /// </summary>
    internal class Transformation
    {
        /// <summary>
        /// A string representing the pattern by which input text is decomposed into its constituent tokens
        /// </summary>
        public string DecompositionRule { get; set; } = string.Empty;
        /// <summary>
        /// A cached regex that implements the <see cref="DecompositionRule"/>
        /// </summary>
        [JsonIgnore]
        private Regex? CachedDecomp = null;

        /// <summary>
        /// A list of strings representing patterns by which decomposed text can be recomposed into outputs
        /// </summary>
        public List<string> ReassemblyRules { get; set; } = new();

        /// <summary>
        /// An index representing which of the <see cref="ReassemblyRules"/> ought to be tried next
        /// </summary>
        private int NextReassemblyIndex = 0;

        private string GetNextReassembly()
        {
            int index = NextReassemblyIndex;

            NextReassemblyIndex = (NextReassemblyIndex + 1) % ReassemblyRules.Count;
            return ReassemblyRules[index];
        }

        /// <summary>
        /// A flag indicating the result of applying a transformation
        /// </summary>
        public enum Status
        {
            /// <summary>
            /// The transformation succeeded
            /// </summary>
            Succ,
            /// <summary>
            /// The transformation failed to apply
            /// </summary>
            Fail,
            /// <summary>
            /// The transformation yielded a link to a different rule that must be applied
            /// </summary>
            Link,
            /// <summary>
            /// The transformation indicated that it should be ignored in lieu of the next one in order of precedence
            /// </summary>
            Next,
            /// <summary>
            /// The transformation succeeded, but yielded a link to a different rule that must also be applied
            /// </summary>
            Prep
        }

        /// <summary>
        /// Attempts to apply the transformation to the contents of the given response object. If provided
        /// with a positive <paramref name="manualAssembly"/> index, attempts to formulate a response using
        /// the reassembly rule at that index.
        /// </summary>
        public Status TryApply(ref Response resp, int manualAssembly = -1)
        {
            resp.LogicLog.Append($"'{resp.Keyword}' --> \"{DecompositionRule}\" --> ");

            if (CachedDecomp == null)
            {
                CachedDecomp = Decomposition.CreateRegexFromPattern(DecompositionRule, resp.ParentScript.Tags);
            }

            if (Decomposition.TryApply(resp.Text, CachedDecomp, out string[] groups) && groups != null)
            {
                groups = resp.ApplySubsToGroups(groups);

                string rsm = manualAssembly > 0
                    ? ReassemblyRules[manualAssembly % ReassemblyRules.Count]
                    : GetNextReassembly();

                if (Reassembly.IsNewKey(rsm))
                {
                    resp.LogicLog.AppendLine(Reassembly.NewKey);
                    return Status.Next;
                }
                else if (Reassembly.GetLink(rsm) is string link && link is not null)
                {
                    resp.Keyword = link;

                    resp.LogicLog.AppendLine($"'{resp.Keyword}'");
                    return Status.Link;
                }
                else if (Reassembly.IsLink(rsm, out string reRule, out string reLink))
                {
                    resp.Text = Reassembly.Reassemble(reRule, groups, resp.ParentScript);
                    resp.Keyword = reLink;

                    resp.LogicLog.AppendLine($"'{resp.Keyword}'");
                    return Status.Prep;
                }
                else
                {
                    resp.Text = Reassembly.Reassemble(rsm, groups, resp.ParentScript);

                    resp.LogicLog.AppendLine("Success!");
                    return Status.Succ;
                }
            }


            resp.LogicLog.AppendLine("No Match");
            return Status.Fail;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Transformation {{{DecompositionRule}}} with {ReassemblyRules.Count} reassembl{(ReassemblyRules.Count == 1 ? "y rules" : "ies")}.";
        }
    }

    /// <summary>
    /// Defines functions for decomposing input text via dynamically-constructed regular expressions
    /// </summary>
    internal static class Decomposition
    {
        /// <summary>
        /// A regex for tokenizing an input pattern, defined here as one of '(', '*', ')', a sequence of numbers, or a sequence of non-spaces
        /// </summary>
        private static readonly Regex Splitter = new Regex(@"(\(|\*|\)|\d+|\\?\b[^\s]+\b)");

        /// <summary>
        /// aA regex pattern that non-greedily consumes characters
        /// </summary>
        private const string RGX_UNGREEGY_PHRASE = @"(.*?)";

        /// <summary>
        /// A regex pattern that greedily matches zero or more space characters
        /// </summary>
        private const string RGX_GREEDY_SPACE = @"\s*";

        /// <summary>
        /// A regex pattern that matches a single sequence of one or more non-spaces
        /// </summary>
        private const string RGX_SOLO_WORD = @"[^\s]+";

        /// <summary>
        /// Given a pattern of text in the script format, returns a regex that matches the pattern
        /// </summary>
        public static Regex CreateRegexFromPattern(string pattern, TagMap tags)
        {
            //begin by splitting the pattern into its constituent tokens
            string[] pieces = Splitter.Matches(pattern).Select(x => x.Value).ToArray();
            StringBuilder sb = new();

            //pattern matches necessarily begin with the beginning of the line
            sb.Append('^');

            //iteratively build the regex, incorporating each token one at a time
            for (int i = 0; i < pieces.Length; ++i)
            {
                //consume all available spaces
                sb.Append(RGX_GREEDY_SPACE);

                //if the token is a number, insert a block to consume that many words
                if (int.TryParse(pieces[i], out int num))
                {
                    //if the number is zero, non-greedily consume zero or more words
                    if (num == 0)
                    {
                        sb.Append(RGX_UNGREEGY_PHRASE);
                    }
                    //else consume exactly the number of words as specified
                    else
                    {
                        sb.Append('(');
                        sb.Append(RGX_SOLO_WORD);

                        for (int j = 1; j < num; ++j)
                        {
                            sb.Append(RGX_GREEDY_SPACE);
                            sb.Append(RGX_SOLO_WORD);
                        }

                        sb.Append(')');
                    }
                }
                else if (pieces[i] == "(" && i + 1 < pieces.Length && pieces[i + 1] == "*")
                {
                    //we've entered a wildcard subpattern. consume words until we exit
                    i += 2; //skip the '(' and '*' tokens

                    if (pieces[i] == ")")
                    {
                        //empty subpattern? shouldn't happen, but it's not invalid either
                        continue;
                    }

                    //insert a block that can match any of the words specified within the wildcard pattern

                    sb.Append('(');
                    sb.Append(pieces[i]);

                    for (++i; pieces[i] != ")"; ++i)
                    {
                        sb.Append('|').Append(pieces[i]);
                    }

                    sb.Append(')');
                }
                else if (pieces[i][0] == '\\' && tags.TryGetValue(pieces[i][1..], out var subs))
                {
                    //if the token is a tag, insert a block that can match any of the tagged words
                    sb.Append('(').Append(string.Join('|', subs)).Append(')');
                }
                else
                {
                    //simply match the current token as presented
                    sb.Append('(').Append(pieces[i].Trim('\\')).Append(')');
                }
            }

            //consume any trailing spaces, then the end of the line
            sb.Append(RGX_GREEDY_SPACE).Append('$');

            return new Regex(sb.ToString());
        }

        /// <summary>
        /// Given some input text and a regex representing a decomposition rule, return whether or not the rule applies to the input.
        /// Also return by reference the match information obtained by applying the rule.
        /// </summary>
        public static bool CanApply(string input, Regex decompRule, out Match? patternMatch)
        {
            if (decompRule.Match(input) is var m && m != null && m.Success)
            {
                patternMatch = m;
                return true;
            }

            patternMatch = null;
            return false;
        }

        /// <summary>
        /// Given some input text and a regex representing a decomposition rule, attempt to apply the rule to the input and return the result.
        /// Also return by reference each of the groups of text matched by the regex.
        /// </summary>
        public static bool TryApply(string input, Regex decompRule, out string[] groups)
        {
            if (CanApply(input, decompRule, out Match? patternMatch) && patternMatch != null)
            {
                groups = patternMatch.Groups.Values.Select(x => x.Value).ToArray();
                return true;
            }

            groups = Array.Empty<string>();
            return false;
        }
    }

    /// <summary>
    /// Defines a set of functions for reassembling decomposed text into an output response
    /// </summary>
    internal static class Reassembly
    {
        public const string NewKey = "@newkey";

        /// <summary>
        /// Returns true if the token matches the newkey flag
        /// </summary>
        public static bool IsNewKey(string token) => token.ToLower() == NewKey;

        /// <summary>
        /// Returns the name of a new rule if the token encodes a link, else null
        /// </summary>
        public static string? GetLink(string token) => token[0] != '='
            ? null
            : token[1..];

        /// <summary>
        /// Returns true if the given reassembly rule encodes a link, along with the name of the linked rule
        /// </summary>
        public static bool IsLink(string reassemblyRule, out string rule, out string link)
        {
            string final = reassemblyRule.Split(' ').Last();

            if (GetLink(final) is string newLink && newLink is not null)
            {
                rule = Regex.Replace(reassemblyRule, @"\s+\=.*$", string.Empty);
                link = newLink;
                return true;
            }

            rule = string.Empty;
            link = string.Empty;
            return false;
        }

        /// <summary>
        /// Given a string representing a reassembly rule and groups of decomposed text, formulate output
        /// text according to the rules provided by the given script
        /// </summary>
        public static string Reassemble(string reassemblyRule, string[] groups, Script scr)
        {
            //first (0th) group is always the whole match, which is forbidden
            for (int i = 1; i < groups.Length; ++i)
            {
                string replacement = groups[i];

                reassemblyRule = reassemblyRule.Replace($"%{i}", replacement);
            }

            //remove any errant spaces between words and sentence-terminating punctuation
            reassemblyRule = Regex.Replace(reassemblyRule, @"\s(\.|\?|\!|\,|\;|\:)", x => x.Groups[1].Value);

            return reassemblyRule;
        }
    }
}
