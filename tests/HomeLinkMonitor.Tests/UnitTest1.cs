using HomeLinkMonitor.Helpers;
using HomeLinkMonitor.Models;

namespace HomeLinkMonitor.Tests;

public class NetworkHelperTests
{
    [Theory]
    [InlineData(2412000, 1)]
    [InlineData(2437000, 6)]
    [InlineData(2462000, 11)]
    [InlineData(5180000, 36)]
    [InlineData(5240000, 48)]
    [InlineData(5745000, 149)]
    public void FrequencyToChannel_ReturnsCorrectChannel(int frequencyKHz, int expectedChannel)
    {
        var channel = NetworkHelper.FrequencyToChannel(frequencyKHz);
        Assert.Equal(expectedChannel, channel);
    }

    [Theory]
    [InlineData(2412000, "2.4 GHz")]
    [InlineData(5180000, "5 GHz")]
    [InlineData(5955000, "6 GHz")]
    [InlineData(1000000, "Unknown")]
    public void FrequencyToBand_ReturnsCorrectBand(int frequencyKHz, string expectedBand)
    {
        var band = NetworkHelper.FrequencyToBand(frequencyKHz);
        Assert.Equal(expectedBand, band);
    }

    [Theory]
    [InlineData(100, -50)]
    [InlineData(0, -100)]
    [InlineData(50, -75)]
    public void SignalQualityToRssi_ReturnsCorrectValue(int quality, int expectedRssi)
    {
        var rssi = NetworkHelper.SignalQualityToRssi(quality);
        Assert.Equal(expectedRssi, rssi);
    }

    [Theory]
    [InlineData(-32, 100)]   // Very strong signal, caps at 100
    [InlineData(-50, 100)]   // -50 dBm = 100%
    [InlineData(-75, 50)]    // Mid-range
    [InlineData(-100, 0)]    // -100 dBm = 0%
    [InlineData(-110, 0)]    // Below floor, caps at 0
    public void RssiToSignalQuality_ReturnsCorrectValue(int rssi, int expectedQuality)
    {
        var quality = NetworkHelper.RssiToSignalQuality(rssi);
        Assert.Equal(expectedQuality, quality);
    }

    [Theory]
    [InlineData(90, NetworkHelper.ConnectionQuality.Excellent)]
    [InlineData(70, NetworkHelper.ConnectionQuality.Good)]
    [InlineData(50, NetworkHelper.ConnectionQuality.Fair)]
    [InlineData(25, NetworkHelper.ConnectionQuality.Poor)]
    [InlineData(10, NetworkHelper.ConnectionQuality.VeryPoor)]
    public void ClassifySignal_ReturnsCorrectQuality(int quality, NetworkHelper.ConnectionQuality expected)
    {
        var result = NetworkHelper.ClassifySignal(quality);
        Assert.Equal(expected, result);
    }
}

public class CircularBufferTests
{
    [Fact]
    public void Add_AddsItemsUpToCapacity()
    {
        var buffer = new CircularBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        Assert.Equal(3, buffer.Count);
        Assert.True(buffer.IsFull);
    }

    [Fact]
    public void Add_OverwritesOldestWhenFull()
    {
        var buffer = new CircularBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);

        Assert.Equal(3, buffer.Count);
        Assert.Equal(2, buffer[0]);
        Assert.Equal(3, buffer[1]);
        Assert.Equal(4, buffer[2]);
    }

    [Fact]
    public void Enumeration_ReturnsItemsInOrder()
    {
        var buffer = new CircularBuffer<int>(5);
        buffer.Add(10);
        buffer.Add(20);
        buffer.Add(30);

        var items = buffer.ToList();
        Assert.Equal(new[] { 10, 20, 30 }, items);
    }

    [Fact]
    public void Clear_ResetsBuffer()
    {
        var buffer = new CircularBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.False(buffer.IsFull);
    }
}

public class AppConfigTests
{
    [Fact]
    public void DefaultConfig_HasExpectedValues()
    {
        var config = new AppConfig();

        Assert.Equal(5, config.PollingIntervalSeconds);
        Assert.Equal("Dark", config.Theme);
        Assert.True(config.MinimizeToTray);
        Assert.True(config.ShowNotifications);
        Assert.Equal("8.8.8.8", config.PrimaryDns);
        Assert.Equal("1.1.1.1", config.SecondaryDns);
        Assert.Equal(2000, config.PingTimeoutMs);
        Assert.Equal(30, config.AlertSignalLowThreshold);
        Assert.Equal(100, config.AlertLatencyHighMs);
        Assert.Equal(7, config.RawDataRetentionDays);
    }
}

public class ConnectionStatusTests
{
    [Fact]
    public void ConnectionStatus_HasAllExpectedValues()
    {
        var values = Enum.GetValues<ConnectionStatus>();
        Assert.Contains(ConnectionStatus.Excellent, values);
        Assert.Contains(ConnectionStatus.Good, values);
        Assert.Contains(ConnectionStatus.Fair, values);
        Assert.Contains(ConnectionStatus.Poor, values);
        Assert.Contains(ConnectionStatus.Disconnected, values);
        Assert.Contains(ConnectionStatus.NoInternet, values);
        Assert.Contains(ConnectionStatus.Unknown, values);
    }
}
