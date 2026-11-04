using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using CastleOverlayV2.Models;

namespace CastleOverlayV2.Services;

/// <summary>
/// A saved run. The Mini Logger's <c>list</c> gives its size; the Big logger's <c>summary</c>
/// gives its length in seconds and peak pack volts instead.
/// </summary>
public sealed record MiniLoggerRunInfo(int Number, long SizeKb, int? Seconds = null, double? PackVolts = null)
{
    // A Big logger CSV row is about 70 bytes and there are 1000 rows a second. Only used for progress.
    private const long BigLoggerBytesPerSecond = 70_000;

    public string FileName => $"run_{Number:0000}.csv";

    public long ExpectedCsvBytes => Seconds is int seconds ? seconds * BigLoggerBytesPerSecond : SizeKb * 1024;

    public override string ToString() => Seconds is int seconds
        ? string.Create(CultureInfo.InvariantCulture, $"Run {Number}  ({seconds} s, pack {PackVolts:0.00} V)")
        : $"Run {Number}  ({SizeKb:N0} KB)";
}

public sealed record MiniLoggerDownloadProgress(
    int RunNumber, int RunIndex, int RunCount, long BytesReceived, long ExpectedBytes);

public enum MiniLoggerConnectStatus
{
    Connected,
    NotFound,
    PortBusy
}

public sealed record MiniLoggerConnectResult(MiniLoggerConnectStatus Status, string? PortName = null);

public sealed record MiniLoggerDumpResult(bool Found, string? FilePath);

public sealed class MiniLoggerStoppedRespondingException : IOException
{
    public MiniLoggerStoppedRespondingException()
        : base("The Mini Logger stopped answering.")
    {
    }
}

public interface IMiniLoggerPortDiscovery
{
    IReadOnlyList<string> FindCandidatePorts();
}

public interface IMiniLoggerLineTransport : IDisposable
{
    void Open(string portName, int baudRate, bool dtrEnable, bool rtsEnable);
    void DiscardInput();
    void WriteLine(string line);
    string? ReadLine(TimeSpan timeout, CancellationToken cancellationToken);
    void Close();
}

public interface IMiniLoggerTransportFactory
{
    IMiniLoggerLineTransport Create();
}

public interface IMiniLoggerDelay
{
    void Wait(TimeSpan delay, CancellationToken cancellationToken);
}

public interface IMiniLoggerUsbClient : IDisposable
{
    MiniLoggerConnectResult Connect(CancellationToken cancellationToken = default);
    IReadOnlyList<MiniLoggerRunInfo> ListRuns(CancellationToken cancellationToken = default);
    MiniLoggerDumpResult DumpRun(
        MiniLoggerRunInfo run,
        string destinationPath,
        IProgress<MiniLoggerDownloadProgress>? progress = null,
        int runIndex = 1,
        int runCount = 1,
        CancellationToken cancellationToken = default);
    TcLoggerStatus ReadTcStatus(CancellationToken cancellationToken = default);
    /// <param name="softPoints">
    /// The soft curve. Null leaves the logger's soft curve alone; empty clears it.
    /// </param>
    TcCurveTransferResult SendTcCurve(
        IReadOnlyList<TcCurvePoint> points,
        IReadOnlyList<TcCurvePoint>? softPoints = null,
        CancellationToken cancellationToken = default);
    /// <summary>Clears the hard curve, and the soft curve when the firmware has one.</summary>
    TcCurveTransferResult ClearTcCurve(CancellationToken cancellationToken = default);
}

