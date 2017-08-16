using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace String2Resources
{
    public class ParseResult
    {
        public bool ToResource { get; set; }
        public Int32 LineNumber { get; set; }
        public Int32 ReplaceCount { get; set; }
        public List<Int32> Quotes { get; set; }
        public string LineContent { get; set; }
        public List<string> ReplaceFinds { get; set; }

        public ParseResult()
        {
            ReplaceCount = 0;
            Quotes = new List<Int32>();
            ReplaceFinds = new List<string>();
            ToResource = false;
        }

        public Int32 StringCount
        {
            get
            
            {
                if (Quotes.Count == 0) return 0;
                return (Int32)(Quotes.Count / 2);
            }
        }
    }
}
