using Microsoft.EntityFrameworkCore;
using HomeLinkMonitor.Models;

namespace HomeLinkMonitor.Data;

public class DataRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public DataRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task SaveSnapshotAsync(MonitoringSnapshot snapshot, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        if (snapshot.Wifi != null)
            db.WifiSnapshots.Add(snapshot.Wifi);

        if (snapshot.Network != null)
            db.NetworkSnapshots.Add(snapshot.Network);

        if (snapshot.PingResults.Count > 0)
            db.PingResults.AddRange(snapshot.PingResults);

        if (snapshot.DnsResults.Count > 0)
            db.DnsResults.AddRange(snapshot.DnsResults);

        if (snapshot.HttpProbe != null)
            db.HttpProbeResults.Add(snapshot.HttpProbe);

        await db.SaveChangesAsync(ct);
    }

    public async Task SaveAlertAsync(AlertEvent alert, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        db.AlertEvents.Add(alert);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveRoamingEventAsync(RoamingEvent roaming, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        db.RoamingEvents.Add(roaming);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PingResult>> GetPingResultsAsync(
        DateTime from, DateTime to, string? targetLabel = null, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var query = db.PingResults
            .Where(p => p.Timestamp >= from && p.Timestamp <= to);

        if (targetLabel != null)
            query = query.Where(p => p.TargetLabel == targetLabel);

        return await query.OrderBy(p => p.Timestamp).ToListAsync(ct);
    }

    public async Task<List<WifiSnapshot>> GetWifiSnapshotsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.WifiSnapshots
            .Where(w => w.Timestamp >= from && w.Timestamp <= to)
            .OrderBy(w => w.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<List<AlertEvent>> GetAlertsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.AlertEvents
            .Where(a => a.Timestamp >= from && a.Timestamp <= to)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<List<DnsResult>> GetDnsResultsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.DnsResults
            .Where(d => d.Timestamp >= from && d.Timestamp <= to)
            .OrderBy(d => d.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<List<RoamingEvent>> GetRoamingEventsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.RoamingEvents
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync(ct);
    }

    public async Task CleanupOldDataAsync(AppConfig config, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var rawCutoff = DateTime.UtcNow.AddDays(-config.RawDataRetentionDays);
        var alertCutoff = DateTime.UtcNow.AddDays(-config.AlertRetentionDays);

        await db.WifiSnapshots.Where(x => x.Timestamp < rawCutoff).ExecuteDeleteAsync(ct);
        await db.NetworkSnapshots.Where(x => x.Timestamp < rawCutoff).ExecuteDeleteAsync(ct);
        await db.PingResults.Where(x => x.Timestamp < rawCutoff).ExecuteDeleteAsync(ct);
        await db.DnsResults.Where(x => x.Timestamp < rawCutoff).ExecuteDeleteAsync(ct);
        await db.HttpProbeResults.Where(x => x.Timestamp < rawCutoff).ExecuteDeleteAsync(ct);
        await db.AlertEvents.Where(x => x.Timestamp < alertCutoff).ExecuteDeleteAsync(ct);
        await db.RoamingEvents.Where(x => x.Timestamp < rawCutoff).ExecuteDeleteAsync(ct);
    }
}
