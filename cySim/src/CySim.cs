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

namespace cySim
{
    public class CySim
    {
        public BufferPool BufferPool { get; private set; }
        CharacterControllers characters;
        public Simulation Simulation { get; private set; }
        SimpleThreadDispatcher ThreadDispatcher;

        public List<CharacterInput> Players;

        public int CurrentFrame { get; private set; }

        public CySim()
        {
            Players = new List<CharacterInput>();
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
        }

        public int AddPlayer(Vector3 startPos)
        {
            var newChar = new CharacterInput(characters, startPos, new Capsule(0.5f, 1), 0.1f, 1f, 15f, 10f, 6f, 4f);

            Players.Add(newChar);
            return Players.Count - 1;
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
