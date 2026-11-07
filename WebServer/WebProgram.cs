using System.Net;
using March7thHoney.Util;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;

namespace March7thHoney.WebServer;

public class WebProgram
{
    public static void Main(string[] args, int port, string address)
    {
        BuildWebHost(args, port, address).Start();
    }

    public static IHost BuildWebHost(string[] args, int port, string address)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseStartup<Startup>()
                    .ConfigureLogging((hostingContext, logging) => { logging.ClearProviders(); })
                    .UseUrls(address);

                if (ConfigManager.Config.HttpServer.UseSSL)
                    webBuilder.UseKestrel(options =>
                    {
                        options.Listen(IPAddress.Any, port, listenOptions =>
                        {
                            listenOptions.UseHttps(
                                ConfigManager.Config.KeyStore.KeyStorePath,
                                ConfigManager.Config.KeyStore.KeyStorePassword
                            );
                        });
                    });
            })
            .Build();
    }
}

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                builder =>
                {
                    builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
        });

        services.AddControllers();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

        app.Use(async (context, next) =>
        {
            Handler.SinglePlayerLogin.RememberPeer(context);
            await next();
        });
        app.UseForwardedHeaders();

        app.Use(async (context, next) =>
        {
            using var buffer = new MemoryStream();
            var request = context.Request;
            var response = context.Response;

            var bodyStream = response.Body;
            response.Body = buffer;

            try
            {
                await next.Invoke();
                buffer.Position = 0;

                if (!response.HasStarted)
                {
                    context.Response.Headers["Content-Length"] = (response.ContentLength ?? buffer.Length).ToString();
                    context.Response.Headers.Remove("Transfer-Encoding");
                }

                await buffer.CopyToAsync(bodyStream);
            }
            finally
            {
                response.Body = bodyStream;
            }
        });

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseCors("AllowAll");

        app.UseAuthorization();

        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
    }
}
