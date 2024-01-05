using Smop.Common;
using System;
using System.Text;
using System.Threading.Tasks;
using WatsonTcp;

namespace Smop.ML;

public enum Status { Disconnected, Connected }

internal class TcpServer : Server
{
    public static int Port => 2339;

    public override bool IsClientConnected => _client != Guid.Empty;

    public event EventHandler<Status>? StatusChanged;

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
        ScreenLogger.Print("[SERVER] connected: " + args.Client.ToString());
        StatusChanged?.Invoke(this, Status.Connected);
    }

    private void ClientDisconnected(object? sender, DisconnectionEventArgs args)
    {
        _client = Guid.Empty;
        ScreenLogger.Print("[SERVER] disconnected: " + args.Client.ToString() + ": " + args.Reason.ToString());
        StatusChanged?.Invoke(this, Status.Disconnected);
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
