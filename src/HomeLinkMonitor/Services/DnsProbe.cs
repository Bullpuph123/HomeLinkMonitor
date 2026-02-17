using System.Diagnostics;
using System.Net;
using DnsClient;
using HomeLinkMonitor.Models;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor.Services;

public interface IDnsProbe
{
    Task<List<DnsResult>> QueryAllAsync(AppConfig config, CancellationToken ct = default);
}

public class DnsProbe : IDnsProbe
{
    private readonly ILogger<DnsProbe> _logger;

    public DnsProbe(ILogger<DnsProbe> logger)
    {
        _logger = logger;
    }

    public async Task<List<DnsResult>> QueryAllAsync(AppConfig config, CancellationToken ct = default)
    {
        var servers = new[]
        {
            config.PrimaryDns,
            config.SecondaryDns
        };

        var tasks = servers.Select(s => QuerySingleAsync(s, config.DnsQueryName, ct));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private async Task<DnsResult> QuerySingleAsync(string server, string queryName, CancellationToken ct)
    {
        var result = new DnsResult
        {
            DnsServer = server,
            QueryName = queryName
        };

        try
        {
            var endpoint = new IPEndPoint(IPAddress.Parse(server), 53);
            var options = new LookupClientOptions(endpoint)
            {
                Timeout = TimeSpan.FromSeconds(3),
                Retries = 0,
                UseCache = false
            };
            var client = new LookupClient(options);

            var sw = Stopwatch.StartNew();
            var response = await client.QueryAsync(queryName, QueryType.A, cancellationToken: ct);
            sw.Stop();

            result.LatencyMs = sw.Elapsed.TotalMilliseconds;
            result.IsSuccess = !response.HasError;
            if (response.HasError)
                result.Error = response.ErrorMessage;
        }
        catch (OperationCanceledException)
        {
            result.IsSuccess = false;
            result.Error = "Cancelled";
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.Error = ex.Message;
            _logger.LogDebug(ex, "DNS query failed for server {Server}", server);
        }

        return result;
    }
}
