﻿namespace Fortwars;

public class PlayerControllerMechanic : EntityComponent<Player>
{
	protected PlayerController Controller => Entity.Controller;
	protected Player Player => Entity;
	protected static float Gravity => 800.0f;

	public bool IsActive { get; protected set; }

	public TimeSince TimeSinceStart { get; protected set; }
	public TimeSince TimeSinceStop { get; protected set; }
	public TimeUntil TimeUntilCanStart { get; protected set; }

	public virtual float EyeHeight { get; protected set; }
	public virtual float WishSpeed { get; protected set; }

	public virtual int SortOrder { get; set; } = 0;

	public Vector3 Position
	{
		get => Controller.Position;
		set => Controller.Position = value;
	}

	public Vector3 Velocity
	{
		get => Controller.Velocity;
		set => Controller.Velocity = value;
	}

	public Vector3 LastVelocity
	{
		get => Controller.LastVelocity;
		set => Controller.LastVelocity = value;
	}

	public Entity GroundEntity
	{
		get => Controller.GroundEntity;
		set => Controller.GroundEntity = value;
	}

	public Entity LastGroundEntity
	{
		get => Controller.LastGroundEntity;
		set => Controller.LastGroundEntity = value;
	}

	public PlayerControllerMechanic()
	{

	}

	public bool TrySimulate( PlayerController controller )
	{
		var wasActive = IsActive;
		IsActive = ShouldStart();

		if ( IsActive )
		{
			if ( wasActive != IsActive )
				Start();

			Simulate();
		}

		if ( wasActive && !IsActive )
			Stop();

		Tick();

		return IsActive;
	}

	protected void Start()
	{
		TimeSinceStart = 0f;
		OnStart();
	}

	protected void Stop()
	{
		TimeSinceStop = 0f;
		OnStop();
	}

	protected virtual void OnStart() { }

	protected virtual void OnStop() { }

	protected virtual bool ShouldStart()
	{
		return TimeUntilCanStart;
	}

	protected virtual void Simulate() { }

	protected virtual void Tick() { }
}