/// <summary>UI-free Mini Logger USB protocol client.</summary>
public sealed class MiniLoggerUsb : IMiniLoggerUsbClient
{
    public static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMilliseconds(1500);
    // A long run is ~900,000 lines. Reporting every line would flood the UI thread with
    // progress posts, so report at most once per this many bytes (plus once at the end).
    public const long ProgressReportBytes = 64 * 1024;
    private static readonly Regex RunLine = new(
        @"^Run (?<number>\d+): (?<size>\d+) KB$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SummaryLine = new(
        @"^Run (?<number>\d+): (?<seconds>\d+) s, wheel \d+, motor \d+, throttle \d+, pack (?<pack>[0-9.]+) V$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IMiniLoggerPortDiscovery _portDiscovery;
    private readonly IMiniLoggerTransportFactory _transportFactory;
    private readonly IMiniLoggerDelay _delay;
    private IMiniLoggerLineTransport? _transport;
    private bool _isBigLogger;

    public MiniLoggerUsb()
        : this(new WmiMiniLoggerPortDiscovery(), new SerialMiniLoggerTransportFactory(), new ThreadMiniLoggerDelay())
    {
    }

    public MiniLoggerUsb(
        IMiniLoggerPortDiscovery portDiscovery,
        IMiniLoggerTransportFactory transportFactory,
        IMiniLoggerDelay? delay = null)
    {
        _portDiscovery = portDiscovery;
        _transportFactory = transportFactory;
        _delay = delay ?? new ThreadMiniLoggerDelay();
    }

    public MiniLoggerConnectResult Connect(CancellationToken cancellationToken = default)
    {
        CloseTransport();
        IReadOnlyList<string> ports = _portDiscovery.FindCandidatePorts();
        if (ports.Count == 0)
            return new MiniLoggerConnectResult(MiniLoggerConnectStatus.NotFound);

        bool openFailed = false;
        foreach (string portName in ports)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IMiniLoggerLineTransport candidate = _transportFactory.Create();
            try
            {
                candidate.Open(portName, 115200, dtrEnable: true, rtsEnable: false);
                _delay.Wait(StartupDelay, cancellationToken);
                candidate.DiscardInput();
                candidate.WriteLine("id");

                // The Mini Logger replies "MINILOGGER 2", the Big logger "BIGLOGGER 1".
                string? reply = ReadUntil(candidate,
                    line => line.StartsWith("MINILOGGER ", StringComparison.Ordinal) ||
                            line.StartsWith("BIGLOGGER ", StringComparison.Ordinal),
                    HandshakeTimeout, cancellationToken);
                if (reply is not null)
                {
                    _transport = candidate;
                    _isBigLogger = reply.StartsWith("BIGLOGGER ", StringComparison.Ordinal);
                    return new MiniLoggerConnectResult(MiniLoggerConnectStatus.Connected, portName);
                }
            }
            catch (OperationCanceledException)
            {
                candidate.Dispose();
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // A candidate may be busy, unplugged, or another Espressif device.
                openFailed = true;
            }

            candidate.Dispose();
        }

        return new MiniLoggerConnectResult(
            openFailed ? MiniLoggerConnectStatus.PortBusy : MiniLoggerConnectStatus.NotFound);
    }

    public IReadOnlyList<MiniLoggerRunInfo> ListRuns(CancellationToken cancellationToken = default)
    {
        IMiniLoggerLineTransport transport = ConnectedTransport();
        if (_isBigLogger)
        {
            IReadOnlyList<MiniLoggerRunInfo>? summary = SummariseRuns(transport, cancellationToken);
            if (summary is not null)
                return summary;
        }

        transport.WriteLine("list");
        var runs = new List<MiniLoggerRunInfo>();

        while (true)
        {
            string line = ReadRequiredLine(transport, cancellationToken);
            if (line == "END")
                return runs;
            if (line == "No runs saved")
                continue;

            Match match = RunLine.Match(line);
            if (!match.Success)
                continue;
            if (int.TryParse(match.Groups["number"].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out int number) &&
                long.TryParse(match.Groups["size"].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out long sizeKb))
            {
                runs.Add(new MiniLoggerRunInfo(number, sizeKb));
            }
        }
    }

