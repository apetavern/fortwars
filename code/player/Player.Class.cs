﻿// Copyright (c) 2022 Ape Tavern, do not share, re-distribute or modify
// without permission of its author (insert_email_here)

using Sandbox;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Fortwars.RadialWheel;

namespace Fortwars;

partial class FortwarsPlayer
{
	public Class Class { get; set; } = new AssaultClass();

	[Net] public string SelectedClass { get; set; } = "fwclass_assault";
	[Net] public string SelectedPrimary { get; set; } = "fw:data/weapons/ksr1.fwweapon";
	[Net] public string SelectedSecondary { get; set; } = "fw:data/weapons/trj.fwweapon";

	public bool CanChangeClass => InSpawnRoom || LifeState != LifeState.Alive;
	[Net, Predicted] public bool InSpawnRoom { get; set; }

	public async Task GiveLoadout( List<string> items, Inventory inventory )
	{
		for ( int i = 0; i < items.Count; i++ )
		{
			string itemPath = items[i];
			Log.Trace( itemPath );
			inventory.Add( ItemUtils.GetItem( itemPath ), i == 0 );
			await Task.Delay( 100 ); //Gotta wait between each weapon added so OnChildAdded gets fired in the correct order...
		}
	}

	public void AssignLoadout( string newClassName, string newPrimaryName, string newSecondaryName )
	{
		if ( !CanChangeClass )
		{
			MessageFeed.AddMessage(
				To.Single( Client ),
				"clear",
				"Go to spawn to change loadout." );

			return;
		}

		bool wasLoadoutChanged = false;

		// TODO: Should probably check here to validate desired loadout
		if ( SelectedClass != newClassName )
		{
			wasLoadoutChanged = true;
			SelectedClass = newClassName;
		}

		if ( SelectedPrimary != newPrimaryName )
		{
			wasLoadoutChanged = true;
			SelectedPrimary = newPrimaryName;
		}

		if ( SelectedSecondary != newSecondaryName )
		{
			wasLoadoutChanged = true;
			SelectedSecondary = newSecondaryName;
		}

		if ( !wasLoadoutChanged )
			return;

		Class?.Cleanup( Inventory as Inventory );
		Class = TypeLibrary.Create<Class>( SelectedClass );

		Reset();
		Game.Instance.Round.SetupInventory( this );
	}

	public override void StartTouch( Entity other )
	{
		base.StartTouch( other );

		if ( other is FuncSpawnArea )
			InSpawnRoom = true;
	}

	public override void EndTouch( Entity other )
	{
		base.EndTouch( other );

		if ( other is FuncSpawnArea )
			InSpawnRoom = false;
	}
}
