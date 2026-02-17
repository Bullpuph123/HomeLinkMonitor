using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor.Services;

public interface ITracerouteService
{
    IAsyncEnumerable<TracerouteHop> RunAsync(string target, int maxHops = 30, int timeoutMs = 3000, CancellationToken ct = default);
}

public record TracerouteHop(int Hop, string Address, string HostName, double? LatencyMs, bool IsTimeout);

public class TracerouteService : ITracerouteService
{
    private readonly ILogger<TracerouteService> _logger;

    public TracerouteService(ILogger<TracerouteService> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<TracerouteHop> RunAsync(
        string target,
        int maxHops = 30,
        int timeoutMs = 3000,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var buffer = new byte[32];
        Array.Fill(buffer, (byte)0x40);

        for (int ttl = 1; ttl <= maxHops; ttl++)
        {
            if (ct.IsCancellationRequested) yield break;

            TracerouteHop hop;
            try
            {
                using var ping = new Ping();
                var options = new PingOptions(ttl, true);
                var sw = Stopwatch.StartNew();
                var reply = await ping.SendPingAsync(target, timeoutMs, buffer, options);
                sw.Stop();

                if (reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired)
                {
                    var address = reply.Address.ToString();
                    string hostName = address;
                    try
                    {
                        var hostEntry = await Dns.GetHostEntryAsync(reply.Address);
                        hostName = hostEntry.HostName;
                    }
                    catch
                    {
                        // DNS reverse lookup failed, use IP
                    }

                    hop = new TracerouteHop(ttl, address, hostName, sw.Elapsed.TotalMilliseconds, false);
                }
                else
                {
                    hop = new TracerouteHop(ttl, "*", "*", null, true);
                }
            }
            catch (PingException)
            {
                hop = new TracerouteHop(ttl, "*", "*", null, true);
            }

            yield return hop;

            // Stop if we reached the destination
            if (hop.Address != "*")
            {
                try
                {
                    var targetAddresses = await Dns.GetHostAddressesAsync(target);
                    if (targetAddresses.Any(a => a.ToString() == hop.Address))
                        yield break;
                }
                catch
                {
                    // If DNS fails, compare string directly
                    if (hop.Address == target)
                        yield break;
                }
            }
        }
    }
}
