namespace Smop.SmellInsp;

public enum Command
{
    FAN1,
    FAN2,
    FAN3,
    FAN0,
    GET_INFO
}

public record class Data(float[] Resistances, float Temperature, float Humidity);

public record class DeviceInfo(string Version, string Address);
