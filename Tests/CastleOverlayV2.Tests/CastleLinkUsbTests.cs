using System.Globalization;
using CastleOverlayV2.Services;

namespace CastleOverlayV2.Tests;

/// <summary>Replays the two real captures in Tests/castle-link-samples.</summary>
public sealed class CastleLinkUsbTests
{
    private static readonly double[] CellTolerance =
        { 0.00011, 0.0006, 0.0051, 0.0006, 0.051, 0.051, 0.51, 0.51, 0.0051, 0.0051, 0.51, 0.0051 };

    [Theory]
    [InlineData(33, 4, 289)]
    [InlineData(32, 10, 478)]
    public void Download_sends_the_captured_messages_and_rebuilds_the_exported_csv(
        int number, int pages, int expectedRows)
    {
        var transport = new ReplayTransport(number);
        using var client = new CastleLinkUsb(new OnePort(), () => transport, new NoDelay());

        CastleLinkConnectResult connection = client.Connect();
        IReadOnlyList<CastleSessionInfo> sessions = client.ListSessions();
        CastleSessionInfo session = sessions.Single(item => item.Number == number);
        byte[] log = client.DownloadSession(session);

        Assert.Equal(CastleLinkConnectStatus.Connected, connection.Status);
        Assert.Equal("GL00136", client.EscFirmware);
        Assert.Equal(48 + 2560, client.TuneBytes!.Length);
        Assert.Equal(4053, (client.TuneBytes[2] << 8) | client.TuneBytes[3]); // Full Forward
        Assert.Equal(32, sessions.Count);
        Assert.Equal(33, sessions[0].Number);
        Assert.Equal(2, sessions[^1].Number);
        Assert.Equal(pages, session.Pages);
        Assert.Equal(CapturedRequests(number), transport.WrittenExceptPolls);

        IReadOnlyList<int[]> rows = CastleLogDecoder.Decode(log);
        Assert.Equal(expectedRows, rows.Count);
        AssertMatchesExport(CastleLogDecoder.ToCsv(rows, number, client.EscFirmware), number);
    }

    // Real hard passes read from the ESC: session 14 reaches 100% power, 552 amps and 192,730 RPM.
    [Theory]
    [InlineData(14, 2351)]
    [InlineData(31, 1234)]
    public void Hard_pass_logs_match_castles_own_export_including_b_markers(int number, int expectedRows)
    {
        byte[] log = File.ReadAllBytes(Path.Combine(SamplesFolder(), $"session-{number}.bin"));

        string csv = CastleLogDecoder.ToCsv(CastleLogDecoder.Decode(log), number, "GL00136");

        Assert.Equal(expectedRows, DataRows(File.ReadAllLines(Path.Combine(SamplesFolder(), $"session-{number}.csv"))).Length);
        AssertMatchesExport(csv, number);
    }

    [Fact]
    public void Decoder_rejects_a_damaged_log()
    {
        byte[] damaged = { 0x02, 0xFF };

        Assert.Throws<InvalidDataException>(() => CastleLogDecoder.Decode(damaged));
    }

    [Fact]
    public void No_castle_link_reports_not_found()
    {
        using var client = new CastleLinkUsb(new OnePort(noPorts: true), () => new SilentTransport(), new NoDelay());

        Assert.Equal(CastleLinkConnectStatus.NotFound, client.Connect().Status);
    }

    [Fact]
    public void Link_answering_without_an_esc_reports_no_esc()
    {
        using var client = new CastleLinkUsb(new OnePort(), () => new SilentTransport(answerFirst: true), new NoDelay());

        Assert.Equal(CastleLinkConnectStatus.NoEsc, client.Connect().Status);
    }

    [Fact]
    public void Busy_port_reports_port_busy()
    {
        using var client = new CastleLinkUsb(new OnePort(), () => new SilentTransport(busy: true), new NoDelay());

        Assert.Equal(CastleLinkConnectStatus.PortBusy, client.Connect().Status);
    }

    private static void AssertMatchesExport(string decodedCsv, int number)
    {
        string[] decoded = DataRows(decodedCsv.Split(Environment.NewLine));
        string[] exported = DataRows(File.ReadAllLines(Path.Combine(SamplesFolder(), $"session-{number}.csv")));
        // Castle's export can leave off the final record: session 31 has one row fewer than the ESC.
        Assert.InRange(decoded.Length, exported.Length, exported.Length + 1);
        for (int row = 0; row < exported.Length; row++)
        {
            double[] want = Cells(exported[row]);
            double[] got = Cells(decoded[row]);
            for (int cell = 0; cell < want.Length; cell++)
                Assert.True(Math.Abs(want[cell] - got[cell]) <= CellTolerance[cell],
                    $"Row {row}, column {cell}: wanted {want[cell]}, got {got[cell]}");
            Assert.Equal(exported[row].Contains('b'), decoded[row].Contains('b'));
        }
    }

