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

namespace cyServer
{
    //serialization descriptions for "normal" simulation bodies
    //anything with complicated constraints or other game logic will have to be serialized separately (like characters)
    
    interface IBodyDesc
    {
        void Serialize(NetOutgoingMessage msg, Simulation Simulation);
    }

    struct BoxDesc : IBodyDesc
    {
        Vector3 shape;
        float mass;
        float specMargin;

        bool IsStatic;
        int count;
        List<RigidPose> poses;
        List<BodyHandle> handles;

        public BoxDesc(Simulation Simulation, Vector3 shape, float specMargin, Vector3 pos, Quaternion orientation)
        {
            IsStatic = true;
            this.shape = shape;
            this.specMargin = specMargin;
            poses = new List<RigidPose>() { new RigidPose(pos, orientation) };

            count = 1;
            handles = default;
            mass = 0;

            Simulation.Statics.Add(new StaticDescription(pos, orientation, new CollidableDescription(Simulation.Shapes.Add(new Box(shape.X, shape.Y, shape.Z)), specMargin)));
        }

        public BoxDesc(Simulation Simulation, Vector3 shape, float specMargin, List<RigidPose> poses)
        {
            IsStatic = true;
            this.shape = shape;
            this.specMargin = specMargin;
            this.poses = poses;

            count = poses.Count;
            handles = default;
            mass = 0;

            var shapeIndex = Simulation.Shapes.Add(new Box(shape.X, shape.Y, shape.Z));
            for (int i = 0; i < count; i++)
            {
                var pose = poses[i];
                Simulation.Statics.Add(new StaticDescription(pose.Position, pose.Orientation, shapeIndex, specMargin));
            }
        }

        public BoxDesc(Vector3 shape, float mass, float specMargin, List<BodyHandle> handles)
        {
            IsStatic = false;
            this.shape = shape;
            this.mass = mass;
            this.specMargin = specMargin;
            this.handles = handles;

            count = handles.Count;
            poses = default;
        }

        public void Serialize(NetOutgoingMessage msg, Simulation Simulation)
        {
            if (count > 1)
                msg.Write((byte)BodyType.MULTI);

            if (IsStatic)
            {
                msg.Write((byte)(BodyType.STATIC | BodyType.BOX));
                msg.Write(shape.X);
                msg.Write(shape.Y);
                msg.Write(shape.Z);
                msg.Write(specMargin);
                if (count > 1)
                    msg.Write(count);
                for (int i = 0; i < count; i++)
                {
                    var pose = poses[i];
                    msg.Write(pose.Position.X);
                    msg.Write(pose.Position.Y);
                    msg.Write(pose.Position.Z);
                    msg.Write(pose.Orientation.X);
                    msg.Write(pose.Orientation.Y);
                    msg.Write(pose.Orientation.Z);
                    msg.Write(pose.Orientation.W);
                }
            }
            else
            {
                msg.Write((byte)BodyType.BOX);
                msg.Write(shape.X);
                msg.Write(shape.Y);
                msg.Write(shape.Z);
                msg.Write(mass);
                msg.Write(specMargin);
                if (count > 1)
                    msg.Write(count);
                for (int i = 0; i < count; i++)
                {
                    var handle = handles[i];
                    NetInterop.SerializeBody(handle, Simulation, msg);
                }
            }
        }
    }

    struct CylinderDesc : IBodyDesc
    {
        float radius;
        float length;
        float specMargin;
        float mass;

        bool IsStatic;
        int count;
        List<RigidPose> poses;
        List<BodyHandle> handles;

        public CylinderDesc(Simulation Simulation, float radius, float length, float specMargin, Vector3 pos, Quaternion orientation)
        {
            IsStatic = true;
            this.radius = radius;
            this.length = length;
            this.specMargin = specMargin;
            poses = new List<RigidPose>() { new RigidPose(pos, orientation) };

            count = 1;
            handles = default;
            mass = 0;

            Simulation.Statics.Add(new StaticDescription(pos, orientation, new CollidableDescription(Simulation.Shapes.Add(new Cylinder(radius, length)), specMargin)));
        }

