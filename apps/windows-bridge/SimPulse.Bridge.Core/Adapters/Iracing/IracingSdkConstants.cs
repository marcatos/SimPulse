namespace SimPulse.Bridge.Core.Adapters.Iracing;

// Copyright (c) iRacing.com Motorsport Simulations, LLC.
// Constants derived from the official IRSDK headers (BSD-style notice).
// Redistribution retains this notice. No endorsement by iRacing.com.

/// <summary>
/// Vendored IRSDK layout constants required to locate session info in the mmap.
/// </summary>
public static class IracingSdkConstants
{
    public const string MemMapFileName = @"Local\IRSDKMemMapFileName";

    public const string DataValidEventName = @"Local\IRSDKDataValidEvent";

    public const int StatusConnected = 1;

    public const int HeaderStatusOffset = 4;

    public const int HeaderSessionInfoLenOffset = 16;

    public const int HeaderSessionInfoOffsetOffset = 20;

    public const int HeaderMinSize = 24;
}
