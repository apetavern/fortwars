﻿// Copyright (c) 2022 Ape Tavern, do not share, re-distribute or modify
// without permission of its author (insert_email_here)

using Sandbox;
using System;

namespace Fortwars;

[Library]
public partial class FortwarsWalkController : BasePlayerController
{
	public float SprintSpeed => 320f;
	public float DefaultSpeed => 250f;
	public float Acceleration => 8.0f;
	public float AirAcceleration => 8.0f;
	public float GroundFriction => 8.0f;
	public float StopSpeed => 100.0f;
	public float DistEpsilon => 0.03125f;
	public float GroundAngle => 46.0f;
	public float StepSize => 18.0f;
	public float MaxNonJumpVelocity => 128.0f;
	public float BodyGirth => 32.0f;
	public float BodyHeight => 72.0f;
	public float EyeHeight => 64.0f;
	public float Gravity => 800.0f;
	public bool AutoJump => false;
	public float AirControl => 32f;

	public DuckSlide DuckSlide { get; private set; }
	public Unstuck Unstuck { get; private set; }
	public bool IsSwimming { get; set; } = false;
	private bool IsGrounded => GroundEntity != null;
	public bool IsSprinting => Input.Down( InputButton.Run ) && IsGrounded && ForwardSpeed > 100f && !DuckSlide.IsActive;
	public float ForwardSpeed => Velocity.Dot( EyeRotation.Forward );

	private TimeSince timeSinceLastJump;
	public float JumpDecayTime => 1.0f; // Seconds


	public FortwarsWalkController()
	{
		DuckSlide = new DuckSlide( this );
		Unstuck = new Unstuck( this );
	}

	/// <summary>
	/// This is temporary, get the hull size for the player's collision
	/// </summary>
	public override BBox GetHull()
	{
		var girth = BodyGirth * 0.5f;
		var mins = new Vector3( -girth, -girth, 0 );
		var maxs = new Vector3( +girth, +girth, BodyHeight );

		return new BBox( mins, maxs );
	}

	// Duck body height 32
	// Eye Height 64
	// Duck Eye Height 28

	protected Vector3 mins;
	protected Vector3 maxs;

	public virtual void SetBBox( Vector3 mins, Vector3 maxs )
	{
		if ( this.mins == mins && this.maxs == maxs )
			return;

		this.mins = mins;
		this.maxs = maxs;
	}

	/// <summary>
	/// Update the size of the bbox. We should really trigger some shit if this changes.
	/// </summary>
	public virtual void UpdateBBox()
	{
		var girth = BodyGirth * 0.5f;

		var mins = new Vector3( -girth, -girth, 0 ) * Pawn.Scale;
		var maxs = new Vector3( +girth, +girth, BodyHeight ) * Pawn.Scale;

		DuckSlide.UpdateBBox( ref mins, ref maxs, Pawn.Scale );

		SetBBox( mins, maxs );
	}

	protected float SurfaceFriction;


	public override void FrameSimulate()
	{
		base.FrameSimulate();

		EyeRotation = Input.Rotation;
	}

	public float FallVelocity { get; private set; } = 0;

