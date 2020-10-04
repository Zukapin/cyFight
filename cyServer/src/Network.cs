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
    enum DataIDSend : int
    {
        NEW_PLAYER = 0,
        NEW_PLAYER_YOU = 1,
    }

    enum DataIDRecv : int
    {

    }

    class Network
    {
        const int PORT = 6114;
        NetServer serv;
        Random random;

        CySim sim;

        public Network(CySim sim)
        {
            this.sim = sim;
            random = new Random();

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
            config.SimulatedDuplicatesChance = 0.0f; //0-1f
            config.SimulatedLoss = 0.0f; //0-1f
            config.SimulatedMinimumLatency = 0.0f; //seconds
            config.SimulatedRandomLatency = 0.0f; //seconds
#endif

            config.Port = PORT; //local port

            sim.Load(out var bodyHandles);

            serv = new NetServer(config);
            serv.Start(); //this sleeps for 50ms >.>
        }

        void OnConnect(NetConnection conn)
        {
            Vector3 startPos = new Vector3(0, 10, 20);
            var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(random.NextDouble() * Math.PI * 2));
            startPos = QuaternionEx.Transform(startPos, rot);
            var playerID = sim.AddPlayer(startPos);

            var msgToNewPlayer = serv.CreateMessage();
            msgToNewPlayer.Write((int)DataIDSend.NEW_PLAYER_YOU);
            msgToNewPlayer.Write(sim.CurrentFrame);
            msgToNewPlayer.Write(playerID);
            SerializeState(ref msgToNewPlayer);
            serv.SendMessage(msgToNewPlayer, conn, NetDeliveryMethod.ReliableOrdered, 0);

            /*
            var msgToOtherPlayers = serv.CreateMessage();
            msgToOtherPlayers.Write((int)DataIDSend.NEW_PLAYER);
            msgToOtherPlayers.Write(sim.CurrentFrame);
            msgToOtherPlayers.Write(playerID);
            msgToOtherPlayers.Write(startPos.X);
            msgToOtherPlayers.Write(startPos.Y);
            msgToOtherPlayers.Write(startPos.Z);
            serv.SendToAll(msgToOtherPlayers, conn, NetDeliveryMethod.ReliableOrdered, 0);
            */
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SerializePlayerInput(ref PlayerInput input, ref NetOutgoingMessage msg)
        {
            msg.Write(input.MoveForward);
            msg.Write(input.MoveBackward);
            msg.Write(input.MoveLeft);
            msg.Write(input.MoveRight);
            msg.Write(input.Sprint);
            msg.Write(input.TryJump); //do we even want to send these?
            msg.Write(input.TryFire);
            msg.WritePadBits();
            msg.Write(input.ViewDirection.X);
            msg.Write(input.ViewDirection.Y);
            msg.Write(input.ViewDirection.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SerializeState(ref NetOutgoingMessage msg)
        {
            msg.Write(sim.Players.Count);
            for (int i = 0; i < sim.Players.Count; i++)
            {
                var p = sim.Players[i];
                msg.Write(i);
                SerializeFullBody(p.BodyHandle, ref msg);
                SerializeFullBody(p.HammerHandle, ref msg);
                SerializePlayerInput(ref p.Input, ref msg);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SerializeFullBody(BodyHandle handle, ref NetOutgoingMessage msg)
        {
            var bodyRef = new BodyReference(handle, sim.Simulation.Bodies);
            msg.Write(handle.Value);
        }

        void OnDisconnect(NetConnection conn)
        {

        }

        void OnData(NetIncomingMessage msg)
        {

        }

        public void SendMessages()
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
    }
}
