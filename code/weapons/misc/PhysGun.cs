﻿// Copyright (c) 2022 Ape Tavern, do not share, re-distribute or modify
// without permission of its author (insert_email_here)

using Fortwars;
using Sandbox;
using System;
using System.Linq;

[Library( "physgun", Title = "Manipulator" )]
public partial class PhysGun : Carriable, IUse
{
	public override string ViewModelPath => "models/weapons/manipulator/manipulator_v.vmdl";

	protected PhysicsBody heldBody;
	protected Vector3 heldPos;
	protected Rotation heldRot;

	protected Vector3 holdPos { get; private set; }
	protected Rotation holdRot { get; private set; }

	protected float holdDistance;
	protected bool grabbing;

	protected virtual float MinTargetDistance => 0.0f;
	protected virtual float MaxTargetDistance => 512.0f;
	protected virtual float LinearFrequency => 20.0f;
	protected virtual float LinearDampingRatio => 1.0f;
	protected virtual float AngularFrequency => 20.0f;
	protected virtual float AngularDampingRatio => 1.0f;
	protected virtual float TargetDistanceSpeed => 50.0f;
	protected virtual float RotateSpeed => 0.2f;
	protected virtual float RotateSnapAt => 45.0f;

	[Net] public bool BeamActive { get; set; }
	[Net] public Entity GrabbedEntity { get; set; }
	[Net] public int GrabbedBone { get; set; }
	[Net] public Vector3 GrabbedPos { get; set; }
	[Net] public bool CanGrab { get; set; }

