using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIZa.TikTok
{
    internal abstract class GenericJSON
    {
        public string Key { get; } = "GenericKey";
        public abstract bool IsLeaf { get; }
        public abstract GenericJSON this[string key] { get; }
        public abstract bool TryGetValue(string key, out GenericJSON? output);
    }

    internal class GenericNode : GenericJSON
    {
        public Dictionary<string, GenericJSON> Branches { get; set; } = new();
        public override bool IsLeaf => false;
        public override GenericJSON this[string key] => this.Branches.TryGetValue(key, out GenericJSON? child) && child != null ? child : throw new IndexOutOfRangeException();
        public override bool TryGetValue(string key, out GenericJSON? output) => this.Branches.TryGetValue(key, out output);
    }

    internal class GenericLeaf : GenericJSON
    {
        public string Value { get; } = String.Empty;
        public override bool IsLeaf => false;
        public override GenericJSON this[string key] => throw new IndexOutOfRangeException();
        public override bool TryGetValue(string key, out GenericJSON? output)
        {
            output = null;
            return false;
        }
    }
}
