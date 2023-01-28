﻿namespace Fortwars;

[Library( "info_flag_spawn" )]
[Title( "Flag Spawn" ), Category( "Fortwars" )]
[EditorModel( "models/rust_props/small_junk/toilet_paper.vmdl" )]
[HammerEntity]
public partial class InfoFlagSpawn : ModelEntity
{
	[Net, Property]
	public Team Team { get; set; }

	private static readonly Model FlagModel = Model.Load( "models/items/bogroll/bogroll_w.vmdl" );

	public override void Spawn()
	{
		base.Spawn();

		PhysicsEnabled = false;
		UsePhysicsCollision = false;
		EnableHideInFirstPerson = true;
		EnableShadowInFirstPerson = true;

		Model = FlagModel;

		BogRoll.SetMaterialGroup( Team, this );
	}

	[Event.Tick.Client]
	public void Tick()
	{
		if ( GamemodeSystem.Instance is not CaptureTheFlag ctf )
			return;

		EnableDrawing = ctf.FlagIsHome( Team );
	}

	const float pitchAngle = 45f;
	const float rotateSpeed = 90f;
	const float bobSpeed = 2f;
	const float bobPower = 5f;

	[Event.PreRender]
	protected void PreRender()
	{
		if ( !SceneObject.IsValid() )
			return;

		// Set the pitch to a 45 degree angle, and slowly rotate around axis.
		SceneObject.Rotation = Rotation.From( pitchAngle, Time.Now * rotateSpeed, 0 );

		// Bob with a sin wave.
		SceneObject.Position = Position + ( Vector3.Up * MathF.Sin( Time.Now * bobSpeed ) * bobPower );
	}
}
