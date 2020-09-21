using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using log;
using cylib;

using BepuUtilities;
using BepuUtilities.Memory;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using cyFight.Sim;

namespace cyFight
{
    static class Assets
    {
        public const string TEX_DUCK = "TEX_angelduck";
    }

    static class ActionTypes
    {
        public const string ESCAPE = "ESCAPE";
        public const string FORWARD = "FORWARD";
        public const string BACKWARD = "BACKWARD";
        public const string LEFT = "LEFT";
        public const string RIGHT = "RIGHT";
        public const string ENTER_FPV = "ENTER_FPV";
        public const string LEAVE_FPV = "LEAVE_FPV";
        public const string FIRE = "FIRE";
        public const string JUMP = "JUMP";
        public const string SPRINT = "SPRINT";
    }

    class TestPlayer
    {
        Simulation Simulation;
        GameStage stage;
        Renderer renderer;
        EventManager em;

        TPVCamera cam;
        CharacterInput charInput;
        Capsule_MRT charGraphics;

        CharacterInput fakeChar;
        Capsule_MRT fakeGraphics;

        BodyHandle hamHandle;
        Cylinder_MRT hamGraphics;

        ConstraintHandle bsHandle;
        BallSocketServo bsDesc;
        ConstraintHandle asHandle;
        AngularServo asDesc;
        ConstraintHandle dHandle;
        DistanceServo dDesc;

