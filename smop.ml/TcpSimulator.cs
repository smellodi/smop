using Smop.Common;
using System.Text;
using System.Threading.Tasks;
using WatsonTcp;

namespace Smop.ML;

internal class TcpSimulator : Simulator
{
    public TcpSimulator()
    {
        _client = new WatsonTcpClient("127.0.0.1", TcpServer.Port);
        _client.Events.ServerConnected += ServerConnected;
        _client.Events.ServerDisconnected += ServerDisconnected;
        _client.Events.MessageReceived += MessageReceived;
        _client.Callbacks.SyncRequestReceived = SyncRequestReceived;
        _client.Connect();
    }

    public override void Dispose()
    {
        _client.Dispose();
    }


    // Internal

    readonly WatsonTcpClient _client;

    protected override async Task SendData(string data)
    {
        await _client.SendAsync(data);
    }

    private void MessageReceived(object? sender, MessageReceivedEventArgs args)
    {
        string json = Encoding.UTF8.GetString(args.Data);
        ParseJson(json);
    }

    private void ServerConnected(object? sender, ConnectionEventArgs args)
    {
        ScreenLogger.Print("[MlSimul] connected");
    }

    private void ServerDisconnected(object? sender, DisconnectionEventArgs args)
    {
        ScreenLogger.Print("[MlSimul] disconnected");
    }

    private SyncResponse SyncRequestReceived(SyncRequest req)
    {
        return new SyncResponse(req, "Ack Sync");
    }
}
