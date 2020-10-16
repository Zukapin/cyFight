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

        protected Capsule_MRT charGraphics;
        protected Cylinder_MRT hamGraphics;

        public Player(CySim sim, Renderer renderer, EventManager em, int playerID)
        {
            this.sim = sim;
            this.playerIndex = playerID;

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

        PlayerInput input;

        public ref PlayerInput Input { get { return ref input; } }


        public MyPlayer(GameStage stage, Renderer renderer, EventManager em, TPVCamera cam, CySim sim, int playerID)
            : base(sim, renderer, em, playerID)
        {
            this.stage = stage;
            this.cam = cam;

            input = default;

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
            input.TryJump = args.buttonDown;
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
            input.TryFire = args.buttonDown;
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

        MyPlayer myPlayer;
        List<Player> players;
        BodyHandle[] ServerHandleToLocal;
        int[] ServerPlayerToLocal;

        public TestScene(GameStage stage)
        {
            this.stage = stage;
            this.renderer = stage.renderer;

            cam = new TPVCamera(stage.renderer.ResolutionWidth / (float)stage.renderer.ResolutionHeight, Vector3.Zero, Vector3.One, 0, 0);
            cam.Offset = new Vector3(0, 0.5f * 1.2f, (0.75f) * 7f);

            ServerHandleToLocal = new BodyHandle[1024];
            ServerPlayerToLocal = new int[128];
            players = new List<Player>();
        }

        void EnsureHandleCapacity(int capacity)
        {
            if (ServerHandleToLocal.Length >= capacity)
                return;

            Array.Resize(ref ServerHandleToLocal, capacity);
        }

        void EnsurePlayerCapacity(int capacity)
        {
            if (ServerPlayerToLocal.Length >= capacity)
                return;

            Array.Resize(ref ServerPlayerToLocal, capacity);
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

            debugText = new FontRenderer(renderer, em, 0, renderer.Assets.GetFont(Renderer.DefaultAssets.FONT_DEFAULT));
            debugText.anchor = FontAnchor.TOP_LEFT;
            debugText.pos = new Vector2(20, 20);
            debugText.text = "TEST LINE 1\nTEST LINE 2\nTEST LINE 3";

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
                    lock (loadFont)
                    {
                        loadText = "Simulation initialized";
                        loadTextChanged = true;
                    }
                    doneLoading = true;
                    break;
                case NetServerToClient.REMOVE_PLAYER:
                    Logger.WriteLine(LogType.DEBUG, "Player removed");
                    break;
                case NetServerToClient.STATE_UPDATE:
                    OnStateUpdate(msg);
                    break;
                default:
                    Logger.WriteLine(LogType.POSSIBLE_ERROR, "Unhandled message type from server " + msgID);
                    break;
            }
        }

        struct PlayerState
        {
            public int playerID;
            public BodyState player;
            public BodyState hammer;
            public HammerState hammerState;
            public float hammerDT;
            public PlayerInput input;

            public PlayerState(int playerID, BodyState player, BodyState hammer, HammerState hammerState, float hammerDT,
                PlayerInput input)
            {
                this.playerID = playerID;
                this.player = player;
                this.hammer = hammer;
                this.hammerState = hammerState;
                this.hammerDT = hammerDT;
                this.input = input;
            }
        }

        struct BodyState
        {
            public int bodyHandle;
            public RigidPose pose;
            public BodyVelocity velocity;

            public BodyState(int handle, Vector3 pos, Quaternion ori, Vector3 linVel, Vector3 angVel)
            {
                this.bodyHandle = handle;
                pose = new RigidPose(pos, ori);
                velocity = new BodyVelocity(linVel, angVel);
            }
        }

        struct SimState
        {
            public int Frame;
            public int numPlayers;
            public PlayerState[] playerStates;
            public int numBodies;
            public BodyState[] bodyStates;
        }

        int JitterReadIndex = 0;
        int JitterWriteIndex = 0;
        int JitterCount = 0;
        SimState[] JitterBuffer = new SimState[10];
        void OnStateUpdate(NetIncomingMessage msg)
        {
            if (JitterCount == JitterBuffer.Length)
            {
                //don't necessarily wan't to immediately resize here -- in cases where clients computer hangs for a bit this will easily happen
                //but doesn't represent the actual running expectations
                Logger.WriteLine(LogType.DEBUG, "Jitter Buffer not large enough to store next state");
                return;
            }
            ref SimState state = ref JitterBuffer[JitterWriteIndex++];
            JitterCount++;
            if (JitterWriteIndex == JitterBuffer.Length)
                JitterWriteIndex = 0;
            state.Frame = msg.ReadInt32();
            state.numPlayers = msg.ReadInt32();
            state.playerStates = new PlayerState[state.numPlayers];
            for (int i = 0; i < state.numPlayers; i++)
            {
                ReadPlayer(msg, ref state.playerStates[i]);
            }
            state.numBodies = msg.ReadInt32();
            state.bodyStates = new BodyState[state.numBodies];
            for (int i = 0; i < state.numBodies; i++)
            {
                ReadBody(msg, ref state.bodyStates[i]);
            }
        }

        void ApplyState(ref SimState state)
        {
            for (int i = 0; i < state.numPlayers; i++)
            {
                ref var pState = ref state.playerStates[i];
                var pID = ServerPlayerToLocal[pState.playerID];
                var p = sim.GetPlayer(pID);
                p.SetState(ref pState.player.pose, ref pState.player.velocity,
                    ref pState.hammer.pose, ref pState.hammer.velocity,
                    pState.hammerState, pState.hammerDT);
                p.Input = pState.input;
            }

            for (int i = 0; i < state.numBodies; i++)
            {
                ref var bState = ref state.bodyStates[i];
                var bID = ServerHandleToLocal[bState.bodyHandle];

                var bodyRef = new BodyReference(bID, sim.Simulation.Bodies);
                bodyRef.Pose = bState.pose;
                bodyRef.Velocity = bState.velocity;
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

            var playerCount = msg.ReadInt32();
            for (int i = 0; i < playerCount; i++)
            {
                OnPlayerData(msg, myPlayerID);
            }
        }

        void OnPlayerData(NetIncomingMessage msg, int playerID)
        {
            PlayerState playerState = default;
            ReadPlayer(msg, ref playerState);

            var localID = sim.AddPlayer(playerState.player.pose.Position);

            EnsurePlayerCapacity(playerState.playerID);
            ServerPlayerToLocal[playerState.playerID] = localID;

            var p = sim.GetPlayer(localID);
            p.SetState(ref playerState.player.pose, ref playerState.player.velocity,
                ref playerState.hammer.pose, ref playerState.hammer.velocity,
                playerState.hammerState, playerState.hammerDT);
            p.Input = playerState.input;

            if (playerState.playerID == playerID)
            {
                myPlayer = new MyPlayer(stage, renderer, load_em, cam, sim, localID);
                players.Add(myPlayer);
            }
            else
            {
                players.Add(new Player(sim, renderer, load_em, localID));
            }
        }

        void ReadPlayer(NetIncomingMessage msg, ref PlayerState state)
        {
            state.playerID = msg.ReadInt32();

            ReadBody(msg, ref state.player);
            ReadBody(msg, ref state.hammer);

            state.hammerState = (HammerState)msg.ReadByte();
            state.hammerDT = msg.ReadFloat();

            NetInterop.ReadPlayerInput(ref state.input, msg);
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
                    BodyState bodyState = default;
                    ReadBody(msg, ref bodyState);

                    BodyHandle myHandle = default;
                    if (mass == 0)
                    {
                        myHandle = sim.Simulation.Bodies.Add(BodyDescription.CreateKinematic(
                            bodyState.pose,
                            bodyState.velocity,
                            new CollidableDescription(shapeIndex, specMargin),
                            new BodyActivityDescription(0.01f)));
                    }
                    else
                    {
                        myHandle = sim.Simulation.Bodies.Add(BodyDescription.CreateDynamic(
                            bodyState.pose,
                            bodyState.velocity,
                            inertia,
                            new CollidableDescription(shapeIndex, specMargin),
                            new BodyActivityDescription(0.01f)));
                    }

                    body.SetHandle(myHandle, sim.Simulation);

                    EnsureHandleCapacity(bodyState.bodyHandle);
                    ServerHandleToLocal[bodyState.bodyHandle] = myHandle;
                }
            }
        }

        void ReadBody(NetIncomingMessage msg, ref BodyState state)
        {
            var handle = msg.ReadInt32();
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

            state = new BodyState(handle, 
                new Vector3(posX, posY, posZ),
                new Quaternion(oriX, oriY, oriZ, oriW),
                new Vector3(velX, velY, velZ),
                new Vector3(aVelX, aVelY, aVelZ));
        }

        public void OnDisconnect()
        {
            Logger.WriteLine(LogType.DEBUG, "Disconnect callback");
        }

        public void Update(float dt)
        {
            network.ReadMessages();
            SendInput();
            network.SendMessages();
            UpdateSim(dt);
        }

        FontRenderer debugText;
        void UpdateSim(float dt)
        {
            debugText.text = "Num states: " + JitterCount + "\n" + "Local current frame: " + sim.CurrentFrame + "\n" + "Jitter buffer frame: " + JitterBuffer[JitterReadIndex].Frame
                + "\n" + "Diff: " + (JitterBuffer[JitterReadIndex].Frame - sim.CurrentFrame)
                + "\n" + "Net: " + network.NetworkStats;
            if (JitterCount != 0)
            {
                ref var state = ref JitterBuffer[JitterReadIndex++];
                ApplyState(ref state);
                if (JitterReadIndex == JitterBuffer.Length)
                    JitterReadIndex = 0;
                JitterCount--;
            }
            sim.Update(dt);
        }

        void SendInput()
        {
            var msg = network.CreateMessage();
            msg.Write((int)NetClientToServer.PLAYER_INPUT);
            NetInterop.SerializePlayerInput(ref myPlayer.Input, msg);
            network.SendMessage(msg, NetDeliveryMethod.Unreliable, 0);
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
                box.color = Color.DarkSlateGray;
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
                cyl.color = Color.DarkRed;
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