    /// <summary>
    /// The Big logger's <c>summary</c>: one line per run without downloading it. Returns null when
    /// the firmware doesn't know the command (it answers with its command list), so the caller
    /// falls back to <c>list</c>.
    /// </summary>
    private static IReadOnlyList<MiniLoggerRunInfo>? SummariseRuns(
        IMiniLoggerLineTransport transport, CancellationToken cancellationToken)
    {
        transport.WriteLine("summary");
        var runs = new List<MiniLoggerRunInfo>();
        while (true)
        {
            string line = ReadRequiredLine(transport, cancellationToken);
            if (line == "END")
                return runs;
            if (line.StartsWith("Commands:", StringComparison.Ordinal))
                return null;

            Match match = SummaryLine.Match(line);
            if (match.Success &&
                int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) &&
                int.TryParse(match.Groups["seconds"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) &&
                double.TryParse(match.Groups["pack"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pack))
            {
                runs.Add(new MiniLoggerRunInfo(number, 0, seconds, pack));
            }
        }
    }

    public MiniLoggerDumpResult DumpRun(
        MiniLoggerRunInfo run,
        string destinationPath,
        IProgress<MiniLoggerDownloadProgress>? progress = null,
        int runIndex = 1,
        int runCount = 1,
        CancellationToken cancellationToken = default)
    {
        IMiniLoggerLineTransport transport = ConnectedTransport();
        string fullPath = Path.GetFullPath(destinationPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        string partialPath = fullPath + ".partial";

        try
        {
            if (File.Exists(partialPath))
                File.Delete(partialPath);

            transport.WriteLine($"dump {run.Number.ToString(CultureInfo.InvariantCulture)}");
            string expectedBegin = $"BEGIN {run.FileName}";
            string? first = ReadUntil(transport,
                line => line == expectedBegin || line == $"Run {run.Number} not found",
                ReplyTimeout, cancellationToken);
            if (first is null)
                throw new MiniLoggerStoppedRespondingException();
            if (first.EndsWith(" not found", StringComparison.Ordinal))
                return new MiniLoggerDumpResult(false, null);

            long bytesReceived = 0;
            long lastReported = 0;
            long expectedBytes = run.ExpectedCsvBytes;
            using (var writer = new StreamWriter(partialPath, false, new UTF8Encoding(false), 64 * 1024))
            {
                bool headerWritten = false;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string line = ReadRequiredLine(transport, cancellationToken);
                    if (line == "END")
                        break;

                    if (!headerWritten)
                    {
                        if (!MiniLoggerLoader.IsHeaderLine(line))
                            continue;
                        headerWritten = true;
                    }
                    else if (!IsCsvDataLine(line))
                    {
                        continue;
                    }

                    writer.WriteLine(line);
                    bytesReceived += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
                    if (bytesReceived - lastReported >= ProgressReportBytes)
                    {
                        lastReported = bytesReceived;
                        progress?.Report(new MiniLoggerDownloadProgress(
                            run.Number, runIndex, runCount, bytesReceived, expectedBytes));
                    }
                }

                progress?.Report(new MiniLoggerDownloadProgress(
                    run.Number, runIndex, runCount, bytesReceived, expectedBytes));

                if (!headerWritten)
                    throw new InvalidDataException("The Mini Logger did not send a CSV header.");
            }

            File.Move(partialPath, fullPath, overwrite: true);
            return new MiniLoggerDumpResult(true, fullPath);
        }
        catch
        {
            TryDelete(partialPath);
            throw;
        }
    }

    public TcLoggerStatus ReadTcStatus(CancellationToken cancellationToken = default)
    {
        IMiniLoggerLineTransport transport = ConnectedTransport();
        transport.WriteLine("tc status");
        var lines = new List<string>();
        bool sawTcHeader = false;
        while (true)
        {
            string line = ReadRequiredLine(transport, cancellationToken);
            if (line.StartsWith("Commands:", StringComparison.Ordinal))
                return TcStatusParser.Parse(new[] { line });
            if (TcStatusParser.IsStatusHeader(line))
                sawTcHeader = true;
            if (sawTcHeader)
                lines.Add(line);
            if (sawTcHeader && line == "END")
                return TcStatusParser.Parse(lines);
        }
    }

    public TcCurveTransferResult SendTcCurve(
        IReadOnlyList<TcCurvePoint> points,
        IReadOnlyList<TcCurvePoint>? softPoints = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            TcCurveBuilder.Validate(points);
            if (softPoints is { Count: > 0 })
                TcCurveBuilder.Validate(softPoints);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return new TcCurveTransferResult(false, ex.Message);
        }

        TcLoggerStatus before = ReadTcStatus(cancellationToken);
        TcCurveTransferResult? refusal = RefuseTcWrite(before);
        if (refusal is not null) return refusal;
        bool firmwareHasSoft = before.SoftCurvePointCount is not null;
        if (softPoints is { Count: > 0 } && !firmwareHasSoft)
            return new TcCurveTransferResult(false,
                "This TC logger's firmware has no soft curve. Upload the TC logger firmware with the soft curve " +
                "(rc-logger PR #25), then send again. Nothing was sent.", before);

        IMiniLoggerLineTransport transport = ConnectedTransport();
        string? error = UploadCurve(transport, "tc curve", points, cancellationToken);
        if (error is not null)
            return new TcCurveTransferResult(false, error, before);

        if (softPoints is not null && firmwareHasSoft)
        {
            error = softPoints.Count > 0
                ? UploadCurve(transport, "tc soft", softPoints, cancellationToken)
                : FailedReply(WriteTcCommand(transport, "tc soft clear", cancellationToken));
            if (error is not null)
                return new TcCurveTransferResult(false, "Hard curve sent, but the soft curve failed: " + error, before);
        }

        TcLoggerStatus after = ReadTcStatus(cancellationToken);
        if (!after.IsTcLogger)
            return new TcCurveTransferResult(false, "The device stopped identifying as a TC logger.", after);
        if (after.CurvePointCount != points.Count)
            return new TcCurveTransferResult(false,
                $"The logger reports {after.CurvePointCount} curve points after sending {points.Count}.", after);
        int softSent = softPoints?.Count ?? 0;
        if (softPoints is not null && firmwareHasSoft && after.SoftCurvePointCount != softSent)
            return new TcCurveTransferResult(false,
                $"The logger reports {after.SoftCurvePointCount} soft curve points after sending {softSent}.", after);

        string softText = softPoints is null || !firmwareHasSoft ? ""
            : softSent > 0 ? $", soft curve {softSent} points" : ", soft curve cleared";
        return new TcCurveTransferResult(true,
            $"Curve sent successfully (hard curve {points.Count} points{softText}).", after);
    }

