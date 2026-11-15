using BoardWalk.Bot;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<BotOptions>(builder.Configuration.GetSection("Bot"));

// One HttpClient for the process's lifetime, pre-set with the API address and the shared secret.
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<BotOptions>>().Value;
    var http = new HttpClient { BaseAddress = new Uri(options.ApiBaseUrl) };
    http.DefaultRequestHeaders.Add("X-Bot-Key", options.ApiKey);
    return new BotApiClient(http);
});

builder.Services.AddSingleton<DumbStrategy>();
builder.Services.AddHostedService<BotWorker>();

await builder.Build().RunAsync();