	public override void Simulate()
	{
		FallVelocity = -Pawn.Velocity.z;

		// EyeLocalPosition = Vector3.Up * ( EyeHeight * Pawn.Scale );
		UpdateBBox();

		EyeLocalPosition += TraceOffset;
		EyeRotation = Input.Rotation;

		if ( Unstuck.TestAndFix() )
			return;

		CheckLadder();
		IsSwimming = Pawn.WaterLevel > 0.6f;

		//
		// Start Gravity
		//
		if ( !IsSwimming && !IsTouchingLadder )
		{
			Velocity -= new Vector3( 0, 0, Gravity * 0.5f ) * Time.Delta;
			Velocity += new Vector3( 0, 0, BaseVelocity.z ) * Time.Delta;

			BaseVelocity = BaseVelocity.WithZ( 0 );
		}

		if ( AutoJump ? Input.Down( InputButton.Jump ) : Input.Pressed( InputButton.Jump ) )
		{
			CheckJumpButton();
		}

		// Fricion is handled before we add in any base velocity. That way, if we are on a conveyor,
		//  we don't slow when standing still, relative to the conveyor.
		bool bStartOnGround = GroundEntity != null;
		//bool bDropSound = false;
		if ( bStartOnGround )
		{
			Velocity = Velocity.WithZ( 0 );

			if ( GroundEntity != null )
			{
				ApplyFriction( GroundFriction * SurfaceFriction * DuckSlide.GetFrictionMultiplier() );
			}
		}

		//
		// Work out wish velocity.. just take input, rotate it to view, clamp to -1, 1
		//
		WishVelocity = new Vector3( Input.Forward, Input.Left, 0 );
		var inSpeed = WishVelocity.Length.Clamp( 0, 1 );
		WishVelocity *= Input.Rotation.Angles().WithPitch( 0 ).ToRotation();

		if ( !IsSwimming && !IsTouchingLadder )
		{
			WishVelocity = WishVelocity.WithZ( 0 );
		}

		WishVelocity = WishVelocity.Normal * inSpeed;
		WishVelocity *= GetWishSpeed();

		//
		// Wish velocity: weapon movement speed multiplier
		//
		if ( ( Pawn as FortwarsPlayer ).ActiveChild is FortwarsWeapon weapon && weapon != null && weapon.WeaponAsset != null )
			WishVelocity *= weapon.WeaponAsset.MovementSpeedMultiplier;

		//
		// Wish velocity: jump decay penalty
		//
		float jumpDecayMul = 1.0f;
		if ( timeSinceLastJump < JumpDecayTime && !IsSwimming )
			jumpDecayMul = timeSinceLastJump / JumpDecayTime;

		WishVelocity *= jumpDecayMul;

		DuckSlide.PreTick();

		bool bStayOnGround = false;
		if ( IsSwimming )
		{
			ApplyFriction( 1 );
			WaterMove();
		}
		else if ( IsTouchingLadder )
		{
			LadderMove();
		}
		else if ( GroundEntity != null )
		{
			bStayOnGround = true;
			WalkMove();
		}
		else
		{
			AirMove();
		}

		CategorizePosition( bStayOnGround );

		// FinishGravity
		if ( !IsSwimming && !IsTouchingLadder )
		{
			Velocity -= new Vector3( 0, 0, Gravity * 0.5f ) * Time.Delta;
		}

		if ( GroundEntity != null )
		{
			Velocity = Velocity.WithZ( 0 );
		}

		CheckFalling();

		if ( Debug )
		{
			DebugOverlay.Box( Position + TraceOffset, mins, maxs, Color.Red );
			DebugOverlay.Box( Position, mins, maxs, Color.Blue );

			var lineOffset = 0;
			if ( Host.IsServer ) lineOffset = 10;

			DebugOverlay.ScreenText( $"        Position: {Position}", lineOffset + 0 );
			DebugOverlay.ScreenText( $"        Velocity: {Velocity}", lineOffset + 1 );
			DebugOverlay.ScreenText( $"    BaseVelocity: {BaseVelocity}", lineOffset + 2 );
			DebugOverlay.ScreenText( $"    GroundEntity: {GroundEntity} [{GroundEntity?.Velocity}]", lineOffset + 3 );
			DebugOverlay.ScreenText( $" SurfaceFriction: {SurfaceFriction}", lineOffset + 4 );
			DebugOverlay.ScreenText( $"    WishVelocity: {WishVelocity}", lineOffset + 5 );
		}
	}

	public float FallPunchThreshold => 350f; // won't make player's screen / make scrape noise unless player falling at least this fast.
	public float PlayerLandOnFloatingObject => 173; // Can fall another 173 in/sec without getting hurt
	public float PlayerMinBounceSpeed => 173;
	public float PlayerMaxSafeFallSpeed => MathF.Sqrt( 2 * Gravity * 20 * 12 ); // approx 20 feet
	public float PlayerFatalFallSpeed => MathF.Sqrt( 2 * Gravity * 60 * 12 ); // approx 60 feet
	public float DamageForFallSpeed => 100.0f / ( PlayerFatalFallSpeed - PlayerMaxSafeFallSpeed );

	private void CheckFalling()
	{
		if ( GroundEntity == null || FallVelocity <= 0 )
			return;

		float fallVelocity = FallVelocity;

		if ( Pawn.LifeState != LifeState.Dead
			&& fallVelocity >= FallPunchThreshold
			&& !( Pawn.WaterLevel >= 1f ) )
		{
			float punchStrength = fallVelocity.LerpInverse( FallPunchThreshold, FallPunchThreshold * 2 );

			// TODO
			// _ = new Sandbox.ScreenShake.ViewPunch( 1f, punchStrength * 2f );

			if ( GroundEntity.WaterLevel >= 1f )
			{
				FallVelocity -= PlayerLandOnFloatingObject;
			}

			if ( GroundEntity.Velocity.z < 0.0f )
			{
				FallVelocity += GroundEntity.Velocity.z;
				FallVelocity = MathF.Max( 0.1f, FallVelocity );
			}

			if ( FallVelocity > PlayerMaxSafeFallSpeed )
			{
				TakeFallDamage();
				PlayRoughLandingEffects( 0.85f );
			}
		}
	}

