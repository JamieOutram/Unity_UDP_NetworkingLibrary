using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UnityNetworkingLibrary
{
    using ExceptionExtensions;

    //Adapted from louis-e/UDPSocket.cs
    //https://gist.github.com/louis-e/888d5031190408775ad130dde353e0fd

    public class UDPSocket
    {
        private Socket _socket;
        public Socket Socket { get { return _socket; } }
        private const int bufSize = PacketManager._maxPacketSizeBits;
        private State state = new State();
        private EndPoint epFrom = new IPEndPoint(IPAddress.Any, 0);
        private AsyncCallback recv = null;

        public delegate void UdpOnReceived(byte[] data, int bytesRead);
        public event UdpOnReceived OnReceived;

        object isReadyLock = new object();
        public bool _isReady;
        public bool IsReady //True when the socket is ready to send data
        {
            get
            {
                lock (isReadyLock)
                {
                    return _isReady;
                }
            }
            private set
            {
                lock (isReadyLock)
                {
                    _isReady = value;
                }
            }
        }

        public UDPSocket()
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IsReady = false;
        }

        public class State
        {
            public byte[] buffer = new byte[bufSize];
        }

        //Attempts to bind socket to provided port and target IP
        public bool Server(string address, int port)
        {
            try
            {
                _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
                _socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
                Receive();
                IsReady = true;
                return true;
            }
            catch (SocketException)
            {
                IsReady = false;
                return false;
            }
        }

        public bool Client(string address, int port)
        {
            try
            {
                _socket.Connect(IPAddress.Parse(address), port);
                Receive();
                IsReady = true;
                return true;
            }
            catch (SocketException)
            {
                IsReady = false;
                return false;
            }
        }

        public void Send(string text)
        {
            
            IsReady = false;
            byte[] data = Encoding.ASCII.GetBytes(text);
            _socket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
            {
                State so = (State)ar.AsyncState;
                int bytes = _socket.EndSend(ar);
                IsReady = true;
                Console.WriteLine("SEND: {0}, {1}", bytes, text);
            }, state);
        }

        public void Send(byte[] data)
        {
            
            if (data.Length > bufSize)
                throw new PacketSizeException();

            if (IsReady == false)
                throw new SocketNotReadyException();

            IsReady = false;
            _socket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
            {
                State so = (State)ar.AsyncState;
                int bytes = _socket.EndSend(ar);
                IsReady = true;
            }, state);
        }


        private void Receive()
        {
            _socket.BeginReceiveFrom(state.buffer, 0, bufSize, SocketFlags.None, ref epFrom, recv = (ar) =>
            {
                try
                {
                    State so = (State)ar.AsyncState;
                    int bytes = _socket.EndReceiveFrom(ar, ref epFrom);
                    _socket.BeginReceiveFrom(so.buffer, 0, bufSize, SocketFlags.None, ref epFrom, recv, so);
                    Console.WriteLine("RECV: {0}: {1}, {2}", epFrom.ToString(), bytes, Encoding.ASCII.GetString(so.buffer, 0, bytes));
                    OnReceived?.Invoke(state.buffer, bytes); //This event call could possibly be optimised by letting delaying till after processing of sent data 
                }
                catch { }//TODO: This should only catch expected errors
            }, state);
        }
    }
}
