using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.CompilerServices;
using Lidgren.Network;
using cyUtility;
using cySim;
using BepuUtilities;
using BepuUtilities.Collections;
using BepuPhysics;
using System.Threading;

namespace cyServer
{
    class Network
    {
        const int PORT = 6114;
        NetServer serv;

        Level CurLevel;

        public Network()
        {
            var config = new NetPeerConfiguration("cyfight");
            config.AutoFlushSendQueue = false;
            config.AcceptIncomingConnections = true;
            config.ConnectionTimeout = 60;
            config.EnableUPnP = false;
            config.MaximumConnections = 128;
            config.AutoExpandMTU = true;
            config.ExpandMTUFailAttempts = 5;
            config.ExpandMTUFrequency = 2.0f;
            config.NetworkThreadName = "cyServerNetwork";
            config.PingInterval = 4.0f;
            config.ResendHandshakeInterval = 3.0f;
            config.MaximumHandshakeAttempts = 5;
            config.UnreliableSizeBehaviour = NetUnreliableSizeBehaviour.IgnoreMTU;
            config.UseMessageRecycling = true;

#if TEST_SIM
            config.AutoExpandMTU = false;
            config.MaximumTransmissionUnit = 8190;
            config.UnreliableSizeBehaviour = NetUnreliableSizeBehaviour.NormalFragmentation;
#endif

#if DEBUG
            config.SimulatedDuplicatesChance = 0.0f; //0-1f
            config.SimulatedLoss = 0.0f; //0-1f
            config.SimulatedMinimumLatency = 0.0f; //seconds
            config.SimulatedRandomLatency = 0.0f; //seconds
#endif

            config.Port = PORT; //local port

            CurLevel = new LevelOne(this);

            serv = new NetServer(config);
            serv.Start(); //this sleeps for 50ms >.>
        }

#if DEBUG
        [Conditional("DEBUG")]
        public void LagStart1()
        {
            serv.Configuration.SimulatedMinimumLatency = 0.1f; //seconds
            serv.Configuration.SimulatedRandomLatency = 0.0f; //seconds
        }

        [Conditional("DEBUG")]
        public void LagStart2()
        {
            serv.Configuration.SimulatedMinimumLatency = 0.0f; //seconds
            serv.Configuration.SimulatedRandomLatency = 0.1f; //seconds
        }

        [Conditional("DEBUG")]
        public void LagStart3()
        {
            serv.Configuration.SimulatedMinimumLatency = 0.1f; //seconds
            serv.Configuration.SimulatedRandomLatency = 0.1f; //seconds
        }

        [Conditional("DEBUG")]
        public void LagStart4()
        {
            serv.Configuration.SimulatedMinimumLatency = 0.3f; //seconds
            serv.Configuration.SimulatedRandomLatency = 0.0f; //seconds
        }

        [Conditional("DEBUG")]
        public void LagStart5()
        {
            serv.Configuration.SimulatedMinimumLatency = 0.0f; //seconds
            serv.Configuration.SimulatedRandomLatency = 0.3f; //seconds
        }

        [Conditional("DEBUG")]
        public void LagStart6()
        {
            serv.Configuration.SimulatedMinimumLatency = 0.3f; //seconds
            serv.Configuration.SimulatedRandomLatency = 0.3f; //seconds
        }

        [Conditional("DEBUG")]
        public void LagStop()
        {
            serv.Configuration.SimulatedMinimumLatency = 0.0f; //seconds
            serv.Configuration.SimulatedRandomLatency = 0.0f; //seconds
        }
#endif

        public void Shutdown()
        {
            serv.Shutdown("Server killing itself alas");
            serv.FlushSendQueue();
            Thread.Sleep(50); //give a little bit of time to send the shutdown messages
        }

        public NetOutgoingMessage CreateMessage()
        {
            return serv.CreateMessage();
        }

        public void Send(NetOutgoingMessage msg, NetConnection conn, NetDeliveryMethod method, int sequence)
        {
            serv.SendMessage(msg, conn, method, sequence);
        }

        public void Send(NetOutgoingMessage msg, List<NetConnection> conn, NetDeliveryMethod method, int sequence)
        {
            serv.SendMessage(msg, conn, method, sequence);
        }

        void OnConnect(NetConnection conn)
        {
            CurLevel.OnConnect(conn);
        }

        void OnDisconnect(NetConnection conn)
        {
            CurLevel.OnDisconnect(conn);
        }

        void OnData(NetIncomingMessage msg)
        {
            CurLevel.OnData(msg);
        }

        public void Update(float dt)
        {
            ReadMessages();
            CurLevel.Update(dt);
            SendMessages();
        }

        void SendMessages()
        {//called after physics update
            serv.FlushSendQueue();
        }

        public void ReadMessages()
        {//called before physics update
            NetIncomingMessage msg;
            while ((msg = serv.ReadMessage()) != null)
            {
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.Error: //"should never happen"
                        Logger.WriteLine(LogType.ERROR, "Unknown error in message read");
                        break;
                    case NetIncomingMessageType.ErrorMessage:
                        Logger.WriteLine(LogType.ERROR, msg.ReadString());
                        break;
                    case NetIncomingMessageType.WarningMessage:
                        Logger.WriteLine(LogType.POSSIBLE_ERROR, msg.ReadString());
                        break;
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.VerboseDebugMessage:
                        Logger.WriteLine(LogType.VERBOSE, "Lidgren debug message: " + msg.ReadString());
                        break;

                    case NetIncomingMessageType.StatusChanged:
                        NetConnectionStatus status = (NetConnectionStatus)msg.ReadByte();
                        var reason = msg.ReadString();

                        switch (status)
                        {
                            case NetConnectionStatus.Connected:
                                Logger.WriteLine(LogType.DEBUG, "Connected to " + msg.SenderEndPoint.Address.ToString());
                                OnConnect(msg.SenderConnection);
                                break;
                            case NetConnectionStatus.Disconnected:
                                Logger.WriteLine(LogType.DEBUG, "Disconnected: " + reason);
                                OnDisconnect(msg.SenderConnection);
                                break;
                            case NetConnectionStatus.RespondedConnect:
                                Logger.WriteLine(LogType.VERBOSE, "Connection attempt recieved: " + reason);
                                break;
                            default:
                                Logger.WriteLine(LogType.DEBUG, "Unhandled connection status: " + status + " with reason: " + reason);
                                break;
                        }
                        break;

                    case NetIncomingMessageType.Data:
                        OnData(msg);
                        break;

                    default:
                        Logger.WriteLine(LogType.DEBUG, "Unhandled message type: " + msg.MessageType + " " + msg.LengthBytes);
                        break;
                }

                serv.Recycle(msg);
            }
        }
    }
}
