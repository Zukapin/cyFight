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

#if DEBUG
            config.SimulatedDuplicatesChance = 0.01f; //0-1f
            config.SimulatedLoss = 0.01f; //0-1f
            config.SimulatedMinimumLatency = 0.1f; //seconds
            config.SimulatedRandomLatency = 0.05f; //seconds
#endif

            config.Port = PORT; //local port

            CurLevel = Level.LoadLevel(0, this);

            serv = new NetServer(config);
            serv.Start(); //this sleeps for 50ms >.>
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
                    case NetIncomingMessageType.ErrorMessage:
                        Logger.WriteLine(LogType.ERROR, msg.ReadString());
                        break;
                    case NetIncomingMessageType.WarningMessage:
                        Logger.WriteLine(LogType.POSSIBLE_ERROR, msg.ReadString());
                        break;
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
                                OnConnect(msg.SenderConnection);
                                break;
                            case NetConnectionStatus.Disconnected:
                                Logger.WriteLine(LogType.DEBUG, "Disconnected: " + reason);
                                OnDisconnect(msg.SenderConnection);
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
            }
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializePlayer(int i, CySim sim, NetOutgoingMessage msg)
        {
            var p = sim.GetPlayer(i);
            msg.Write(i);
            SerializeBody(p.BodyHandle, sim.Simulation, msg);
            //possibly send character support or other status here
            SerializeBody(p.HammerHandle, sim.Simulation, msg);
            ref var h = ref p.Hammer;
            msg.Write((byte)h.HammerState);
            msg.Write(h.HammerDT);
            NetInterop.SerializePlayerInput(ref p.Input, msg);
        }

        /// <summary>
        /// Serializes a dynamic body's current state -- position, orientation, linear velocity, angular velocity
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeBody(BodyHandle handle, Simulation Simulation, NetOutgoingMessage msg)
        {
            var bodyRef = new BodyReference(handle, Simulation.Bodies);
            var pose = bodyRef.Pose;
            var vel = bodyRef.Velocity;

            msg.Write(handle.Value);
            msg.Write(pose.Position.X);
            msg.Write(pose.Position.Y);
            msg.Write(pose.Position.Z);
            msg.Write(pose.Orientation.X);
            msg.Write(pose.Orientation.Y);
            msg.Write(pose.Orientation.Z);
            msg.Write(pose.Orientation.W);
            msg.Write(vel.Linear.X);
            msg.Write(vel.Linear.Y);
            msg.Write(vel.Linear.Z);
            msg.Write(vel.Angular.X);
            msg.Write(vel.Angular.Y);
            msg.Write(vel.Angular.Z);
        }
    }
}
