using System.Diagnostics;
using System.IO.MemoryMappedFiles;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SimPulse.Bridge.Core.Ports;

namespace SimPulse.Bridge.Core.Adapters.Iracing;

public sealed class WindowsIracingSharedMemory : IIracingSharedMemory, IDisposable
{
    private readonly ILogger<WindowsIracingSharedMemory> _logger;
    private MemoryMappedFile? _map;

    public WindowsIracingSharedMemory(ILogger<WindowsIracingSharedMemory>? logger = null)
    {
        _logger = logger ?? NullLogger<WindowsIracingSharedMemory>.Instance;
    }

    public bool TryOpen()
    {
        if (_map is not null)
        {
            return true;
        }

        Stopwatch started = Stopwatch.StartNew();
        _logger.LogInformation(
            "iRacing mmap open starting. MapName={MapName} Component={Component}",
            IracingSdkConstants.MemMapFileName,
            nameof(WindowsIracingSharedMemory));

        if (!OperatingSystem.IsWindows())
        {
            _logger.LogInformation(
                "iRacing mmap unsupported on this OS after {ElapsedMs} ms. Component={Component}",
                started.ElapsedMilliseconds,
                nameof(WindowsIracingSharedMemory));
            return false;
        }

        try
        {
            _map = MemoryMappedFile.OpenExisting(
                IracingSdkConstants.MemMapFileName,
                MemoryMappedFileRights.Read);
            _logger.LogInformation(
                "iRacing mmap open succeeded in {ElapsedMs} ms. MapName={MapName}",
                started.ElapsedMilliseconds,
                IracingSdkConstants.MemMapFileName);
            return true;
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            _logger.LogInformation(
                "iRacing mmap unavailable after {ElapsedMs} ms. MapName={MapName} Reason={Reason}",
                started.ElapsedMilliseconds,
                IracingSdkConstants.MemMapFileName,
                ex.GetType().Name);
            return false;
        }
    }

    public void Close()
    {
        MemoryMappedFile? map = _map;
        _map = null;
        map?.Dispose();
    }

    public bool TryReadSnapshot(out IracingMemorySnapshot snapshot)
    {
        snapshot = default;
        if (_map is null)
        {
            return false;
        }

        Stopwatch started = Stopwatch.StartNew();
        try
        {
            using MemoryMappedViewAccessor accessor = _map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            if (!TryReadHeader(accessor, out _, out int infoLen, out int infoOffset, out bool connected))
            {
                return false;
            }

            string? yaml = connected ? TryReadYaml(accessor, infoOffset, infoLen) : null;
            snapshot = new IracingMemorySnapshot(yaml, connected);
            _logger.LogDebug(
                "iRacing mmap snapshot read in {ElapsedMs} ms. Connected={Connected} YamlLength={YamlLength}",
                started.ElapsedMilliseconds,
                connected,
                yaml?.Length ?? 0);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "iRacing mmap read failed in {ElapsedMs} ms. Component={Component}",
                started.ElapsedMilliseconds,
                nameof(WindowsIracingSharedMemory));
            return false;
        }
    }

    public void Dispose()
    {
        Close();
    }

    private static bool TryReadHeader(
        MemoryMappedViewAccessor accessor,
        out int status,
        out int infoLen,
        out int infoOffset,
        out bool connected)
    {
        status = 0;
        infoLen = 0;
        infoOffset = 0;
        connected = false;
        if (accessor.Capacity < IracingSdkConstants.HeaderMinSize)
        {
            return false;
        }

        byte[] header = new byte[IracingSdkConstants.HeaderMinSize];
        accessor.ReadArray(0, header, 0, header.Length);
        return IracingHeaderReader.TryReadHeader(header, out status, out infoLen, out infoOffset, out connected);
    }

    private static string? TryReadYaml(MemoryMappedViewAccessor accessor, int infoOffset, int infoLen)
    {
        if (infoLen <= 0 || infoOffset < 0 || infoOffset + (long)infoLen > accessor.Capacity)
        {
            return null;
        }

        byte[] yamlBytes = new byte[infoLen];
        accessor.ReadArray(infoOffset, yamlBytes, 0, infoLen);
        return IracingHeaderReader.DecodeYaml(yamlBytes);
    }

    private static bool IsUnavailable(Exception ex)
    {
        return ex is FileNotFoundException
            or DirectoryNotFoundException
            or UnauthorizedAccessException
            or IOException
            or PlatformNotSupportedException
            or ArgumentException
            or InvalidOperationException;
    }
}
