﻿using BepuPhysics;
using BepuPhysics.Collidables;
using System.Numerics;
using System;
using System.Diagnostics;
using BepuUtilities;
using BepuPhysics.Constraints;

using cylib;

namespace cySim
{
    public struct PlayerInput
    {
        public Vector3 MoveDirection;
        public bool MoveForward, MoveBackward, MoveLeft, MoveRight;
        public bool TryJump;
        public bool Sprint;
        public bool TryFire;
    }

    /// <summary>
    /// Convenience structure that wraps a CharacterController reference and its associated body.
    /// </summary>
    /// <remarks>
    /// <para>This should be treated as an example- nothing here is intended to suggest how you *must* handle characters. 
    /// On the contrary, this does some fairly inefficient stuff if you're dealing with hundreds of characters in a predictable way.
    /// It's just a fairly convenient interface for demos usage.</para>
    /// <para>Note that all characters are dynamic and respond to constraints and forces in the simulation.</para>
    /// </remarks>
    public class CharacterInput
    {
        static BallSocketServo Constraints_POS_IDLE = new BallSocketServo
        {
            LocalOffsetA = Vector3.UnitX * 1.5f,
            LocalOffsetB = Vector3.Zero,
            ServoSettings = new ServoSettings(2f, 1f, 15f),
            SpringSettings = new SpringSettings(30, 1)
        };

        static BallSocketServo Constraints_POS_SMASH = new BallSocketServo
        {
            LocalOffsetA = Vector3.UnitX * 1.5f, //this gets set by game logic
            LocalOffsetB = Vector3.Zero,
            ServoSettings = new ServoSettings(10f, 2f, 30f),
            SpringSettings = new SpringSettings(30, 1)
        };

