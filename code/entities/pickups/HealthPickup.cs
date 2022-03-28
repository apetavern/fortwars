﻿// Copyright (c) 2022 Ape Tavern, do not share, re-distribute or modify
// without permission of its author support@apetavern.com

using Sandbox;

namespace Fortwars
{
    public class HealthPickup : Pickup
    {
        public override void Spawn()
        {
            base.Spawn();

            SetModel( "models/sbox_props/burger_box/burger_box.vmdl" );
            Scale = 3.0f;
        }

        public override void StartTouch( Entity other )
        {
            base.StartTouch( other );

            if ( !IsServer )
                return;

            if ( other is not FortwarsPlayer player )
                return;

            if ( player.Health >= 100 )
                return;

            player.Health += 50f;
            player.Health = player.Health.Clamp( 0, 100 );

            this.Delete();
        }
    }
}
