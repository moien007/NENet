namespace ENetDotNet.Logging
{
    public interface IENetLogger
    {
        void Info(string text);
        void Debug(string text);
        void Fatal(string text);
        void Error(string text);
    }
}
