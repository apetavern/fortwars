﻿// Copyright (c) 2022 Ape Tavern, do not share, re-distribute or modify
// without permission of its author (insert_email_here)

using Sandbox;
using System.Collections.Generic;

namespace Fortwars;

public partial class Deployable : Pickup
{
	[Net] public bool HasLanded { get; set; }

	private float ResupplyRadius => 96f;
	private float ThrowSpeed => 100f;
	private float TimeBetweenResupplies => 15f;
	private Particles radiusParticles;

	private Dictionary<long, TimeSince> playerResupplyPairs = new();

	public override void Spawn()
	{
		base.Spawn();

		Predictable = true;
	}

	[Event.Tick]
	public void OnTick()
	{
		if ( HasLanded )
		{
			SetLandedAppearance();
			SetRadiusParticleAppearance();

			ResupplyNearby();
		}
		else
		{
			Move();
		}
	}

	private void SetLandedAppearance()
	{
		SetAnimParameter( "deployed", true );
		Rotation = Rotation.Angles().WithPitch( 0 ).ToRotation();
	}

	private void SetRadiusParticleAppearance()
	{
		if ( IsServer )
			radiusParticles ??= Particles.Create( "particles/deployable/deployable.vpcf", this );

		radiusParticles?.SetPosition( 0, Position );
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		radiusParticles?.Destroy( true );
	}

	private void ResupplyNearby()
	{

		foreach ( var entity in Entity.FindInSphere( Position, ResupplyRadius ) )
		{
			if ( entity is not FortwarsPlayer player )
				continue;

			if ( !playerResupplyPairs.ContainsKey( player.Client.PlayerId ) 
				|| playerResupplyPairs[player.Client.PlayerId] > TimeBetweenResupplies )
			{
				Resupply( player );
				playerResupplyPairs[player.Client.PlayerId] = 0;
			}
		}
	}

	private void Move()
	{
		if ( !IsAuthority )
			return;
		
		var moveHelper = new MoveHelper( Position, Velocity );
		moveHelper.Trace = moveHelper.Trace.WorldOnly().Radius( 4f );

		moveHelper.Velocity += ThrowSpeed * Rotation.Forward * Time.Delta;
		moveHelper.Velocity += Vector3.Down * 800f * Time.Delta;

		bool grounded = moveHelper.TraceDirection( Vector3.Down ).Hit;
		if ( grounded )
		{
			moveHelper.ApplyFriction( 8.0f, Time.Delta );
			HasLanded = true;
		}

		moveHelper.TryMove( Time.Delta );

		Position = moveHelper.Position;
		Velocity = moveHelper.Velocity;
	}

	public virtual void Resupply( FortwarsPlayer player )
	{
	}
}