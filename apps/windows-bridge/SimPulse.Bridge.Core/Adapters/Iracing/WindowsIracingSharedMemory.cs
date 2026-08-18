using System.Diagnostics;
using System.IO.MemoryMappedFiles;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SimPulse.Bridge.Core.Ports;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Adapters.Iracing;

public sealed class WindowsIracingSharedMemory : IIracingSharedMemory, IDisposable
{
    private readonly ILogger<WindowsIracingSharedMemory> _logger;
    private MemoryMappedFile? _map;
    private byte[] _scratch = [];
    private bool _opened;

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
        _logger.LogDebug(
            "iRacing mmap open starting. MapName={MapName} Component={Component}",
            IracingSdkConstants.MemMapFileName,
            nameof(WindowsIracingSharedMemory));

        if (!OperatingSystem.IsWindows())
        {
            _logger.LogDebug(
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
            _opened = true;
            _logger.LogInformation(
                "iRacing mmap open succeeded in {ElapsedMs} ms. MapName={MapName}",
                started.ElapsedMilliseconds,
                IracingSdkConstants.MemMapFileName);
            return true;
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            _logger.LogDebug(
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
        if (_opened)
        {
            _opened = false;
            _logger.LogInformation(
                "iRacing mmap closed. MapName={MapName} Component={Component}",
                IracingSdkConstants.MemMapFileName,
                nameof(WindowsIracingSharedMemory));
        }
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
            if (!TryCopy(accessor, out ReadOnlySpan<byte> buffer))
            {
                return false;
            }

            if (!IracingMemorySnapshotReader.TryRead(buffer, out snapshot))
            {
                return false;
            }

            _logger.LogDebug(
                "iRacing mmap snapshot read in {ElapsedMs} ms. Connected={Connected} YamlLength={YamlLength} SessionInfoUpdate={SessionInfoUpdate} TelemetryPresent={TelemetryPresent}",
                started.ElapsedMilliseconds,
                snapshot.Connected,
                snapshot.SessionYaml?.Length ?? 0,
                snapshot.SessionInfoUpdate,
                snapshot.Telemetry.SessionTime.Presence == DataPresence.Available);
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

    public bool WaitForUpdate(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || !OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            if (!EventWaitHandle.TryOpenExisting(IracingSdkConstants.DataValidEventName, out EventWaitHandle? handle))
            {
                return false;
            }

            using (handle)
            {
                int waitMs = timeout <= TimeSpan.Zero ? 0 : (int)Math.Clamp(timeout.TotalMilliseconds, 0, int.MaxValue);
                int index = WaitHandle.WaitAny([handle, cancellationToken.WaitHandle], waitMs);
                return index == 0;
            }
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            return false;
        }
    }

    public void Dispose()
    {
        Close();
    }

    private bool TryCopy(MemoryMappedViewAccessor accessor, out ReadOnlySpan<byte> buffer)
    {
        long capacity = accessor.Capacity;
        if (capacity < IracingSdkConstants.HeaderMinSize)
        {
            buffer = default;
            return false;
        }

        int length = capacity > int.MaxValue ? int.MaxValue : (int)capacity;
        if (_scratch.Length < length)
        {
            _scratch = new byte[length];
        }

        accessor.ReadArray(0, _scratch, 0, length);
        buffer = _scratch.AsSpan(0, length);
        return true;
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
