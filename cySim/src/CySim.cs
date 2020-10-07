using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using BepuUtilities.Collections;
using System.Diagnostics;

namespace cySim
{
    public class CySim
    {
        public BufferPool BufferPool { get; private set; }
        CharacterControllers characters;
        public Simulation Simulation { get; private set; }
        SimpleThreadDispatcher ThreadDispatcher;

        int numPlayers;
        IdPool playerIDToPlayer;
        CharacterInput[] players;

        public int CurrentFrame { get; private set; }

        public CySim()
        {
        }

        public void Init()
        {
            BufferPool = new BufferPool();

            characters = new CharacterControllers(BufferPool);

            Simulation = Simulation.Create(
                BufferPool,
                new CyNarrowphaseCallbacks(characters),
                new CyIntegratorCallbacks(new Vector3(0, -10, 0)),
                new PositionFirstTimestepper());

            var targetThreadCount = Math.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1);
            ThreadDispatcher = new SimpleThreadDispatcher(targetThreadCount);

            playerIDToPlayer = new IdPool(128, BufferPool);
            players = new CharacterInput[128];
        }

        public int AddPlayer(Vector3 startPos)
        {
            var newChar = new CharacterInput(characters, startPos, new Capsule(0.5f, 1), 0.1f, 1f, 15f, 10f, 6f, 4f);

            EnsurePlayerCapacity(numPlayers + 1);
            int id = playerIDToPlayer.Take();
            Debug.Assert(players[id] == null, "Player ID has been assigned to a non-null player.");
            players[id] = newChar;
            numPlayers++;
            return id;
        }

        private void EnsurePlayerCapacity(int len)
        {
            playerIDToPlayer.EnsureCapacity(len, BufferPool);
            if (len > players.Length)
            {
                if (playerIDToPlayer.Capacity > players.Length)
                {
                    int oldCap = players.Length;
                    var oldPlayers = players;

                    players = new CharacterInput[playerIDToPlayer.Capacity];
                    for (int i = 0; i < oldCap; i++)
                    {
                        players[i] = oldPlayers[i];
                    }
                }
            }
        }

        public int PlayerCount { get { return numPlayers; } }

        public CharacterInput GetPlayer(int playerID)
        {
            return players[playerID];
        }

        public bool PlayerExists(int playerID)
        {
            return players[playerID] != null;
        }

        public IEnumerable<CharacterInput> Players
        {
            get
            {
                foreach (var p in players)
                {
                    yield return p;
                }
            }
        }

        public void RemovePlayer(int playerID)
        {
            Debug.Assert(players[playerID] != null, "Removing a playerID that is already null.");
            players[playerID].Dispose();
            players[playerID] = null;
            playerIDToPlayer.Return(playerID, BufferPool);
            numPlayers--;
        }

        public void Update(float dt)
        {
            foreach (var p in Players)
            {
                p.UpdateCharacterGoals(dt);
            }

            Simulation.Timestep(dt, ThreadDispatcher);

            CurrentFrame++;
        }
    }
}
