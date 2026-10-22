using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

namespace CastleOverlayV2.Services;

public sealed record MiniLoggerRunInfo(int Number, long SizeKb)
{
    public string FileName => $"run_{Number:0000}.csv";
    public override string ToString() => $"Run {Number}  ({SizeKb:N0} KB)";
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
}

/// <summary>UI-free Mini Logger USB protocol client.</summary>
public sealed class MiniLoggerUsb : IMiniLoggerUsbClient
{
    public static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMilliseconds(1500);
    private static readonly Regex RunLine = new(
        @"^Run (?<number>\d+): (?<size>\d+) KB$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IMiniLoggerPortDiscovery _portDiscovery;
    private readonly IMiniLoggerTransportFactory _transportFactory;
    private readonly IMiniLoggerDelay _delay;
    private IMiniLoggerLineTransport? _transport;

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

                string? reply = ReadUntil(candidate,
                    line => line.StartsWith("MINILOGGER ", StringComparison.Ordinal),
                    HandshakeTimeout, cancellationToken);
                if (reply is not null)
                {
                    _transport = candidate;
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
            long expectedBytes = run.SizeKb * 1024;
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
                        if (!line.Equals(MiniLoggerLoader.HeaderLine, StringComparison.OrdinalIgnoreCase))
                            continue;
                        headerWritten = true;
                    }
                    else if (!IsCsvDataLine(line))
                    {
                        continue;
                    }

                    writer.WriteLine(line);
                    bytesReceived += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
                    progress?.Report(new MiniLoggerDownloadProgress(
                        run.Number, runIndex, runCount, bytesReceived, expectedBytes));
                }

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
            if (!deviceId.Contains("VID_303A", StringComparison.OrdinalIgnoreCase))
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