	private void PlayRoughLandingEffects( float soundVolume )
	{
		Sound.FromEntity( "damage.fall", Pawn ).SetVolume( soundVolume );
	}

	private void TakeFallDamage()
	{
		float fallDamage = ( FallVelocity - PlayerMaxSafeFallSpeed ) * DamageForFallSpeed;
		Pawn.TakeDamage( DamageInfoExtension.FromFall( fallDamage, Pawn ) );
	}

	public virtual float GetWishSpeed()
	{
		var ws = DuckSlide.GetWishSpeed();
		if ( ws >= 0 ) return ws;

		if ( Input.Down( InputButton.Run ) )
			return SprintSpeed;

		return DefaultSpeed;
	}

	public virtual void WalkMove()
	{
		var wishdir = WishVelocity.Normal;
		var wishspeed = WishVelocity.Length;

		WishVelocity = WishVelocity.WithZ( 0 );
		WishVelocity = WishVelocity.Normal * wishspeed;

		Velocity = Velocity.WithZ( 0 );
		Accelerate( wishdir, wishspeed, 0, Acceleration );
		Velocity = Velocity.WithZ( 0 );

		// Add in any base velocity to the current velocity.
		Velocity += BaseVelocity;

		try
		{
			if ( Velocity.Length < 1.0f )
			{
				Velocity = Vector3.Zero;
				return;
			}

			// first try just moving to the destination
			var dest = ( Position + Velocity * Time.Delta ).WithZ( Position.z );

			var pm = TraceBBox( Position, dest );

			if ( pm.Fraction == 1 )
			{
				Position = pm.EndPosition;
				StayOnGround();
				return;
			}

			StepMove();
		}
		finally
		{

			// Now pull the base velocity back out.   Base velocity is set if you are on a moving object, like a conveyor (or maybe another monster?)
			Velocity -= BaseVelocity;
		}

		StayOnGround();
	}

	public virtual void StepMove()
	{
		MoveHelper mover = new MoveHelper( Position, Velocity );
		mover.Trace = mover.Trace.Size( mins, maxs ).WithoutTags( "nocollide" ).Ignore( Pawn );
		mover.MaxStandableAngle = GroundAngle;

		mover.TryMoveWithStep( Time.Delta, StepSize );

		Position = mover.Position;
		Velocity = mover.Velocity;
	}

	public virtual void Move()
	{
		MoveHelper mover = new MoveHelper( Position, Velocity );
		mover.Trace = mover.Trace.Size( mins, maxs ).WithoutTags( "nocollide" ).Ignore( Pawn );
		mover.MaxStandableAngle = GroundAngle;

		mover.TryMove( Time.Delta );

		Position = mover.Position;
		Velocity = mover.Velocity;
	}

	/// <summary>
	/// Add our wish direction and speed onto our velocity
	/// </summary>
	public virtual void Accelerate( Vector3 wishdir, float wishspeed, float speedLimit, float acceleration )
	{
		// This gets overridden because some games (CSPort) want to allow dead (observer) players
		// to be able to move around.
		// if ( !CanAccelerate() )
		//     return;

		if ( speedLimit > 0 && wishspeed > speedLimit )
			wishspeed = speedLimit;

		// See if we are changing direction a bit
		var currentspeed = Velocity.Dot( wishdir );

		// Reduce wishspeed by the amount of veer.
		var addspeed = wishspeed - currentspeed;

		// If not going to add any speed, done.
		if ( addspeed <= 0 )
			return;

		// Determine amount of acceleration.
		var accelspeed = acceleration * Time.Delta * wishspeed * SurfaceFriction;

		// Cap at addspeed
		if ( accelspeed > addspeed )
			accelspeed = addspeed;

		Velocity += wishdir * accelspeed;
	}

