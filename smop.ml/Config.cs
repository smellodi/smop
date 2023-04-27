namespace Smop.ML
{
    public record class ChannelProps(int Slot, string Gas);
    public record class Config(string Type, ChannelProps[] Channels);
}
