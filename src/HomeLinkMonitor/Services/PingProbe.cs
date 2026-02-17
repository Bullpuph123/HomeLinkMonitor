using System.Diagnostics;
using System.Net.NetworkInformation;
using HomeLinkMonitor.Helpers;
using HomeLinkMonitor.Models;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor.Services;

public interface IPingProbe
{
    Task<List<PingResult>> PingAllTargetsAsync(AppConfig config, CancellationToken ct = default);
}

public class PingProbe : IPingProbe
{
    private readonly ILogger<PingProbe> _logger;

    public PingProbe(ILogger<PingProbe> logger)
    {
        _logger = logger;
    }

    public async Task<List<PingResult>> PingAllTargetsAsync(AppConfig config, CancellationToken ct = default)
    {
        var targets = new List<(string address, string label)>();

        // Auto-detect gateway
        var gateway = NetworkHelper.GetDefaultGateway();
        if (gateway != null)
            targets.Add((gateway, "Gateway"));

        // DNS servers
        targets.Add((config.PrimaryDns, "DNS1"));
        targets.Add((config.SecondaryDns, "DNS2"));

        // Custom targets
        foreach (var custom in config.CustomPingTargets)
        {
            if (!string.IsNullOrWhiteSpace(custom))
                targets.Add((custom, "Custom"));
        }

        var tasks = targets.Select(t => PingSingleAsync(t.address, t.label, config.PingTimeoutMs, ct));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private async Task<PingResult> PingSingleAsync(string target, string label, int timeoutMs, CancellationToken ct)
    {
        var result = new PingResult
        {
            Target = target,
            TargetLabel = label
        };

        try
        {
            using var ping = new Ping();
            var sw = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(target, timeoutMs);
            sw.Stop();

            result.LatencyMs = reply.Status == IPStatus.Success ? sw.Elapsed.TotalMilliseconds : null;
            result.IsSuccess = reply.Status == IPStatus.Success;
            result.Status = reply.Status.ToString();
            result.Ttl = reply.Options?.Ttl ?? 0;
        }
        catch (PingException ex)
        {
            result.IsSuccess = false;
            result.Status = "Error";
            _logger.LogDebug(ex, "Ping failed for {Target}", target);
        }
        catch (OperationCanceledException)
        {
            result.IsSuccess = false;
            result.Status = "Cancelled";
        }

        return result;
    }
}
