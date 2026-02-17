using HomeLinkMonitor.Data;
using HomeLinkMonitor.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeLinkMonitor.Services;

public class DataRetentionService : BackgroundService
{
    private readonly DataRepository _repository;
    private readonly AppConfig _config;
    private readonly ILogger<DataRetentionService> _logger;

    public DataRetentionService(
        DataRepository repository,
        AppConfig config,
        ILogger<DataRetentionService> logger)
    {
        _repository = repository;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay before first cleanup
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Running data retention cleanup");
                await _repository.CleanupOldDataAsync(_config, stoppingToken);
                _logger.LogInformation("Data retention cleanup complete");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data retention cleanup");
            }

            // Run every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
