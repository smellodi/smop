using Smop.Common;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tcp.NET.Server;
using Tcp.NET.Server.Events.Args;
using Tcp.NET.Server.Models;

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
        _connectionCheckTask?.Wait();
        _connectionCheckTask?.Dispose();
        _connectionCheckTaskCancellation?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly TcpNETServer _server;
    Task? _connectionCheckTask;
    CancellationTokenSource? _connectionCheckTaskCancellation;

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

        if (status == Status.Connected)
        {
            _connectionCheckTaskCancellation?.Cancel();
            try
            {
                _connectionCheckTask?.Wait();
                _connectionCheckTask = null;
            }
            catch (Exception) { }
        }

        _client = status == Status.Connected ? args.Connection : null;
        ScreenLogger.Print($"[MlServer] {status}: {args.Connection?.TcpClient?.Client?.LocalEndPoint}");

        StatusChanged?.Invoke(this, status);

        if (status == Status.Connected)
        {
            // The following routine sends \0 byte every second to the client. This is the only way to keep tracking whether the client is still connected,
            // as the native server's mechanism to detect disconnection does no function if ping-pong functionality is disabled (and we want it to be disabled,
            // otherwise it constantly sends a string to ML client)

            _connectionCheckTaskCancellation = new();
            _connectionCheckTask = Task.Run(async () =>
            {
                await Task.Delay(1000);

                try
                {
                    while (_server.IsServerRunning)
                    {
                        var isConnected = args.Connection?.TcpClient?.Client?.Connected ?? false;
                        if (!isConnected)
                            break;

                        var isAbleToSendData = args.Connection?.TcpClient?.Client?.Send(new byte[] { 0 }) > 0;
                        if (!isAbleToSendData)
                            break;

                        await Task.Delay(1000);
                    }
                }
                catch (System.Net.Sockets.SocketException) { }

                _connectionCheckTaskCancellation = null;
                _connectionCheckTask = null;

                await Task.Delay(300);

            }, _connectionCheckTaskCancellation.Token);
        }
    }
}
