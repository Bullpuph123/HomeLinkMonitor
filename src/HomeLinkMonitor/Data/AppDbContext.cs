using System.IO;
using Microsoft.EntityFrameworkCore;
using HomeLinkMonitor.Models;

namespace HomeLinkMonitor.Data;

public class AppDbContext : DbContext
{
    public DbSet<WifiSnapshot> WifiSnapshots => Set<WifiSnapshot>();
    public DbSet<NetworkSnapshot> NetworkSnapshots => Set<NetworkSnapshot>();
    public DbSet<PingResult> PingResults => Set<PingResult>();
    public DbSet<DnsResult> DnsResults => Set<DnsResult>();
    public DbSet<HttpProbeResult> HttpProbeResults => Set<HttpProbeResult>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();
    public DbSet<RoamingEvent> RoamingEvents => Set<RoamingEvent>();

    private readonly string _dbPath;

    public AppDbContext()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HomeLinkMonitor");
        Directory.CreateDirectory(appData);
        _dbPath = Path.Combine(appData, "homelink.db");
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _dbPath = string.Empty;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={_dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // WifiSnapshots indexes
        modelBuilder.Entity<WifiSnapshot>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.Ssid);
        });

        // NetworkSnapshots indexes
        modelBuilder.Entity<NetworkSnapshot>(e =>
        {
            e.HasIndex(x => x.Timestamp);
        });

        // PingResults indexes
        modelBuilder.Entity<PingResult>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => new { x.Timestamp, x.TargetLabel });
        });

        // DnsResults indexes
        modelBuilder.Entity<DnsResult>(e =>
        {
            e.HasIndex(x => x.Timestamp);
        });

        // HttpProbeResults indexes
        modelBuilder.Entity<HttpProbeResult>(e =>
        {
            e.HasIndex(x => x.Timestamp);
        });

        // AlertEvents indexes
        modelBuilder.Entity<AlertEvent>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.AlertType);
        });

        // RoamingEvents indexes
        modelBuilder.Entity<RoamingEvent>(e =>
        {
            e.HasIndex(x => x.Timestamp);
        });
    }
}
