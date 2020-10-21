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
using BepuUtilities.Collections;

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
        EventManager em;
        public int playerIndex;

        protected Capsule_MRT charGraphics;
        protected Cylinder_MRT hamGraphics;

        public Player(CySim sim, Renderer renderer, EventManager em, int playerID)
        {
            this.sim = sim;
            this.playerIndex = playerID;
            this.em = em;

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

        public void Dispose()
        {
            charGraphics.Dispose();
            hamGraphics.Dispose();
            em.removeUpdateListener(GraphicsUpdate);
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
            for (int i = 0; i < ServerPlayerToLocal.Length; i++)
                ServerPlayerToLocal[i] = -1;
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

            int oldLen = ServerPlayerToLocal.Length;
            Array.Resize(ref ServerPlayerToLocal, capacity);
            for (int i = oldLen; i < capacity; i++)
            {
                ServerPlayerToLocal[i] = -1;
            }
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
                    {
                        Logger.WriteLine(LogType.DEBUG, "New player joined");
                        var frame = msg.ReadInt32();
                        var playerID = msg.ReadInt32();
                        var posX = msg.ReadFloat();
                        var posY = msg.ReadFloat();
                        var posZ = msg.ReadFloat();
                        var localID = sim.AddPlayer(new Vector3(posX, posY, posZ));

                        EnsurePlayerCapacity(playerID);
                        ServerPlayerToLocal[playerID] = localID;
                        players.Add(new Player(sim, renderer, load_em, localID));
                    }
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
                    {
                        Logger.WriteLine(LogType.DEBUG, "Player removed");
                        var frame = msg.ReadInt32();
                        var playerID = msg.ReadInt32();

                        var localID = ServerPlayerToLocal[playerID];
                        if (localID < 0)
                        {
                            Logger.WriteLine(LogType.DEBUG, "Got remove player for a player we don't have.");
                            break;
                        }

                        for (int i = 0; i < players.Count; i++)
                        {
                            if (players[i].playerIndex == localID)
                            {
                                players[i].Dispose();
                                players.RemoveAt(i);
                                break;
                            }
                        }

                        sim.RemovePlayer(localID);
                        ServerPlayerToLocal[playerID] = -1;
                    }
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
            public QuickList<PlayerState> playerStates;
            public int numBodies;
            public QuickList<BodyState> bodyStates;
        }

        RingBuffer<SimState> JitterBuffer = new RingBuffer<SimState>(32); //about 500ms of state

        float NetStatsTimeWindow = 10; //seconds
        float JitterBufferAdjustmentWindow = 2;
        int JitterAcceptableDrops = 10; //number of dropped states due to jitter that is acceptable per time period
        int LatestStateRecieved = -1;
        float CurrentJitterTime = 0;
        int JitterFrameReshuffleMax = 20;

        RingBuffer<float> FramesDroppedOutOfOrder = new RingBuffer<float>(128);
        RingBuffer<float> FramesMissedDueToJitter = new RingBuffer<float>(128);
        RingBuffer<float> FramesDroppedDuped = new RingBuffer<float>(128);
        RingBuffer<float> FramesSkippedDueToMinBuffer = new RingBuffer<float>(128);
        RingBuffer<float> FramesDoubledDueToMaxBuffer = new RingBuffer<float>(128);
        RingBuffer<float> FramesWithNoServerState = new RingBuffer<float>(128);
        RingBuffer<int> FramesRecieved = new RingBuffer<int>(128);
        void OnStateUpdate(NetIncomingMessage msg)
        {
            if (JitterBuffer.Count == JitterBuffer.Capacity)
            {
                //don't necessarily wan't to immediately resize here -- in cases where clients computer hangs for a bit this will easily happen
                //but doesn't represent the actual running expectations
                JitterBuffer.RemoveFirst();
            }

            var frame = msg.ReadInt32();
            if (frame == LatestStateRecieved)
            {
                FramesDroppedDuped.Add(CurrentJitterTime);
                return;
            }
            FramesRecieved.Add(frame);
            if (frame > LatestStateRecieved)
                LatestStateRecieved = frame;
            if (frame < sim.CurrentFrame)
            {
                FramesMissedDueToJitter.Add(CurrentJitterTime);
                return;
            }

            ref var state = ref JitterBuffer.AllocateUnsafely();
            if (frame < LatestStateRecieved)
            {
                if (LatestStateRecieved - frame < JitterFrameReshuffleMax)
                {
                    //try to insert into the ring buffer
                    //there is some... weirdass 'safe' pointer shenanigans here
                    //be very careful
                    int insertIndex = -1;
                    for (int i = 0; i < JitterFrameReshuffleMax; i++)
                    {
                        int index = JitterBuffer.Count - 3 - i;
                        if (index < 0)
                        {
                            insertIndex = i + 1;
                            break;
                        }
                        ref var cur = ref JitterBuffer[index];
                        if (frame == cur.Frame)
                        {
                            //duped
                            FramesDroppedDuped.Add(CurrentJitterTime);
                            JitterBuffer.RemoveLast();
                            return;
                        }
                        else if (frame > cur.Frame)
                        {
                            insertIndex = i + 1;
                            break;
                        }
                    }

                    if (insertIndex < 0)
                    {
                        //couldn't find?
                        FramesDroppedOutOfOrder.Add(CurrentJitterTime);
                        JitterBuffer.RemoveLast();
                        return;
                    }
                    else
                    {
                        for (int i = 0; i < insertIndex; i++)
                        {
                            //this is pretty dumb
                            ref var cur = ref JitterBuffer[JitterBuffer.Count - 2 - i];
                            var statePlayers = state.playerStates;
                            var stateBodies = state.bodyStates;
                            state = cur;
                            cur.playerStates = statePlayers;
                            cur.bodyStates = stateBodies;
                            state = ref cur;
                        }
                    }
                }
                else
                {//way the hell out of order
                    FramesDroppedOutOfOrder.Add(CurrentJitterTime);
                    JitterBuffer.RemoveLast();
                    return;
                }
            }

            state.Frame = frame;
            state.numPlayers = msg.ReadInt32();
            state.playerStates.EnsureCapacity(state.numPlayers, sim.BufferPool);
            state.playerStates.Count = 0;
            for (int i = 0; i < state.numPlayers; i++)
            {
                ReadPlayer(msg, ref state.playerStates.AllocateUnsafely());
            }
            state.numBodies = msg.ReadInt32();
            state.bodyStates.EnsureCapacity(state.numBodies, sim.BufferPool);
            state.bodyStates.Count = 0;
            for (int i = 0; i < state.numBodies; i++)
            {
                ReadBody(msg, ref state.bodyStates.AllocateUnsafely());
            }
        }

        void ApplyState(ref SimState state)
        {
            for (int i = 0; i < state.numPlayers; i++)
            {
                ref var pState = ref state.playerStates[i];
                var pID = ServerPlayerToLocal[pState.playerID];
                if (pID < 0)
                {
                    Logger.WriteLine(LogType.DEBUG, "Recieved a player ID from the server that we don't have an active map for.");
                    continue;
                }
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
                if (bID.Value < 0)
                {
                    Logger.WriteLine(LogType.DEBUG, "Recieved a body ID from the server that we don't have an active map for.");
                    continue;
                }

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
        int JitterGoalMinBuffer = 0;
        int JitterGoalMaxBuffer = 2;

        RingBuffer<(float Time, int Min, int Max)> JitterMinMaxFrames = new RingBuffer<(float, int, int)>(1024);
        int ActualJitterBufferRange = -1;
        void UpdateSim(float dt)
        {
            CurrentJitterTime += dt;
            UpdateStatsBuffers();

            int diffToNextState = -1;
            if (JitterBuffer.Count != 0)
                diffToNextState = JitterBuffer.ReadFirst().Frame - sim.CurrentFrame;
            debugText.text =
                "Num frames buffered: " + JitterBuffer.Count
                + "\nLocal current frame: " + sim.CurrentFrame
                + "\nFrame diff to next state available: " + diffToNextState
                + "\nFrame diff to latest state recieved: " + (LatestStateRecieved - sim.CurrentFrame)
                + "\nFrames dropped due to out of order: " + FramesDroppedOutOfOrder.Count
                + "\nFrames dropped due to jitter: " + FramesMissedDueToJitter.Count
                + "\nFrames dropped due to duplication: " + FramesDroppedDuped.Count
                + "\nGoal Jitter Max Size: " + JitterGoalMaxBuffer
                + "\nFrames skipped due to min buffer: " + FramesSkippedDueToMinBuffer.Count
                + "\nFrames doubled due to max buffer: " + FramesDoubledDueToMaxBuffer.Count
                + "\nFrames with no server state: " + FramesWithNoServerState.Count
                + "\nActual jitter range: " + ActualJitterBufferRange
                + "\n" + network.NetworkStats;

            //decide here how much to update
            //we want, over the last time period, to have a frame available some % of the time -- not counting dropped packets
            //the sliding window timeframe lets us adjust the jitter buffer size if network conditions change
            //window needs to be long enough to have reasonable stats, short enough to respond to shifting network conditions
            //we don't expect 'network conditions' to change very much, or care to respond to 1-2s blips

            //if we don't have any jitter buffer we're not sure how far we off from the goal, so we just keep simming until something happens
            if (FramesMissedDueToJitter.Count > JitterAcceptableDrops)
            {
                //JitterGoalMaxBuffer++;
                //FramesMissedDueToJitter.Clear();
                //JitterMinMaxFrames.Clear();
                //return;
            }
            int LastFrameDiff = LatestStateRecieved - sim.CurrentFrame;
            int JitterRangeMin = LastFrameDiff;
            int JitterRangeMax = LastFrameDiff;

            while (FramesRecieved.Count != 0)
            {
                var cur = FramesRecieved.ReadFirst();
                FramesRecieved.RemoveFirst();

                var diff = cur - sim.CurrentFrame;
                if (JitterRangeMin > diff)
                    JitterRangeMin = diff;
                if (JitterRangeMax < diff)
                    JitterRangeMax = diff;
            }

            if (JitterMinMaxFrames.Count != 0)
            {
                bool LargestFound = false;
                bool SmallestFound = false;

                for (int i = JitterMinMaxFrames.Count - 1; i >= 0; i--)
                {
                    ref var t = ref JitterMinMaxFrames[i];
                    if (JitterRangeMin < t.Min)
                    {
                        t.Min = JitterRangeMin;
                        SmallestFound = true;
                    }
                    if (JitterRangeMax > t.Max)
                    {
                        t.Max = JitterRangeMax;
                        LargestFound = true;
                    }
                }

                if (LargestFound || SmallestFound)
                {
                    JitterMinMaxFrames.Add((CurrentJitterTime, JitterRangeMin, JitterRangeMax));
                }
            }

            if (JitterMinMaxFrames.Count == 0)
            {
                JitterMinMaxFrames.Add((CurrentJitterTime, JitterRangeMin, JitterRangeMax));
            }

            float timeCutoff = CurrentJitterTime - JitterBufferAdjustmentWindow;
            while (JitterMinMaxFrames.Count != 0)
            {
                ref var t = ref JitterMinMaxFrames.ReadFirst();
                ActualJitterBufferRange = t.Max - t.Min;
                JitterGoalMaxBuffer = ActualJitterBufferRange + JitterGoalMinBuffer;

                if (t.Time < timeCutoff)
                {
                    JitterMinMaxFrames.RemoveFirst();
                }
                else
                    break;
            }

            if (JitterRangeMin < JitterGoalMinBuffer)
            {
                FramesSkippedDueToMinBuffer.Add(CurrentJitterTime);
                return;
            }
            if (LastFrameDiff > JitterGoalMaxBuffer)
            {//we have some choices here
                //right now it just runs an 'extra' simulation update, which it'll keep doing til it catches up
                //other options include just setting our simFrame higher -- it'll "jump" everything forward and take a second to resync with the server
                //or doing more than 1 extra timestep here
                FramesDoubledDueToMaxBuffer.Add(CurrentJitterTime);
                TimestepSim(dt);
            }

            TimestepSim(dt);
        }

        void UpdateStatsBuffers()
        {
            float timeCutoff = CurrentJitterTime - NetStatsTimeWindow;
            while (FramesDroppedOutOfOrder.Count != 0 && FramesDroppedOutOfOrder.ReadFirst() < timeCutoff)
            {
                FramesDroppedOutOfOrder.RemoveFirst();
            }
            while (FramesMissedDueToJitter.Count != 0 && FramesMissedDueToJitter.ReadFirst() < timeCutoff)
            {
                FramesMissedDueToJitter.RemoveFirst();
            }
            while (FramesDroppedDuped.Count != 0 && FramesDroppedDuped.ReadFirst() < timeCutoff)
            {
                FramesDroppedDuped.RemoveFirst();
            }
            while (FramesSkippedDueToMinBuffer.Count != 0 && FramesSkippedDueToMinBuffer.ReadFirst() < timeCutoff)
            {
                FramesSkippedDueToMinBuffer.RemoveFirst();
            }
            while (FramesDoubledDueToMaxBuffer.Count != 0 && FramesDoubledDueToMaxBuffer.ReadFirst() < timeCutoff)
            {
                FramesDoubledDueToMaxBuffer.RemoveFirst();
            }
            while (FramesWithNoServerState.Count != 0 && FramesWithNoServerState.ReadFirst() < timeCutoff)
            {
                FramesWithNoServerState.RemoveFirst();
            }
        }

        void TimestepSim(float dt)
        {
            bool stateApplied = false;
            while (JitterBuffer.Count != 0)
            {
                ref var state = ref JitterBuffer.ReadFirst();
                if (state.Frame == sim.CurrentFrame)
                {
                    ApplyState(ref state);
                    JitterBuffer.RemoveFirst();
                    stateApplied = true;
                    break;
                }
                else if (state.Frame < sim.CurrentFrame)
                {
                    JitterBuffer.RemoveFirst();
                }
                else //state fame is higher than our current frame
                {
                    break;
                }
            }

            if (!stateApplied)
                FramesWithNoServerState.Add(CurrentJitterTime);
            sim.Update(dt);
        }

        void SendInput()
        {
            var msg = network.CreateMessage();
            msg.Write((int)NetClientToServer.PLAYER_INPUT);
            msg.Write(sim.CurrentFrame);
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

        static Color[] AcceptableColors = new Color[]
        {
            Color.IndianRed,
            Color.DarkMagenta,
            Color.DarkBlue,
            Color.DarkCyan,
            Color.DarkGreen,
            Color.DarkOrange,
            Color.DarkGoldenrod,
            Color.DarkGray
        };

        static Random ColorRandomizer = new Random();

        class BoxThingy : IBodyRenderer
        {
            List<BodyReference> bodyRefs;
            Box_MRT box;
            public BoxThingy(float width, float height, float length, Renderer renderer, EventManager em)
            {
                box = new Box_MRT(renderer, null, 0);
                box.color = AcceptableColors[ColorRandomizer.Next(0, AcceptableColors.Length)];
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
                cyl.color = AcceptableColors[ColorRandomizer.Next(0, AcceptableColors.Length)];
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
                box.color = AcceptableColors[ColorRandomizer.Next(0, AcceptableColors.Length)];
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