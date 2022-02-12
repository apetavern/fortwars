﻿using Sandbox;

namespace Fortwars
{
	partial class Game
	{
		[AdminCmd( "recreatehud" )]
		public static void RecreateHud()
		{
			hud?.Delete();
			hud = new();
		}

		[AdminCmd( "fw_give_ammo" )]
		public static void GiveAmmo( int amount )
		{
			var owner = ConsoleSystem.Caller;
			var player = owner.Pawn;

			if ( player.ActiveChild is not FortwarsWeapon weapon )
				return;

			weapon.ReserveAmmo += amount;
		}

		[ServerCmd( "fw_spawn" )]
		public static void Spawn( string blockName )
		{
			var owner = ConsoleSystem.Caller;
			var player = owner.Pawn;

			if ( Instance.Round is not BuildRound )
				return;

			if ( ConsoleSystem.Caller == null )
				return;

			if ( !player.IsValid() || player.LifeState != LifeState.Alive )
				return;

			if ( blockName.Contains( "steel" ) && BlockMaterial.Steel.GetRemainingCount( owner ) <= 0 )
				return;

			if ( BlockMaterial.Wood.GetRemainingCount( owner ) <= 0 )
				return;

			var tr = Trace.Ray( player.EyePosition, player.EyePosition + player.EyeRotation.Forward * 500 )
				.UseHitboxes()
				.Ignore( player )
				.Size( 2 )
				.Run();

			var ent = new FortwarsBlock();
			ent.Position = tr.EndPos;
			ent.Rotation = Rotation.From( new Angles( 0, player.EyeRotation.Yaw(), 0 ) ) * Rotation.FromAxis( Vector3.Up, 180 );


			if ( blockName.Contains( "steel" ) )
			{
				ent.BlockMaterial = BlockMaterial.Steel;
				ent.SetModel( $"models/blocks/steel/fw_{blockName.Split('_')[1]}.vmdl" );
				ent.SetupPhysicsFromModel( PhysicsMotionType.Dynamic );
			}
			else
			{
				ent.SetModel( $"models/blocks/wood/fw_{blockName}.vmdl" );
				ent.SetupPhysicsFromModel( PhysicsMotionType.Dynamic );
			}

			ent.TeamID = (player as FortwarsPlayer).TeamID;
			ent.Owner = player;

			ent.OnTeamIDChanged();

			// Drop to floor
			if ( ent.PhysicsBody != null && ent.PhysicsGroup.BodyCount == 1 )
			{
				var p = ent.PhysicsBody.FindClosestPoint( tr.EndPos );

				var delta = p - tr.EndPos;
				ent.PhysicsBody.Position -= delta;
			}
		}
	}
}
