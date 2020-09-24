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
using cySim;

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
        CySim sim;
        GameStage stage;
        Renderer renderer;
        EventManager em;

        TPVCamera cam;
        int charIndex;
        Capsule_MRT charGraphics;

        Cylinder_MRT hamGraphics;

        PlayerInput input;

        bool fire = false;
        float fireDt = 0;

        public TestPlayer(GameStage stage, Renderer renderer, EventManager em, TPVCamera cam, CySim sim)
        {
            this.stage = stage;
            this.renderer = renderer;
            this.em = em;
            this.cam = cam;
            this.sim = sim;

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

        public void Init()
        {
            charIndex = sim.AddPlayer();

            charGraphics = new Capsule_MRT(renderer, em, 0, Renderer.DefaultAssets.VB_CAPSULE_POS_NORM_HALFRAD);

            hamGraphics = new Cylinder_MRT(renderer, em, 0);
            hamGraphics.color = Color.Blue;
            hamGraphics.scale = new Vector3(0.25f, 0.75f, 0.25f);
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
            input.Sprint = args.buttonDown;
            return true;
        }

        bool OnJump(ActionEventArgs args)
        {
            if (args.buttonDown)
                input.TryJump = true;
            return true;
        }

        bool OnMoveForward(ActionEventArgs args)
        {
            input.MoveForward = args.buttonDown;
            return true;
        }
        bool OnMoveBackward(ActionEventArgs args)
        {
            input.MoveBackward = args.buttonDown;
            return true;
        }
        bool OnMoveLeft(ActionEventArgs args)
        {
            input.MoveLeft = args.buttonDown;
            return true;
        }
        bool OnMoveRight(ActionEventArgs args)
        {
            input.MoveRight = args.buttonDown;
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


        void onUpdate(float dt)
        {
            var c = sim.Players[charIndex];

            input.MoveDirection = cam.getForwardVec();
            c.input = input;

            var characterBody = new BodyReference(c.BodyHandle, sim.Simulation.Bodies);
            var cPos = characterBody.Pose.Position;
            cam.AnchorPos = cPos;
            charGraphics.position = cPos;

            var hammerBody = new BodyReference(c.HammerHandle, sim.Simulation.Bodies);
            var hPose = hammerBody.Pose;

            hamGraphics.position = hPose.Position;
            hamGraphics.rotation = Matrix3x3.CreateFromQuaternion(hPose.Orientation);
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

        CySim sim;

        public TestScene(GameStage stage)
        {
            this.stage = stage;
            this.renderer = stage.renderer;

            cam = new TPVCamera(stage.renderer.ResolutionWidth / (float)stage.renderer.ResolutionHeight, Vector3.Zero, Vector3.One, 0, 0);
            cam.Offset = new Vector3(0, 0.5f * 1.2f, (0.75f) * 4f);

            sim = new CySim();
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
            sim.Load();

            foreach (var h in sim.BodyHandles)
            {
                new MagicBox(sim.Simulation, renderer, em, h);
            }

            var playerHandle = sim.AddPlayer();

            //add lights
            Vector3 lightDir = Vector3.Normalize(new Vector3(0.25f, -1, -0.5f));
            var l = new DirectionalLight(renderer, em, lightDir, Color.White, 1);
            //l = new DirectionalLight(renderer, em, Vector3.UnitY, Color.White, 0.1f);

        }

        public void Update(float dt)
        {
        }

        public ICamera GetCamera()
        {
            return cam;
        }

        public void Dispose()
        {
            //TODO, probably never.
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

    }
}