using CastleOverlayV2.Models;
using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests;

public sealed class TcLoggerProtocolTests
{
    [Theory]
    [InlineData("DISARMED", TcLoggerState.Disarmed)]
    [InlineData("ACTIVE", TcLoggerState.Active)]
    public void Parses_tc_status_sample(string stateText, TcLoggerState expectedState)
    {
        TcLoggerStatus status = TcStatusParser.Parse(
            new[] { "TC logger running, recording run 42" }.Concat(Status(stateText, 12)));

        Assert.True(status.IsTcLogger);
        Assert.Equal(expectedState, status.State);
        Assert.Equal(1520, status.NeutralUs);
        Assert.Equal(1900, status.FullUs);
        Assert.Equal(1100, status.BrakeUs);
        Assert.Equal(12, status.CurvePointCount);
        Assert.Equal(20, status.Settings["launchPct"]);
        Assert.Equal(0.02, status.Settings["gainUsPerRpm"], 6);
    }

    [Fact]
    public void Commands_reply_is_not_a_tc_logger()
    {
        TcLoggerStatus status = TcStatusParser.Parse(new[]
        {
            "Commands: status, live, list, dump N, dumpall, erase all"
        });

        Assert.False(status.IsTcLogger);
    }

    [Fact]
    public void Send_curve_succeeds_and_ignores_banner_lines()
    {
        var replies = new List<string?> { "MINILOGGER 2" };
        replies.Add("TC logger running, recording run 42");
        replies.AddRange(Status("DISARMED", 0));
        replies.AddRange(new[] { "TC logger running", "OK", "banner", "OK", "OK", "OK 2 points" });
        replies.AddRange(Status("DISARMED", 2));
        var transport = new FakeTransport(replies.ToArray());
        using var client = Client(transport);
        client.Connect();

        TcCurveTransferResult result = client.SendTcCurve(new[]
        {
            new TcCurvePoint(0, 10000),
            new TcCurvePoint(1000, 20000)
        });

        Assert.True(result.Success, result.Message);
        Assert.Equal(new[]
        {
            "id", "tc status", "tc curve begin", "tc curve pt 0 10000",
            "tc curve pt 1000 20000", "tc curve end", "tc status"
        }, transport.Writes);
    }

    [Fact]
    public void Error_on_point_aborts_without_end()
    {
        var transport = TransportForSend(Status("DISARMED", 0), "OK", "OK", "ERROR times must rise");
        using var client = Client(transport);
        client.Connect();

        TcCurveTransferResult result = client.SendTcCurve(new[]
        {
            new TcCurvePoint(0, 10000), new TcCurvePoint(1000, 20000)
        });

        Assert.False(result.Success);
        Assert.DoesNotContain("tc curve end", transport.Writes);
    }

    [Fact]
    public void Busy_on_begin_aborts()
    {
        var transport = TransportForSend(Status("DISARMED", 0), "ERROR busy: a pass is running");
        using var client = Client(transport);
        client.Connect();

        TcCurveTransferResult result = client.SendTcCurve(new[] { new TcCurvePoint(0, 10000) });

        Assert.False(result.Success);
        Assert.DoesNotContain(transport.Writes, write => write.StartsWith("tc curve pt", StringComparison.Ordinal));
        Assert.DoesNotContain("tc curve end", transport.Writes);
    }

    [Fact]
    public void Count_mismatch_after_end_is_failure()
    {
        var replies = new List<string?> { "MINILOGGER 2" };
        replies.AddRange(Status("DISARMED", 0));
        replies.AddRange(new[] { "OK", "OK", "OK 1 points" });
        replies.AddRange(Status("DISARMED", 0));
        var transport = new FakeTransport(replies.ToArray());
        using var client = Client(transport);
        client.Connect();

        TcCurveTransferResult result = client.SendTcCurve(new[] { new TcCurvePoint(0, 10000) });

        Assert.False(result.Success);
        Assert.Contains("reports 0", result.Message);
    }

    [Fact]
    public void Active_status_refuses_before_begin()
    {
        var transport = TransportForSend(Status("ACTIVE", 10));
        using var client = Client(transport);
        client.Connect();

        TcCurveTransferResult result = client.SendTcCurve(new[] { new TcCurvePoint(0, 10000) });

        Assert.False(result.Success);
        Assert.Contains("ACTIVE", result.Message);
        Assert.DoesNotContain("tc curve begin", transport.Writes);
    }

    [Fact]
    public void Clear_curve_checks_status_and_confirms_zero_points()
    {
        var replies = new List<string?> { "MINILOGGER 2" };
        replies.AddRange(Status("DISARMED", 4));
        replies.Add("OK 0 points");
        replies.AddRange(Status("DISARMED", 0));
        var transport = new FakeTransport(replies.ToArray());
        using var client = Client(transport);
        client.Connect();

        TcCurveTransferResult result = client.ClearTcCurve();

        Assert.True(result.Success, result.Message);
        Assert.Equal(new[] { "id", "tc status", "tc curve clear", "tc status" }, transport.Writes);
    }

    private static string[] Status(string state, int points) =>
    [
        $"TC {state} | in 1520 us | out 1518 us | ceiling none | rpm 12345 | curve {points} points",
        "  neutral 1520 us | full 1900 us | brake 1100 us | pulses sent 9 | pulses cut 2 | signal timeouts 0 | log lost 0",
        "  neutralUs 1500",
        "  fullUs 1900",
        "  brakeUs 1100",
        "  launchPct 20",
        "  gainUsPerRpm 0.02",
        "  firmware tc_logger Oct 29 2026 12:34:56",
        "END"
    ];

    private static FakeTransport TransportForSend(IEnumerable<string> status, params string[] commandReplies) =>
        new(new string?[] { "MINILOGGER 2" }.Concat(status).Concat(commandReplies.Cast<string?>()).ToArray());

    private static MiniLoggerUsb Client(FakeTransport transport) =>
        new(new FakeDiscovery(), new FakeFactory(transport), new NoDelay());

    private sealed class FakeDiscovery : IMiniLoggerPortDiscovery
    {
        public IReadOnlyList<string> FindCandidatePorts() => new[] { "COM7" };
    }

    private sealed class FakeFactory(FakeTransport transport) : IMiniLoggerTransportFactory
    {
        public IMiniLoggerLineTransport Create() => transport;
    }

    private sealed class NoDelay : IMiniLoggerDelay
    {
        public void Wait(TimeSpan delay, CancellationToken cancellationToken) =>
            cancellationToken.ThrowIfCancellationRequested();
    }

    private sealed class FakeTransport(params string?[] replies) : IMiniLoggerLineTransport
    {
        private readonly Queue<string?> _replies = new(replies);
        public List<string> Writes { get; } = new();
        public void Open(string portName, int baudRate, bool dtrEnable, bool rtsEnable) { }
        public void DiscardInput() { }
        public void WriteLine(string line) => Writes.Add(line);
        public string? ReadLine(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _replies.Count == 0 ? null : _replies.Dequeue();
        }
        public void Close() { }
        public void Dispose() { }
    }
}