        public TestPlayer(GameStage stage, Renderer renderer, EventManager em, TPVCamera cam)
        {
            this.stage = stage;
            this.renderer = renderer;
            this.em = em;
            this.cam = cam;

            em.addUpdateListener(0, onUpdate);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.ESCAPE, OnExit);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.FORWARD, OnMoveForward);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.BACKWARD, OnMoveBackward);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.LEFT, OnMoveLeft);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.RIGHT, OnMoveRight);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.ENTER_FPV, OnEnterFPV);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.LEAVE_FPV, OnLeaveFPV);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.SPRINT, OnSprint);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.JUMP, OnJump);
            em.addEventHandler((int)InterfacePriority.LOW, ActionTypes.FIRE, OnFire);
            em.addEventHandler((int)InterfacePriority.HIGHEST, onPointerEvent);
        }

        public void Init(CharacterControllers characters, Simulation Simulation)
        {
            this.Simulation = Simulation;

            charInput = new CharacterInput(characters, new Vector3(0, 1, -20), new Capsule(0.5f, 1), 0.1f, 1f, 15f, 10f, 6f, 4f);
            charGraphics = new Capsule_MRT(renderer, em, 0, Renderer.DefaultAssets.VB_CAPSULE_POS_NORM_HALFRAD);

            fakeChar = new CharacterInput(characters, new Vector3(0, 1, -30), new Capsule(0.5f, 1), 0.1f, 1f, 15f, 10f, 6f, 4f);
            fakeGraphics = new Capsule_MRT(renderer, em, 0, Renderer.DefaultAssets.VB_CAPSULE_POS_NORM_HALFRAD);
            fakeGraphics.color = Color.LightGoldenrodYellow;

            hamHandle = Simulation.Bodies.Add(BodyDescription.CreateConvexDynamic(new Vector3(-2f, 0.25f, -20f), 0.25f, Simulation.Shapes, new Cylinder(0.25f, 0.5f)));
            hamGraphics = new Cylinder_MRT(renderer, em, 0);
            hamGraphics.color = Color.Blue;
            hamGraphics.scale = new Vector3(0.25f, 0.75f, 0.25f);

            bsDesc = new BallSocketServo
            {
                LocalOffsetA = Vector3.UnitX * 1.5f,
                LocalOffsetB = Vector3.Zero,
                ServoSettings = new ServoSettings(2f, 1f, 15f),
                SpringSettings = new SpringSettings(30, 1)
            };

            bsHandle = Simulation.Solver.Add(charInput.BodyHandle, hamHandle, bsDesc);

            asDesc = new AngularServo
            {
                TargetRelativeRotationLocalA = Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)(Math.PI * 0.5f)),
                ServoSettings = new ServoSettings(2f, 1f, 5f),
                SpringSettings = new SpringSettings(30, 1)
            };

            asHandle = Simulation.Solver.Add(charInput.BodyHandle, hamHandle, asDesc);

            dDesc = new DistanceServo
            {
                LocalOffsetA = Vector3.UnitX * 0.25f,
                LocalOffsetB = Vector3.Zero,
                TargetDistance = 1.25f,
                ServoSettings = new ServoSettings(2f, 1f, 20f),
                SpringSettings = new SpringSettings(30, 1)
            };

            dHandle = Simulation.Solver.Add(charInput.BodyHandle, hamHandle, dDesc);
            /*
                        Simulation.Solver.Add(charInput.BodyHandle, hamHandle,
                            new LinearAxisServo
                            {
                                LocalOffsetA = Vector3.Zero,
                                LocalOffsetB = Vector3.Zero,
                                LocalPlaneNormal = Vector3.UnitY,
                                TargetOffset = 2f,
                                ServoSettings = new ServoSettings(1000f, 1f, 50f),
                                SpringSettings = new SpringSettings(30, 1)
                            });*/
            /*
                        Simulation.Solver.Add(charInput.BodyHandle, hamHandle,
                            new DistanceServo
                            {
                                LocalOffsetA = Vector3.Zero,
                                LocalOffsetB = Vector3.Zero,
                                TargetDistance = 2f,
                                ServoSettings = new ServoSettings(1000f, 1f, 50f),
                                SpringSettings = new SpringSettings(30, 1)
                            });*/
        }

        bool onPointerEvent(PointerEventArgs args)
        {
            if (args.type == PointerEventType.AIM)
            {
                cam.Yaw -= args.aimDeltaX;
                cam.Pitch -= args.aimDeltaY;
                return true;
            }
            return false;
        }

        bool OnSprint(ActionEventArgs args)
        {
            charInput.Sprint = args.buttonDown;
            return true;
        }

        bool OnJump(ActionEventArgs args)
        {
            if (args.buttonDown)
                charInput.TryJump = true;
            return true;
        }

        bool OnMoveForward(ActionEventArgs args)
        {
            charInput.MoveForward = args.buttonDown;
            return true;
        }
        bool OnMoveBackward(ActionEventArgs args)
        {
            charInput.MoveBackward = args.buttonDown;
            return true;
        }
        bool OnMoveLeft(ActionEventArgs args)
        {
            charInput.MoveLeft = args.buttonDown;
            return true;
        }
        bool OnMoveRight(ActionEventArgs args)
        {
            charInput.MoveRight = args.buttonDown;
            return true;
        }

        bool OnEnterFPV(ActionEventArgs args)
        {
            if (args.buttonDown)
                stage.EnterFPVMode();
            return true;
        }

        bool OnLeaveFPV(ActionEventArgs args)
        {
            if (args.buttonDown)
                stage.LeaveFPVMode();
            return true;
        }

        bool OnFire(ActionEventArgs args)
        {
            if (args.buttonDown && !fire)
            {
                fire = true;
                fireDt = 0;
            }
            return true;
        }

        bool OnExit(ActionEventArgs args)
        {
            stage.Exit();
            return true;
        }

        bool fire = false;
        float fireDt = 0;

        void onUpdate(float dt)
        {
            charInput.UpdateCharacterGoals(cam.getForwardVec(), dt);
            charInput.UpdateCameraPosition(cam);

            charGraphics.position = cam.AnchorPos;

            fakeGraphics.position = new BodyReference(fakeChar.BodyHandle, Simulation.Bodies).Pose.Position;

            var hamRef = new BodyReference(hamHandle, Simulation.Bodies);
            var p = hamRef.Pose;

            hamGraphics.position = p.Position;
            hamGraphics.rotation = Matrix3x3.CreateFromQuaternion(p.Orientation);

            if (fire)
            {
                if (foundHit)
                {
                    var hitRef = new BodyReference(hamHit, Simulation.Bodies);
                    var offset = hitRef.Pose.Position - p.Position;
                    var offLen = offset.Length();
                    var hamVel = hamRef.Velocity;
                    var velLen = hamVel.Linear.Length();

                    hitRef.ApplyImpulse(offset / offLen * velLen * 5, -offset);

                    bsDesc.ServoSettings = new ServoSettings(2f, 1f, 15f);
                    bsDesc.LocalOffsetA = Vector3.UnitX * 1.5f;
                    Simulation.Solver.ApplyDescriptionWithoutWaking(bsHandle, ref bsDesc);
                    foundHit = false;
                    fire = false;
                }
                else
                {
                    float fireSpeed = 6;
                    var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, fireDt * fireSpeed);
                    QuaternionEx.Transform(Vector3.UnitX * 2f, rot, out bsDesc.LocalOffsetA);
                    bsDesc.ServoSettings = new ServoSettings(10, 5, 10);
                    Simulation.Solver.ApplyDescriptionWithoutWaking(bsHandle, ref bsDesc);
                    fireDt += dt;
                }
            }
        }

        bool foundHit = false;
        BodyHandle hamHit;

        void Explode(BodyHandle notHammer)
        {
            foundHit = true;
            hamHit = notHammer;
        }

        public void Contacts<TManifold>(CollidablePair pair, ref TManifold manifold) where TManifold : struct, IContactManifold<TManifold>
        {
            if (!fire)
                return;

            if (pair.B.Mobility != CollidableMobility.Dynamic)
                return;

            if (pair.A.BodyHandle != hamHandle && pair.B.BodyHandle != hamHandle)
                return;

            for (int i = 0; i < manifold.Count; ++i)
            {
                if (manifold.GetDepth(ref manifold, i) >= -1e-3f)
                {
                    //An actual collision was found. 
                    if (pair.A.BodyHandle == hamHandle)
                    {
                        Explode(pair.B.BodyHandle);
                    }
                    else
                    {
                        Explode(pair.A.BodyHandle);
                    }
                    break;
                }
            }
        }
    }

    class TestScene : IScene
    {
        TPVCamera cam;

        GameStage stage;
        Renderer renderer;

        public TestScene(GameStage stage)
        {
            this.stage = stage;
            this.renderer = stage.renderer;

            cam = new TPVCamera(stage.renderer.ResolutionWidth / (float)stage.renderer.ResolutionHeight, Vector3.Zero, Vector3.One, 0, 0);
        }

        public float LoadTime()
        {
            return 0f;
        }

        public HashSet<string> GetAssetList()
        {
            return new HashSet<string>()
            {
                Renderer.DefaultAssets.SH_POS_TEX,
                Renderer.DefaultAssets.VB_QUAD_POS_TEX_UNIT,
                Assets.TEX_DUCK,
                Renderer.DefaultAssets.BUF_WORLD,
            };
        }

        public HashSet<string> GetPreloadAssetList()
        {
            return new HashSet<string>()
            {
                Renderer.DefaultAssets.SH_POS_TEX,
                Assets.TEX_DUCK,
                Renderer.DefaultAssets.VB_QUAD_POS_TEX_UNIT,
                Renderer.DefaultAssets.BUF_WORLD,
            };
        }

        public void Preload(EventManager em)
        {
            Texture t_duck = renderer.Assets.GetTexture(Assets.TEX_DUCK);
            var testQuad = new TexturedQuad_2D(renderer, em, 0, t_duck);
            testQuad.position = new Vector2(10, 10);
            testQuad.scale = new Vector2(1000, 1000);
        }

        public void LoadUpdate(float dt)
        {

        }

        public void LoadEnd()
        {
        }

        public bool Draw3D()
        {
            return true;
        }

        public void Load(EventManager em)
        {
            var b = new Box_MRT(renderer, em, 0);
            b.position = new Vector3(0, 1f, -3);

            var c = new Sphere_MRT(renderer, em, 0);
            c.position = new Vector3(3f, 1f, -3f);

            var d = new Capsule_MRT(renderer, em, 0);
            d.position = new Vector3(-3f, 1.5f, -3f);

            var e = new Cylinder_MRT(renderer, em, 0);
            e.position = new Vector3(0, 1f, -6f);

            //add lights
            Vector3 lightDir = Vector3.Normalize(new Vector3(0.25f, -1, -0.5f));
            var l = new DirectionalLight(renderer, em, lightDir, Color.White, 1);
            //l = new DirectionalLight(renderer, em, Vector3.UnitY, Color.White, 0.1f);

            BufferPool = new BufferPool();
            //Simulation = Simulation.Create(BufferPool, new DemoNarrowPhaseCallbacks(), new DemoPoseIntegratorCallbacks(new Vector3(0, -10, 0)), new PositionFirstTimestepper());

            CharacterControllers characters;
            characters = new CharacterControllers(BufferPool);
            //The PositionFirstTimestepper is the simplest timestepping mode, but since it integrates velocity into position at the start of the frame, directly modified velocities outside of the timestep
            //will be integrated before collision detection or the solver has a chance to intervene. That's fine in this demo. Other built-in options include the PositionLastTimestepper and the SubsteppingTimestepper.
            //Note that the timestepper also has callbacks that you can use for executing logic between processing stages, like BeforeCollisionDetection.
            var player = new TestPlayer(stage, renderer, em, cam);
            var simCallback = new CharacterNarrowphaseCallbacks(characters, player);
            Simulation = Simulation.Create(BufferPool, simCallback, new DemoPoseIntegratorCallbacks(new Vector3(0, -10, 0)), new PositionLastTimestepper());

            player.Init(characters, Simulation);

            simCallback.player = player;

            var targetThreadCount = Math.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1);
            ThreadDispatcher = new SimpleThreadDispatcher(targetThreadCount);

            var boxShape = new Box(1, 1, 1);
            boxShape.ComputeInertia(1, out var boxInertia);
            var boxIndex = Simulation.Shapes.Add(boxShape);

            var capShape = new Capsule(1f, 1f);
            capShape.ComputeInertia(1f, out var capInertia);
            var capIndex = Simulation.Shapes.Add(capShape);

            var sphShape = new Sphere(1f);
            sphShape.ComputeInertia(1f, out var sphInertia);
            var sphIndex = Simulation.Shapes.Add(sphShape);

            var cylShape = new Cylinder(1f, 1f);
            cylShape.ComputeInertia(1f, out var cylInertia);
            var cylIndex = Simulation.Shapes.Add(cylShape);

            const int pyramidCount = 4;
            for (int pyramidIndex = 0; pyramidIndex < pyramidCount; ++pyramidIndex)
            {
                const int rowCount = 30;
                for (int rowIndex = 0; rowIndex < rowCount; ++rowIndex)
                {
                    int columnCount = rowCount - rowIndex;
                    for (int columnIndex = 0; columnIndex < columnCount; ++columnIndex)
                    {
                        /*
                        var h = Simulation.Bodies.Add(BodyDescription.CreateDynamic(new Vector3(
                            (-columnCount * 0.5f + columnIndex) * boxShape.Width,
                            (rowIndex + 0.5f) * boxShape.Height,
                            (pyramidIndex - pyramidCount * 0.5f) * (boxShape.Length + 4)),
                            boxInertia,
                            new CollidableDescription(boxIndex, 0.1f),
                            new BodyActivityDescription(0.01f)));
                        */

                        /*
                        var h = Simulation.Bodies.Add(BodyDescription.CreateDynamic(new Vector3(
                            (-columnCount * 0.5f + columnIndex) * boxShape.Width,
                            (rowIndex + 0.5f) * boxShape.Height,
                            (pyramidIndex - pyramidCount * 0.5f) * (boxShape.Length + 4)),
                            capInertia,
                            new CollidableDescription(capIndex, 0.1f),
                            new BodyActivityDescription(0.01f)));
                        */

                        /*
                        var h = Simulation.Bodies.Add(BodyDescription.CreateDynamic(new Vector3(
                            (-columnCount * 0.5f + columnIndex) * boxShape.Width,
                            (rowIndex + 0.5f) * boxShape.Height,
                            (pyramidIndex - pyramidCount * 0.5f) * (boxShape.Length + 4)),
                            sphInertia,
                            new CollidableDescription(sphIndex, 0.1f),
                            new BodyActivityDescription(0.01f)));
                        */

                        var h = Simulation.Bodies.Add(BodyDescription.CreateDynamic(new Vector3(
                            (-columnCount * 0.5f + columnIndex) * boxShape.Width,
                            (rowIndex + 0.5f) * boxShape.Height,
                            (pyramidIndex - pyramidCount * 0.5f) * (boxShape.Length + 4)),
                            cylInertia,
                            new CollidableDescription(cylIndex, 0.1f),
                            new BodyActivityDescription(0.01f)));

                        

                        new MagicBox(Simulation, renderer, em, h);
                    }
                }
            }

            Simulation.Statics.Add(new StaticDescription(new Vector3(0, -0.5f, 0), new CollidableDescription(Simulation.Shapes.Add(new Box(2500, 1, 2500)), 0.1f)));
            var g = new Box_MRT(renderer, em, 0);
            g.position = new Vector3(0, -0.5f, 0);
            g.scale = new Vector3(2500, 1, 2500);
            g.color = Color.White;

            var bulletShape = new Sphere(5.5f);
            //Note that the use of radius^3 for mass can produce some pretty serious mass ratios. 
            //Observe what happens when a large ball sits on top of a few boxes with a fraction of the mass-
            //the collision appears much squishier and less stable. For most games, if you want to maintain rigidity, you'll want to use some combination of:
            //1) Limit the ratio of heavy object masses to light object masses when those heavy objects depend on the light objects.
            //2) Use a shorter timestep duration and update more frequently.
            //3) Use a greater number of solver iterations.
            //#2 and #3 can become very expensive. In pathological cases, it can end up slower than using a quality-focused solver for the same simulation.
            //Unfortunately, at the moment, bepuphysics v2 does not contain any alternative solvers, so if you can't afford to brute force the the problem away,
            //the best solution is to cheat as much as possible to avoid the corner cases.
            var bodyDescription = BodyDescription.CreateConvexDynamic(
                new Vector3(0, 8, -180), new BodyVelocity(new Vector3(0, 0, 200)), bulletShape.Radius * bulletShape.Radius * bulletShape.Radius, Simulation.Shapes, bulletShape);
            //Simulation.Bodies.Add(bodyDescription);

        }

        class MagicBox
        {
            BodyReference bodyRef;
            Cylinder_MRT box;

            public MagicBox(Simulation simulation, Renderer renderer, EventManager em, BodyHandle handle)
            {
                box = new Cylinder_MRT(renderer, em, 0);
                box.color = Color.DarkRed;
                box.scale = new Vector3(1f, 1f, 1f);
                em.addUpdateListener(0, Update);

                bodyRef = simulation.Bodies.GetBodyReference(handle);
            }

            void Update(float dt)
            {
                box.position = bodyRef.Pose.Position;
                box.rotation = Matrix3x3.CreateFromQuaternion(bodyRef.Pose.Orientation);
            }
        }

        Simulation Simulation;
        BufferPool BufferPool;
        public SimpleThreadDispatcher ThreadDispatcher { get; private set; }

        public void Update(float dt)
        {
            Simulation.Timestep(dt, ThreadDispatcher);
        }

        public ICamera GetCamera()
        {
            return cam;
        }

        public void Dispose()
        {
            //TODO, probably never.
        }

        public struct DemoPoseIntegratorCallbacks : IPoseIntegratorCallbacks
        {
            public Vector3 Gravity;
            public float LinearDamping;
            public float AngularDamping;
            Vector3 gravityDt;
            float linearDampingDt;
            float angularDampingDt;

            public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

            public DemoPoseIntegratorCallbacks(Vector3 gravity, float linearDamping = .03f, float angularDamping = .03f) : this()
            {
                Gravity = gravity;
                LinearDamping = linearDamping;
                AngularDamping = angularDamping;
            }

            public void PrepareForIntegration(float dt)
            {
                //No reason to recalculate gravity * dt for every body; just cache it ahead of time.
                gravityDt = Gravity * dt;
                //Since this doesn't use per-body damping, we can precalculate everything.
                linearDampingDt = MathF.Pow(MathHelper.Clamp(1 - LinearDamping, 0, 1), dt);
                angularDampingDt = MathF.Pow(MathHelper.Clamp(1 - AngularDamping, 0, 1), dt);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IntegrateVelocity(int bodyIndex, in RigidPose pose, in BodyInertia localInertia, int workerIndex, ref BodyVelocity velocity)
            {
                //Note that we avoid accelerating kinematics. Kinematics are any body with an inverse mass of zero (so a mass of ~infinity). No force can move them.
                if (localInertia.InverseMass > 0)
                {
                    velocity.Linear = (velocity.Linear + gravityDt) * linearDampingDt;
                    velocity.Angular = velocity.Angular * angularDampingDt;
                }
                //Implementation sidenote: Why aren't kinematics all bundled together separately from dynamics to avoid this per-body condition?
                //Because kinematics can have a velocity- that is what distinguishes them from a static object. The solver must read velocities of all bodies involved in a constraint.
                //Under ideal conditions, those bodies will be near in memory to increase the chances of a cache hit. If kinematics are separately bundled, the the number of cache
                //misses necessarily increases. Slowing down the solver in order to speed up the pose integrator is a really, really bad trade, especially when the benefit is a few ALU ops.

                //Note that you CAN technically modify the pose in IntegrateVelocity by directly accessing it through the Simulation.Bodies.ActiveSet.Poses, it just requires a little care and isn't directly exposed.
                //If the PositionFirstTimestepper is being used, then the pose integrator has already integrated the pose.
                //If the PositionLastTimestepper or SubsteppingTimestepper are in use, the pose has not yet been integrated.
                //If your pose modification depends on the order of integration, you'll want to take this into account.

                //This is also a handy spot to implement things like position dependent gravity or per-body damping.
            }

        }
        public unsafe struct DemoNarrowPhaseCallbacks : INarrowPhaseCallbacks
        {
            public SpringSettings ContactSpringiness;

            public void Initialize(Simulation simulation)
            {
                //Use a default if the springiness value wasn't initialized.
                if (ContactSpringiness.AngularFrequency == 0 && ContactSpringiness.TwiceDampingRatio == 0)
                    ContactSpringiness = new SpringSettings(30, 1);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b)
            {
                //While the engine won't even try creating pairs between statics at all, it will ask about kinematic-kinematic pairs.
                //Those pairs cannot emit constraints since both involved bodies have infinite inertia. Since most of the demos don't need
                //to collect information about kinematic-kinematic pairs, we'll require that at least one of the bodies needs to be dynamic.
                return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
            {
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            {
                pairMaterial.FrictionCoefficient = 1f;
                pairMaterial.MaximumRecoveryVelocity = 2f;
                pairMaterial.SpringSettings = ContactSpringiness;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
            {
                return true;
            }

            public void Dispose()
            {
            }
        }

        public class SimpleThreadDispatcher : IThreadDispatcher, IDisposable
        {
            int threadCount;
            public int ThreadCount => threadCount;
            struct Worker
            {
                public Thread Thread;
                public AutoResetEvent Signal;
            }

            Worker[] workers;
            AutoResetEvent finished;

            BufferPool[] bufferPools;

            public SimpleThreadDispatcher(int threadCount)
            {
                this.threadCount = threadCount;
                workers = new Worker[threadCount - 1];
                for (int i = 0; i < workers.Length; ++i)
                {
                    workers[i] = new Worker { Thread = new Thread(WorkerLoop), Signal = new AutoResetEvent(false) };
                    workers[i].Thread.IsBackground = true;
                    workers[i].Thread.Start(workers[i].Signal);
                }
                finished = new AutoResetEvent(false);
                bufferPools = new BufferPool[threadCount];
                for (int i = 0; i < bufferPools.Length; ++i)
                {
                    bufferPools[i] = new BufferPool();
                }
            }

            void DispatchThread(int workerIndex)
            {
                Debug.Assert(workerBody != null);
                workerBody(workerIndex);

                if (Interlocked.Increment(ref completedWorkerCounter) == threadCount)
                {
                    finished.Set();
                }
            }

            volatile Action<int> workerBody;
            int workerIndex;
            int completedWorkerCounter;

            void WorkerLoop(object untypedSignal)
            {
                var signal = (AutoResetEvent)untypedSignal;
                while (true)
                {
                    signal.WaitOne();
                    if (disposed)
                        return;
                    DispatchThread(Interlocked.Increment(ref workerIndex) - 1);
                }
            }

            void SignalThreads()
            {
                for (int i = 0; i < workers.Length; ++i)
                {
                    workers[i].Signal.Set();
                }
            }

            public void DispatchWorkers(Action<int> workerBody)
            {
                Debug.Assert(this.workerBody == null);
                workerIndex = 1; //Just make the inline thread worker 0. While the other threads might start executing first, the user should never rely on the dispatch order.
                completedWorkerCounter = 0;
                this.workerBody = workerBody;
                SignalThreads();
                //Calling thread does work. No reason to spin up another worker and block this one!
                DispatchThread(0);
                finished.WaitOne();
                this.workerBody = null;
            }

            volatile bool disposed;
            public void Dispose()
            {
                if (!disposed)
                {
                    disposed = true;
                    SignalThreads();
                    for (int i = 0; i < bufferPools.Length; ++i)
                    {
                        bufferPools[i].Clear();
                    }
                    foreach (var worker in workers)
                    {
                        worker.Thread.Join();
                        worker.Signal.Dispose();
                    }
                }
            }

            public BufferPool GetThreadMemoryPool(int workerIndex)
            {
                return bufferPools[workerIndex];
            }
        }
    }
}