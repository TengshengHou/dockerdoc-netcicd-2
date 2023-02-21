using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Xunit;

public sealed class UnitTest1
{
    private const ushort HttpPort = 80;

    private readonly CancellationTokenSource _cts = new(TimeSpan.FromMinutes(1));

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts.Dispose();
    }

    [Fact]
    public async Task Test1()
    {
        IDockerNetwork _network;

        _network = new TestcontainersNetworkBuilder()
               .WithName(Guid.NewGuid().ToString("D"))
               .Build();
        await _network.CreateAsync(_cts.Token)
           .ConfigureAwait(false);


        IDockerContainer _dbContainer;
        _dbContainer = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("postgres")
            .WithNetwork(_network)
            .WithNetworkAliases("db")
            .WithVolumeMount("postgres-data", "/var/lib/postgresql/data")
            .Build();
        await _dbContainer.StartAsync(_cts.Token)
            .ConfigureAwait(false);

        IDockerContainer _appContainer;
        _appContainer = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("dotnet-docker")
            .WithNetwork(_network)
            .WithPortBinding(HttpPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(HttpPort))
            .Build();
        await _appContainer.StartAsync(_cts.Token)
           .ConfigureAwait(false);

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new UriBuilder("http", _appContainer.Hostname, _appContainer.GetMappedPublicPort(HttpPort)).Uri;

        var httpResponseMessage = await httpClient.GetAsync(string.Empty)
            .ConfigureAwait(false);

        var body = await httpResponseMessage.Content.ReadAsStringAsync()
            .ConfigureAwait(false);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        Assert.Contains("Welcome", body);
    }
}