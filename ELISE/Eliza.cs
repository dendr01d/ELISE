using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.RegularExpressions;

namespace ELISE
{
    public class Eliza
    {
        public static readonly string[] QuitterTalk = new string[]
        {
            "bye", "farewell", "quit", "exit", "shut"
        };

        private static readonly string[] Delimiters = new string[]
        {
            ".", ",", "!", "?", "but",
        };

        private static string CondDelimiters = string.Join("|", Delimiters.Select(x => x.Length > 1 ? $"\b{x}\b" : $"\\{x}"));
        private static readonly Regex Splitter = new Regex(@$"({CondDelimiters}|\b[^\s]+\b)");

        private Queue<string> InputMemory = new();

        public static Eliza FromScript(string path)
        {
            return new Eliza()
            {
                Script = ScriptReader.ReadFromFile(path)
            };
        }

        private int SecretLimit = 1;

        private Script Script = new();

        private bool TryCreateMemory(Response resp)
        {
            Response newResponse = new(resp.Text, resp.ParentScript, resp.LogicLog);
            newResponse.UpdateKeyword();

            if (Script.MemoryRule != null)
            {
                resp.LogicLog.AppendLine("Attempting to memorize input...");

                int transIndex = Hollerith.Hash(Hollerith.ChunkAsBCD(resp.SplitWords.Last()), 2);
                Transformation memTransform = Script.MemoryRule.Transforms.First();

                if (memTransform.ReassemblyRules.Count >= transIndex
                    && memTransform.TryApply(ref newResponse, transIndex) == Transformation.Status.Succ)
                {
                    InputMemory.Enqueue(newResponse.Text);
                    return true;
                }
            }

            return false;
        }

        private void IncrementLimit()
        {
            SecretLimit = SecretLimit % 4 + 1;
        }

        public string ProduceGreeting()
        {
            Response resp = new Response("hello", Script, new StringBuilder());
            if (Script.Rules.TryGetValue(resp.RawInput, out Rule? r) && r != null)
            {
                if (r.TryApply(ref resp) == Transformation.Status.Succ)
                {
                    return resp.Text.ToUpper();
                }
            }

            return "how do you do? please state your problem.".ToUpper();
        }

