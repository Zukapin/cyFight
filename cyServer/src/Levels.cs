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
                    Network.SerializeBody(handle, Simulation, msg);
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
                    Network.SerializeBody(handle, Simulation, msg);
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
            public int LatestInputFrame;
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
            const int rowCount = 2;
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
            for (int i = 0; i <= sim.HighestPlayerID; i++)
            {
                if (sim.PlayerExists(i))
                    Network.SerializePlayer(i, sim, msg);
            }
        }

        public override void OnConnect(NetConnection conn)
        {
            Vector3 startPos = new Vector3(0, 1, 20);
            var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(Random.NextDouble() * Math.PI * 2));
            startPos = QuaternionEx.Transform(startPos, rot);
            var playerID = sim.AddPlayer(startPos);
            conn.Tag = playerID;
            EnsurePlayerSize(playerID);
            PlayerData[playerID] = default;

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

            connections.Remove(conn);
            sim.RemovePlayer(playerID.Value);

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
        
        public void SendUpdateMessages()
        {
            //doing a very dumb 'send everything to everyone' plan
            if (connections.Count > 0)
            {
                var msg = Network.CreateMessage();
                msg.Write((int)NetServerToClient.STATE_UPDATE);
                msg.Write(sim.CurrentFrame);
                msg.Write(sim.PlayerCount);
                for (int i = 0; i <= sim.HighestPlayerID; i++)
                {
                    if (sim.PlayerExists(i))
                    {
                        Network.SerializePlayer(i, sim, msg);
                    }
                }
                msg.Write(dynBodies.Count);
                for (int i = 0; i < dynBodies.Count; i++)
                {
                    Network.SerializeBody(dynBodies[i], sim.Simulation, msg);
                }
                //note: can check individual connection MTU with conn.CurrentMTU, but it will essentially always be 1500 so whatever
                Debug.Assert(msg.LengthBytes <= 1500, "Player state update is larger than network MTU");
                Network.Send(msg, connections, NetDeliveryMethod.Unreliable, 0);
            }
        }
    }
}
