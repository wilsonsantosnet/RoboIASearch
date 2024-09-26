
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoboIASearch;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostCtx, services) =>
    {
        var config = hostCtx.Configuration.GetSection("WorkerConfig");
        services.Configure<WorkerConfig>(config);
        services.AddSingleton<IAServices>();
        services.AddHttpClient("IAClient", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // Define o timeout para 5 minutos
        });
    })
    .Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;
await services.GetRequiredService<IAServices>().Run(args);
