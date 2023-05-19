using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIZa.TikTok
{
    internal class TikTokResponse
    {
        public string message { get; set; }
        public int status_code { get; set; }

        public TikTokData data { get; set; }
        public TikTokExtra extra { get; set; }

        public byte[] Sound => Convert.FromBase64String(this.data.v_str);
    }

    internal struct TikTokData
    {
        public string v_str { get; set; }
        public double duration { get; set; }
        public int speaker { get; set; }
    }

    internal struct TikTokExtra
    {
        public string log_id { get; set; }
    }
}
