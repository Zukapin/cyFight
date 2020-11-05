using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Numerics;

using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Collections;
using BepuUtilities.Memory;

using cySim;
using System.ComponentModel;
using Lidgren.Network;
using System.Runtime.InteropServices;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using cyUtility;
using System.Transactions;
using System.Threading;

namespace cyServer
{
    abstract class Level
    {
        protected Network Network;
        protected CySim sim;
        protected Simulation Simulation { get { return sim.Simulation; } }
        List<IBodyDesc> bodyDesc;

        Random Random = new Random();
        List<NetConnection> connections = new List<NetConnection>();

        QuickList<BodyHandle> dynBodies;
        QuickList<BodyHandle> kinBodies;

        struct ServerPlayerData
        {
            public NetConnection Conn;
            public int LatestInputFrame;
            public PriorityQueue PlayerInputPriorities;
            public PriorityQueue PlayerBodyPriorities;
            public PriorityQueue BodyPriorities;

            public void Clear()
            {
                LatestInputFrame = -1;
                PlayerInputPriorities.Clear();
                PlayerBodyPriorities.Clear();
                BodyPriorities.Clear();
            }
            public void EnsurePlayerCapacity(int capacity)
            {
                PlayerInputPriorities.EnsureCapacity(capacity);
                PlayerBodyPriorities.EnsureCapacity(capacity);
            }

            public void EnsureBodyCapacity(int capacity)
            {
                BodyPriorities.EnsureCapacity(capacity);
            }

            public void SortPriorities()
            {
                PlayerInputPriorities.Sort();
                PlayerBodyPriorities.Sort();
                BodyPriorities.Sort();
            }
        }
        ServerPlayerData[] PlayerData;

        public Level(Network Network)
        {
            this.Network = Network;
            sim = new CySim();
            sim.Init();

            bodyDesc = new List<IBodyDesc>();
            dynBodies = new QuickList<BodyHandle>(1024, sim.BufferPool);
            kinBodies = new QuickList<BodyHandle>(1024, sim.BufferPool);
            Init(ref bodyDesc, ref dynBodies, ref kinBodies);

            EnsurePlayerSize(128);
            EnsureBodySize(sim.Simulation.Bodies.HandlePool.HighestPossiblyClaimedId + 1);
        }

        protected abstract void Init(ref List<IBodyDesc> bodyDesc, ref QuickList<BodyHandle> dynBodies, ref QuickList<BodyHandle> kinBodies);

        void EnsurePlayerSize(int capacity)
        {
            if (PlayerData == null)
            {
                PlayerData = new ServerPlayerData[capacity];
            }
            else if (capacity > PlayerData.Length)
            {
                int newSize = Math.Max(capacity, PlayerData.Length * 2);
                Array.Resize(ref PlayerData, newSize);
            }

            foreach (var pID in sim.PlayerIDs)
            {
                PlayerData[pID].EnsurePlayerCapacity(capacity);
            }
        }

        void EnsureBodySize(int capacity)
        {
            foreach (var pID in sim.PlayerIDs)
            {
                PlayerData[pID].EnsureBodyCapacity(capacity);
            }
        }

        int NewPlayer(out Vector3 startPos)
        {
            startPos = new Vector3(0, 1, 20);
            var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(Random.NextDouble() * Math.PI * 2));
            startPos = QuaternionEx.Transform(startPos, rot);

            var playerID = sim.AddPlayer(startPos);
            EnsurePlayerSize(playerID + 1);
            ref var playerData = ref PlayerData[playerID];
            playerData.Clear();
            playerData.EnsureBodyCapacity(sim.Simulation.Bodies.HandlePool.HighestPossiblyClaimedId + 1);

            foreach (var d in dynBodies)
            {
                playerData.BodyPriorities.AddIndex(d.Value);
            }

            foreach (var k in kinBodies)
            {
                playerData.BodyPriorities.AddIndex(k.Value);
            }

            foreach (var pID in sim.PlayerIDs)
            {
                playerData.PlayerBodyPriorities.AddIndex(pID);
                playerData.PlayerInputPriorities.AddIndex(pID);

                if (pID == playerID)
                    continue;

                ref var p = ref PlayerData[pID];
                p.PlayerBodyPriorities.AddIndex(playerID);
                p.PlayerInputPriorities.AddIndex(playerID);
            }

            return playerID;
        }

        void RemovePlayer(int playerID)
        {
            sim.RemovePlayer(playerID);

            foreach (var pID in sim.PlayerIDs)
            {
                var pDat = PlayerData[pID];
                pDat.PlayerInputPriorities.RemoveIndex(playerID);
                pDat.PlayerBodyPriorities.RemoveIndex(playerID);
            }
        }