	public PhysicsBody HeldBody => heldBody;

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "models/weapons/manipulator/manipulator_w.vmdl" );
	}

	public bool CheckCanGrab( Player owner, Vector3 EyePosition, Rotation EyeRotation, Vector3 eyeDir )
	{
		if ( GrabbedEntity.IsValid() )
			return true;

		var tr = Trace.Ray( EyePosition, EyePosition + eyeDir * MaxTargetDistance )
			.UseHitboxes()
			.Ignore( owner, false )
			.Run();

		if ( !tr.Hit || !tr.Entity.IsValid() || tr.Entity.IsWorld || tr.StartedSolid )
			return false;

		var rootEnt = tr.Entity.Root;
		var body = tr.Body;

		if ( !body.IsValid() || tr.Entity.Parent.IsValid() )
		{
			if ( rootEnt.IsValid() && rootEnt.PhysicsGroup != null )
			{
				body = rootEnt.PhysicsGroup.BodyCount > 0 ? rootEnt.PhysicsGroup.GetBody( 0 ) : null;
			}
		}

		if ( !body.IsValid() )
			return false;

		if ( rootEnt is not FortwarsBlock )
			return false;

		if ( ( rootEnt as FortwarsBlock ).TeamID != ( owner as FortwarsPlayer ).TeamID )
			return false;

		if ( rootEnt.Owner != owner )
			return false;

		return true;
	}

	public override void Simulate( Client client )
	{
		UpdateViewmodel();

		if ( Owner is not Player owner ) return;

		var EyePosition = owner.EyePosition;
		var eyeDir = owner.EyeRotation.Forward;
		var EyeRotation = Rotation.From( new Angles( 0.0f, owner.EyeRotation.Angles().yaw, 0.0f ) );

		CanGrab = CheckCanGrab( owner, EyePosition, EyeRotation, eyeDir );

		if ( Input.Pressed( InputButton.PrimaryAttack ) )
		{
			( Owner as AnimatedEntity )?.SetAnimParameter( "b_attack", true );

			if ( !grabbing )
				grabbing = true;
		}

		bool grabEnabled = grabbing && Input.Down( InputButton.PrimaryAttack );

		BeamActive = grabEnabled;

		if ( IsServer )
		{
			using ( Prediction.Off() )
			{
				if ( Input.Pressed( InputButton.Reload ) && CanGrab )
				{
					TryDelete( owner, EyePosition, EyeRotation, eyeDir );
				}

				if ( grabEnabled )
				{
					if ( heldBody.IsValid() )
					{
						UpdateGrab( EyePosition, EyeRotation, eyeDir );
					}
					else
					{
						TryStartGrab( owner, EyePosition, EyeRotation, eyeDir );
					}
				}
				else if ( grabbing )
				{
					GrabEnd();
				}
			}
		}

		if ( BeamActive )
		{
			Input.MouseWheel = 0;
		}
	}

	private void TryDelete( Player owner, Vector3 EyePosition, Rotation EyeRotation, Vector3 eyeDir )
	{
		var tr = Trace.Ray( EyePosition, EyePosition + eyeDir * MaxTargetDistance )
			.UseHitboxes()
			.Ignore( owner, false )
			.Run();

		var rootEnt = tr.Entity.Root;
		if ( rootEnt is FortwarsBlock )
			rootEnt.Delete();
	}

	private static bool IsBodyGrabbed( PhysicsBody body ) => All.OfType<PhysGun>().Any( x => x?.HeldBody?.PhysicsGroup == body?.PhysicsGroup );

	private void TryStartGrab( Player owner, Vector3 EyePosition, Rotation EyeRotation, Vector3 eyeDir )
	{
		if ( !CanGrab ) return;

		var tr = Trace.Ray( EyePosition, EyePosition + eyeDir * MaxTargetDistance )
			.UseHitboxes()
			.Ignore( owner, false )
			.Run();

		var rootEnt = tr.Entity.Root;
		var body = tr.Body;

		// Unfreeze
		if ( body.BodyType == PhysicsBodyType.Static )
		{
			body.BodyType = PhysicsBodyType.Dynamic;
		}

		if ( IsBodyGrabbed( body ) )
			return;

		GrabInit( body, EyePosition, tr.EndPosition, EyeRotation );

		GrabbedEntity = rootEnt;
		GrabbedPos = body.Transform.PointToLocal( tr.EndPosition );
		GrabbedBone = body.GroupIndex;

		Client?.Pvs.Add( GrabbedEntity );
	}

	private void UpdateGrab( Vector3 EyePosition, Rotation EyeRotation, Vector3 eyeDir )
	{
		MoveTargetDistance( Input.MouseWheel * TargetDistanceSpeed );

		bool rotating = Input.Down( InputButton.Use );
		bool snapping = false;

		if ( Input.UsingController )
		{
			rotating = Input.Down( InputButton.SecondaryAttack ); //Attack2 feels good to use

			if ( rotating )
			{
				DoRotate( EyeRotation, ( Input.GetAnalog( InputAnalog.Look ) * RotateSpeed * 25f ) + ( new Vector2( Input.MouseDelta.x, Input.MouseDelta.y ) * RotateSpeed ) ); //Using analog look as well as the mouse so the Steam controller is supported with its mouse emulation
				snapping = Input.Down( InputButton.SlotPrev ); //This is bound to the left bumper, so essentially holding the left 2 top buttons makes you able to snap rotate
			}
		}
		else
		{
			if ( rotating )
			{
				DoRotate( EyeRotation, Input.MouseDelta * RotateSpeed );
				snapping = Input.Down( InputButton.Run );
			}
		}

		GrabMove( EyePosition, eyeDir, EyeRotation, snapping );
	}

	private void Activate()
	{
		if ( !IsServer )
			return;
	}

	private void Deactivate()
	{
		if ( IsServer )
		{
			GrabEnd();
		}

		KillEffects();
	}

	public override void ActiveStart( Entity ent )
	{
		base.ActiveStart( ent );

		Activate();
	}

	public override void ActiveEnd( Entity ent, bool dropped )
	{
		base.ActiveEnd( ent, dropped );

		Deactivate();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		Deactivate();
	}

	private void GrabInit( PhysicsBody body, Vector3 startPos, Vector3 grabPos, Rotation rot )
	{
		if ( !body.IsValid() )
			return;

		GrabEnd();

		grabbing = true;
		heldBody = body;
		holdDistance = Vector3.DistanceBetween( startPos, grabPos );
		holdDistance = holdDistance.Clamp( MinTargetDistance, MaxTargetDistance );
		heldPos = heldBody.Transform.PointToLocal( grabPos );
		heldRot = rot.Inverse * heldBody.Rotation;

		heldRot = rot.Inverse * heldBody.Rotation;
		heldPos = heldBody.Transform.PointToLocal( grabPos );

		holdPos = heldBody.Position;
		holdRot = heldBody.Rotation;

		heldBody.Sleeping = false;
		heldBody.AutoSleep = false;
	}

	private void GrabEnd()
	{
		if ( heldBody.IsValid() )
		{
			heldBody.AutoSleep = true;
			heldBody.BodyType = PhysicsBodyType.Static;
		}

		Client?.Pvs.Remove( GrabbedEntity );

		heldBody = null;
		GrabbedEntity = null;
		grabbing = false;
	}

	[Event.Physics.PreStep]
	public void OnPrePhysicsStep()
	{
		if ( !IsServer )
			return;

		if ( !heldBody.IsValid() )
			return;

		if ( GrabbedEntity is Player )
			return;


		var velocity = heldBody.Velocity;
		Vector3.SmoothDamp( heldBody.Position, holdPos, ref velocity, 0.075f, Time.Delta );
		heldBody.Velocity = velocity;
	
		var angularVelocity = heldBody.AngularVelocity;
		Rotation.SmoothDamp( heldBody.Rotation, holdRot, ref angularVelocity, 0.075f, Time.Delta );
		heldBody.AngularVelocity = angularVelocity;
	}

	bool StopPushing;

	private void GrabMove( Vector3 startPos, Vector3 dir, Rotation rot, bool snapAngles )
	{
		if ( !heldBody.IsValid() )
			return;

		TraceResult walltr = Trace.Sweep( heldBody, heldBody.Transform, heldBody.Transform.WithPosition( startPos - heldPos * heldBody.Rotation + dir * holdDistance ) ).Ignore( heldBody.GetEntity() ).Run();
		//Trace.Ray( heldBody.Transform.PointToWorld( heldPos ), startPos + dir * holdDistance ).Ignore( heldBody.GetEntity() ).Run(); //The old ray-based collision check

		StopPushing = walltr.Hit;

		if ( !StopPushing )
		{
			holdPos = startPos - heldPos * heldBody.Rotation + dir * holdDistance;
		}
		else
		{
			holdPos = walltr.EndPosition + walltr.Normal;//Snap to the trace sweep position.
		}

		if ( GrabbedEntity is Player player )
		{
			var velocity = player.Velocity;
			Vector3.SmoothDamp( player.Position, holdPos, ref velocity, 0.075f, Time.Delta );
			player.Velocity = velocity;
			player.GroundEntity = null;

			return;
		}

		holdRot = rot * heldRot;

		if ( snapAngles )
		{
			var angles = holdRot.Angles();

			holdRot = Rotation.From(
				MathF.Round( angles.pitch / RotateSnapAt ) * RotateSnapAt,
				MathF.Round( angles.yaw / RotateSnapAt ) * RotateSnapAt,
				MathF.Round( angles.roll / RotateSnapAt ) * RotateSnapAt
			);
		}
	}

	private void MoveTargetDistance( float distance )
	{
		holdDistance += distance;
		holdDistance = holdDistance.Clamp( MinTargetDistance, MaxTargetDistance );
	}

	protected virtual void DoRotate( Rotation eye, Vector3 input )
	{
		var localRot = eye;
		localRot *= Rotation.FromAxis( Vector3.Up, input.x );
		localRot *= Rotation.FromAxis( Vector3.Right, input.y );
		localRot = eye.Inverse * localRot;

		heldRot = localRot * heldRot;
	}

	public override void BuildInput( InputBuilder owner )
	{
		if ( !GrabbedEntity.IsValid() )
			return;

		if ( !owner.Down( InputButton.PrimaryAttack ) )
			return;

		if ( Input.UsingController )
		{
			if ( owner.Down( InputButton.SecondaryAttack ) )
			{
				owner.ViewAngles = owner.OriginalViewAngles;
			}
		}
		else
		{
			if ( owner.Down( InputButton.Use ) )
			{
				owner.ViewAngles = owner.OriginalViewAngles;
			}
		}
	}

	public bool OnUse( Entity user )
	{
		throw new NotImplementedException();
	}

	public bool IsUsable( Entity user )
	{
		return Owner == null || HeldBody.IsValid();
	}
}