	/// <summary>
	/// Remove ground friction from velocity
	/// </summary>
	public virtual void ApplyFriction( float frictionAmount = 1.0f )
	{
		// Calculate speed
		var speed = Velocity.Length;
		if ( speed < 0.1f ) return;

		// Bleed off some speed, but if we have less than the bleed
		//  threshold, bleed the threshold amount.
		float control = ( speed < StopSpeed ) ? StopSpeed : speed;

		// Add the amount to the drop amount.
		var drop = control * Time.Delta * frictionAmount;

		// scale the velocity
		float newspeed = speed - drop;
		if ( newspeed < 0 ) newspeed = 0;

		if ( newspeed != speed )
		{
			newspeed /= speed;
			Velocity *= newspeed;
		}
	}

	public virtual void CheckJumpButton()
	{
		// If we are in the water most of the way...
		if ( IsSwimming )
		{
			// swimming, not jumping
			ClearGroundEntity();

			Velocity = Velocity.WithZ( 100 );

			return;
		}

		if ( GroundEntity == null )
			return;

		if ( DuckSlide.IsActiveSlide )
			DuckSlide.CancelSlide();

		float jumpDecayMul = 1.0f;
		if ( timeSinceLastJump < JumpDecayTime && !IsSwimming )
			jumpDecayMul = timeSinceLastJump / JumpDecayTime;

		jumpDecayMul.Clamp( 0.0f, 1.0f );

		ClearGroundEntity();

		float flGroundFactor = 1.0f;
		float flMul = 268.3281572999747f * 1.2f * jumpDecayMul;

		float startz = Velocity.z;

		if ( DuckSlide.IsActive )
			flMul *= 0.8f;

		Velocity = Velocity.WithZ( startz + flMul * flGroundFactor );

		Velocity -= new Vector3( 0, 0, Gravity * 0.5f ) * Time.Delta;

		AddEvent( "jump" );

		timeSinceLastJump = 0;
	}

	public virtual void AirMove()
	{
		var wishdir = WishVelocity.Normal;
		var wishspeed = WishVelocity.Length;

		Accelerate( wishdir, wishspeed, AirControl, AirAcceleration );

		Velocity += BaseVelocity;

		Move();

		Velocity -= BaseVelocity;
	}

	public virtual void WaterMove()
	{
		var wishdir = WishVelocity.Normal;
		var wishspeed = WishVelocity.Length;

		wishspeed *= 0.8f;

		Accelerate( wishdir, wishspeed, 100, Acceleration );

		Velocity += BaseVelocity;

		Move();

		Velocity -= BaseVelocity;
	}

	bool IsTouchingLadder = false;
	Vector3 LadderNormal;

	public virtual void CheckLadder()
	{
		var wishvel = new Vector3( Input.Forward, Input.Left, 0 );
		wishvel *= Input.Rotation.Angles().WithPitch( 0 ).ToRotation();
		wishvel = wishvel.Normal;

		if ( IsTouchingLadder )
		{
			if ( Input.Pressed( InputButton.Jump ) )
			{
				Velocity = LadderNormal * 100.0f;
				IsTouchingLadder = false;

				return;

			}
			else if ( GroundEntity != null && LadderNormal.Dot( wishvel ) > 0 )
			{
				IsTouchingLadder = false;

				return;
			}
		}

		const float ladderDistance = 1.0f;
		var start = Position;
		Vector3 end = start + ( IsTouchingLadder ? ( LadderNormal * -1.0f ) : wishvel ) * ladderDistance;

		var pm = Trace.Ray( start, end )
					.Size( mins, maxs )
					.WithTag("solid")
					.Ignore( Pawn )
					.WithoutTags( "nocollide" )
					.Run();

		IsTouchingLadder = false;

		if ( pm.Hit && !( pm.Entity is ModelEntity me ) )
		{
			IsTouchingLadder = true;
			LadderNormal = pm.Normal;
		}
	}

	public virtual void LadderMove()
	{
		var velocity = WishVelocity;
		float normalDot = velocity.Dot( LadderNormal );
		var cross = LadderNormal * normalDot;
		Velocity = velocity - cross + ( -normalDot * LadderNormal.Cross( Vector3.Up.Cross( LadderNormal ).Normal ) );

		Move();
	}