        void SerializeAll(NetOutgoingMessage msg)
        {
            msg.Write(sim.CurrentFrame);

            msg.Write(bodyDesc.Count);
            foreach (var b in bodyDesc)
            {
                b.Serialize(msg, Simulation);
            }

            msg.Write(sim.PlayerCount);
            foreach (var i in sim.PlayerIDs)
            {
                NetInterop.SerializePlayer(i, sim, msg);
                NetInterop.SerializePlayerInput(ref sim.GetPlayer(i).Input, msg);
            }
        }

        public void OnConnect(NetConnection conn)
        {
            var playerID = NewPlayer(out var startPos);
            conn.Tag = playerID;
            PlayerData[playerID].Conn = conn;

            var msgToNewPlayer = Network.CreateMessage();
            msgToNewPlayer.Write((int)NetServerToClient.NEW_PLAYER_YOU);
            msgToNewPlayer.Write(playerID);
            SerializeAll(msgToNewPlayer);
            Network.Send(msgToNewPlayer, conn, NetDeliveryMethod.ReliableOrdered, 0);

            if (connections.Count > 0)
            {
                var msgToOtherPlayers = Network.CreateMessage();
                msgToOtherPlayers.Write((int)NetServerToClient.NEW_PLAYER);
                msgToOtherPlayers.Write(sim.CurrentFrame);
                msgToOtherPlayers.Write(playerID);
                msgToOtherPlayers.Write(startPos.X);
                msgToOtherPlayers.Write(startPos.Y);
                msgToOtherPlayers.Write(startPos.Z);
                Network.Send(msgToOtherPlayers, connections, NetDeliveryMethod.ReliableOrdered, 0);
            }

            connections.Add(conn);
        }

        public void OnData(NetIncomingMessage msg)
        {
            var playerID = msg.SenderConnection.Tag as int?;
            if (!playerID.HasValue)
            {
                Logger.WriteLine(LogType.ERROR, "Conn on data doesn't have a player ID tag");
                return;
            }

            if (!sim.PlayerExists(playerID.Value))
            {
                Logger.WriteLine(LogType.ERROR, "Data connection has an invalid player ID tag? " + playerID.Value);
                return;
            }

            ref var playerData = ref PlayerData[playerID.Value];

            var msgID = (NetClientToServer)msg.ReadInt32();
            if (!Enum.IsDefined(msgID))
            {
                Logger.WriteLine(LogType.POSSIBLE_ERROR, "Recieved an invalid message ID " + (int)msgID + " from: " + msg.SenderConnection);
                return;
            }

            switch (msgID)
            {
                case NetClientToServer.PLAYER_INPUT:
                    var player = sim.GetPlayer(playerID.Value);
                    var frame = msg.ReadInt32();
                    if (frame > playerData.LatestInputFrame)
                    {
                        playerData.LatestInputFrame = frame;
                        NetInterop.ReadPlayerInput(ref player.Input, msg);
                    }
                    break;
                default:
                    Logger.WriteLine(LogType.ERROR, "Unhandled message type on server data: " + msgID);
                    break;
            }
        }

