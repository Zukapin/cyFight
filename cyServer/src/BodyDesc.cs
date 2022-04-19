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
    interface IBodyDesc
    {
        void Serialize(NetOutgoingMessage msg, Simulation Simulation);
    }

    struct BoxDesc : IBodyDesc
    {
        Vector3 shape;
        float mass;

        bool IsStatic;
        int count;
        List<RigidPose> poses; //for static bodies
        List<BodyHandle> handles; //for non-static bodies

        public BoxDesc(Simulation Simulation, Vector3 shape, Vector3 pos, Quaternion orientation)
        {
            IsStatic = true;
            this.shape = shape;
            poses = new List<RigidPose>() { new RigidPose(pos, orientation) };

            count = 1;
            handles = default;
            mass = 0;

            var shapeIndex = Simulation.Shapes.Add(new Box(shape.X, shape.Y, shape.Z));
            Simulation.Statics.Add(new StaticDescription(pos, orientation, shapeIndex));
        }

        public BoxDesc(Simulation Simulation, Vector3 shape, float specMargin, List<RigidPose> poses)
        {
            IsStatic = true;
            this.shape = shape;
            this.poses = poses;

            count = poses.Count;
            handles = default;
            mass = 0;

            var shapeIndex = Simulation.Shapes.Add(new Box(shape.X, shape.Y, shape.Z));
            for (int i = 0; i < count; i++)
            {
                var pose = poses[i];
                Simulation.Statics.Add(new StaticDescription(pose.Position, pose.Orientation, shapeIndex));
            }
        }

        public BoxDesc(Vector3 shape, float mass, BodyHandle handle)
        {
            IsStatic = false;
            this.shape = shape;
            this.mass = mass;
            handles = new List<BodyHandle>() { handle };
            count = 1;

            poses = default;
        }

        public BoxDesc(Vector3 shape, float mass, List<BodyHandle> handles)
        {
            IsStatic = false;
            this.shape = shape;
            this.mass = mass;
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
        float mass;

        bool IsStatic;
        int count;
        List<RigidPose> poses;
        List<BodyHandle> handles;

        public CylinderDesc(Simulation Simulation, float radius, float length, Vector3 pos, Quaternion orientation)
        {
            IsStatic = true;
            this.radius = radius;
            this.length = length;
            poses = new List<RigidPose>() { new RigidPose(pos, orientation) };

            count = 1;
            handles = default;
            mass = 0;

            var shapeIndex = Simulation.Shapes.Add(new Cylinder(radius, length));
            Simulation.Statics.Add(new StaticDescription(pos, orientation, shapeIndex));
        }

        public CylinderDesc(Simulation Simulation, float radius, float length, List<RigidPose> poses)
        {
            IsStatic = true;
            this.radius = radius;
            this.length = length;
            this.poses = poses;

            count = poses.Count;
            handles = default;
            mass = 0;

            var shapeIndex = Simulation.Shapes.Add(new Cylinder(radius, length));
            for (int i = 0; i < count; i++)
            {
                var pose = poses[i];
                Simulation.Statics.Add(new StaticDescription(pose.Position, pose.Orientation, shapeIndex));
            }
        }
        public CylinderDesc(float radius, float length, float mass, BodyHandle handle)
        {
            IsStatic = false;
            this.radius = radius;
            this.length = length;
            this.mass = mass;
            handles = new List<BodyHandle>() { handle };

            count = 1;
            poses = default;
        }

        public CylinderDesc(float radius, float length, float mass, List<BodyHandle> handles)
        {
            IsStatic = false;
            this.radius = radius;
            this.length = length;
            this.mass = mass;
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

    struct CapsuleDesc : IBodyDesc
    {
        float radius;
        float length;
        float mass;

        bool IsStatic;
        int count;
        List<RigidPose> poses;
        List<BodyHandle> handles;

        public CapsuleDesc(Simulation Simulation, float radius, float length, Vector3 pos, Quaternion orientation)
        {
            IsStatic = true;
            this.radius = radius;
            this.length = length;
            poses = new List<RigidPose>() { new RigidPose(pos, orientation) };

            count = 1;
            handles = default;
            mass = 0;

            var shapeIndex = Simulation.Shapes.Add(new Capsule(radius, length));
            Simulation.Statics.Add(new StaticDescription(pos, orientation, shapeIndex));
        }

        public CapsuleDesc(Simulation Simulation, float radius, float length, List<RigidPose> poses)
        {
            IsStatic = true;
            this.radius = radius;
            this.length = length;
            this.poses = poses;

            count = poses.Count;
            handles = default;
            mass = 0;

            var shapeIndex = Simulation.Shapes.Add(new Capsule(radius, length));
            for (int i = 0; i < count; i++)
            {
                var pose = poses[i];
                Simulation.Statics.Add(new StaticDescription(pose.Position, pose.Orientation, shapeIndex));
            }
        }
        public CapsuleDesc(float radius, float length, float mass, BodyHandle handle)
        {
            IsStatic = false;
            this.radius = radius;
            this.length = length;
            this.mass = mass;
            handles = new List<BodyHandle>() { handle };

            count = 1;
            poses = default;
        }

        public CapsuleDesc(float radius, float length, float mass, List<BodyHandle> handles)
        {
            IsStatic = false;
            this.radius = radius;
            this.length = length;
            this.mass = mass;
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
                msg.Write((byte)(BodyType.STATIC | BodyType.CAPSULE));
                msg.Write(radius);
                msg.Write(length);
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
                msg.Write((byte)BodyType.CAPSULE);
                msg.Write(radius);
                msg.Write(length);
                msg.Write(mass);
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

    struct SphereDesc : IBodyDesc
    {
        float radius;
        float mass;

        bool IsStatic;
        int count;
        List<RigidPose> poses;
        List<BodyHandle> handles;

        public SphereDesc(Simulation Simulation, float radius, Vector3 pos, Quaternion orientation)
        {
            IsStatic = true;
            this.radius = radius;
            poses = new List<RigidPose>() { new RigidPose(pos, orientation) };

            count = 1;
            handles = default;
            mass = 0;

            var shapeIndex = Simulation.Shapes.Add(new Sphere(radius));
            Simulation.Statics.Add(new StaticDescription(pos, orientation, shapeIndex));
        }

        public SphereDesc(Simulation Simulation, float radius, List<RigidPose> poses)
        {
            IsStatic = true;
            this.radius = radius;
            this.poses = poses;

            count = poses.Count;
            handles = default;
            mass = 0;

            var shapeIndex = Simulation.Shapes.Add(new Sphere(radius));
            for (int i = 0; i < count; i++)
            {
                var pose = poses[i];
                Simulation.Statics.Add(new StaticDescription(pose.Position, pose.Orientation, shapeIndex));
            }
        }
        public SphereDesc(float radius, float mass, BodyHandle handle)
        {
            IsStatic = false;
            this.radius = radius;
            this.mass = mass;
            handles = new List<BodyHandle>() { handle };

            count = 1;
            poses = default;
        }

        public SphereDesc(float radius, float mass, List<BodyHandle> handles)
        {
            IsStatic = false;
            this.radius = radius;
            this.mass = mass;
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
                msg.Write((byte)(BodyType.STATIC | BodyType.SPHERE));
                msg.Write(radius);
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
                msg.Write((byte)BodyType.SPHERE);
                msg.Write(radius);
                msg.Write(mass);
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
}
