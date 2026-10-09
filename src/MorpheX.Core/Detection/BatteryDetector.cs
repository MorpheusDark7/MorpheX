namespace MorpheX.Core.Detection;

public sealed class BatteryDetector
{
    public bool IsOnBattery()
    {
        if (Desktop.NativeMethods.GetSystemPowerStatus(out var status))
        {
            // ACLineStatus: 0=offline (battery), 1=online (AC), 255=unknown/no battery (desktop)
            // Treat unknown (255) as AC-powered — desktops have no battery.
            return status.ACLineStatus == 0;
        }
        return false; // If API fails, assume AC power (safe default)
    }
}