        public void OnDisconnect(NetConnection conn)
        {
            var playerID = conn.Tag as int?;
            if (!playerID.HasValue)
            {
                Logger.WriteLine(LogType.ERROR, "Conn on disconnect doesn't have a player ID tag");
                return;
            }

            if (!sim.PlayerExists(playerID.Value))
            {
                Logger.WriteLine(LogType.ERROR, "Disconnected connection has an invalid player ID tag? " + playerID.Value);
                return;
            }

            RemovePlayer(playerID.Value);
            connections.Remove(conn);

            if (connections.Count > 0)
            {
                var msg = Network.CreateMessage();
                msg.Write((int)NetServerToClient.REMOVE_PLAYER);
                msg.Write(sim.CurrentFrame);
                msg.Write(playerID.Value);
                Network.Send(msg, connections, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public void Update(float dt)
        {
            SendUpdateMessages();

            sim.Update(dt);
        }

        void UpdatePriorities()
        {
            //notes for further improvements for state updates:
            //ideas ordered based on expected payoff
            //massively shrink the player input size -- 1or2 byte for playerID, 1 byte for input, 2 bytes for yaw, for 4-5 bytes down from 15 -- can do 100 players and only take 1/3rd of the buffer
            //on kinematic velocity change add a priority to it -- somewhere between 10 and 100 probably -- required if kinematic velocity changes exist
            //add priority per dyn body based on total impulse size per frame -- should capture the 'diverging' events better, could also direct-priority hammerhits
            //add priority based on distance -- just doing straight distancedsquared checks probably best, norbo says should be ezfast, can split distance updates over multipleframes
            foreach (var pID in sim.PlayerIDs)
            {
                var p = PlayerData[pID];
                
                foreach (var p2 in sim.PlayerIDs)
                {
                    p.PlayerInputPriorities.Priorities[p2] += 100;
                    p.PlayerBodyPriorities.Priorities[p2] += 10;
                }

                foreach (var d in dynBodies)
                {
                    p.BodyPriorities.Priorities[d.Value] += 1;
                }
            }
        }
        
        void SendUpdateMessages()
        {
            UpdatePriorities();

            //doing a very dumb 'send everything to everyone' plan
            foreach (var pID in sim.PlayerIDs)
            {
                var p = PlayerData[pID];
                int MTU = 1450;

                var msg = Network.CreateMessage();
                WriteMessage(p, MTU, msg);
#if TEST_SIM
                WriteAll(msg);
#endif

                Network.Send(msg, p.Conn, NetDeliveryMethod.Unreliable, 0);
            }
        }

        void WriteAll(NetOutgoingMessage msg)
        {
            msg.Write((int)NetServerToClient.STATE_UPDATE);
            msg.Write(sim.CurrentFrame);
            msg.Write((short)sim.PlayerCount);
            foreach (var i in sim.PlayerIDs)
            {
                var p = sim.GetPlayer(i);
                msg.Write((short)i);
                NetInterop.SerializePlayerInput(ref p.Input, msg);
            }
            msg.Write((short)sim.PlayerCount);
            foreach (var i in sim.PlayerIDs)
            {
                NetInterop.SerializePlayer(i, sim, msg);
            }
            msg.Write((short)(dynBodies.Count + kinBodies.Count));
            for (int i = 0; i < dynBodies.Count; i++)
            {
                NetInterop.SerializeBody(dynBodies[i], sim.Simulation, msg);
            }
            for (int i = 0; i < kinBodies.Count; i++)
            {
                NetInterop.SerializeBody(kinBodies[i], sim.Simulation, msg);
            }
        }

        void WriteMessage(ServerPlayerData p, int MTU, NetOutgoingMessage msg)
        {
            p.SortPriorities();

            const int InputSize = NetInterop.PlayerInputSerializationSize + 2;
            const int PlayerSize = NetInterop.PlayerSerializationSize;
            const int BodySize = NetInterop.BodySerializationSize;
            const int MinSize = InputSize;
            int SpaceRemaining = MTU - 14; //const overhead
            int numInputs = 0;
            int numPlayers = 0;
            int numBodies = 0;
            int numTotal = 0;
            int maxThings = p.PlayerInputPriorities.Count + p.PlayerBodyPriorities.Count + p.BodyPriorities.Count;

            while (SpaceRemaining >= MinSize && numTotal < maxThings)
            {
                float inputPri = float.MinValue;
                float playerPri = float.MinValue;
                float bodyPri = float.MinValue;

                if (numInputs < p.PlayerInputPriorities.Count)
                    inputPri = p.PlayerInputPriorities.PeekPrioriy(numInputs);
                if (numPlayers < p.PlayerBodyPriorities.Count)
                    playerPri = p.PlayerBodyPriorities.PeekPrioriy(numPlayers);
                if (numBodies < p.BodyPriorities.Count)
                    bodyPri = p.BodyPriorities.PeekPrioriy(numBodies);

                //should be sorted smallest to largest by bytes
                //first doesn't need to check room since its the smallest
                if ((SpaceRemaining < PlayerSize || inputPri >= playerPri)
                    && (SpaceRemaining < BodySize || inputPri >= bodyPri))
                {
                    if (numInputs == p.PlayerInputPriorities.Count)
                        break; //if we don't have room for other things, but we're out of inputs

                    Debug.Assert(SpaceRemaining >= InputSize);
                    Debug.Assert(inputPri != float.MinValue);
                    numInputs++;
                    numTotal++;
                    SpaceRemaining -= InputSize;
                }
                else if ((SpaceRemaining < PlayerSize || bodyPri >= playerPri))
                {
                    if (numBodies == p.BodyPriorities.Count)
                        break;

                    Debug.Assert(SpaceRemaining >= BodySize);
                    Debug.Assert(bodyPri != float.MinValue);
                    numBodies++;
                    numTotal++;
                    SpaceRemaining -= BodySize;
                }
                else
                {
                    Debug.Assert(SpaceRemaining >= PlayerSize);
                    Debug.Assert(playerPri != float.MinValue);
                    numPlayers++;
                    numTotal++;
                    SpaceRemaining -= PlayerSize;
                }
            }

            msg.Write((int)NetServerToClient.STATE_UPDATE);
            msg.Write(sim.CurrentFrame);
            msg.Write((short)numInputs);
            for (int i = 0; i < numInputs; i++)
            {
                var input = p.PlayerInputPriorities.Pop();
                var player = sim.GetPlayer(input);
                msg.Write((short)input);
                NetInterop.SerializePlayerInput(ref player.Input, msg);
            }
            msg.Write((short)numPlayers);
            for (int i = 0; i < numPlayers; i++)
            {
                var pID = p.PlayerBodyPriorities.Pop();
                NetInterop.SerializePlayer(pID, sim, msg);
            }
            msg.Write((short)numBodies);
            for (int i = 0; i < numBodies; i++)
            {
                var bID = p.BodyPriorities.Pop();
                NetInterop.SerializeBody(new BodyHandle(bID), sim.Simulation, msg);
            }
        }
    }
}
