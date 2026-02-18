using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using HomeLinkMonitor.Models;
using HomeLinkMonitor.Services;

namespace HomeLinkMonitor.Views;

public partial class TracerouteMapWindow : Window
{
    private readonly IReadOnlyList<TracerouteHop> _hops;
    private readonly string _target;
    private readonly IGeoIpService _geoIpService;
    private readonly AppConfig _config;
    private string? _tempHtmlPath;

    public TracerouteMapWindow(
        IReadOnlyList<TracerouteHop> hops,
        string target,
        IGeoIpService geoIpService,
        AppConfig config)
    {
        InitializeComponent();
        _hops = hops;
        _target = target;
        _geoIpService = geoIpService;
        _config = config;

        HeaderText.Text = $"Route map to {_target} — {_hops.Count} hops";
        StatusText.Text = "Loading map...";

        Loaded += OnLoaded;
    }

    private record HopLocation(
        TracerouteHop Hop,
        double Lat, double Lon,
        string City, string Country, string Isp, string Source);

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await MapWebView.EnsureCoreWebView2Async();

            StatusText.Text = "Looking up IP locations...";

            var ips = _hops.Select(h => h.Address).ToList();
            var geoResults = await _geoIpService.LookupBatchAsync(ips);

            var geoLookup = geoResults
                .Where(r => r.Status == "success" && r.Lat.HasValue && r.Lon.HasValue)
                .ToDictionary(r => r.Query, r => r);

            // Build locations: prefer hostname-parsed coords, fall back to ip-api.com
            var locatedHops = new List<HopLocation>();
            int hostnameCount = 0;

            foreach (var hop in _hops)
            {
                var parsed = HostnameGeoParser.TryParse(hop.HostName);
                var hasGeo = geoLookup.TryGetValue(hop.Address, out var geo);

                if (parsed != null)
                {
                    var isp = hasGeo ? geo!.Isp ?? "Unknown" : "Unknown";
                    var city = string.IsNullOrEmpty(parsed.Region)
                        ? parsed.City
                        : $"{parsed.City}, {parsed.Region}";
                    locatedHops.Add(new HopLocation(
                        hop, parsed.Lat, parsed.Lon,
                        city, parsed.Country, isp, "hostname"));
                    hostnameCount++;
                }
                else if (hasGeo)
                {
                    var city = geo!.City ?? "Unknown";
                    if (!string.IsNullOrEmpty(geo.RegionName))
                        city += $", {geo.RegionName}";
                    locatedHops.Add(new HopLocation(
                        hop, geo.Lat!.Value, geo.Lon!.Value,
                        city, geo.Country ?? "Unknown", geo.Isp ?? "Unknown", "geoip"));
                }
            }

            if (locatedHops.Count == 0)
            {
                MapWebView.NavigateToString(GenerateEmptyHtml());
                StatusText.Text = "No geolocatable hops found (all addresses are private/local)";
                return;
            }

            var mapHtml = GenerateMapHtml(locatedHops);
            NavigateToTempFile(mapHtml);
            StatusText.Text = $"Showing {locatedHops.Count} of {_hops.Count} hops on map ({hostnameCount} from hostname)";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void NavigateToTempFile(string html)
    {
        _tempHtmlPath = Path.Combine(Path.GetTempPath(), $"homelink_map_{Guid.NewGuid():N}.html");
        File.WriteAllText(_tempHtmlPath, html, Encoding.UTF8);
        MapWebView.CoreWebView2.Navigate(new Uri(_tempHtmlPath).AbsoluteUri);
    }

    protected override void OnClosed(EventArgs e)
    {
        MapWebView.Dispose();
        if (_tempHtmlPath != null)
        {
            try { File.Delete(_tempHtmlPath); } catch { }
        }
        base.OnClosed(e);
    }

