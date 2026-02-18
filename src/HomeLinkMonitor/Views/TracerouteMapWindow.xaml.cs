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

            var locatedHops = _hops
                .Where(h => geoLookup.ContainsKey(h.Address))
                .Select(h => (Hop: h, Geo: geoLookup[h.Address]))
                .ToList();

            if (locatedHops.Count == 0)
            {
                var html = GenerateEmptyHtml();
                MapWebView.NavigateToString(html);
                StatusText.Text = "No geolocatable hops found (all addresses are private/local)";
                return;
            }

            var mapHtml = GenerateMapHtml(locatedHops);
            MapWebView.NavigateToString(mapHtml);
            StatusText.Text = $"Showing {locatedHops.Count} of {_hops.Count} hops on map";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private string GenerateMapHtml(List<(TracerouteHop Hop, GeoIpResult Geo)> locatedHops)
    {
        bool isDark = _config.Theme == "Dark";
        var tileUrl = isDark
            ? "https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png"
            : "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
        var tileAttrib = isDark
            ? "&copy; <a href='https://www.openstreetmap.org/copyright'>OSM</a> &copy; <a href='https://carto.com/'>CARTO</a>"
            : "&copy; <a href='https://www.openstreetmap.org/copyright'>OpenStreetMap</a>";

        var bgColor = isDark ? "#1a1a2e" : "#ffffff";
        var textColor = isDark ? "#e0e0e0" : "#333333";
        var popupBg = isDark ? "#2d2d44" : "#ffffff";
        var popupBorder = isDark ? "#444466" : "#cccccc";
        var lineColor = isDark ? "#00d4ff" : "#0066cc";
        var markerBg = isDark ? "#00d4ff" : "#0066cc";
        var markerText = isDark ? "#1a1a2e" : "#ffffff";

        var sb = new StringBuilder();

        // Build markers JS
        sb.Append("var markers = [];\n");
        foreach (var (hop, geo) in locatedHops)
        {
            var lat = geo.Lat!.Value;
            var lon = geo.Lon!.Value;
            var latency = hop.LatencyMs.HasValue ? $"{hop.LatencyMs.Value:F1} ms" : "N/A";
            var city = EscapeJs(geo.City ?? "Unknown");
            var country = EscapeJs(geo.Country ?? "Unknown");
            var isp = EscapeJs(geo.Isp ?? "Unknown");
            var address = EscapeJs(hop.Address);
            var hostname = EscapeJs(hop.HostName);

            sb.Append($@"
(function() {{
    var icon = L.divIcon({{
        className: 'hop-marker',
        html: '<div class=""hop-circle"">{hop.Hop}</div>',
        iconSize: [28, 28],
        iconAnchor: [14, 14],
        popupAnchor: [0, -16]
    }});
    var m = L.marker([{lat}, {lon}], {{icon: icon}}).addTo(map);
    m.bindPopup('<div class=""hop-popup"">' +
        '<b>Hop {hop.Hop}</b><br>' +
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
        var coords = string.Join(",", locatedHops.Select(h => $"[{h.Geo.Lat!.Value},{h.Geo.Lon!.Value}]"));

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
