namespace HomeLinkMonitor.Models;

public record GeoIpResult(
    string Query,
    string Status,
    string? Country,
    string? RegionName,
    string? City,
    double? Lat,
    double? Lon,
    string? Isp,
    string? Org);
