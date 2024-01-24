using Smop.Common;
using System;
using System.Text;
using System.Threading.Tasks;
//using WatsonTcp;
using Tcp.NET.Server;
using Tcp.NET.Server.Models;
using Tcp.NET.Server.Events.Args;

namespace Smop.ML;

public enum Status { Disconnected, Connected }

internal class TcpServer : Server
{
    public static int Port => 2339;

    public override bool IsClientConnected => _server.ConnectionCount > 0;

    public event EventHandler<Status>? StatusChanged;

    public TcpServer()
    {
        _server = new TcpNETServer(new ParamsTcpServer(Port, "\r\n", null, false, 0));
        _server.ConnectionEvent += Server_ConnectionEvent; ;
        _server.MessageEvent += Server_MessageEvent;
        _server.ErrorEvent += (s, e) => ScreenLogger.Print($"[MlServer] error: {e.Message}"); ;
        _server.ServerEvent += (s, e) => ScreenLogger.Print($"[MlServer] event: {e.ServerEventType}"); ;
        _server.StartAsync();
    }

    public override void Dispose()
    {
        _server.StopAsync();
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly TcpNETServer _server;

    ConnectionTcpServer? _client = null;

    protected override async Task SendTextAsync(string data)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(data);
        await _server.SendToConnectionAsync(bytes, _client);
    }

    private void Server_MessageEvent(object sender, TcpMessageServerEventArgs args)
    {
        if (args.MessageEventType == PHS.Networking.Enums.MessageEventType.Receive)
        {
            var json = Encoding.UTF8.GetString(args.Bytes).Trim();
            ParseJson(json);
        }
    }

    private void Server_ConnectionEvent(object sender, TcpConnectionServerEventArgs args)
    {
        var status = args.ConnectionEventType == PHS.Networking.Enums.ConnectionEventType.Connected ? Status.Connected : Status.Disconnected;
        ScreenLogger.Print($"[MlServer] {status}: {args.Connection?.TcpClient?.Client?.LocalEndPoint}");

        _client = status == Status.Connected ? args.Connection : null;
        StatusChanged?.Invoke(this, status);
    }

    /*
    public override bool IsClientConnected => _client != Guid.Empty;

    public TcpServer()
    {
        _server = new WatsonTcpServer("0.0.0.0", Port);
        _server.Events.ClientConnected += ClientConnected;
        _server.Events.ClientDisconnected += ClientDisconnected;
        _server.Events.MessageReceived += MessageReceived;
        _server.Events.ExceptionEncountered += (s, e) => ScreenLogger.Print("[MlServer] error: " + e.Exception.ToString());
        _server.Events.StreamReceived += (s, e) =>
        {
            var a = Encoding.UTF8.GetString(e.Data);
            ScreenLogger.Print("[MlServer] stream: " + a);
        };
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
        ScreenLogger.Print("[MlServer] connected: " + args.Client.ToString());
        StatusChanged?.Invoke(this, Status.Connected);
    }

    private void ClientDisconnected(object? sender, DisconnectionEventArgs args)
    {
        _client = Guid.Empty;
        ScreenLogger.Print("[MlServer] disconnected: " + args.Client.ToString() + ": " + args.Reason.ToString());
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
    */
}
