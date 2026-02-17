using System.Diagnostics;
using System.Net.Http;
using HomeLinkMonitor.Models;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor.Services;

public interface IHttpProbe
{
    Task<HttpProbeResult> CheckAsync(AppConfig config, CancellationToken ct = default);
}

public class HttpProbe : IHttpProbe
{
    private readonly ILogger<HttpProbe> _logger;
    private readonly HttpClient _httpClient;

    // Microsoft captive portal test expected response
    private const string ExpectedResponse = "Microsoft Connect Test";

    public HttpProbe(ILogger<HttpProbe> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<HttpProbeResult> CheckAsync(AppConfig config, CancellationToken ct = default)
    {
        var result = new HttpProbeResult
        {
            Url = config.HttpProbeUrl
        };

        try
        {
            _httpClient.Timeout = TimeSpan.FromMilliseconds(config.HttpTimeoutMs);

            var sw = Stopwatch.StartNew();
            var response = await _httpClient.GetAsync(config.HttpProbeUrl, ct);
            sw.Stop();

            result.LatencyMs = sw.Elapsed.TotalMilliseconds;
            result.StatusCode = (int)response.StatusCode;
            result.IsSuccess = response.IsSuccessStatusCode;

            // Check for captive portal
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                result.IsCaptivePortal = !body.Contains(ExpectedResponse, StringComparison.OrdinalIgnoreCase)
                    && body.Length > 0;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Found
                  || response.StatusCode == System.Net.HttpStatusCode.MovedPermanently)
            {
                result.IsCaptivePortal = true;
            }
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            result.IsSuccess = false;
            result.Error = "Timeout";
        }
        catch (OperationCanceledException)
        {
            result.IsSuccess = false;
            result.Error = "Cancelled";
        }
        catch (HttpRequestException ex)
        {
            result.IsSuccess = false;
            result.Error = ex.Message;
            _logger.LogDebug(ex, "HTTP probe failed");
        }

        return result;
    }
}