	public virtual void CategorizePosition( bool bStayOnGround )
	{
		SurfaceFriction = 1.0f;

		// Doing this before we move may introduce a potential latency in water detection, but
		// doing it after can get us stuck on the bottom in water if the amount we move up
		// is less than the 1 pixel 'threshold' we're about to snap to.	Also, we'll call
		// this several times per frame, so we really need to avoid sticking to the bottom of
		// water on each call, and the converse case will correct itself if called twice.
		//CheckWater();

		var point = Position - Vector3.Up * 2;
		var vBumpOrigin = Position;

		//
		//  Shooting up really fast.  Definitely not on ground trimed until ladder shit
		//
		bool bMovingUpRapidly = Velocity.z > MaxNonJumpVelocity;
		bool bMovingUp = Velocity.z > 0;

		bool bMoveToEndPos = false;

		if ( GroundEntity != null ) // and not underwater
		{
			bMoveToEndPos = true;
			point.z -= StepSize;
		}
		else if ( bStayOnGround )
		{
			bMoveToEndPos = true;
			point.z -= StepSize;
		}

		if ( bMovingUpRapidly || IsSwimming ) // or ladder and moving up
		{
			ClearGroundEntity();
			return;
		}

		var pm = TraceBBox( vBumpOrigin, point, 4.0f );

		if ( pm.Entity == null || Vector3.GetAngle( Vector3.Up, pm.Normal ) > GroundAngle )
		{
			ClearGroundEntity();
			bMoveToEndPos = false;

			if ( Velocity.z > 0 )
				SurfaceFriction = 0.25f;
		}
		else
		{
			UpdateGroundEntity( pm );
		}

		if ( bMoveToEndPos && !pm.StartedSolid && pm.Fraction > 0.0f && pm.Fraction < 1.0f )
		{
			Position = pm.EndPosition;
		}

	}

	/// <summary>
	/// We have a new ground entity
	/// </summary>
	public virtual void UpdateGroundEntity( TraceResult tr )
	{
		GroundNormal = tr.Normal;

		// VALVE HACKHACK: Scale this to fudge the relationship between vphysics friction values and player friction values.
		// A value of 0.8f feels pretty normal for vphysics, whereas 1.0f is normal for players.
		// This scaling trivially makes them equivalent.  REVISIT if this affects low friction surfaces too much.
		SurfaceFriction = tr.Surface.Friction * 1.25f;
		if ( SurfaceFriction > 1 ) SurfaceFriction = 1;

		//if ( tr.Entity == GroundEntity ) return;

		Vector3 oldGroundVelocity = default;
		if ( GroundEntity != null ) oldGroundVelocity = GroundEntity.Velocity;

		bool wasOffGround = GroundEntity == null;

		GroundEntity = tr.Entity;

		if ( GroundEntity != null )
		{
			BaseVelocity = GroundEntity.Velocity;
		}
	}

	/// <summary>
	/// We're no longer on the ground, remove it
	/// </summary>
	public virtual void ClearGroundEntity()
	{
		if ( GroundEntity == null ) return;

		GroundEntity = null;
		GroundNormal = Vector3.Up;
		SurfaceFriction = 1.0f;
	}

	public override TraceResult TraceBBox( Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, float liftFeet = 0.0f )
	{
		if ( liftFeet > 0 )
		{
			start += Vector3.Up * liftFeet;
			maxs = maxs.WithZ( maxs.z - liftFeet );
		}

		var tr = Trace.Ray( start + TraceOffset, end + TraceOffset )
					.Size( mins, maxs )
					.WithAnyTags( "solid", "player" )
					.Ignore( Pawn )
					.WithoutTags( "nocollide" )
					.Run();

		tr.EndPosition -= TraceOffset;
		return tr;
	}

	/// <summary>
	/// Traces the current bbox and returns the result.
	/// liftFeet will move the start position up by this amount, while keeping the top of the bbox at the same
	/// position. This is good when tracing down because you won't be tracing through the ceiling above.
	/// </summary>
	public override TraceResult TraceBBox( Vector3 start, Vector3 end, float liftFeet = 0.0f )
	{
		return TraceBBox( start, end, mins, maxs, liftFeet );
	}

	/// <summary>
	/// Try to keep a walking player on the ground when running down slopes etc
	/// </summary>
	public virtual void StayOnGround()
	{
		var start = Position + Vector3.Up * 2;
		var end = Position + Vector3.Down * StepSize;

		// See how far up we can go without getting stuck
		var trace = TraceBBox( Position, start );
		start = trace.EndPosition;

		// Now trace down from a known safe position
		trace = TraceBBox( start, end );

		if ( trace.Fraction <= 0 ) return;
		if ( trace.Fraction >= 1 ) return;
		if ( trace.StartedSolid ) return;
		if ( Vector3.GetAngle( Vector3.Up, trace.Normal ) > GroundAngle ) return;

		Position = trace.EndPosition;
	}
}
