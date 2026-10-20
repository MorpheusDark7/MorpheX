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

            // ACLineStatus: 0 = offline (battery), 1 = online (AC), 255 = unknown/no battery
            if (status.ACLineStatus == 0)
                return true;

            // On modern standby laptops, ACLineStatus can be 255 (unknown) while on battery (not charging)
            if (status.ACLineStatus == 255 && (status.BatteryFlag & 8) == 0 && status.BatteryLifePercent <= 100)
                return true;

            return false;
        }
        return false; // If API fails, assume AC power (safe default)
    }
}
