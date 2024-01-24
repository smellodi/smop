using Smop.Common;
using System.Text;
using System.Threading.Tasks;
using Tcp.NET.Client;
using Tcp.NET.Client.Models;

namespace Smop.ML;

internal class TcpSimulator : Simulator
{
    public TcpSimulator()
    {
        _client = new TcpNETClient(new ParamsTcpClient("localhost", TcpServer.Port, TcpServer.LineEnd, isSSL: false));
        _client.MessageEvent += Client_MessageEvent;
        _client.ConnectionEvent += (s, e) => ScreenLogger.Print($"[MlSimul] {e.ConnectionEventType}");
        _client.ErrorEvent += (s, e) => ScreenLogger.Print($"[MlSimul] error: {e.Message}"); ;
        _client.ConnectAsync();
    }

    public override void Dispose()
    {
        Task.WaitAll(_client.DisconnectAsync());
        _client.Dispose();
    }


    // Internal

    readonly ITcpNETClient _client;

    protected override async Task SendData(string data)
    {
        await _client.SendAsync(data);
    }

    private void Client_MessageEvent(object sender, Tcp.NET.Client.Events.Args.TcpMessageClientEventArgs args)
    {
        if (args.MessageEventType == PHS.Networking.Enums.MessageEventType.Receive)
        {
            string json = Encoding.UTF8.GetString(args.Bytes);
            ParseJson(json);
        }
    }
}
