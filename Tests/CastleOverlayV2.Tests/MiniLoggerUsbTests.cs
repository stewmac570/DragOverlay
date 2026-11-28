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

    [Fact]
    public void Big_logger_is_found_and_lists_runs_from_summary()
    {
        var transport = new FakeTransport("BIGLOGGER 1",
            "Run 17: 12 s, wheel 0, motor 0, throttle 0, pack 7.40 V",
            "Run 18: 6 s, wheel 395, motor 10434, throttle 12314, pack 7.65 V", "END");
        using var client = Client(transport);
        Assert.Equal(MiniLoggerConnectStatus.Connected, client.Connect().Status);
        Assert.True(client.IsBigLogger);

        IReadOnlyList<MiniLoggerRunInfo> runs = client.ListRuns();

        Assert.Equal(new[] { "id", "summary" }, transport.Writes);
        Assert.Equal(new[] { 17, 18 }, runs.Select(run => run.Number));
        Assert.Equal(6, runs[1].Seconds);
        Assert.Equal(7.65, runs[1].PackVolts);
        Assert.Equal("Run 18  (6 s, pack 7.65 V)", runs[1].ToString());
    }

    [Fact]
    public void Big_logger_without_summary_falls_back_to_list()
    {
        var transport = new FakeTransport("BIGLOGGER 1",
            "Commands: status, live, list, dump N, dumpall, erase all", "Run 4: 300 KB", "END");
        using var client = Client(transport);
        client.Connect();

        IReadOnlyList<MiniLoggerRunInfo> runs = client.ListRuns();

        Assert.Equal(new[] { "id", "summary", "list" }, transport.Writes);
        Assert.Equal(new[] { new MiniLoggerRunInfo(4, 300) }, runs);
    }

    [Fact]
    public void Big_logger_download_reports_rows_against_seconds()
    {
        // Firmware from before 28 Nov 2026 answers "raw" with its command list: falls back to dump.
        var transport = new FakeTransport("BIGLOGGER 1",
            "Commands: status, gps, live, list, summary, dump N, dumpall, erase all",
            "BEGIN run_0018.csv", MiniLoggerLoader.HeaderLine,
            "0.000,1.500,0,0.00", "0.001,1.500,0,0.00", "0.002,1.500,0,0.00", "END");
        using var client = Client(transport);
        client.Connect();
        var reports = new List<MiniLoggerDownloadProgress>();

        client.DumpRun(new MiniLoggerRunInfo(18, 0, Seconds: 6, PackVolts: 7.65),
            Path.Combine(_workDir, "run_0018.csv"), new SynchronousProgress(reports.Add));

        MiniLoggerDownloadProgress last = reports.Last();
        Assert.Equal(3, last.RowsReceived);
        Assert.Equal(6000, last.ExpectedRows);
    }

    [Fact]
    public void Big_logger_download_uses_raw_files_and_rebuilds_the_csv()
    {
        // A tiny real-shaped run: header, one block of throttle edges (1.500 ms pulses every 7 ms)
        // and motor edges, and three slow readings.
        var bin = new List<byte>();
        bin.AddRange("BLG2"u8.ToArray());
        bin.AddRange(BitConverter.GetBytes(1000u));
        bin.AddRange(BitConverter.GetBytes(1_000_000UL));      // start
        bin.Add(2); bin.Add(2);                                   // wheel magnets, pole pairs
        bin.AddRange(BitConverter.GetBytes((ushort)1000));
        bin.AddRange(new byte[4]);                                // struct padding to 24
        var events = new List<uint>();
        for (uint t = 1_000_000; t < 1_010_000; t += 7000)
        {
            events.Add((2u << 30) | t);                           // throttle rise
            events.Add((3u << 30) | (t + 1500));                  // throttle fall: 1500 us
        }
        for (uint t = 1_000_500; t < 1_010_000; t += 500)
            events.Add((1u << 30) | t);                           // motor edge every 0.5 ms = 60000 rpm
        events.Sort((x, y) => (x & 0x3FFFFFFF).CompareTo(y & 0x3FFFFFFF));
        bin.AddRange(BitConverter.GetBytes((uint)events.Count));
        bin.AddRange(BitConverter.GetBytes(0u));
        bin.AddRange(BitConverter.GetBytes(1_020_000UL));
        foreach (uint e in events) bin.AddRange(BitConverter.GetBytes(e));
        var slo = new List<byte>();
        for (uint t = 1_000_000; t < 1_003_000; t += 1000)
        {
            slo.AddRange(BitConverter.GetBytes(t));
            slo.AddRange(BitConverter.GetBytes((short)1250));     // 1.25 g
            slo.AddRange(BitConverter.GetBytes((short)-250));     // pitch -2.5
            slo.AddRange(BitConverter.GetBytes((short)75));       // roll 0.75
            slo.AddRange(BitConverter.GetBytes((ushort)2500));    // 2.5 V at A0 = 8.00 V pack
            slo.AddRange(BitConverter.GetBytes((ushort)0xFFFF));
            slo.AddRange(BitConverter.GetBytes((ushort)0xFFFF));
        }
        var transport = new FakeTransport("BIGLOGGER 1", $"RAW 18 {bin.Count} {slo.Count} 0", "END");
        transport.Bytes.AddRange(bin);
        transport.Bytes.AddRange(slo);
        using var client = Client(transport);
        client.Connect();
        string path = Path.Combine(_workDir, "run_0018.csv");

        var result = client.DumpRun(new MiniLoggerRunInfo(18, 0, Seconds: 1), path);

        Assert.True(result.Found);
        Assert.Contains("raw 18", transport.Writes);
        Assert.DoesNotContain(transport.Writes, w => w.StartsWith("dump", StringComparison.Ordinal));
        string[] lines = File.ReadAllLines(path);
        Assert.Equal(BigLoggerRawRebuild.Header, lines[0]);
        // Expected lines from rc-logger tools/big_logger_rebuild.py on the same bytes, which matched
        // the logger's own dump line for line on run 14 (451,208 lines).
        Assert.Equal(12, lines.Length);
        Assert.Equal("0.000,0.000,0,0.00,8.00,1.250,-2.50,0.75,,,,,,,,", lines[1]);
        Assert.Equal("0.002,1.500,60000,0.00,8.00,1.250,-2.50,0.75,,,,,,,,", lines[3]);
        Assert.Equal("0.005,1.500,60000,0.00,8.00,1.250,-2.50,0.75,,,,,,,,", lines[6]);
        Assert.True(MiniLoggerLoader.IsHeaderLine(lines[0]));
    }

    [Theory]
    [InlineData(0.125, 2, "0.12")]       // an exact half rounds to even, as printf on the Nano
    [InlineData(0.375, 2, "0.38")]
    [InlineData(-0.001, 2, "-0.00")]     // printf keeps the sign
    [InlineData(1.5, 3, "1.500")]
    [InlineData(-33.1234567, 7, "-33.1234567")]
    [InlineData(60000.0, 0, "60000")]
    public void Rebuild_formats_numbers_like_the_logger(double value, int places, string expected) =>
        Assert.Equal(expected, BigLoggerRawRebuild.F(value, places));

    [Fact]
    public void Mini_logger_is_not_flagged_as_big_logger()
    {
        using var client = Client(ConnectedTransport());
        client.Connect();
        Assert.False(client.IsBigLogger);
    }

    [Fact]
    public void Mini_logger_never_gets_the_summary_command()
    {
        var transport = ConnectedTransport("Run 1: 25 KB", "END");
        using var client = Client(transport);
        client.Connect();

        client.ListRuns();

        Assert.Equal(new[] { "id", "list" }, transport.Writes);
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
        public List<byte> Bytes { get; } = new();
        public byte[]? ReadBytes(int count, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (Bytes.Count < count) return null;
            byte[] result = Bytes.GetRange(0, count).ToArray();
            Bytes.RemoveRange(0, count);
            return result;
        }
        public void Close() { }
        public void Dispose() { }
    }
}