    private string GenerateMapHtml(List<HopLocation> locatedHops)
    {
        bool isDark = _config.Theme == "Dark";
        var tileUrl = isDark
            ? "https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png"
            : "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
        var tileAttrib = isDark
            ? "&copy; <a href=\"https://www.openstreetmap.org/copyright\">OSM</a> &copy; <a href=\"https://carto.com/\">CARTO</a>"
            : "&copy; <a href=\"https://www.openstreetmap.org/copyright\">OpenStreetMap</a>";

        var bgColor = isDark ? "#1a1a2e" : "#ffffff";
        var textColor = isDark ? "#e0e0e0" : "#333333";
        var popupBg = isDark ? "#2d2d44" : "#ffffff";
        var popupBorder = isDark ? "#444466" : "#cccccc";
        var lineColor = isDark ? "#00d4ff" : "#0066cc";
        var markerBg = isDark ? "#00d4ff" : "#0066cc";
        var markerText = isDark ? "#1a1a2e" : "#ffffff";

        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        // Build markers JS
        sb.Append("var markers = [];\n");
        foreach (var hl in locatedHops)
        {
            var lat = hl.Lat.ToString(inv);
            var lon = hl.Lon.ToString(inv);
            var latency = hl.Hop.LatencyMs.HasValue ? $"{hl.Hop.LatencyMs.Value:F1} ms" : "N/A";
            var city = EscapeJs(hl.City);
            var country = EscapeJs(hl.Country);
            var isp = EscapeJs(hl.Isp);
            var address = EscapeJs(hl.Hop.Address);
            var hostname = EscapeJs(hl.Hop.HostName);

            sb.Append($@"
(function() {{
    var icon = L.divIcon({{
        className: 'hop-marker',
        html: '<div class=""hop-circle"">{hl.Hop.Hop}</div>',
        iconSize: [28, 28],
        iconAnchor: [14, 14],
        popupAnchor: [0, -16]
    }});
    var m = L.marker([{lat}, {lon}], {{icon: icon}}).addTo(map);
    m.bindPopup('<div class=""hop-popup"">' +
        '<b>Hop {hl.Hop.Hop}</b><br>' +
        'IP: {address}<br>' +
        'Host: {hostname}<br>' +
        'Latency: {latency}<br>' +
        'Location: {city}, {country}<br>' +
        'ISP: {isp}' +
        '</div>');
    markers.push(m);
}})();
");
        }

        // Build polyline coords
        var coords = string.Join(",", locatedHops.Select(h =>
            $"[{h.Lat.ToString(inv)},{h.Lon.ToString(inv)}]"));

        var html = $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
    body {{ margin: 0; padding: 0; background: {bgColor}; color: {textColor}; }}
    #map {{ width: 100%; height: 100vh; }}
    .hop-circle {{
        width: 28px; height: 28px;
        background: {markerBg};
        color: {markerText};
        border-radius: 50%;
        display: flex; align-items: center; justify-content: center;
        font-weight: bold; font-size: 12px;
        border: 2px solid {(isDark ? "#ffffff33" : "#00000033")};
        box-shadow: 0 2px 6px rgba(0,0,0,0.3);
    }}
    .hop-marker {{ background: transparent; border: none; }}
    .leaflet-popup-content-wrapper {{
        background: {popupBg} !important;
        color: {textColor} !important;
        border: 1px solid {popupBorder} !important;
        border-radius: 8px !important;
        box-shadow: 0 3px 12px rgba(0,0,0,0.3) !important;
    }}
    .leaflet-popup-tip {{ background: {popupBg} !important; }}
    .hop-popup {{ font-family: 'Segoe UI', sans-serif; font-size: 13px; line-height: 1.5; }}
    .hop-popup b {{ color: {markerBg}; }}
</style>
</head>
<body>
<div id='map'></div>
<script>
var map = L.map('map', {{ zoomControl: true }});
L.tileLayer('{tileUrl}', {{
    attribution: '{tileAttrib}',
    maxZoom: 18
}}).addTo(map);

{sb}

var polyline = L.polyline([{coords}], {{
    color: '{lineColor}',
    weight: 2,
    opacity: 0.7,
    dashArray: '8, 6'
}}).addTo(map);

if (markers.length > 0) {{
    var group = L.featureGroup(markers);
    map.fitBounds(group.getBounds().pad(0.15));
}}
</script>
</body>
</html>";

        return html;
    }

    private string GenerateEmptyHtml()
    {
        bool isDark = _config.Theme == "Dark";
        var bgColor = isDark ? "#1a1a2e" : "#ffffff";
        var textColor = isDark ? "#888899" : "#666666";

        return $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'>
<style>
    body {{
        margin: 0; padding: 0;
        background: {bgColor};
        display: flex; align-items: center; justify-content: center;
        height: 100vh;
        font-family: 'Segoe UI', sans-serif;
    }}
    .msg {{ color: {textColor}; font-size: 18px; text-align: center; }}
</style>
</head>
<body>
<div class='msg'>No Geolocatable Hops<br><span style='font-size:14px;'>All addresses in this trace are private or local.</span></div>
</body>
</html>";
    }

    private static string EscapeJs(string s)
    {
        return s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
    }
}