    private static string[] DataRows(IEnumerable<string> lines) =>
        lines.Where(line => line.Trim().Length > 0 && !line.StartsWith('#')).Skip(2).ToArray();

    private static double[] Cells(string line) =>
        line.Split(',').Select(cell => double.Parse(cell.Replace("b", "").Trim(), CultureInfo.InvariantCulture)).ToArray();

    private static List<string> CapturedRequests(int number) =>
        ReplayTransport.Conversation(number)
            .Where(line => line.Outgoing && !ReplayTransport.IsPoll(line.Hex))
            .Select(line => line.Hex)
            .ToList();

    private static string SamplesFolder()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null)
        {
            string candidate = Path.Combine(folder.FullName, "Tests", "castle-link-samples");
            if (Directory.Exists(candidate))
                return candidate;
            folder = folder.Parent;
        }
        throw new DirectoryNotFoundException("Tests/castle-link-samples was not found.");
    }

    private sealed class OnePort : ICastleLinkPortDiscovery
    {
        private readonly bool _noPorts;
        public OnePort(bool noPorts = false) => _noPorts = noPorts;
        public IReadOnlyList<string> FindCandidatePorts() => _noPorts ? Array.Empty<string>() : new[] { "COM3" };
    }

    private sealed class NoDelay : ICastleLinkDelay
    {
        public void Wait(TimeSpan delay, CancellationToken cancellationToken)
        {
        }
    }

    private sealed class SilentTransport : ICastleLinkTransport
    {
        private readonly bool _busy;
        private bool _answerNext;
        private byte[] _pending = Array.Empty<byte>();

        public SilentTransport(bool answerFirst = false, bool busy = false)
        {
            _answerNext = answerFirst;
            _busy = busy;
        }

        public void Open(string portName)
        {
            if (_busy)
                throw new UnauthorizedAccessException();
        }

        public void DiscardInput()
        {
        }

        public void Write(byte[] data)
        {
            if (_answerNext)
                _pending = data;
            _answerNext = false;
        }

        public byte[]? Read(int count, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (_pending.Length < count)
                return null;
            byte[] result = _pending[..count];
            _pending = _pending[count..];
            return result;
        }

        public void Dispose()
        {
        }
    }

    private sealed class ReplayTransport : ICastleLinkTransport
    {
        private readonly Dictionary<string, Queue<byte[]>> _replies = new();
        private readonly List<byte> _pending = new();

        public List<string> WrittenExceptPolls { get; } = new();

        public ReplayTransport(int number)
        {
            string? request = null;
            foreach ((bool outgoing, string hex) in Conversation(number))
            {
                if (outgoing)
                {
                    request = hex;
                    continue;
                }
                if (request is null)
                    continue;
                if (!_replies.TryGetValue(request, out Queue<byte[]>? queue))
                    _replies[request] = queue = new Queue<byte[]>();
                queue.Enqueue(Convert.FromHexString(hex));
                request = null;
            }
        }

        public static bool IsPoll(string hex) =>
            hex.StartsWith("4312", StringComparison.Ordinal) || hex.StartsWith("4301", StringComparison.Ordinal);

        public static List<(bool Outgoing, string Hex)> Conversation(int number) =>
            File.ReadAllLines(Path.Combine(SamplesFolder(), $"session-{number}-conversation.txt"))
                .Select(line => line.Split('\t'))
                .Select(fields => (fields[1] == "0x03", fields[2]))
                .ToList();

        public void Open(string portName)
        {
        }

        public void DiscardInput()
        {
        }

        public void Write(byte[] data)
        {
            string hex = Convert.ToHexString(data).ToLowerInvariant();
            if (!IsPoll(hex))
                WrittenExceptPolls.Add(hex);
            Assert.True(_replies.TryGetValue(hex, out Queue<byte[]>? queue), $"Not in the capture: {hex}");
            _pending.AddRange(queue!.Count > 1 ? queue.Dequeue() : queue.Peek());
        }

        public byte[]? Read(int count, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (_pending.Count < count)
                return null;
            byte[] result = _pending.Take(count).ToArray();
            _pending.RemoveRange(0, count);
            return result;
        }

        public void Dispose()
        {
        }
    }
}
