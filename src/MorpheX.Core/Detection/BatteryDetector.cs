namespace MorpheX.Core.Detection;

public sealed class BatteryDetector
{
    public bool IsOnBattery()
    {
        if (Desktop.NativeMethods.GetSystemPowerStatus(out var status))
        {
            // BatteryFlag: 128 = No system battery (desktop PC). Never pause for battery on a desktop.
            if ((status.BatteryFlag & 128) != 0)
                return false;

            // ACLineStatus: 0=offline (battery), 1=online (AC), 255=unknown/no battery
            return status.ACLineStatus == 0;
        }
        return false; // If API fails, assume AC power (safe default)
    }
}
