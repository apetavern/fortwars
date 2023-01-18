﻿namespace Fortwars;

public class WeaponViewModel : AnimatedEntity
{
	public static WeaponViewModel Current { get; set; }

	protected Weapon Weapon { get; init; }

	public WeaponViewModel( Weapon weapon )
	{
		if ( Current.IsValid() )
		{
			Current.Delete();
		}

		Current = this;
		EnableShadowCasting = false;
		EnableViewmodelRendering = true;
		Weapon = weapon;
	}

	protected override void OnDestroy()
	{
		Current = null;
	}

	[Event.Client.PostCamera]
	public void PlaceViewModel()
	{
		Camera.Main.SetViewModelCamera( 80f, 1f, 500f );
	}
}