        static AngularServo Constraints_ANGULAR = new AngularServo
        {
            TargetRelativeRotationLocalA = Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)(Math.PI * 0.5f)),
            ServoSettings = new ServoSettings(2f, 1f, 5f),
            SpringSettings = new SpringSettings(30, 1)
        };

        static DistanceServo Constraints_DISTANCE = new DistanceServo
        {
            LocalOffsetA = Vector3.UnitX * 0.25f,
            LocalOffsetB = Vector3.Zero,
            TargetDistance = 1.25f,
            ServoSettings = new ServoSettings(2f, 1f, 20f),
            SpringSettings = new SpringSettings(30, 1)
        };

        static OneBodyAngularServo Constraints_CHAR_ANGULAR = new OneBodyAngularServo
        {
            TargetOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1f),
            ServoSettings = new ServoSettings(5, 5, 20f),
            SpringSettings = new SpringSettings(30, 1)
        };

        BodyHandle bodyHandle;
        ConstraintHandle bodyAngularHandle;
        CharacterControllers characters;
        float speed;
        Capsule shape;

        public BodyHandle BodyHandle { get { return bodyHandle; } }
        public BodyHandle HammerHandle { get { return hamHandle; } }

        public ref PlayerInput Input { get { return ref input; } }

        PlayerInput input;

        BodyHandle hamHandle;
        ConstraintHandle bsHandle;
        ConstraintHandle asHandle;
        ConstraintHandle dHandle;

        public CharacterInput(CharacterControllers characters, Vector3 initialPosition, Capsule shape,
            float speculativeMargin, float mass, float maximumHorizontalForce, float maximumVerticalGlueForce,
            float jumpVelocity, float speed, float maximumSlope = MathF.PI * 0.25f)
        {
            this.characters = characters;
            var shapeIndex = characters.Simulation.Shapes.Add(shape);

            //Because characters are dynamic, they require a defined BodyInertia. For the purposes of the demos, we don't want them to rotate or fall over, so the inverse inertia tensor is left at its default value of all zeroes.
            //This is effectively equivalent to giving it an infinite inertia tensor- in other words, no torque will cause it to rotate.
            bodyHandle = characters.Simulation.Bodies.Add(BodyDescription.CreateDynamic(initialPosition, new BodyInertia { InverseMass = 1f / mass, InverseInertiaTensor = new Symmetric3x3 { XX = 1, YY = 1, ZZ = 1 } }, new CollidableDescription(shapeIndex, speculativeMargin), new BodyActivityDescription(shape.Radius * 0.02f)));
            ref var character = ref characters.AllocateCharacter(bodyHandle);
            character.LocalUp = new Vector3(0, 1, 0);
            character.CosMaximumSlope = MathF.Cos(maximumSlope);
            character.JumpVelocity = jumpVelocity;
            character.MaximumVerticalForce = maximumVerticalGlueForce;
            character.MaximumHorizontalForce = maximumHorizontalForce;
            character.MinimumSupportDepth = shape.Radius * -0.01f;
            character.MinimumSupportContinuationDepth = -speculativeMargin;
            this.speed = speed;
            this.shape = shape;

            bodyAngularHandle = characters.Simulation.Solver.Add(bodyHandle, Constraints_CHAR_ANGULAR);

            input = new PlayerInput();

            hamHandle = characters.Simulation.Bodies.Add(BodyDescription.CreateConvexDynamic(initialPosition + Constraints_POS_IDLE.LocalOffsetA, 0.25f, characters.Simulation.Shapes, new Cylinder(0.25f, 0.5f)));

            bsHandle = characters.Simulation.Solver.Add(bodyHandle, hamHandle, Constraints_POS_IDLE);
            asHandle = characters.Simulation.Solver.Add(bodyHandle, hamHandle, Constraints_ANGULAR);
            dHandle = characters.Simulation.Solver.Add(bodyHandle, hamHandle, Constraints_DISTANCE);

            ref var hammer = ref characters.AllocateHammer(hamHandle, bodyHandle);
            hammer.HammerState = HammerState.IDLE;
            hammer.SmashValue = 2;
        }


        public void UpdateCharacterGoals(float dt)
        {
            Vector2 movementDirection = default;
            if (input.MoveForward)
            {
                movementDirection = new Vector2(0, 1);
            }
            if (input.MoveBackward)
            {
                movementDirection += new Vector2(0, -1);
            }
            if (input.MoveLeft)
            {
                movementDirection += new Vector2(-1, 0);
            }
            if (input.MoveRight)
            {
                movementDirection += new Vector2(1, 0);
            }
            var movementDirectionLengthSquared = movementDirection.LengthSquared();
            if (movementDirectionLengthSquared > 0)
            {
                movementDirection /= MathF.Sqrt(movementDirectionLengthSquared);
            }

            ref var character = ref characters.GetCharacterByBodyHandle(bodyHandle);
            character.TryJump = input.TryJump;
            input.TryJump = false;
            var characterBody = new BodyReference(bodyHandle, characters.Simulation.Bodies);
            var effectiveSpeed = input.Sprint ? speed * 1.75f : speed;
            var newTargetVelocity = movementDirection * effectiveSpeed;
            var viewDirection = input.MoveDirection;
            //Modifying the character's raw data does not automatically wake the character up, so we do so explicitly if necessary.
            //If you don't explicitly wake the character up, it won't respond to the changed motion goals.
            //(You can also specify a negative deactivation threshold in the BodyActivityDescription to prevent the character from sleeping at all.)
            if (!characterBody.Awake &&
                ((character.TryJump && character.Supported) ||
                newTargetVelocity != character.TargetVelocity ||
                (newTargetVelocity != Vector2.Zero && character.ViewDirection != viewDirection)))
            {
                characters.Simulation.Awakener.AwakenBody(character.BodyHandle);
                characters.Simulation.Awakener.AwakenBody(hamHandle);
            }
            character.TargetVelocity = newTargetVelocity;
            character.ViewDirection = viewDirection;

            //The character's motion constraints aren't active while the character is in the air, so if we want air control, we'll need to apply it ourselves.
            //(You could also modify the constraints to do this, but the robustness of solved constraints tends to be a lot less important for air control.)
            //There isn't any one 'correct' way to implement air control- it's a nonphysical gameplay thing, and this is just one way to do it.
            //Note that this permits accelerating along a particular direction, and never attempts to slow down the character.
            //This allows some movement quirks common in some game character controllers.
            //Consider what happens if, starting from a standstill, you accelerate fully along X, then along Z- your full velocity magnitude will be sqrt(2) * maximumAirSpeed.
            //Feel free to try alternative implementations. Again, there is no one correct approach.
            if (!character.Supported && movementDirectionLengthSquared > 0)
            {
                QuaternionEx.Transform(character.LocalUp, characterBody.Pose.Orientation, out var characterUp);
                var characterRight = Vector3.Cross(character.ViewDirection, characterUp);
                var rightLengthSquared = characterRight.LengthSquared();
                if (rightLengthSquared > 1e-10f)
                {
                    characterRight /= MathF.Sqrt(rightLengthSquared);
                    var characterForward = Vector3.Cross(characterUp, characterRight);
                    var worldMovementDirection = characterRight * movementDirection.X + characterForward * movementDirection.Y;
                    var currentVelocity = Vector3.Dot(characterBody.Velocity.Linear, worldMovementDirection);
                    //We'll arbitrarily set air control to be a fraction of supported movement's speed/force.
                    const float airControlForceScale = .2f;
                    const float airControlSpeedScale = .2f;
                    var airAccelerationDt = characterBody.LocalInertia.InverseMass * character.MaximumHorizontalForce * airControlForceScale * dt;
                    var maximumAirSpeed = effectiveSpeed * airControlSpeedScale;
                    var targetVelocity = MathF.Min(currentVelocity + airAccelerationDt, maximumAirSpeed);
                    //While we shouldn't allow the character to continue accelerating in the air indefinitely, trying to move in a given direction should never slow us down in that direction.
                    var velocityChangeAlongMovementDirection = MathF.Max(0, targetVelocity - currentVelocity);
                    characterBody.Velocity.Linear += worldMovementDirection * velocityChangeAlongMovementDirection;
                    Debug.Assert(characterBody.Awake, "Velocity changes don't automatically update objects; the character should have already been woken up before applying air control.");
                }
            }

            ref var hammer = ref characters.GetHammerByBodyHandle(hamHandle);
            if (input.TryFire)
            {
                input.TryFire = false;

                if (hammer.HammerState == HammerState.IDLE)
                {
                    hammer.HammerState = HammerState.SMASH;
                    hammer.HammerDT = 0;
                }
            }

            if (hammer.HammerState == HammerState.RECOIL)
            {
                hammer.HammerState = HammerState.IDLE;
                characters.Simulation.Solver.ApplyDescriptionWithoutWaking(bsHandle, ref Constraints_POS_IDLE);
            }
            else if (hammer.HammerState == HammerState.SMASH)
            {
                hammer.HammerDT += dt;

                float fireSpeed = 6;
                var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, hammer.HammerDT * fireSpeed);
                QuaternionEx.Transform(Constraints_POS_IDLE.LocalOffsetA, rot, out Constraints_POS_SMASH.LocalOffsetA);

                characters.Simulation.Solver.ApplyDescriptionWithoutWaking(bsHandle, ref Constraints_POS_SMASH);
            }

            /*
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

                    if (fireDt > 0.4f)
                    {
                        bsDesc.ServoSettings = new ServoSettings(2f, 1f, 15f);
                        bsDesc.LocalOffsetA = Vector3.UnitX * 1.5f;
                        Simulation.Solver.ApplyDescriptionWithoutWaking(bsHandle, ref bsDesc);
                        fire = false;
                    }
                }
            }
            */
        }


        /// <summary>
        /// Removes the character's body from the simulation and the character from the associated characters set.
        /// </summary>
        public void Dispose()
        {
            characters.Simulation.Shapes.Remove(new BodyReference(bodyHandle, characters.Simulation.Bodies).Collidable.Shape);
            characters.Simulation.Bodies.Remove(bodyHandle);
            characters.RemoveCharacterByBodyHandle(bodyHandle);
        }
    }
}


