namespace SimPulse.Bridge.Core.Adapters.Iracing;

// Copyright (c) iRacing.com Motorsport Simulations, LLC.
// Constants derived from the official IRSDK headers (BSD-style notice).
// Redistribution retains this notice. No endorsement by iRacing.com.

/// <summary>
/// Vendored IRSDK layout constants required to locate session info and the variable table.
/// </summary>
public static class IracingSdkConstants
{
    public const string MemMapFileName = @"Local\IRSDKMemMapFileName";

    public const string DataValidEventName = @"Local\IRSDKDataValidEvent";

    public const int StatusConnected = 1;

    public const int HeaderStatusOffset = 4;

    public const int HeaderSessionInfoUpdateOffset = 12;

    public const int HeaderSessionInfoLenOffset = 16;

    public const int HeaderSessionInfoOffsetOffset = 20;

    public const int HeaderNumVarsOffset = 24;

    public const int HeaderVarHeaderOffsetOffset = 28;

    public const int HeaderNumBufOffset = 32;

    public const int HeaderBufLenOffset = 36;

    public const int HeaderVarBufOffset = 48;

    public const int VarBufStride = 16;

    public const int MaxBufs = 4;

    /// <summary>YAML-only header subset (status + session info length/offset).</summary>
    public const int HeaderMinSize = 24;

    /// <summary>Full <c>irsdk_header</c> including <c>varBuf[IRSDK_MAX_BUFS]</c>.</summary>
    public const int HeaderLayoutMinSize = HeaderVarBufOffset + (MaxBufs * VarBufStride);

    public const int VarHeaderSize = 144;

    public const int VarHeaderTypeOffset = 0;

    public const int VarHeaderOffsetOffset = 4;

    public const int VarHeaderCountOffset = 8;

    public const int VarHeaderNameOffset = 16;

    public const int VarHeaderNameSize = 32;

    public const int VarTypeInt = 2;

    public const int VarTypeDouble = 5;
}