    public TcCurveTransferResult ClearTcCurve(CancellationToken cancellationToken = default)
    {
        TcLoggerStatus before = ReadTcStatus(cancellationToken);
        TcCurveTransferResult? refusal = RefuseTcWrite(before);
        if (refusal is not null) return refusal;

        IMiniLoggerLineTransport transport = ConnectedTransport();
        string? error = FailedReply(WriteTcCommand(transport, "tc curve clear", cancellationToken));
        if (error is not null)
            return new TcCurveTransferResult(false, error, before);
        bool firmwareHasSoft = before.SoftCurvePointCount is not null;
        if (firmwareHasSoft)
        {
            error = FailedReply(WriteTcCommand(transport, "tc soft clear", cancellationToken));
            if (error is not null)
                return new TcCurveTransferResult(false, error, before);
        }

        TcLoggerStatus after = ReadTcStatus(cancellationToken);
        if (!after.IsTcLogger || after.CurvePointCount != 0 || (firmwareHasSoft && after.SoftCurvePointCount != 0))
            return new TcCurveTransferResult(false,
                "The logger did not confirm an empty curve after clearing.", after);
        return new TcCurveTransferResult(true, firmwareHasSoft ? "Logger curves cleared." : "Logger curve cleared.", after);
    }

    /// <summary>"tc curve" or "tc soft": begin, one pt per point, end. Returns the error, or null.</summary>
    private static string? UploadCurve(
        IMiniLoggerLineTransport transport,
        string prefix,
        IReadOnlyList<TcCurvePoint> points,
        CancellationToken cancellationToken)
    {
        string? error = FailedReply(WriteTcCommand(transport, prefix + " begin", cancellationToken));
        if (error is not null) return error;
        foreach (TcCurvePoint point in points)
        {
            error = FailedReply(WriteTcCommand(transport,
                string.Create(CultureInfo.InvariantCulture, $"{prefix} pt {point.TimeMs} {point.Rpm}"),
                cancellationToken));
            if (error is not null) return error;
        }
        return FailedReply(WriteTcCommand(transport, prefix + " end", cancellationToken));
    }

    private static string? FailedReply(string reply) =>
        reply.StartsWith("ERROR", StringComparison.Ordinal) ? reply : null;

    public void Dispose() => CloseTransport();

