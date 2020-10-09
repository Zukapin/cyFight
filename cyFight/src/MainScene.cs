using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using cyUtility;
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
using Lidgren.Network;
using System.Reflection.Metadata;

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

    class Player
    {
        CySim sim;
        public int playerIndex;
        public ref PlayerInput input { get { return ref sim.GetPlayer(playerIndex).Input; } }

        protected Capsule_MRT charGraphics;
        protected Cylinder_MRT hamGraphics;

        public Player(CySim sim, Renderer renderer, EventManager em, Vector3 startPos)
        {
            this.sim = sim;

            playerIndex = sim.AddPlayer(startPos);

            charGraphics = new Capsule_MRT(renderer, em, 0, Renderer.DefaultAssets.VB_CAPSULE_POS_NORM_HALFRAD);

            hamGraphics = new Cylinder_MRT(renderer, em, 0);
            hamGraphics.color = Color.Blue;
            hamGraphics.scale = new Vector3(0.25f, 0.5f, 0.25f);

            em.addUpdateListener(0, GraphicsUpdate);
        }

        void GraphicsUpdate(float dt)
        {
            var c = sim.GetPlayer(playerIndex);

            var characterBody = new BodyReference(c.BodyHandle, sim.Simulation.Bodies);
            var cPos = characterBody.Pose.Position;
            charGraphics.position = cPos;
            charGraphics.rotation = Matrix3x3.CreateFromQuaternion(characterBody.Pose.Orientation);

            var hammerBody = new BodyReference(c.HammerHandle, sim.Simulation.Bodies);
            var hPose = hammerBody.Pose;

            hamGraphics.position = hPose.Position;
            hamGraphics.rotation = Matrix3x3.CreateFromQuaternion(hPose.Orientation);
        }
    }

    class MyPlayer : Player
    {
        TPVCamera cam;
        GameStage stage;


        public MyPlayer(GameStage stage, Renderer renderer, EventManager em, TPVCamera cam, CySim sim, Vector3 startPos)
            : base(sim, renderer, em, startPos)
        {
            this.stage = stage;
            this.cam = cam;

            em.addUpdateListener(0, Update);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.FORWARD, OnMoveForward);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.BACKWARD, OnMoveBackward);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.LEFT, OnMoveLeft);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.RIGHT, OnMoveRight);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.ENTER_FPV, OnEnterFPV);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.LEAVE_FPV, OnLeaveFPV);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.SPRINT, OnSprint);
            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.JUMP, OnJump);
            em.addEventHandler((int)InterfacePriority.LOW, ActionTypes.FIRE, OnFire);
            em.addEventHandler((int)InterfacePriority.HIGHEST, OnPointerEvent);
        }

        bool OnPointerEvent(PointerEventArgs args)
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
            if (args.buttonDown)
            {
                input.TryFire = true;
            }
            return true;
        }

        void Update(float dt)
        {
            input.ViewDirection = cam.getForwardVec();
            cam.AnchorPos = charGraphics.position;
        }
    }

    class TestScene : IScene, INetworkCallbacks
    {
        TPVCamera cam;

        GameStage stage;
        Renderer renderer;

        CySim sim;
        Network network;

        public TestScene(GameStage stage)
        {
            this.stage = stage;
            this.renderer = stage.renderer;

            cam = new TPVCamera(stage.renderer.ResolutionWidth / (float)stage.renderer.ResolutionHeight, Vector3.Zero, Vector3.One, 0, 0);
            cam.Offset = new Vector3(0, 0.5f * 1.2f, (0.75f) * 7f);
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
                Renderer.DefaultAssets.FONT_DEFAULT,
                Renderer.DefaultAssets.SH_FONT_SDF,
                Renderer.DefaultAssets.BUF_FONT,
                Renderer.DefaultAssets.BUF_COLOR
            };
        }

        FontRenderer loadFont;
        string loadText = "Initial Loading...";
        bool loadTextChanged = false;
        public void Preload(EventManager em)
        {
            Texture t_duck = renderer.Assets.GetTexture(Assets.TEX_DUCK);
            var testQuad = new TexturedQuad_2D(renderer, em, 0, t_duck);
            testQuad.position = new Vector2(10, 10);
            testQuad.scale = new Vector2(500, 500);

            loadFont = new FontRenderer(renderer, em, 0, renderer.Assets.GetFont(Renderer.DefaultAssets.FONT_DEFAULT));
            loadFont.color = Color.White;
            loadFont.text = loadText;
            loadFont.pos = new Vector2(renderer.ResolutionWidth * 0.5f, renderer.ResolutionHeight * 0.5f);
            loadFont.anchor = FontAnchor.CENTER_CENTER;
        }

        public void LoadUpdate(float dt)
        {
            if (loadTextChanged)
            {
                lock (loadFont)
                {
                    loadFont.text = loadText;
                    loadTextChanged = false;
                }
            }
        }

        public void LoadEnd()
        {
            loadFont = null;
            loadText = null;
        }

        public bool Draw3D()
        {
            return true;
        }

        bool doneLoading = false;
        EventManager load_em;
        public void Load(EventManager em)
        {
            load_em = em;
            sim = new CySim();
            network = new Network(this);
            network.Connect();

            Vector3 lightDir = Vector3.Normalize(new Vector3(0.25f, -1, -0.5f));
            var l = new DirectionalLight(renderer, em, lightDir, Color.White, 1);

            em.addEventHandler((int)InterfacePriority.MEDIUM, ActionTypes.ESCAPE, OnExit);


            while (!doneLoading)
            {
                network.ReadMessages();

                if (!network.IsConnected)
                {
                    lock (loadFont)
                    {
                        loadText = "Attempting to connect to " + network.CurrentConnectionTarget;
                        loadTextChanged = true;
                    }
                }

                network.SendMessages();
                //this sleeps for way longer than 1 ms on average, because windows, but whatever
                Thread.Sleep(1);
            }
        }

        public void OnConnect()
        {
            Logger.WriteLine(LogType.DEBUG, "Connected callback");

            lock (loadFont)
            {
                loadText = "Connected, waiting on data from server";
                loadTextChanged = true;
            }
        }

        public void OnConnectionFailed()
        {
            Logger.WriteLine(LogType.DEBUG, "Connection failed callback");

            lock (loadFont)
            {
                loadText = "Connection failed, deleting sys32";
                loadTextChanged = true;
            }

            Thread.Sleep(2000);
            OnExit(default);
            doneLoading = true;
        }

        public void OnData(NetIncomingMessage msg)
        {
            Logger.WriteLine(LogType.VERBOSE3, "Data callback");

            var msgID = (NetServerToClient)msg.ReadInt32();
            if (!Enum.IsDefined(msgID))
            {
                Logger.WriteLine(LogType.POSSIBLE_ERROR, "Recieved an invalid message ID " + (int)msgID + " from: " + msg.SenderConnection);
                return;
            }

            switch (msgID)
            {
                case NetServerToClient.NEW_PLAYER:
                    Logger.WriteLine(LogType.DEBUG, "New player joined");
                    break;
                case NetServerToClient.NEW_PLAYER_YOU:
                    Logger.WriteLine(LogType.DEBUG, "I joined " + msg.LengthBytes);
                    lock (loadFont)
                    {
                        loadText = "Data recieved, initializing";
                        loadTextChanged = true;
                    }
                    OnLevelData(msg);
                    break;
                case NetServerToClient.REMOVE_PLAYER:
                    Logger.WriteLine(LogType.DEBUG, "Player removed");
                    break;
                case NetServerToClient.STATE_UPDATE:
                    break;
                default:
                    Logger.WriteLine(LogType.POSSIBLE_ERROR, "Unhandled message type from server " + msgID);
                    break;
            }
        }

        void OnLevelData(NetIncomingMessage msg)
        {
            var myPlayerID = msg.ReadInt32();
            var startFrame = msg.ReadInt32();
            sim.Init(startFrame);

            var bodyCount = msg.ReadInt32();
            for (int i = 0; i < bodyCount; i++)
            {
                OnBodyData(msg);
            }
        }

        void OnBodyData(NetIncomingMessage msg)
        {
            var typeByte = msg.ReadByte();
            bool isMulti = false;
            if (typeByte == (byte)BodyType.MULTI)
            {
                isMulti = true;
                typeByte = msg.ReadByte();
            }

            var staticBit = typeByte & (byte)BodyType.STATIC;
            bool isStatic = staticBit != 0;
            var type = (BodyType)(typeByte - staticBit);

            if (!Enum.IsDefined(type))
            {
                //this should probably just hard error out
                Logger.WriteLine(LogType.ERROR, "Invalid body type in body data " + typeByte + " " + staticBit + " " + isMulti);
                return;
            }

            TypedIndex shapeIndex = default;
            float specMargin = -1;
            IBodyRenderer body = null;
            float mass = -1;
            BodyInertia inertia = default;
            switch (type)
            {
                case BodyType.BOX:
                    {
                        var width = msg.ReadFloat();
                        var height = msg.ReadFloat();
                        var length = msg.ReadFloat();

                        var box = new Box(width, height, length);
                        if (!isStatic)
                        {
                            mass = msg.ReadFloat();
                            box.ComputeInertia(mass, out inertia);
                        }
                        specMargin = msg.ReadFloat();

                        shapeIndex = sim.Simulation.Shapes.Add(box);
                        body = new BoxThingy(width, height, length, renderer, load_em);
                    }
                    break;
                case BodyType.CYLINDER:
                    {
                        var radius = msg.ReadFloat();
                        var length = msg.ReadFloat();

                        var cyl = new Cylinder(radius, length);
                        if (!isStatic)
                        {
                            mass = msg.ReadFloat();
                            cyl.ComputeInertia(mass, out inertia);
                        }
                        specMargin = msg.ReadFloat();

                        shapeIndex = sim.Simulation.Shapes.Add(cyl);
                        body = new CylinderThingy(radius, length, renderer, load_em);
                    }
                    break;
                default:
                    Logger.WriteLine(LogType.ERROR, "Unhandled body type " + type);
                    return;
            }

            int count = 1;
            if (isMulti)
            {
                count = msg.ReadInt32();
            }

            if (isStatic)
            {
                for (int i = 0; i < count; i++)
                {
                    var posX = msg.ReadFloat();
                    var posY = msg.ReadFloat();
                    var posZ = msg.ReadFloat();
                    var oriX = msg.ReadFloat();
                    var oriY = msg.ReadFloat();
                    var oriZ = msg.ReadFloat();
                    var oriW = msg.ReadFloat();

                    var pos = new Vector3(posX, posY, posZ);
                    var ori = new Quaternion(oriX, oriY, oriZ, oriW);

                    sim.Simulation.Statics.Add(new StaticDescription(
                        pos,
                        ori,
                        shapeIndex,
                        specMargin));

                    body.SetPose(pos, ori);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    ReadBody(msg, out var handle, out var pose, out var vel);

                    BodyHandle myHandle = default;

                    if (mass == 0)
                    {
                        myHandle = sim.Simulation.Bodies.Add(BodyDescription.CreateKinematic(
                            pose,
                            vel,
                            new CollidableDescription(shapeIndex, specMargin),
                            new BodyActivityDescription(0.01f)));
                    }
                    else
                    {
                        myHandle = sim.Simulation.Bodies.Add(BodyDescription.CreateDynamic(
                            pose,
                            vel,
                            inertia,
                            new CollidableDescription(shapeIndex, specMargin),
                            new BodyActivityDescription(0.01f)));
                    }

                    body.SetHandle(myHandle, sim.Simulation);
                }
            }
        }

        void ReadBody(NetIncomingMessage msg, out int Handle, out RigidPose pose, out BodyVelocity velocity)
        {
            Handle = msg.ReadInt32();
            var posX = msg.ReadFloat();
            var posY = msg.ReadFloat();
            var posZ = msg.ReadFloat();
            var oriX = msg.ReadFloat();
            var oriY = msg.ReadFloat();
            var oriZ = msg.ReadFloat();
            var oriW = msg.ReadFloat();
            var velX = msg.ReadFloat();
            var velY = msg.ReadFloat();
            var velZ = msg.ReadFloat();
            var aVelX = msg.ReadFloat();
            var aVelY = msg.ReadFloat();
            var aVelZ = msg.ReadFloat();

            var pos = new Vector3(posX, posY, posZ);
            var ori = new Quaternion(oriX, oriY, oriZ, oriW);
            var vel = new Vector3(velX, velY, velZ);
            var aVel = new Vector3(aVelX, aVelY, aVelZ);

            pose = new RigidPose(pos, ori);
            velocity = new BodyVelocity(vel, aVel);
        }

        public void OnDisconnect()
        {
            Logger.WriteLine(LogType.DEBUG, "Disconect callback");
        }

        public void Update(float dt)
        {
            network.ReadMessages();
            network.SendMessages();
            sim.Update(dt);
        }

        public ICamera GetCamera()
        {
            return cam;
        }

        bool OnExit(ActionEventArgs args)
        {
            stage.Exit();
            return true;
        }

        public void Dispose()
        {
            doneLoading = true;
            if (network != null)
            {
                network.Dispose();
            }
        }

        interface IBodyRenderer
        {
            void SetPose(Vector3 pos, Quaternion ori);
            void SetHandle(BodyHandle handle, Simulation Simulation);
        }

        class BoxThingy : IBodyRenderer
        {
            List<BodyReference> bodyRefs;
            Box_MRT box;
            public BoxThingy(float width, float height, float length, Renderer renderer, EventManager em)
            {
                box = new Box_MRT(renderer, null, 0);
                box.color = Color.DarkBlue;
                box.scale = new Vector3(width, height, length);

                em.addDrawMRT(0, DrawMRT);
            }

            public void SetPose(Vector3 pos, Quaternion ori)
            {
                box.position = pos;
                box.rotation = Matrix3x3.CreateFromQuaternion(ori);
            }

            public void SetHandle(BodyHandle handle, Simulation Simulation)
            {
                if (bodyRefs == null)
                {
                    bodyRefs = new List<BodyReference>();
                }
                bodyRefs.Add(new BodyReference(handle, Simulation.Bodies));
            }

            void DrawMRT()
            {
                if (bodyRefs != null)
                {
                    for (int i = 0; i < bodyRefs.Count; i++)
                    {
                        var bodyRef = bodyRefs[i];
                        var pose = bodyRef.Pose;

                        box.position = pose.Position;
                        box.rotation = Matrix3x3.CreateFromQuaternion(pose.Orientation);
                        box.DrawMRT();
                    }
                }
                else
                {
                    box.DrawMRT();
                }
            }
        }

        class CylinderThingy : IBodyRenderer
        {
            List<BodyReference> bodyRefs;
            Cylinder_MRT cyl;
            public CylinderThingy(float radius, float length, Renderer renderer, EventManager em)
            {
                cyl = new Cylinder_MRT(renderer, null, 0);
                cyl.color = Color.DarkBlue;
                cyl.scale = new Vector3(radius, length, radius);

                em.addDrawMRT(0, DrawMRT);
            }

            public void SetPose(Vector3 pos, Quaternion ori)
            {
                cyl.position = pos;
                cyl.rotation = Matrix3x3.CreateFromQuaternion(ori);
            }

            public void SetHandle(BodyHandle handle, Simulation Simulation)
            {
                if (bodyRefs == null)
                {
                    bodyRefs = new List<BodyReference>();
                }    
                bodyRefs.Add(new BodyReference(handle, Simulation.Bodies));
            }

            void DrawMRT()
            {
                if (bodyRefs != null)
                {
                    for (int i = 0; i < bodyRefs.Count; i++)
                    {
                        var bodyRef = bodyRefs[i];
                        var pose = bodyRef.Pose;

                        cyl.position = pose.Position;
                        cyl.rotation = Matrix3x3.CreateFromQuaternion(pose.Orientation);
                        cyl.DrawMRT();
                    }
                }
                else
                {
                    cyl.DrawMRT();
                }
            }
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