        public string CreateClassicalResponse(string text, out StringBuilder logicLog)
        {

            Response resp = new Response(text.ToLower().Trim(), Script, new StringBuilder());
            logicLog = resp.LogicLog;

            IncrementLimit();
            logicLog.AppendLine($"(Limit = {SecretLimit})");


            IEnumerable<string> allWords = Splitter.Matches(resp.RawInput).Select(x => x.Value);//.Reverse();
            string[] words = Array.Empty<string>();

            //while (allWords.Any())
            //{
            //    IEnumerable<string> testClause = allWords.TakeWhile(x => !Delimiters.Contains(x)).ToArray();
            //    allWords = allWords.SkipWhile(x => !Delimiters.Contains(x)).Skip(1);
            //    resp.Text = String.Join(' ', testClause.Reverse());

            //    if (!String.IsNullOrWhiteSpace(resp.Text))
            //    {
            //        logicLog.AppendLine($"Read: {resp.Text}");
            //    }

            //    //memorize prior clauses of the sentence as we consume new ones. the last goes unmemorized for now
            //    if (allWords.Any())
            //    {
            //        InputMemory.Push(resp.Text);
            //    }
            //}

            while (allWords.Any())
            {
                resp.Text = string.Join(' ', allWords.TakeWhile(x => !Delimiters.Contains(x)));
                allWords = allWords.SkipWhile(x => !Delimiters.Contains(x)).Skip(1);

                //keep only the first clause to contain a keyword
                if (Script.GetKeywordMatches(resp.Text).Any())
                {
                    logicLog.AppendLine($"Read: {resp.Text}");
                    break;
                }
            }

            //use a custom comparer to have it sort biggest score to least
            PriorityQueue<KeyValuePair<string, Rule>, Rank> keyStack = new(Comparer<Rank>.Create((x, y) => y.Priority.CompareTo(x.Priority) * 10 + y.Owner.Keywords.Last().CompareTo(x.Owner.Keywords.Last())));

            foreach (var kvp in Script.GetKeywordMatches(resp.Text))
            {
                if (kvp.Value.Transforms.Any())
                {
                    keyStack.Enqueue(kvp, kvp.Value.Ranking);
                }

                //if (resp.ParentScript.KeywordSubstitutions.TryGetValue(kvp.Key, out string? sub) && sub != null)
                //{
                //    resp.Subs.Add(kvp.Key, sub);
                //}
            }

            //slapdash way of checking if the stack is empty
            //if it is, try to pull something out of the conversational memory
            //if (this.Script.MemoryRule != null && !keyStack.TryPeek(out var _, out var _))
            //{
            //    while(this.InputMemory.TryDequeue(out string? mem) && mem != null)
            //    {
            //        logicLog.AppendLine($"Engaging memory: {mem}");

            //        resp.Text = mem;
            //        resp.UpdateKeyword();
            //        Transformation.Status attempt = this.Script.MemoryRule.TryApply(ref resp);

            //        if (attempt == Transformation.Status.Succ)
            //        {
            //            return resp.Text;
            //            //break;
            //        }
            //        else if (attempt == Transformation.Status.Link && resp.Keyword != null)
            //        {
            //            keyStack.Enqueue(new (resp.Keyword, this.Script.Rules[resp.Keyword]), Rank.Max); //arbitrary priority since the stack was empty to begin with
            //            break; //continue on as normal with our new keyword rule
            //        }
            //        else if (attempt == Transformation.Status.Prep && resp.Keyword != null)
            //        {
            //            logicLog.AppendLine($"Modified input: {resp.Text}");

            //            keyStack.Enqueue(new(resp.Keyword, this.Script.Rules[resp.Keyword]), Rank.Max);
            //            break;
            //        }
            //    }
            //}

            //don't memorize the new input until after the memory check
            //to avoid an infinite loop of memorizing and recalling the same input
            //this.InputMemory.Push(resp.Text);
            TryCreateMemory(resp);

            if (!keyStack.TryPeek(out var _, out var _) && InputMemory.Any())
            {
                logicLog.AppendLine($"No applicable keywords");
                logicLog.AppendLine($"Engaging memory...");

                if (SecretLimit == 4 && InputMemory.TryDequeue(out string? mem) && mem != null)
                {
                    return mem;
                }
                else
                {
                    if (SecretLimit != 4)
                    {
                        logicLog.AppendLine($"... but the limit ({SecretLimit}) isn't equal to 4");
                    }
                    else
                    {
                        logicLog.AppendLine($"... but no memories were found");
                    }
                }
            }
            else
            {
                logicLog.AppendLine($"Found {keyStack.Count} applicable keyword/s");
            }

            //attempt to apply rules until one sticks
            while (keyStack.TryDequeue(out KeyValuePair<string, Rule> kvp, out Rank r))
            {
                //this.InputMemory.Enqueue(resp.Text);

                logicLog.AppendLine($"Applying new rule...");

                resp.UpdateKeyword(kvp.Key);
                Transformation.Status attempt = kvp.Value.TryApply(ref resp);

                if (attempt == Transformation.Status.Succ)
                {
                    return resp.Text;
                }
                else if (attempt == Transformation.Status.Link && resp.Keyword != null)
                {
                    keyStack.Enqueue(new(resp.Keyword, Script.Rules[resp.Keyword]), Rank.MoreThan(r)); //+1 to make sure it's above all the rest
                }
                else if (attempt == Transformation.Status.Prep && resp.Keyword != null)
                {
                    logicLog.AppendLine($"Modified input: {resp.Text}");

                    keyStack.Enqueue(new(resp.Keyword, Script.Rules[resp.Keyword]), Rank.MoreThan(r));
                }
            }

            logicLog.AppendLine("Resorting to None rule...");

            if (Script.NoneRule != null)
            {
                resp.UpdateKeyword();
                Script.NoneRule.TryApply(ref resp);
            }
            else
            {
                return new string[]
                {
                    "please, continue.",
                    "hmmm.",
                    "go on, please",
                    "i see."
                }[SecretLimit].ToUpper();
            }


            return resp.Text;
        }
    }
}
