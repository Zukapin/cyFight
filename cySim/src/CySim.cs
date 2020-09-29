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
        BufferPool BufferPool;
        CharacterControllers characters;
        public Simulation Simulation { get; private set; }
        SimpleThreadDispatcher ThreadDispatcher;

        public List<CharacterInput> Players;

        public CySim()
        {
            Players = new List<CharacterInput>();
        }

        public void Load(out QuickList<BodyHandle> BodyHandles)
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

            var cylShape = new Cylinder(1f, 1f);
            cylShape.ComputeInertia(1f, out var cylInertia);
            var cylIndex = Simulation.Shapes.Add(cylShape);

            const int pyramidCount = 4;
            const int rowCount = 30;
            BodyHandles = new QuickList<BodyHandle>(1024, BufferPool);
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
                            new CollidableDescription(cylIndex, 0.1f),
                            new BodyActivityDescription(0.01f)));

                        BodyHandles.Add(h, BufferPool);
                    }
                }
            }

            Simulation.Statics.Add(new StaticDescription(new Vector3(0, -0.5f, 0), new CollidableDescription(Simulation.Shapes.Add(new Box(2500, 1, 2500)), 0.1f)));
            Simulation.Statics.Add(new StaticDescription(new Vector3(0, 0.5f, 30), new CollidableDescription(Simulation.Shapes.Add(new Box(2, 1f, 2)), 0.1f)));
        }

        public int AddPlayer()
        {
            var newChar = new CharacterInput(characters, new Vector3(0, 1, 20), new Capsule(0.5f, 1), 0.1f, 1f, 15f, 10f, 6f, 4f);

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
        }
    }
}
