using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    class Program
    {
        static bool _UseKeepalives = false;
        static TcpClient _Client = null;
        static NetworkStream _NetworkStream = null;
        static CancellationTokenSource _TokenSource = new CancellationTokenSource();
        static CancellationToken _Token;

        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
                _UseKeepalives = args.Any(a => a.Equals("--keepalive"));

            _Client = new TcpClient();
            _Token = _TokenSource.Token;

            if (_UseKeepalives)
            {
                Console.WriteLine("Enabling TCP keepalives");
                SetTcpKeepalives();
            }
            else
            {
                Console.WriteLine("TCP keepalives disabled");
            }

            IAsyncResult ar = _Client.BeginConnect("127.0.0.1", 9000, null, null);
            WaitHandle wh = ar.AsyncWaitHandle;
             
            if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5), false))
            {
                _Client.Close();
                throw new TimeoutException();
            }

            _Client.EndConnect(ar);
            _NetworkStream = _Client.GetStream();
                  
            Task.Run(() => DataReceiver(), _Token);

            Console.WriteLine("Connected to tcp://127.0.0.1:9000, ENTER to exit");
            Console.ReadLine();
            _TokenSource.Cancel();
        }

        static void SetTcpKeepalives()
        {
            try
            {
#if NETCOREAPP || NET5_0

                _Client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                _Client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 5);
                _Client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
                _Client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);

#elif NETFRAMEWORK

                byte[] keepAlive = new byte[12];

                // Turn keepalive on
                Buffer.BlockCopy(BitConverter.GetBytes((uint)1), 0, keepAlive, 0, 4);

                // Set TCP keepalive time
                Buffer.BlockCopy(BitConverter.GetBytes((uint)4), 0, keepAlive, 4, 4); 

                // Set TCP keepalive interval
                Buffer.BlockCopy(BitConverter.GetBytes((uint)4), 0, keepAlive, 8, 4); 

                // Set keepalive settings on the underlying Socket
                _Client.Client.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);

#elif NETSTANDARD

#endif
            }
            catch (Exception)
            { 
            }
        }

        static async Task DataReceiver()
        { 
            while (!_Token.IsCancellationRequested)
            {
                byte[] data = new byte[4096]; 
                using (MemoryStream ms = new MemoryStream())
                {
                    int read = await _NetworkStream.ReadAsync(data, 0, data.Length, _Token).ConfigureAwait(false);

                    if (read > 0)
                    {
                        await ms.WriteAsync(data, 0, read, _Token).ConfigureAwait(false);
                        data = ms.ToArray();
                        break;
                    }
                }

                Console.WriteLine("[127.0.0.1:9000] " + Encoding.UTF8.GetString(data));
            } 
        } 
    }
}
