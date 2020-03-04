using System;
using System.Collections.Generic;

namespace Crawler
{
    public class Resource
    {
        public Resource(string title, string url)
        {
            Title = title;
            Url = url;
        }

        public string Title { get; }
        public string Url { get; }
        public List<string> ContentParts { get; } = new List<string>();
        public string Content => string.Join(" ", ContentParts);
        public string SanitizedContent => Content.Replace('\n', ' ').Replace('\t', ' ').Replace('\r', ' ').Trim();

        public override string ToString() => $"{Title}\n\t- {Url}";
    }
}
