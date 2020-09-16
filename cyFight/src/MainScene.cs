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
    }

    class TestPlayer
    {
        GameStage stage;
        Renderer renderer;
        EventManager em;

        FPVCamera cam;

        bool isForwardDown = false;
        bool isBackDown = false;
        bool isLeftDown = false;
        bool isRightDown = false;

        public TestPlayer(GameStage stage, Renderer renderer, EventManager em, FPVCamera cam)
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
            em.addEventHandler((int)InterfacePriority.LOW, ActionTypes.FIRE, OnFire);
            em.addEventHandler((int)InterfacePriority.HIGHEST, onPointerEvent);
        }

        bool onPointerEvent(PointerEventArgs args)
        {
            if (args.type == PointerEventType.AIM)
            {
                cam.yaw -= args.aimDeltaX;
                cam.pitch -= args.aimDeltaY;
                return true;
            }
            return false;
        }

        bool OnMoveForward(ActionEventArgs args)
        {
            isForwardDown = args.buttonDown;
            return true;
        }
        bool OnMoveBackward(ActionEventArgs args)
        {
            isBackDown = args.buttonDown;
            return true;
        }
        bool OnMoveLeft(ActionEventArgs args)
        {
            isLeftDown = args.buttonDown;
            return true;
        }
        bool OnMoveRight(ActionEventArgs args)
        {
            isRightDown = args.buttonDown;
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
            return true;
        }

        bool OnExit(ActionEventArgs args)
        {
            stage.Exit();
            return true;
        }

        void onUpdate(float dt)
        {
            float speed = 20;

            Vector3 vel = new Vector3();
            Vector3 forward = cam.getForwardVec();
            Vector3 right = cam.getRightVec();
            if (isLeftDown)
                vel += -right;
            if (isRightDown)
                vel += right;
            if (isForwardDown)
                vel += forward;
            if (isBackDown)
                vel += -forward;

            cam.pos = cam.pos + vel * dt * speed;
        }
    }

    class TestScene : IScene
    {
        FPVCamera cam;

        GameStage stage;
        Renderer renderer;

        public TestScene(GameStage stage)
        {
            this.stage = stage;
            this.renderer = stage.renderer;

            cam = new FPVCamera(stage.renderer.ResolutionWidth / (float)stage.renderer.ResolutionHeight, Vector3.UnitY, 0, 0);
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
            var player = new TestPlayer(stage, renderer, em, cam);

            Shader s_posTex = renderer.Assets.GetShader(Renderer.DefaultAssets.SH_POS_TEX);
            Texture t_duck = renderer.Assets.GetTexture(Assets.TEX_DUCK);
            VertexBuffer vb_quad = renderer.Assets.GetVertexBuffer(Renderer.DefaultAssets.VB_QUAD_POS_TEX_UNIT);

            //mip ducks
            var testQuad = new TexturedQuad_2D(renderer, em, 0, t_duck);
            testQuad.position = new Vector2(10, 10);
            testQuad.scale = new Vector2(testQuad.tex.width, testQuad.tex.height);

            testQuad = new TexturedQuad_2D(renderer, em, 0, t_duck);
            testQuad.position = new Vector2(276, 10);
            testQuad.scale = new Vector2(testQuad.tex.width / 2f, testQuad.tex.height / 2f);

            testQuad = new TexturedQuad_2D(renderer, em, 0, t_duck);
            testQuad.position = new Vector2(414, 10);
            testQuad.scale = new Vector2(testQuad.tex.width / 4f, testQuad.tex.height / 4f);

            testQuad = new TexturedQuad_2D(renderer, em, 0, t_duck);
            testQuad.position = new Vector2(488, 10);
            testQuad.scale = new Vector2(testQuad.tex.width / 8f, testQuad.tex.height / 8f);

            //ground duck
            var a = new TexturedCircle_MRT(renderer, em, 0, t_duck);
            a.position = new Vector3(0, 0, 0);
            a.scale = new Vector2(20f, 20f);
            a.face = new Vector3(0, 1, 0);
            a.face = a.face / a.face.Length();

            //default duck
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
            Simulation = Simulation.Create(BufferPool, new DemoNarrowPhaseCallbacks(), new DemoPoseIntegratorCallbacks(new Vector3(0, -10, 0)), new PositionFirstTimestepper());

            var targetThreadCount = Math.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1);
            ThreadDispatcher = new SimpleThreadDispatcher(targetThreadCount);

            var boxShape = new Box(1, 1, 1);
            boxShape.ComputeInertia(1, out var boxInertia);
            var boxIndex = Simulation.Shapes.Add(boxShape);
            const int pyramidCount = 40;
            for (int pyramidIndex = 0; pyramidIndex < pyramidCount; ++pyramidIndex)
            {
                const int rowCount = 20;
                for (int rowIndex = 0; rowIndex < rowCount; ++rowIndex)
                {
                    int columnCount = rowCount - rowIndex;
                    for (int columnIndex = 0; columnIndex < columnCount; ++columnIndex)
                    {
                        var h = Simulation.Bodies.Add(BodyDescription.CreateDynamic(new Vector3(
                            (-columnCount * 0.5f + columnIndex) * boxShape.Width,
                            (rowIndex + 0.5f) * boxShape.Height,
                            (pyramidIndex - pyramidCount * 0.5f) * (boxShape.Length + 4)),
                            boxInertia,
                            new CollidableDescription(boxIndex, 0.1f),
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
            Simulation.Bodies.Add(bodyDescription);
        }

        class MagicBox
        {
            BodyReference bodyRef;
            Box_MRT box;

            public MagicBox(Simulation simulation, Renderer renderer, EventManager em, BodyHandle handle)
            {
                box = new Box_MRT(renderer, em, 0);
                box.color = Color.DarkRed;
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