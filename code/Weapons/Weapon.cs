﻿namespace Fortwars;

public partial class Weapon : AnimatedEntity
{
	public WeaponViewModel ViewModelEntity { get; protected set; }
	public Player Player => Owner as Player;

	public override void Spawn()
	{
		EnableHideInFirstPerson = true;
		EnableShadowInFirstPerson = true;
		EnableDrawing = false;
	}

	public void OnDeploy( Player player )
	{
		SetParent( player, true );
		Owner = player;

		EnableDrawing = true;

		if ( Game.IsServer )
		{
			CreateViewModel( To.Single( player ) );
		}
	}

	public void OnHolster( Player player )
	{
		EnableDrawing = false;
	}

	[ClientRpc]
	public void CreateViewModel()
	{
		var viewModel = new WeaponViewModel( this );

		// TODO: Set the model...

		ViewModelEntity = viewModel;
	}
}