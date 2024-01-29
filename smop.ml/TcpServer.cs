using Smop.Common;
using System;
using System.Text;
using System.Threading.Tasks;
using Tcp.NET.Server;
using Tcp.NET.Server.Models;
using Tcp.NET.Server.Events.Args;

namespace Smop.ML;

public enum Status { Disconnected, Connected }

internal class TcpServer : Server
{
    public static int Port => 2339;
    public static string LineEnd => "\r\n";

    public override bool IsClientConnected => _server.ConnectionCount > 0;

    public event EventHandler<Status>? StatusChanged;

    public TcpServer()
    {
        _server = new TcpNETServer(new ParamsTcpServer(Port, LineEnd, pingIntervalSec: 0));
        _server.ConnectionEvent += Server_ConnectionEvent;
        _server.MessageEvent += Server_MessageEvent;
        _server.ErrorEvent += (s, e) => PublishError(e.Message);
        _server.ServerEvent += (s, e) => ScreenLogger.Print($"[MlServer] event: {e.ServerEventType}");
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
}
