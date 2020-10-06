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

namespace cyServer
{
    //serialization descriptions for "normal" simulation bodies
    //anything with complicated constraints or other game logic will have to be serialized separately (like characters)
    enum BodyType : byte
    {//serialization info for "normal" static/dynamic objects
        MULTI = 0, //if a set with the same shape, wrote first, then followed by shape
        BOX = 1,
        CAPSULE = 2,
        SPHERE = 3,
        CYLINDER = 4,

        STATIC = 128 //shape flag -- kinematic objects are sent with 0 inverse mass
    }
    interface IBodyDesc
    {
        void Serialize(NetOutgoingMessage msg, Simulation Simulation);
    }

    struct BoxDesc : IBodyDesc
    {
        Vector3 shape;
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

            var shapeIndex = Simulation.Shapes.Add(new Box(shape.X, shape.Y, shape.Z));
            for (int i = 0; i < count; i++)
            {
                var pose = poses[i];
                Simulation.Statics.Add(new StaticDescription(pose.Position, pose.Orientation, shapeIndex, specMargin));
            }
        }

        public BoxDesc(Vector3 shape, float specMargin, List<BodyHandle> handles)
        {
            IsStatic = false;
            this.shape = shape;
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
                msg.Write(specMargin);
                if (count > 1)
                    msg.Write(count);
                for (int i = 0; i < count; i++)
                {
                    var handle = handles[i];
                    Network.SerializeBody(handle, Simulation, msg);

                    var bodyRef = new BodyReference(handle, Simulation.Bodies);
                    msg.Write(bodyRef.LocalInertia.InverseMass);
                }
            }
        }
    }

    struct CylinderDesc : IBodyDesc
    {
        float radius;
        float length;
        float specMargin;

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

            var shapeIndex = Simulation.Shapes.Add(new Cylinder(radius, length));
            for (int i = 0; i < count; i++)
            {
                var pose = poses[i];
                Simulation.Statics.Add(new StaticDescription(pose.Position, pose.Orientation, shapeIndex, specMargin));
            }
        }

        public CylinderDesc(float radius, float length, float specMargin, List<BodyHandle> handles)
        {
            IsStatic = false;
            this.radius = radius;
            this.length = length;
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
                msg.Write(specMargin);
                if (count > 1)
                    msg.Write(count);
                for (int i = 0; i < count; i++)
                {
                    var handle = handles[i];
                    Network.SerializeBody(handle, Simulation, msg);

                    var bodyRef = new BodyReference(handle, Simulation.Bodies);
                    msg.Write(bodyRef.LocalInertia.InverseMass);
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
        public LevelOne(Network Network)
        {
            this.Network = Network;
            sim = new CySim();
            sim.Init();

            bodyDesc = new List<IBodyDesc>();
            bodyDesc.Add(new BoxDesc(Simulation, new Vector3(2500, 1, 2500), 0.1f, new Vector3(0, -0.5f, 0), Quaternion.Identity));
            bodyDesc.Add(new BoxDesc(Simulation, new Vector3(2, 1, 2), 0.1f, new Vector3(0, 0.5f, 30), Quaternion.Identity));

            float cylRadius = 1;
            float cylLength = 1;
            float cylSpecMargin = 0.1f;
            var cylShape = new Cylinder(cylRadius, cylLength);
            cylShape.ComputeInertia(1f, out var cylInertia);
            var cylIndex = Simulation.Shapes.Add(cylShape);

            const int pyramidCount = 4;
            const int rowCount = 30;
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
                    }
                }
            }

            bodyDesc.Add(new CylinderDesc(cylRadius, cylLength, cylSpecMargin, cylinders));
        }

        void SerializeAll(NetOutgoingMessage msg)
        {
            msg.Write(sim.CurrentFrame);

            foreach (var b in bodyDesc)
            {
                b.Serialize(msg, Simulation);
            }

            msg.Write(sim.Players.Count);
            for (int i = 0; i < sim.Players.Count; i++)
            {
                Network.SerializePlayer(i, sim, msg);
            }
        }

        public override void OnConnect(NetConnection conn)
        {
            Vector3 startPos = new Vector3(0, 10, 20);
            var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(Random.NextDouble() * Math.PI * 2));
            startPos = QuaternionEx.Transform(startPos, rot);
            var playerID = sim.AddPlayer(startPos);

            var msgToNewPlayer = Network.CreateMessage();
            msgToNewPlayer.Write((int)DataIDSend.NEW_PLAYER_YOU);
            msgToNewPlayer.Write(playerID);
            SerializeAll(msgToNewPlayer);
            Network.Send(msgToNewPlayer, conn, NetDeliveryMethod.ReliableOrdered, 0);

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

        public override void OnData(NetIncomingMessage msg)
        {
            throw new NotImplementedException();
        }

        public override void OnDisconnect(NetConnection conn)
        {
            throw new NotImplementedException();
        }

        public override void Update(float dt)
        {
            sim.Update(dt);

            SendUpdateMessages();
        }
        
        public void SendUpdateMessages()
        { 

        }
    }
}