        public CylinderDesc(Simulation Simulation, float radius, float length, float specMargin, List<RigidPose> poses)
        {
            IsStatic = true;
            this.radius = radius;
            this.length = length;
            this.specMargin = specMargin;
            this.poses = poses;

            count = poses.Count;
            handles = default;
            mass = 0;

            var shapeIndex = Simulation.Shapes.Add(new Cylinder(radius, length));
            for (int i = 0; i < count; i++)
            {
                var pose = poses[i];
                Simulation.Statics.Add(new StaticDescription(pose.Position, pose.Orientation, shapeIndex, specMargin));
            }
        }

        public CylinderDesc(float radius, float length, float mass, float specMargin, List<BodyHandle> handles)
        {
            IsStatic = false;
            this.radius = radius;
            this.length = length;
            this.mass = mass;
            this.specMargin = specMargin;
            this.handles = handles;

            count = handles.Count;
            poses = default;
        }

        public void Serialize(NetOutgoingMessage msg, Simulation Simulation)
        {
            if (count > 1)
                msg.Write((byte)BodyType.MULTI);

            if (IsStatic)
            {
                msg.Write((byte)(BodyType.STATIC | BodyType.CYLINDER));
                msg.Write(radius);
                msg.Write(length);
                msg.Write(specMargin);
                if (count > 1)
                    msg.Write(count);
                for (int i = 0; i < count; i++)
                {
                    var pose = poses[i];
                    msg.Write(pose.Position.X);
                    msg.Write(pose.Position.Y);
                    msg.Write(pose.Position.Z);
                    msg.Write(pose.Orientation.X);
                    msg.Write(pose.Orientation.Y);
                    msg.Write(pose.Orientation.Z);
                    msg.Write(pose.Orientation.W);
                }
            }
            else
            {
                msg.Write((byte)BodyType.CYLINDER);
                msg.Write(radius);
                msg.Write(length);
                msg.Write(mass);
                msg.Write(specMargin);
                if (count > 1)
                    msg.Write(count);
                for (int i = 0; i < count; i++)
                {
                    var handle = handles[i];
                    NetInterop.SerializeBody(handle, Simulation, msg);
                }
            }
        }
    }

    abstract class Level
    {
        public static Level LoadLevel(int i, Network Network)
        {
            return new LevelOne(Network);
        }

        public abstract void OnConnect(NetConnection conn);
        public abstract void OnDisconnect(NetConnection conn);
        public abstract void OnData(NetIncomingMessage msg);

        public abstract void Update(float dt);
    }

    struct PriorityQueue : IComparer<int>
    {
        int Capacity;
        public int Count;
        public float[] Priorities;
        int[] SortedIndexes;
        int SortedUsed;

        public void EnsureCapacity(int NewCapacity)
        {
            if (NewCapacity > Capacity)
            {
                int newCap = Math.Max(Capacity * 2, NewCapacity);
                Array.Resize(ref Priorities, newCap);
                Array.Resize(ref SortedIndexes, newCap);

                Capacity = newCap;
            }
        }

        public void AddIndex(int i)
        {
#if DEBUG
            for (int t = 0; t < Count; t++)
            {
                Debug.Assert(SortedIndexes[t] != i, "Index already exists in this priority queue");
            }
#endif
            Debug.Assert(i >= 0 && i < Capacity, "Index must be greater than 0 and less than capacity" + i + " " + Capacity);
            Debug.Assert(Count < Capacity, "Current count is already at capacity");
            SortedIndexes[Count] = i;
            Priorities[i] = 0;
            Count++;
        }

        public void RemoveIndex(int i)
        {
            Debug.Assert(i >= 0 && i < Capacity);
#if DEBUG
            bool found = false;
#endif
            for (int t = 0; t < Count; t++)
            {
                if (SortedIndexes[t] == i)
                {
                    SortedIndexes[t] = SortedIndexes[Count - 1];
#if DEBUG
                    found = true;
#endif
                    break;
                }
            }

#if DEBUG
            Debug.Assert(found);
#endif
            Count--;
        }

        public void Clear()
        {
            Count = 0;
        }

