using LiteDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using UrlShortnerService.Models;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(builder =>
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILiteDatabase, LiteDatabase>((sp) =>
            {
                var db = new LiteDatabase("url-shortener.db");
                var collection = db.GetCollection<UrlShortnerLink>(BsonAutoId.Int32);
                collection.EnsureIndex(p => p.Url);
                collection.Upsert(new UrlShortnerLink
                {
                    Id = 100_000,
                    Url = "https://www.google.com/",
                });
                return db;
            });
            services.AddRouting();
        })
        .Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints((endpoints) =>
            {
                endpoints.MapPost("/shorten", CreateShortenUrl);
                endpoints.MapFallback(GetUrl);
            });
        });
    })
    .Build();

await host.RunAsync();

static Task WriteResponse(HttpContext context, int status, string response)
{
    context.Response.StatusCode = status;
    return context.Response.WriteAsync(response);
}

static Task CreateShortenUrl(HttpContext context)
{
    // Retrieve our dependencies
    var db = context.RequestServices.GetService<ILiteDatabase>();
    var collection = db.GetCollection<UrlShortnerLink>(nameof(UrlShortnerLink));

    // Perform basic form validation
    if (!context.Request.HasFormContentType || !context.Request.Form.ContainsKey("url"))
    {
        return WriteResponse(context, StatusCodes.Status400BadRequest, "Cannot process request.");
    }
    else
    {
        context.Request.Form.TryGetValue("url", out var formData);
        var requestedUrl = formData.ToString();

        // Test our URL
        if (!Uri.TryCreate(requestedUrl, UriKind.Absolute, out Uri result))
        {
            return WriteResponse(context, StatusCodes.Status400BadRequest, "Could not understand URL");
        }

        var url = result.ToString();
        var entry = collection.Find(p => p.Url == url).FirstOrDefault();

        if (entry is null)
        {
            entry = new UrlShortnerLink
            {
                Url = url
            };
            collection.Insert(entry);
        }

        var urlChunk = entry.GetShortUrl();
        var responseUri = $"{context.Request.Scheme}://{context.Request.Host}/{urlChunk}";
        context.Response.Redirect($"/#{responseUri}");
        return Task.CompletedTask;
    }
}

static Task GetUrl(HttpContext context)
{
    if (context.Request.Path == "/")
    {
        return context.Response.SendFileAsync("wwwroot/index.htm");
    }

    // Default to home page if no matching url.
    var redirect = "/";

    var db = context.RequestServices.GetService<ILiteDatabase>();
    var collection = db.GetCollection<UrlShortnerLink>();

    var path = context.Request.Path.ToUriComponent().Trim('/');
    var id = UrlShortnerLink.GetId(path);
    var entry = collection.Find(p => p.Id == id).SingleOrDefault();

    if (entry is not null)
    {
        redirect = entry.Url;
    }

    context.Response.Redirect(redirect);
    return Task.CompletedTask;
}
