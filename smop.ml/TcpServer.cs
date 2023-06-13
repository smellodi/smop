using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WatsonTcp;

namespace Smop.ML;

internal class TcpServer : Server
{
    public static int Port => 2339;

    public override bool IsClientConnected => _client != Guid.Empty;

    public TcpServer()
    {
        _server = new WatsonTcpServer("127.0.0.1", Port);
        _server.Events.ClientConnected += ClientConnected;
        _server.Events.ClientDisconnected += ClientDisconnected;
        _server.Events.MessageReceived += MessageReceived;
        _server.Callbacks.SyncRequestReceived = SyncRequestReceived;
        _server.Start();
    }

    public override void Dispose()
    {
        _server.Stop();
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly WatsonTcpServer _server;

    Guid _client = Guid.Empty;

    protected override async Task SendTextAsync(string data)
    {
        await _server.SendAsync(_client, data);
    }

    private void ClientConnected(object? sender, ConnectionEventArgs args)
    {
        _client = args.Client.Guid;
        Console.WriteLine("[SERVER] connected: " + args.Client.ToString());
    }

    private void ClientDisconnected(object? sender, DisconnectionEventArgs args)
    {
        _client = Guid.Empty;
        Console.WriteLine("[SERVER] disconnected: " + args.Client.ToString() + ": " + args.Reason.ToString());
    }

    private void MessageReceived(object? sender, MessageReceivedEventArgs args)
    {
        var json = Encoding.UTF8.GetString(args.Data);
        ParseJson(json);
    }

    private SyncResponse SyncRequestReceived(SyncRequest req)
    {
        return new SyncResponse(req, "Ack Sync");
    }
}
