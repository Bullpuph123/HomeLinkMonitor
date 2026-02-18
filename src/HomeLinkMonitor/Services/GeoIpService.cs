using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using HomeLinkMonitor.Models;

namespace HomeLinkMonitor.Services;

public interface IGeoIpService
{
    Task<List<GeoIpResult>> LookupBatchAsync(IEnumerable<string> ips, CancellationToken ct = default);
    bool IsPrivateOrLocalIp(string ip);
}

public class GeoIpService : IGeoIpService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeoIpService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<GeoIpResult>> LookupBatchAsync(IEnumerable<string> ips, CancellationToken ct = default)
    {
        var publicIps = ips
            .Where(ip => !IsPrivateOrLocalIp(ip))
            .Distinct()
            .Take(100)
            .ToList();

        if (publicIps.Count == 0)
            return [];

        var json = JsonSerializer.Serialize(publicIps);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("http://ip-api.com/batch", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<GeoIpResult>>(responseJson, _jsonOptions) ?? [];
    }

    public bool IsPrivateOrLocalIp(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || ip == "*")
            return true;

        if (!IPAddress.TryParse(ip, out var addr))
            return true;

        var bytes = addr.GetAddressBytes();
        if (bytes.Length != 4)
            return true; // IPv6 — skip for geo lookup

        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || bytes[0] == 127
            || (bytes[0] == 169 && bytes[1] == 254);
    }
}