    private static bool IsCsvDataLine(string line)
    {
        string[] fields = line.Split(',');
        return fields.Length >= 4 &&
               double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
               double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
               double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
               double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static TcCurveTransferResult? RefuseTcWrite(TcLoggerStatus status)
    {
        if (!status.IsTcLogger)
            return new TcCurveTransferResult(false,
                "This is a plain Mini Logger, not a TC logger (tc status returned Commands:).", status);
        if (status.State == TcLoggerState.Active)
            return new TcCurveTransferResult(false,
                "The TC logger is ACTIVE. Curve changes are refused while a pass is running.", status);
        return null;
    }

    private static string WriteTcCommand(
        IMiniLoggerLineTransport transport,
        string command,
        CancellationToken cancellationToken)
    {
        transport.WriteLine(command);
        while (true)
        {
            string line = ReadRequiredLine(transport, cancellationToken);
            if (line.StartsWith("OK", StringComparison.Ordinal) ||
                line.StartsWith("ERROR", StringComparison.Ordinal))
                return line;
        }
    }

    private static string ReadRequiredLine(
        IMiniLoggerLineTransport transport,
        CancellationToken cancellationToken)
    {
        string? line;
        try
        {
            line = transport.ReadLine(ReplyTimeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            throw new MiniLoggerStoppedRespondingException();
        }
        if (line is null)
            throw new MiniLoggerStoppedRespondingException();
        return NormalizeLine(line);
    }

    private static string? ReadUntil(
        IMiniLoggerLineTransport transport,
        Func<string, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan remaining = timeout - stopwatch.Elapsed;
            string? raw;
            try
            {
                raw = transport.ReadLine(remaining, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                throw new MiniLoggerStoppedRespondingException();
            }
            if (raw is null)
                return null;
            string line = NormalizeLine(raw);
            if (predicate(line))
                return line;
        }
        return null;
    }

    private static string NormalizeLine(string line) => line.TrimEnd('\n').TrimEnd('\r');

    private IMiniLoggerLineTransport ConnectedTransport() =>
        _transport ?? throw new InvalidOperationException("The Mini Logger is not connected.");

    private void CloseTransport()
    {
        if (_transport is null)
            return;
        _transport.Close();
        _transport.Dispose();
        _transport = null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public sealed class WmiMiniLoggerPortDiscovery : IMiniLoggerPortDiscovery
{
    private static readonly Regex ComPort = new(@"\((COM\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyList<string> FindCandidatePorts()
    {
        var ports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, DeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
        foreach (ManagementBaseObject device in searcher.Get())
        {
            string deviceId = Convert.ToString(device["DeviceID"], CultureInfo.InvariantCulture) ?? "";
            string name = Convert.ToString(device["Name"], CultureInfo.InvariantCulture) ?? "";
            // VID_303A: Espressif, the Mini Logger's XIAO ESP32-S3. VID_2341: Arduino, the Big
            // logger's Nano ESP32. Other boards share both IDs, so the "id" reply decides.
            if (!deviceId.Contains("VID_303A", StringComparison.OrdinalIgnoreCase) &&
                !deviceId.Contains("VID_2341", StringComparison.OrdinalIgnoreCase))
                continue;
            Match match = ComPort.Match(name);
            if (match.Success)
                ports.Add(match.Groups[1].Value.ToUpperInvariant());
        }
        return ports.OrderBy(port => port, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public sealed class SerialMiniLoggerTransportFactory : IMiniLoggerTransportFactory
{
    public IMiniLoggerLineTransport Create() => new SerialMiniLoggerLineTransport();
}

public sealed class SerialMiniLoggerLineTransport : IMiniLoggerLineTransport
{
    private SerialPort? _port;
    private readonly StringBuilder _buffer = new();
    private readonly Queue<string> _lines = new();

    public void Open(string portName, int baudRate, bool dtrEnable, bool rtsEnable)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            DtrEnable = dtrEnable,
            RtsEnable = rtsEnable,
            NewLine = "\n",
            Encoding = Encoding.UTF8
        };
        _port.Open();
    }

    public void DiscardInput()
    {
        Port().DiscardInBuffer();
        _buffer.Clear();
        _lines.Clear();
    }

    public void WriteLine(string line)
    {
        SerialPort port = Port();
        port.Write(line);
        port.Write("\n");
    }

    public string? ReadLine(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_lines.Count > 0)
                return _lines.Dequeue();

            string incoming = Port().ReadExisting();
            foreach (char ch in incoming)
            {
                if (ch == '\n')
                {
                    _lines.Enqueue(_buffer.ToString());
                    _buffer.Clear();
                }
                else
                {
                    _buffer.Append(ch);
                }
            }

            if (_lines.Count == 0)
                cancellationToken.WaitHandle.WaitOne(10);
        }
        return _lines.Count > 0 ? _lines.Dequeue() : null;
    }

    public void Close()
    {
        if (_port?.IsOpen == true)
            _port.Close();
    }

    public void Dispose()
    {
        Close();
        _port?.Dispose();
        _port = null;
    }

    private SerialPort Port() =>
        _port is { IsOpen: true } port ? port : throw new InvalidOperationException("Serial port is not open.");
}

public sealed class ThreadMiniLoggerDelay : IMiniLoggerDelay
{
    public void Wait(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.WaitHandle.WaitOne(delay);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
