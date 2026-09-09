namespace MorpheX.Core.Detection;

public sealed class BatteryDetector
{
    public bool IsOnBattery()
    {
        if (Desktop.NativeMethods.GetSystemPowerStatus(out var status))
        {
            return !status.IsOnAcPower;
        }
        return false;
    }
}
