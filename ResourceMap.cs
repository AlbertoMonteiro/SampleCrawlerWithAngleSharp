using CsvHelper.Configuration;

namespace Crawler
{
    internal class ResourceMap : ClassMap<Resource>
    {
        public ResourceMap()
        {
            Map(c => c.Title);
            Map(c => c.Url);
            Map(c => c.SanitizedContent);
        }
    }
}