        public float PeekPrioriy(int i)
        {
            Debug.Assert(SortedUsed == 0);
            Debug.Assert(i < Count);
            return Priorities[SortedIndexes[i]];
        }

        public int Pop()
        {
            Debug.Assert(SortedUsed < Count);
            var toRet = SortedIndexes[SortedUsed++];
            Priorities[toRet] = 0;
            return toRet;
        }

        public void Sort()
        {
            //theoretically could pass in the max number of items we'll ever pull from this list, based on MTU size
            //and then only do a partial sort
            SortedUsed = 0;
            Array.Sort(SortedIndexes, 0, Count, this);
        }

        public int Compare(int x, int y)
        {//don't use this for anything other than the array sort please
            Debug.Assert(x >= 0 && x < Capacity);
            Debug.Assert(y >= 0 && y < Capacity);
            return Math.Sign(Priorities[y] - Priorities[x]);
        }
    }

    class LevelOne : Level
    {
        Network Network;
        CySim sim;
        Simulation Simulation { get { return sim.Simulation; } }
        List<IBodyDesc> bodyDesc;

        Random Random = new Random();
        List<NetConnection> connections = new List<NetConnection>();

        QuickList<BodyHandle> dynBodies;

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

        public LevelOne(Network Network)
        {
            this.Network = Network;
            sim = new CySim();
            sim.Init();

            dynBodies = new QuickList<BodyHandle>(1024, sim.BufferPool);
            bodyDesc = new List<IBodyDesc>();
            bodyDesc.Add(new BoxDesc(Simulation, new Vector3(2500, 1, 2500), 0.1f, new Vector3(0, -0.5f, 0), Quaternion.Identity));
            bodyDesc.Add(new BoxDesc(Simulation, new Vector3(2, 1, 2), 0.1f, new Vector3(0, 0.5f, 30), Quaternion.Identity));

            float cylRadius = 1;
            float cylLength = 1;
            float cylSpecMargin = 0.1f;
            float cylMass = 1;
            var cylShape = new Cylinder(cylRadius, cylLength);
            cylShape.ComputeInertia(cylMass, out var cylInertia);
            var cylIndex = Simulation.Shapes.Add(cylShape);

            const int pyramidCount = 1;
            const int rowCount = 20;
            var cylinders = new List<BodyHandle>();
            for (int pyramidIndex = 0; pyramidIndex < pyramidCount; ++pyramidIndex)
            {
                for (int rowIndex = 0; rowIndex < rowCount; ++rowIndex)
                {
                    int columnCount = rowCount - rowIndex;
                    for (int columnIndex = 0; columnIndex < columnCount; ++columnIndex)
                    {
                        var h = Simulation.Bodies.Add(BodyDescription.CreateDynamic(new Vector3(
                            (-columnCount * 0.5f + columnIndex) * 1,
                            (rowIndex + 0.5f) * 1,
                            (pyramidIndex - pyramidCount * 0.5f) * 6),
                            cylInertia,
                            new CollidableDescription(cylIndex, cylSpecMargin),
                            new BodyActivityDescription(0.01f)));

                        cylinders.Add(h);
                        dynBodies.Add(h, sim.BufferPool);
                    }
                }
            }

            bodyDesc.Add(new CylinderDesc(cylRadius, cylLength, cylMass, cylSpecMargin, cylinders));

            EnsurePlayerSize(128);
            EnsureBodySize(sim.Simulation.Bodies.HandlePool.HighestPossiblyClaimedId + 1);
        }

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

        public override void OnConnect(NetConnection conn)
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

        public override void OnData(NetIncomingMessage msg)
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

        public override void OnDisconnect(NetConnection conn)
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

        public override void Update(float dt)
        {
            SendUpdateMessages();

            sim.Update(dt);
        }

        void UpdatePriorities()
        {
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
                //WriteAll(msg);

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
            msg.Write((short)dynBodies.Count);
            for (int i = 0; i < dynBodies.Count; i++)
            {
                NetInterop.SerializeBody(dynBodies[i], sim.Simulation, msg);
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

        static int Test()
        {
            return 0;
        }
    }
}
