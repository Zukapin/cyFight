using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using cyUtility;

namespace cyFight
{
    interface INetworkCallbacks
    {
        void OnConnect();
        void OnConnectionFailed();
        void OnDisconnect();
        void OnData(NetIncomingMessage msg);
    }

    class Network
    {
        NetClient client;
        INetworkCallbacks callbacks;

        bool tryConnect = false;
        int connectIndex = 0;
        static string[] hostnames = new string[] { "70.59.28.38", "192.168.0.11", "127.0.0.1" };

        const int port = 6114;

        public Network(INetworkCallbacks callbacks)
        {
            this.callbacks = callbacks;

            var config = new NetPeerConfiguration("cyfight");
            config.AutoFlushSendQueue = false;
            config.AcceptIncomingConnections = false;
            config.ConnectionTimeout = 60;
            config.EnableUPnP = false;
            config.MaximumConnections = 1;
            config.AutoExpandMTU = true;
            config.ExpandMTUFailAttempts = 5;
            config.ExpandMTUFrequency = 2.0f;
            config.NetworkThreadName = "cyClientNetwork";
            config.PingInterval = 4.0f;
            config.ResendHandshakeInterval = 3.0f;
            config.MaximumHandshakeAttempts = 5;
            config.UnreliableSizeBehaviour = NetUnreliableSizeBehaviour.IgnoreMTU;
            config.UseMessageRecycling = true;

#if DEBUG
            config.SimulatedDuplicatesChance = 0.0f; //0-1f
            config.SimulatedLoss = 0.0f; //0-1f
            config.SimulatedMinimumLatency = 0.0f; //seconds
            config.SimulatedRandomLatency = 0.0f; //seconds
#endif

            config.Port = 0; //local port

            client = new NetClient(config);
            client.Start(); //this sleeps for 50ms >.>
        }

        public string CurrentConnectionTarget
        {
            get
            {
                if (tryConnect)
                    return hostnames[connectIndex - 1];
                return "Not Connecting";
            }
        }

        public string NetworkStats
        {
            get
            {
                if (client.ServerConnection == null)
                    return "No Connection";
                return client.Statistics.ToString()
                    + "Roundtrip Time: " + (client.ServerConnection.AverageRoundtripTime * 1000) + " ms";
            }
        }

        public bool IsConnected
        {
            get
            {
                return client.ConnectionStatus == NetConnectionStatus.Connected;
            }
        }

        public NetOutgoingMessage CreateMessage()
        {
            return client.CreateMessage();
        }

        public void SendMessage(NetOutgoingMessage msg, NetDeliveryMethod deliveryMethod, int sequence)
        {
            client.SendMessage(msg, deliveryMethod, sequence);
        }

        public void Connect()
        {
            var status = client.ConnectionStatus;
            if (client.Status != NetPeerStatus.Running || 
                !(status == NetConnectionStatus.None || status == NetConnectionStatus.Disconnected) ||
                tryConnect)
            {
                Logger.WriteLine(LogType.POSSIBLE_ERROR, "Network Connect() was called, while the NetClient had an invalid state: " + client.Status + " " + status + " " + tryConnect);
            }

            tryConnect = true;
            connectIndex = 1;
            client.Connect(hostnames[0], port);
            //the return from Connect() is mostly useless -- the client isn't actually connected yet, 
            //and we only send messages to the connected server so we don't terribly care about the conn info
        }

        public void Disconnect()
        {//can be called by client or by getting a disconnect message -- in either case, clean up here
        }

        public void SendMessages()
        {//we wait for an update to finish before actually sending any messages -- we want things to be bundled into a single packet per frame, ideally
            client.FlushSendQueue();
        }

        public void ReadMessages()
        {
            NetIncomingMessage msg;
            while ((msg = client.ReadMessage()) != null)
            {
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.Error: //"should never happen"
                    case NetIncomingMessageType.ErrorMessage:
                    case NetIncomingMessageType.WarningMessage:
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.VerboseDebugMessage:
                        Logger.WriteLine(LogType.DEBUG, msg.ReadString());
                        break;

                    case NetIncomingMessageType.StatusChanged:
                        NetConnectionStatus status = (NetConnectionStatus)msg.ReadByte();
                        var reason = msg.ReadString();

                        switch (status)
                        {
                            case NetConnectionStatus.Connected:
                                Logger.WriteLine(LogType.DEBUG, "Connected:" + reason);
                                tryConnect = false;
                                callbacks.OnConnect();
                                break;
                            case NetConnectionStatus.Disconnected:
                                Logger.WriteLine(LogType.DEBUG, "Disconnected: " + reason);
                                if (tryConnect)
                                {
                                    if (connectIndex != hostnames.Length)
                                    {
                                        client.Connect(hostnames[connectIndex++], port);
                                    }
                                    else
                                    {
                                        tryConnect = false;
                                        callbacks.OnConnectionFailed();
                                    }
                                }
                                else
                                {
                                    Disconnect();
                                    callbacks.OnDisconnect();
                                }
                                break;
                            default:
                                Logger.WriteLine(LogType.DEBUG, "Unhandled connection status: " + status + " with reason: " + reason);
                                break;
                        }
                        break;

                    case NetIncomingMessageType.Data:
                        callbacks.OnData(msg);
                        break;

                    default:
                        Logger.WriteLine(LogType.DEBUG, "Unhandled message type: " + msg.MessageType + " " + msg.LengthBytes);
                        break;
                }
                client.Recycle(msg);
            }
        }

        public void Dispose()
        {
            //this ends this network forever
            //further calls to network will go poorly
            if (client.ConnectionStatus != NetConnectionStatus.Disconnected)
                client.Disconnect("Client weird shutdown"); //sometimes this message is actually used
            client.Shutdown("Client shutdown"); //mostly its this one
            //we have the thread.sleep here to help it actually send the disconnects to the remote peer
            //remote may *still* not get it, with latency and if the packet drops or something
            //but, well, should only really happen, in a finished game, during alt-f4 or hardcrash, and we're not waiting around to find out
            System.Threading.Thread.Sleep(50);
        }
    }
}
