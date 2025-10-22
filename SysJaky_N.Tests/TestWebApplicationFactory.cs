using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using SysJaky_N.Services.Pohoda;

namespace SysJaky_N.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<IPohodaClient> PohodaClientMock { get; } = new(MockBehavior.Strict);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IPohodaClient));
            services.AddSingleton(_ => PohodaClientMock.Object);

            services.RemoveAll(typeof(IHostedService));
        });
    }
}
