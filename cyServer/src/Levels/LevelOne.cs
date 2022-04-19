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
    class LevelOne : Level
    {
        public LevelOne(Network Network)
            : base(Network)
        {

        }

        protected override void Init(ref List<IBodyDesc> bodyDesc, ref QuickList<BodyHandle> dynBodies, ref QuickList<BodyHandle> kinBodies)
        {
            bodyDesc.Add(new BoxDesc(Simulation, new Vector3(2500, 1, 2500), new Vector3(0, -0.5f, 0), Quaternion.Identity));
            bodyDesc.Add(new BoxDesc(Simulation, new Vector3(2, 1, 2), new Vector3(0, 0.5f, 30), Quaternion.Identity));

            float cylRadius = 1;
            float cylLength = 1;
            float cylSpecMargin = 0.1f;
            float cylMass = 1;
            var cylShape = new Cylinder(cylRadius, cylLength);
            var cylInertia = cylShape.ComputeInertia(cylMass);
            var cylIndex = Simulation.Shapes.Add(cylShape);

            const int pyramidCount = 1;
            const int rowCount = 15;
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

            bodyDesc.Add(new CylinderDesc(cylRadius, cylLength, cylMass, cylinders));

            var spinShape = Simulation.Shapes.Add(new Box(10, 0.5f, 1f));
            var spinHandle = Simulation.Bodies.Add(BodyDescription.CreateKinematic(
                new RigidPose(new Vector3(0, 1f, -10f), Quaternion.Identity),
                new BodyVelocity(new Vector3(0), new Vector3(0, 1f, 0)),
                new CollidableDescription(spinShape, 0.1f),
                new BodyActivityDescription(0.01f)));
            kinBodies.Add(spinHandle, sim.BufferPool);
            bodyDesc.Add(new BoxDesc(new Vector3(10, 0.5f, 1f), 0f, spinHandle));
        }
    }
}
