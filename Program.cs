using AngleSharp;
using AngleSharp.Html.Dom;
using CsvHelper;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Crawler
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var config = Configuration.Default.WithDefaultLoader();

            var context = BrowsingContext.New(config);

            await CralwerCulturaNegra(context);
            //await CralwerCartaCapital(context);
            //await CrawlerJogorama(context);
        }

        private static async Task CralwerCulturaNegra(IBrowsingContext context)
        {
            var allResources = new List<Resource>();
            using var driver = new ChromeDriver();
            var driverOptions = driver.Manage();
            //(driverOptions as ChromeOptions).enab
            driverOptions.Window.Maximize();
            driverOptions.Timeouts().ImplicitWait = TimeSpan.FromSeconds(20);

            driver.Navigate().GoToUrl("https://caraeculturanegra.blogspot.com/");

            Console.Clear();

            for (var i = 0; i < 2; i++)
            {
                var webElement = driver.FindElementsByCssSelector(".blog-pager .displaypageNum:not(.lastpage)").Last();
                var page = await context.OpenAsync(r => r.Content(driver.PageSource)).ConfigureAwait(false);

                var resources = page.QuerySelectorAll("h2.post-title.entry-title a").Cast<IHtmlAnchorElement>()
                        .Select(a => new Resource(a.TextContent.Trim(), a.Href.Trim()))
                        .ToArray();

                allResources.AddRange(resources);

                webElement.Click();
                await Task.Delay(5000);
            }
            driver.Quit();

            foreach (var element in allResources)
            {
                var newPage = await context.OpenAsync(element.Url);
                if (newPage.StatusCode != System.Net.HttpStatusCode.OK)
                    continue;

                var child = newPage.QuerySelector(".post-body.entry-content");
                element.ContentParts.Add(child.TextContent);
            }

            SerializeToCsv(allResources);
        }

        private static async Task CralwerCartaCapital(IBrowsingContext context)
        {
            var allResources = new List<Resource>();
            var pages = await Task.WhenAll(Enumerable.Range(1, 2)
                            .Select(i => context.OpenAsync($"https://envolverde.cartacapital.com.br/category/canais-cat/ambiente/page/{i}/")));

            foreach (var pageDocument in pages)
            {
                var elements = pageDocument.QuerySelectorAll(".index-post-inner .tl-home-post-header h2 a").Cast<IHtmlAnchorElement>()
                    .Select(a => new Resource(a.TextContent.Trim(), a.Href.Trim()))
                    .ToArray();

                allResources.AddRange(elements);

                foreach (var element in elements)
                {
                    var newPage = await context.OpenAsync(element.Url);
                    if (newPage.StatusCode != System.Net.HttpStatusCode.OK)
                        continue;

                    var child = newPage.QuerySelector(".single-post-inner-content");
                    var elementsToRemove = child.QuerySelector("div")?.QuerySelectorAll("p")?.Take(3);
                    child.QuerySelector("p:nth-child(1) > em > strong")?.Remove();
                    if (elementsToRemove?.Any() == true)
                        foreach (var item in elementsToRemove)
                            item.Remove();
                    element.ContentParts.Add(child.TextContent);
                }
            }

            SerializeToCsv(allResources);
        }

        private static async Task CrawlerJogorama(IBrowsingContext context)
        {
            var pageDocument = await context.OpenAsync("https://jogorama.com.br/noticias/todas-as-noticias/");

            var elements = pageDocument.QuerySelectorAll("#colunacentral a").Cast<IHtmlAnchorElement>()
                                .Select(a => new Resource(a.TextContent, a.Href))
                                .ToArray();

            foreach (var element in elements)
            {
                var newPage = await context.OpenAsync(element.Url);
                if (newPage.StatusCode != System.Net.HttpStatusCode.OK)
                    continue;

                var children = newPage.QuerySelector("#colunacentral > article").Children;
                foreach (var child in children)
                {
                    if (child.TagName.Equals("blockquote", StringComparison.OrdinalIgnoreCase))
                        break;
                    if (child.TagName.Equals("p", StringComparison.OrdinalIgnoreCase))
                        element.ContentParts.Add(child.TextContent);
                }
            }
        }

        static void SerializeToCsv(List<Resource> allResources)
        {
            var fileName = Path.ChangeExtension(Path.GetTempFileName(), "csv");
            using var reader = new StreamWriter(fileName);
            using var csv = new CsvWriter(reader, CultureInfo.InvariantCulture);
            csv.Configuration.RegisterClassMap<ResourceMap>();
            csv.WriteRecords(allResources);
            Console.WriteLine(fileName);
        }
    }
}
