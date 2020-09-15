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
using Matrix = BepuUtilities.Matrix;
using Matrix3x3 = BepuUtilities.Matrix3x3;

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
        TexturedQuad_2D playerSprite;

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

            playerSprite = new TexturedQuad_2D(renderer, em, -1024, renderer.Assets.GetTexture(Assets.TEX_DUCK));
            playerSprite.position = new Vector2(400, 400);
            playerSprite.scale = new Vector2(100, 100);

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
            if (args.buttonDown)
                playerSprite.scale = playerSprite.scale * 2;
            else
                playerSprite.scale = playerSprite.scale * 0.5f;
            return true;
        }

        bool OnExit(ActionEventArgs args)
        {
            stage.Exit();
            return true;
        }

        void onUpdate(float dt)
        {
            float speed = 2;

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
            var b = new Cylinder_MRT(renderer, em, 0);
            b.position = new Vector3(0, 1f, -3);

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
    }
}