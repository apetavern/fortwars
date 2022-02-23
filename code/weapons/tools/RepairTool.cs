﻿using Sandbox;
using System.Collections.Generic;

namespace Fortwars
{
	[Library( "repairtool", Title = "Repair Tool" )]
	public partial class RepairTool : Carriable
	{
		public virtual float PrimaryRate => 2.0f;

		public override string ViewModelPath => "models/weapons/amhammer/amhammer_v.vmdl";

		public override void Spawn()
		{
			base.Spawn();

			CollisionGroup = CollisionGroup.Weapon; // so players touch it as a trigger but not as a solid
			SetInteractsAs( CollisionLayer.Debris ); // so player movement doesn't walk into it

			SetModel( "models/weapons/amhammer/amhammer_w.vmdl" );
		}

		[Net, Predicted]
		public TimeSince TimeSincePrimaryAttack { get; set; }

		public override void Simulate( Client player )
		{
			if ( !Owner.IsValid() )
				return;

			if ( CanPrimaryAttack() )
			{
				using ( LagCompensation() )
				{
					TimeSincePrimaryAttack = 0;
					AttackPrimary();
				}
			}
		}

		public virtual bool CanPrimaryAttack()
		{
			if ( !Owner.IsValid() || !Input.Down( InputButton.Attack1 ) ) return false;

			var rate = PrimaryRate;
			if ( rate <= 0 ) return true;

			return TimeSincePrimaryAttack > (1 / rate);
		}

		public virtual void AttackPrimary()
		{
			var player = Owner as FortwarsPlayer;
			player.SetAnimParameter( "b_attack", true );
			foreach ( var tr in TraceHit( Owner.EyePosition, Owner.EyePosition + Owner.EyeRotation.Forward * 128f ) )
			{
				ViewModelEntity?.SetAnimParameter( "hit", tr.Hit );

				if ( !tr.Hit )
				{
					if ( IsLocalPawn )
					{
						MissEffects();
					}
					continue;
				}

				if ( IsLocalPawn )
				{
					HitEffects();
				}

				if ( tr.Entity is FortwarsBlock block && block.TeamID == player.TeamID && block.IsValid )
				{
					block?.Heal( 50, tr.EndPosition );
					continue;
				}

				tr.Entity.TakeDamage( DamageInfo.FromBullet( tr.EndPosition, -tr.Normal * 10f, 10 ) );
			}

			ViewModelEntity?.SetAnimParameter( "fire", true );
		}

		[ClientRpc]
		private void MissEffects()
		{
			_ = new Sandbox.ScreenShake.Perlin( 1.0f, 0.1f, 4.0f, 1.0f );
		}

		[ClientRpc]
		private void HitEffects()
		{
			_ = new Sandbox.ScreenShake.Perlin( 0.25f, 4.0f, 4.0f, 0.5f );
		}

		public virtual IEnumerable<TraceResult> TraceHit( Vector3 start, Vector3 end, float radius = 2.0f )
		{
			bool InWater = Map.Physics.IsPointWater( start );

			var tr = Trace.Ray( start, end )
					.UseHitboxes()
					.HitLayer( CollisionLayer.Water, !InWater )
					.HitLayer( CollisionLayer.Debris )
					.Ignore( Owner )
					.Ignore( this )
					.Size( radius )
					.Run();

			yield return tr;
		}

		public override void SimulateAnimator( PawnAnimator anim )
		{
			anim.SetAnimParameter( "holdtype", 4 );
			anim.SetAnimParameter( "aimat_weight", 1.0f );
			anim.SetAnimParameter( "holdtype_handedness", 1 );
			anim.SetAnimParameter( "holdtype_pose_hand", 0.07f );
			anim.SetAnimParameter( "holdtype_attack", 1 );
		}
	}
}
