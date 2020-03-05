using AngleSharp;
using AngleSharp.Html.Dom;
using CsvHelper;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

            await CralwerTwitter(context);
            //await CralwerCulturaNegra(context);
            //await CralwerCartaCapital(context);
            //await CrawlerJogorama(context);
        }

        private static async Task CralwerTwitter(IBrowsingContext context)
        {
            var allResources = new List<Resource>();
            var options = new ChromeOptions();
            //options.AddArguments("--disable-popup-blocking");
            options.AddArguments("--headless");
            options.AddArguments("--window-size=1920x1080");
            using var driver = new ChromeDriver(options);
            var driverOptions = driver.Manage();
            driverOptions.Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            //

            driver.Navigate().GoToUrl("https://twitter.com/search?l=pt&src=typd&lang=pt&f=tweets&vertical=default&q=Jogos");

            Console.Clear();

            allResources.AddRange(driver.FindElementsByCssSelector("div.css-1dbjc4n article")
                .Select(x => new Resource("", x.FindElement(By.CssSelector("div > div:nth-child(2) > div:nth-child(2) > div:nth-child(1) > div > div:nth-child(1) > a")).GetAttribute("href")) { ContentParts = { x.FindElement(By.CssSelector("div > div:nth-child(2) > div:nth-child(2) > div:nth-child(2)")).Text } }));

            driver.ExecuteScript("document.value_tweets = []");
            driver.ExecuteScript(@"
document.querySelector('div.css-1dbjc4n')
    .addEventListener('DOMNodeInserted', a => {
    let element = a.target.querySelector('article > div > div:nth-child(2) > div:nth-child(2) > div:nth-child(2)');
    if(element != null)
        document.value_tweets.push({ text: element.textContent, link: a.target.querySelector('article a > time').closest('a').href })
}, false);");

            for (var i = 0; i < 5; i++)
            {
                Console.WriteLine("Fazendo scroll");
                driver.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                await Task.Delay(5000);

                if (driver.ExecuteScript("return document.value_tweets") is IReadOnlyCollection<object> objs)
                    allResources.AddRange(objs.Cast<IDictionary<string, object>>().Select(x => new Resource("", x["link"].ToString()) { ContentParts = { x["text"].ToString() } }));
            }
            driver.Quit();

            var distinctValues = allResources.Distinct(new ResourceComparer()).ToList();
            SerializeToCsv(distinctValues);
        }

        public class ResourceComparer : IEqualityComparer<Resource>
        {
            public bool Equals([AllowNull] Resource x, [AllowNull] Resource y)
                => x?.SanitizedContent.Equals(y?.SanitizedContent) == true;
            public int GetHashCode([DisallowNull] Resource obj)
                => obj.SanitizedContent.GetHashCode();
        }

        private static async Task CralwerCulturaNegra(IBrowsingContext context)
        {
            var allResources = new List<Resource>();
            var options = new ChromeOptions();
            options.AddArguments("--disable-popup-blocking");
            options.AddArguments("--headless");
            using var driver = new ChromeDriver(options);
            var driverOptions = driver.Manage();
            driverOptions.Window.Maximize();
            driverOptions.Timeouts().ImplicitWait = TimeSpan.FromSeconds(20);
            //driver.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");

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
