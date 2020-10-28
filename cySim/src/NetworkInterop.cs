using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.CompilerServices;
using Lidgren.Network;
using BepuPhysics;
using System.Numerics;

namespace cySim
{
    //client <-> server data IDs
    public enum NetServerToClient : int
    {
        NEW_PLAYER = 0,
        NEW_PLAYER_YOU,
        REMOVE_PLAYER,
        STATE_UPDATE
    }

    public enum NetClientToServer : int
    {
        PLAYER_INPUT = 0
    }

    //physics bodytype serialization byte
    public enum BodyType : byte
    {//serialization info for "normal" static/dynamic objects
        MULTI = 0, //if a set with the same shape, wrote first, then followed by shape
        BOX = 1,
        CAPSULE = 2,
        SPHERE = 3,
        CYLINDER = 4,

        STATIC = 128 //shape flag -- kinematic objects are sent with 0 inverse mass
    }

    public struct PlayerState
    {
        public int playerID;
        public BodyState player;
        public BodyState hammer;
        public HammerState hammerState;
        public float hammerDT;

        public PlayerState(int playerID, BodyState player, BodyState hammer, HammerState hammerState, float hammerDT)
        {
            this.playerID = playerID;
            this.player = player;
            this.hammer = hammer;
            this.hammerState = hammerState;
            this.hammerDT = hammerDT;
        }
    }

    public struct BodyState
    {
        public int bodyHandle;
        public RigidPose pose;
        public BodyVelocity velocity;

        public BodyState(int handle, Vector3 pos, Quaternion ori, Vector3 linVel, Vector3 angVel)
        {
            this.bodyHandle = handle;
            pose = new RigidPose(pos, ori);
            velocity = new BodyVelocity(linVel, angVel);
        }
    }

    public abstract class NetInterop
    {
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
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadPlayer(NetIncomingMessage msg, ref PlayerState state)
        {
            state.playerID = msg.ReadInt32();

            ReadBody(msg, ref state.player);
            ReadBody(msg, ref state.hammer);

            state.hammerState = (HammerState)msg.ReadByte();
            state.hammerDT = msg.ReadFloat();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadBody(NetIncomingMessage msg, ref BodyState state)
        {
            var handle = msg.ReadInt32();
            var posX = msg.ReadFloat();
            var posY = msg.ReadFloat();
            var posZ = msg.ReadFloat();
            var oriX = msg.ReadFloat();
            var oriY = msg.ReadFloat();
            var oriZ = msg.ReadFloat();
            var oriW = msg.ReadFloat();
            var velX = msg.ReadFloat();
            var velY = msg.ReadFloat();
            var velZ = msg.ReadFloat();
            var aVelX = msg.ReadFloat();
            var aVelY = msg.ReadFloat();
            var aVelZ = msg.ReadFloat();

            state = new BodyState(handle,
                new Vector3(posX, posY, posZ),
                new Quaternion(oriX, oriY, oriZ, oriW),
                new Vector3(velX, velY, velZ),
                new Vector3(aVelX, aVelY, aVelZ));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializePlayerInput(ref PlayerInput input, NetOutgoingMessage msg)
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
        public static void ReadPlayerInput(ref PlayerInput input, NetIncomingMessage msg)
        {
            input.MoveForward = msg.ReadBoolean();
            input.MoveBackward = msg.ReadBoolean();
            input.MoveLeft = msg.ReadBoolean();
            input.MoveRight = msg.ReadBoolean();
            input.Sprint = msg.ReadBoolean();
            input.TryJump = msg.ReadBoolean();
            input.TryFire = msg.ReadBoolean();
            msg.ReadPadBits();
            input.ViewDirection.X = msg.ReadFloat();
            input.ViewDirection.Y = msg.ReadFloat();
            input.ViewDirection.Z = msg.ReadFloat();
        }
    }
}
