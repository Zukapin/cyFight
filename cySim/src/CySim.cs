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
        public Simulation Simulation { get; private set; }
        CharacterControllers characters;
        ThreadDispatcher ThreadDispatcher;

        int numPlayers;
        IdPool playerIDToPlayer;
        CharacterInput[] players;

        public int CurrentFrame { get; private set; }

#if TEST_SIM
        public Simulation Normal_Simulation { get; private set; }
        public Simulation Test_Simulation { get; private set; }
        CharacterControllers Test_Characters;
        CharacterInput[] Test_Players;

        public void UseNormalSim()
        {
            Simulation = Normal_Simulation;
        }
        public void UseTestSim()
        {
            Simulation = Test_Simulation;
        }
#else
#endif

        public CySim()
        {
            BufferPool = new BufferPool();
        }

        public void Init(int StartFrame = 0)
        {
            CurrentFrame = StartFrame;

            characters = new CharacterControllers(BufferPool);

            DefaultTimestepper asdf = new DefaultTimestepper();
            Simulation = Simulation.Create(
                BufferPool,
                new CyNarrowphaseCallbacks(characters),
                new CyIntegratorCallbacks(new Vector3(0, -10, 0)),
                new SolveDescription(8, 1));

            var targetThreadCount = Math.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1);
            ThreadDispatcher = new ThreadDispatcher(targetThreadCount);

            playerIDToPlayer = new IdPool(128, BufferPool);
            players = new CharacterInput[128];

#if TEST_SIM
            Normal_Simulation = Simulation;
            Test_Characters = new CharacterControllers(BufferPool);
            Test_Players = new CharacterInput[players.Length];
            Test_Simulation = Simulation.Create(
                BufferPool,
                new CyNarrowphaseCallbacks(Test_Characters),
                new CyIntegratorCallbacks(new Vector3(0, -10, 0)),
                new SolveDescription(8, 1));
#endif
        }

        public int AddPlayer(Vector3 startPos)
        {
            var newChar = new CharacterInput(characters, startPos, new Capsule(0.5f, 1), 0.1f, 1f, 15f, 10f, 6f, 4f);

            EnsurePlayerCapacity(numPlayers + 1);
            int id = playerIDToPlayer.Take();
            Debug.Assert(players[id] == null, "Player ID has been assigned to a non-null player.");
            players[id] = newChar;
            numPlayers++;

#if TEST_SIM
            var testChar = new CharacterInput(Test_Characters, startPos, new Capsule(0.5f, 1), 0.1f, 1f, 15f, 10f, 6f, 4f);
            Test_Players[id] = testChar;
#endif
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

#if TEST_SIM
                    oldPlayers = Test_Players;
                    Test_Players = new CharacterInput[playerIDToPlayer.Capacity];
                    for (int i = 0; i < oldCap; i++)
                    {
                        Test_Players[i] = oldPlayers[i];
                    }
#endif
                }
            }

        }

        public int PlayerCount { get { return numPlayers; } }

        public CharacterInput GetPlayer(int playerID)
        {
            Debug.Assert(players[playerID] != null, "Trying to access a player ID that doesn't exist");
#if TEST_SIM
            if (Simulation == Test_Simulation)
                return Test_Players[playerID];
#endif
            return players[playerID];
        }

        public bool PlayerExists(int playerID)
        {
#if TEST_SIM
            if (Simulation == Test_Simulation)
                return Test_Players[playerID] != null;
#endif
            return players[playerID] != null;
        }

        public IEnumerable<CharacterInput> Players
        {
            get
            {
                for (int i = 0; i <= playerIDToPlayer.HighestPossiblyClaimedId; i++)
                {
                    var p = players[i];
                    if (p != null)
                    {
                        yield return p;
                    }
                }
            }
        }

        public IEnumerable<int> PlayerIDs
        {
            get
            {
                for (int i = 0; i <= playerIDToPlayer.HighestPossiblyClaimedId; i++)
                {
                    if (players[i] != null)
                    {
                        yield return i;
                    }
                }
            }
        }

#if TEST_SIM
        public IEnumerable<CharacterInput> TestPlayers
        {
            get
            {
                for (int i = 0; i <= playerIDToPlayer.HighestPossiblyClaimedId; i++)
                {
                    var p = players[i];
                    if (p != null)
                    {
                        yield return Test_Players[i];
                    }
                }
            }
        }
#endif

        public void RemovePlayer(int playerID)
        {
            Debug.Assert(players[playerID] != null, "Removing a playerID that is already null.");
            players[playerID].Dispose();
            players[playerID] = null;
            playerIDToPlayer.Return(playerID, BufferPool);
            numPlayers--;

#if TEST_SIM
            Test_Players[playerID].Dispose();
            Test_Players[playerID] = null;
#endif
        }

        public void Update(float dt)
        {
            foreach (var p in Players)
            {
                p.UpdateCharacterGoals(dt);
            }

            Simulation.Timestep(dt, ThreadDispatcher);
#if TEST_SIM
            foreach (var p in TestPlayers)
            {
                p.UpdateCharacterGoals(dt);
            }
            Test_Simulation.Timestep(dt, ThreadDispatcher);
#endif

            CurrentFrame++;
        }
    }
}
