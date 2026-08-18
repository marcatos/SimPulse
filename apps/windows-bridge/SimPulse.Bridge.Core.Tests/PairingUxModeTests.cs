using SimPulse.Bridge.Core.Application;

namespace SimPulse.Bridge.Core.Tests;

public sealed class PairingUxModeTests
{
    [Fact]
    public void Uses_tray_on_windows_interactive_when_tray_env_unset()
    {
        Assert.True(PairingUxMode.UseTray(windowsTrayBuild: true, userInteractive: true, trayEnv: null));
    }

    [Fact]
    public void Uses_console_when_tray_env_is_zero()
    {
        Assert.False(PairingUxMode.UseTray(windowsTrayBuild: true, userInteractive: true, trayEnv: "0"));
    }

    [Fact]
    public void Uses_console_when_not_interactive()
    {
        Assert.False(PairingUxMode.UseTray(windowsTrayBuild: true, userInteractive: false, trayEnv: null));
    }

    [Fact]
    public void Uses_console_when_windows_tray_not_built()
    {
        Assert.False(PairingUxMode.UseTray(windowsTrayBuild: false, userInteractive: true, trayEnv: null));
    }
}
