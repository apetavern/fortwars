﻿// Copyright (c) 2022 Ape Tavern, do not share, re-distribute or modify
// without permission of its author support@apetavern.com

using Sandbox;

namespace Fortwars;

    public class Pickup : AnimEntity
    {
        public Spawner Spawner { get; set; }

        public override void Spawn()
        {
            base.Spawn();

            float bboxSize = 4;
            SetupPhysicsFromAABB( PhysicsMotionType.Static, new Vector3( -bboxSize ), new Vector3( bboxSize ) );

            CollisionGroup = CollisionGroup.Trigger;
            EnableSolidCollisions = false;
            EnableTouch = true;

            Components.Add<BobbingComponent>( new() );
        }

        protected override void OnDestroy()
        {
            if ( IsServer && Spawner.IsValid() )
                Spawner.ResetSpawnTimer();

            base.OnDestroy();
        }
    }
