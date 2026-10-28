using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests;

public sealed class MiniLoggerUsbTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"minilogger-usb-{Guid.NewGuid():N}");

    public MiniLoggerUsbTests() => Directory.CreateDirectory(_workDir);

    [Fact]
    public void Handshake_accepts_logger_reply_after_banner_and_uses_safe_control_lines()
    {
        var transport = new FakeTransport("startup banner", "MINILOGGER 2\r\n");
        using var client = Client(transport);

        MiniLoggerConnectResult result = client.Connect();

        Assert.Equal(MiniLoggerConnectStatus.Connected, result.Status);
        Assert.Equal("COM7", result.PortName);
        Assert.Equal(("COM7", 115200, true, false), transport.OpenArguments);
        Assert.Equal(new[] { "id" }, transport.Writes);
        Assert.True(transport.InputDiscarded);
    }

    [Theory]
    [InlineData("ESP32 development board")]
    [InlineData(null)]
    public void Handshake_rejects_other_replies_and_timeouts(string? reply)
    {
        var transport = new FakeTransport(reply);
        using var client = Client(transport);

        MiniLoggerConnectResult result = client.Connect();

        Assert.Equal(MiniLoggerConnectStatus.NotFound, result.Status);
    }

    [Fact]
    public void Handshake_reports_busy_port_when_candidate_cannot_open()
    {
        var transport = new FakeTransport { OpenException = new UnauthorizedAccessException() };
        using var client = Client(transport);

        MiniLoggerConnectResult result = client.Connect();

        Assert.Equal(MiniLoggerConnectStatus.PortBusy, result.Status);
    }

    [Fact]
    public void List_parses_runs_skips_banner_and_handles_crlf()
    {
        var transport = ConnectedTransport(
            "unrelated banner\r\n", "Run 1: 25 KB\r\n", "Run 12: 1024 KB\r\n", "END\r\n");
        using var client = Client(transport);
        Assert.Equal(MiniLoggerConnectStatus.Connected, client.Connect().Status);

        IReadOnlyList<MiniLoggerRunInfo> runs = client.ListRuns();

        Assert.Equal(new[] { new MiniLoggerRunInfo(1, 25), new MiniLoggerRunInfo(12, 1024) }, runs);
        Assert.Equal(new[] { "id", "list" }, transport.Writes);
    }

    [Fact]
    public void List_handles_no_runs_saved()
    {
        var transport = ConnectedTransport("No runs saved", "END");
        using var client = Client(transport);
        client.Connect();

        Assert.Empty(client.ListRuns());
    }

    [Fact]
    public void Dump_writes_exact_csv_lines_and_skips_unrelated_lines()
    {
        const string row1 = "0.000,0.000,0,0.00";
        const string row2 = "0.001,1.520,15528,0.00";
        var transport = ConnectedTransport(
            "noise before begin", "BEGIN run_0003.csv\r\n", "banner",
            MiniLoggerLoader.HeaderLine + "\r\n", row1 + "\r\n", row2 + "\r\n", "END\r\n");
        using var client = Client(transport);
        client.Connect();
        string path = Path.Combine(_workDir, "run_0003.csv");

        MiniLoggerDumpResult result = client.DumpRun(new MiniLoggerRunInfo(3, 1), path);

        Assert.True(result.Found);
        Assert.Equal(path, result.FilePath);
        Assert.Equal(string.Join(Environment.NewLine,
            MiniLoggerLoader.HeaderLine, row1, row2, ""), File.ReadAllText(path));
        Assert.False(File.Exists(path + ".partial"));
    }

    [Fact]
    public void Dump_preserves_tc_header_and_extra_columns()
    {
        string header = MiniLoggerLoader.HeaderLine + ",Throttle Out (ms),TC State";
        const string row = "0.001,1.900,15528,2.00,1.700,2";
        var transport = ConnectedTransport("BEGIN run_0008.csv", header, row, "END");
        using var client = Client(transport);
        client.Connect();
        string path = Path.Combine(_workDir, "run_0008.csv");

        MiniLoggerDumpResult result = client.DumpRun(new MiniLoggerRunInfo(8, 1), path);

        Assert.True(result.Found);
        Assert.Equal(string.Join(Environment.NewLine, header, row, ""), File.ReadAllText(path));
    }

    [Fact]
    public void Dump_progress_is_throttled_for_long_runs()
    {
        // ~100,000 lines (~2.3 MB): reporting per line would post 100,000 UI updates.
        var replies = new List<string?> { "BEGIN run_0006.csv", MiniLoggerLoader.HeaderLine };
        for (int i = 0; i < 100_000; i++)
            replies.Add($"{i / 1000.0:0.000},1.520,15528,0.00");
        replies.Add("END");
        var transport = ConnectedTransport(replies.ToArray());
        using var client = Client(transport);
        client.Connect();
        var reports = new List<MiniLoggerDownloadProgress>();
        var progress = new SynchronousProgress(reports.Add);

        client.DumpRun(new MiniLoggerRunInfo(6, 2300), Path.Combine(_workDir, "run_0006.csv"), progress);

        long fileBytes = new FileInfo(Path.Combine(_workDir, "run_0006.csv")).Length;
        Assert.InRange(reports.Count, 2, fileBytes / MiniLoggerUsb.ProgressReportBytes + 2);
        Assert.Equal(reports.Max(r => r.BytesReceived), reports.Last().BytesReceived);
    }

    private sealed class SynchronousProgress(Action<MiniLoggerDownloadProgress> report) : IProgress<MiniLoggerDownloadProgress>
    {
        public void Report(MiniLoggerDownloadProgress value) => report(value);
    }

    [Fact]
    public void Dump_handles_run_not_found_without_creating_a_file()
    {
        var transport = ConnectedTransport("Run 9 not found\r\n");
        using var client = Client(transport);
        client.Connect();
        string path = Path.Combine(_workDir, "run_0009.csv");

        MiniLoggerDumpResult result = client.DumpRun(new MiniLoggerRunInfo(9, 1), path);

        Assert.False(result.Found);
        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".partial"));
    }

    [Fact]
    public void Cancel_deletes_partial_file()
    {
        var cancellation = new CancellationTokenSource();
        var transport = ConnectedTransport(
            "BEGIN run_0004.csv", MiniLoggerLoader.HeaderLine, "0.000,1.500,0,0.00", "END");
        using var client = Client(transport);
        client.Connect();
        transport.CancelAfterReads = 4;
        transport.Cancellation = cancellation;
        string path = Path.Combine(_workDir, "run_0004.csv");

        Assert.Throws<OperationCanceledException>(() => client.DumpRun(
            new MiniLoggerRunInfo(4, 1), path, cancellationToken: cancellation.Token));
        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".partial"));
    }

    [Fact]
    public void Timeout_mid_dump_reports_stopped_answering_and_deletes_partial_file()
    {
        var transport = ConnectedTransport(
            "BEGIN run_0005.csv", MiniLoggerLoader.HeaderLine, "0.000,1.500,0,0.00", null);
        using var client = Client(transport);
        client.Connect();
        string path = Path.Combine(_workDir, "run_0005.csv");

        MiniLoggerStoppedRespondingException error = Assert.Throws<MiniLoggerStoppedRespondingException>(
            () => client.DumpRun(new MiniLoggerRunInfo(5, 1), path));

        Assert.Contains("stopped answering", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(transport.ReadTimeouts.Last() >= TimeSpan.FromSeconds(15));
        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".partial"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
            Directory.Delete(_workDir, recursive: true);
    }

    private static FakeTransport ConnectedTransport(params string?[] replies) =>
        new(new string?[] { "MINILOGGER 2" }.Concat(replies).ToArray());

    private static MiniLoggerUsb Client(FakeTransport transport) =>
        new(new FakeDiscovery("COM7"), new FakeFactory(transport), new NoDelay());

    private sealed class FakeDiscovery(params string[] ports) : IMiniLoggerPortDiscovery
    {
        public IReadOnlyList<string> FindCandidatePorts() => ports;
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

    private sealed class FakeTransport : IMiniLoggerLineTransport
    {
        private readonly Queue<string?> _replies;
        private int _reads;

        public FakeTransport(params string?[] replies) => _replies = new Queue<string?>(replies);

        public (string port, int baud, bool dtr, bool rts) OpenArguments { get; private set; }
        public List<string> Writes { get; } = new();
        public bool InputDiscarded { get; private set; }
        public int CancelAfterReads { get; set; }
        public CancellationTokenSource? Cancellation { get; set; }
        public Exception? OpenException { get; set; }
        public List<TimeSpan> ReadTimeouts { get; } = new();

        public void Open(string portName, int baudRate, bool dtrEnable, bool rtsEnable)
        {
            if (OpenException is not null) throw OpenException;
            OpenArguments = (portName, baudRate, dtrEnable, rtsEnable);
        }
        public void DiscardInput() => InputDiscarded = true;
        public void WriteLine(string line) => Writes.Add(line);
        public string? ReadLine(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadTimeouts.Add(timeout);
            _reads++;
            string? result = _replies.Count == 0 ? null : _replies.Dequeue();
            if (CancelAfterReads > 0 && _reads >= CancelAfterReads)
                Cancellation?.Cancel();
            return result;
        }
        public void Close() { }
        public void Dispose() { }
    }
}
