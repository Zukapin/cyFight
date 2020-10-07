using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.CompilerServices;
using Lidgren.Network;

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

    public abstract class NetInterop
    {
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
