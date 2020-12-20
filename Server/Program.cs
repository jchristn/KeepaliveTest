using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        static bool _UseKeepalives = false;
        static TcpListener _Listener = null;
        static CancellationTokenSource _TokenSource = new CancellationTokenSource();
        static CancellationToken _Token;

        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
                _UseKeepalives = args.Any(a => a.Equals("--keepalive"));

            _Listener = new TcpListener(IPAddress.Loopback, 9000);

            if (_UseKeepalives)
            {
                Console.WriteLine("Enabling TCP keepalives");
                SetTcpKeepalives();
            }
            else
            {
                Console.WriteLine("TCP keepalives disabled");
            }

            _Listener.Start();
            _Token = _TokenSource.Token;

            Task.Run(() => AcceptConnections(), _Token);

            Console.WriteLine("Waiting for connections on tcp://127.0.0.1:9000, ENTER to exit");
            Console.ReadLine();
            _TokenSource.Cancel();
        }

        static void SetTcpKeepalives()
        {
#if NETCOREAPP || NET5_0

            _Listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _Listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 5);
            _Listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
            _Listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);

#elif NETFRAMEWORK

            byte[] keepAlive = new byte[12];

            // Turn keepalive on
            Buffer.BlockCopy(BitConverter.GetBytes((uint)1), 0, keepAlive, 0, 4);

            // Set TCP keepalive time
            Buffer.BlockCopy(BitConverter.GetBytes((uint)4), 0, keepAlive, 4, 4); 

            // Set TCP keepalive interval
            Buffer.BlockCopy(BitConverter.GetBytes((uint)4), 0, keepAlive, 8, 4); 

            // Set keepalive settings on the underlying Socket
            _Listener.Server.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);

#elif NETSTANDARD

#endif
        }

        static async void AcceptConnections()
        {
            while (!_Token.IsCancellationRequested)
            {
                TcpClient tcpClient = await _Listener.AcceptTcpClientAsync().ConfigureAwait(false);
                string clientIp = tcpClient.Client.RemoteEndPoint.ToString();
                Console.WriteLine("Connection received from: " + clientIp);
                await Task.Run(() => DataReceiver(tcpClient, clientIp), _Token);
            }
        }

        static async void DataReceiver(TcpClient tcpClient, string clientIp)
        {
            while (!_Token.IsCancellationRequested)
            {
                byte[] data = new byte[4096];

                using (MemoryStream ms = new MemoryStream())
                {
                    while (true)
                    {
                        int read = await tcpClient.GetStream().ReadAsync(data, 0, data.Length, _Token).ConfigureAwait(false);

                        if (read > 0)
                        {
                            await ms.WriteAsync(data, 0, read, _Token).ConfigureAwait(false);
                            data = ms.ToArray();
                            break;
                        }
                    }
                }

                Console.WriteLine("[" + clientIp + "] " + Encoding.UTF8.GetString(data));
            }
        }
    }
}
