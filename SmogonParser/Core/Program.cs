using System.Collections.ObjectModel;
using Microsoft.Playwright;

Console.WriteLine("Starting Smogon Parser");

const string BaseUrl = "https://www.smogon.com";
const string listUrl = "/dex/ss/pokemon";

using var playwright = await Playwright.CreateAsync();
var chromium = playwright.Chromium;
// Make sure to run headed.
var browser = await chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });

// Setup context however you like.
var context = await browser.NewContextAsync(); // Pass any options

// Pause the page, and start recording manually.
var page = await context.NewPageAsync();

async Task<Collection<string>> GetRoutes()
{
    await page.GotoAsync(BaseUrl + listUrl);
    var locator = page.Locator(".PokemonAltRow-name a");
    Collection<string> routes = new Collection<string>();

    int lastCount = 0;
    do
    {
        lastCount = routes.Count;
        for (var i = 0; i < await locator.CountAsync(); i++)
        {
            var str = await locator.Nth(i).GetAttributeAsync("href");
            if (string.IsNullOrWhiteSpace(str))
            {
                continue;
            }

            if (routes.Contains(str))
            {
                continue;
            }

            routes.Add(str);
        }

        await page.Mouse.WheelAsync(0, 300);
    } while (routes.Count != lastCount);

    return routes;
}

async Task<(string, string, string)> TryOtherGen()
{
    var otherGen = page.Locator(".OtherGensList ul li");
    string eligibleRoute = null;
    for (var i = 0; i < await otherGen.CountAsync(); i++)
    {
        var link = otherGen.Nth(i).Locator("a");
        if (await link.CountAsync() < 1)
        {
            break;
        }
        var genLink = await link.GetAttributeAsync("href");
        if (string.IsNullOrWhiteSpace(genLink))
        {
            break;
        }

        eligibleRoute = genLink;
    }

    if (eligibleRoute == null)
    {
        return (page.Url.Split("/")[^2], "", "");
    }

    return await GetInfo(eligibleRoute);
}

async Task<(string, string, string)> GetInfo(string route)
{
    await page.GotoAsync(BaseUrl + route);
    var tierLocator = page.Locator(".FormatList li a").First;

    if (await tierLocator.CountAsync() < 1)
    {
        Console.WriteLine($"Could not find tier for {route}. Trying other gens");
        return await TryOtherGen();
    }

    var tier = await tierLocator.InnerTextAsync();

    var exportButton = page.Locator(".ExportButton").First;
    if (await exportButton.CountAsync() < 1)
    {
        Console.WriteLine($"Could not find moveset for {route}. Trying other gens");
        return await TryOtherGen();
    }

    await exportButton.ScrollIntoViewIfNeededAsync();

    await exportButton.ClickAsync();

    var moveSet = await page.Locator(".BlockMovesetInfo textarea").First.InputValueAsync();

    return (page.Url.Split("/")[^2], tier, moveSet);
}



var infos = new List<(string name, string tier, string moveSet)>();
var routes = await GetRoutes();
foreach (var route in routes)
{
    infos.Add(await GetInfo(route));
}

foreach (var info in infos)
{
    var path = $"output/{info.tier}";
    Directory.CreateDirectory(path);
    File.WriteAllText($"{path}/{info.name}.txt", info.moveSet);
}

Console.WriteLine("end");