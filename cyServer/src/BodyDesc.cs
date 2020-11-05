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
        float specMargin;

        bool IsStatic;
        int count;
        List<RigidPose> poses; //for static bodies
        List<BodyHandle> handles; //for non-static bodies

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

        public BoxDesc(Vector3 shape, float mass, float specMargin, BodyHandle handle)
        {
            IsStatic = false;
            this.shape = shape;
            this.specMargin = specMargin;
            this.mass = mass;
            handles = new List<BodyHandle>() { handle };
            count = 1;

            poses = default;
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
        public CylinderDesc(float radius, float length, float mass, float specMargin, BodyHandle handle)
        {
            IsStatic = false;
            this.radius = radius;
            this.length = length;
            this.mass = mass;
            this.specMargin = specMargin;
            handles = new List<BodyHandle>() { handle };

            count = 1;
            poses = default;
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

    struct CapsuleDesc : IBodyDesc
    {
        float radius;
        float length;
        float specMargin;
        float mass;

        bool IsStatic;
        int count;
        List<RigidPose> poses;
        List<BodyHandle> handles;

        public CapsuleDesc(Simulation Simulation, float radius, float length, float specMargin, Vector3 pos, Quaternion orientation)
        {
            IsStatic = true;
            this.radius = radius;
            this.length = length;
            this.specMargin = specMargin;
            poses = new List<RigidPose>() { new RigidPose(pos, orientation) };

            count = 1;
            handles = default;
            mass = 0;

            Simulation.Statics.Add(new StaticDescription(pos, orientation, new CollidableDescription(Simulation.Shapes.Add(new Capsule(radius, length)), specMargin)));
        }

        public CapsuleDesc(Simulation Simulation, float radius, float length, float specMargin, List<RigidPose> poses)
        {
            IsStatic = true;
            this.radius = radius;
            this.length = length;
            this.specMargin = specMargin;
            this.poses = poses;

            count = poses.Count;
            handles = default;
            mass = 0;

            var shapeIndex = Simulation.Shapes.Add(new Capsule(radius, length));
            for (int i = 0; i < count; i++)
            {
                var pose = poses[i];
                Simulation.Statics.Add(new StaticDescription(pose.Position, pose.Orientation, shapeIndex, specMargin));
            }
        }
        public CapsuleDesc(float radius, float length, float mass, float specMargin, BodyHandle handle)
        {
            IsStatic = false;
            this.radius = radius;
            this.length = length;
            this.mass = mass;
            this.specMargin = specMargin;
            handles = new List<BodyHandle>() { handle };

            count = 1;
            poses = default;
        }

        public CapsuleDesc(float radius, float length, float mass, float specMargin, List<BodyHandle> handles)
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
                msg.Write((byte)(BodyType.STATIC | BodyType.CAPSULE));
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
                msg.Write((byte)BodyType.CAPSULE);
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

    struct SphereDesc : IBodyDesc
    {
        float radius;
        float specMargin;
        float mass;

        bool IsStatic;
        int count;
        List<RigidPose> poses;
        List<BodyHandle> handles;

        public SphereDesc(Simulation Simulation, float radius, float specMargin, Vector3 pos, Quaternion orientation)
        {
            IsStatic = true;
            this.radius = radius;
            this.specMargin = specMargin;
            poses = new List<RigidPose>() { new RigidPose(pos, orientation) };

            count = 1;
            handles = default;
            mass = 0;

            Simulation.Statics.Add(new StaticDescription(pos, orientation, new CollidableDescription(Simulation.Shapes.Add(new Sphere(radius)), specMargin)));
        }

        public SphereDesc(Simulation Simulation, float radius, float specMargin, List<RigidPose> poses)
        {
            IsStatic = true;
            this.radius = radius;
            this.specMargin = specMargin;
            this.poses = poses;

            count = poses.Count;
            handles = default;
            mass = 0;

            var shapeIndex = Simulation.Shapes.Add(new Sphere(radius));
            for (int i = 0; i < count; i++)
            {
                var pose = poses[i];
                Simulation.Statics.Add(new StaticDescription(pose.Position, pose.Orientation, shapeIndex, specMargin));
            }
        }
        public SphereDesc(float radius, float mass, float specMargin, BodyHandle handle)
        {
            IsStatic = false;
            this.radius = radius;
            this.mass = mass;
            this.specMargin = specMargin;
            handles = new List<BodyHandle>() { handle };

            count = 1;
            poses = default;
        }

        public SphereDesc(float radius, float mass, float specMargin, List<BodyHandle> handles)
        {
            IsStatic = false;
            this.radius = radius;
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
                msg.Write((byte)(BodyType.STATIC | BodyType.SPHERE));
                msg.Write(radius);
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
                msg.Write((byte)BodyType.SPHERE);
                msg.Write(radius);
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
}
