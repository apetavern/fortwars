﻿using static Sandbox.Event;

namespace Fortwars;

public partial class CaptureTheFlag : Gamemode
{
	public override string GamemodeName => "Capture the Flag";

	private const string BogRollPath = "data/weapons/ctf/bogroll.fwweapon";

	/// <summary>
	/// Define the Game State's possible in Capture the Flag mode.
	/// </summary>
	public enum GameState
	{
		Lobby,
		Countdown,
		Build,
		Combat,
		GameOver
	}

	public override string GetGameStateLabel()
	{
		return CurrentState switch
		{
			GameState.Lobby => "Waiting for players",
			GameState.Countdown => "Game starting soon",
			GameState.Build => "Build Phase",
			GameState.Combat => "Combat Phase",
			GameState.GameOver => "Game Over!",
			_ => null,
		};
	}

	public override float GetTimeRemaining()
	{
		return TimeUntilNextState;
	}

	/// <summary>
	/// How long the countdown lasts before the game starts.
	/// </summary>
	public static float CountdownDuration => 15f;

	/// <summary>
	/// How long the build phase lasts.
	/// </summary>
	public static float BuildPhaseDuration => 90f;

	/// <summary>
	/// How long the combat phase lasts.
	/// </summary>
	public static float CombatPhaseDuration => 180f;

	/// <summary>
	/// How long the game over phase lasts before the game is restarted.
	/// </summary>
	public static float GameOverDuration => 10f;

	public override IEnumerable<Team> Teams
	{
		get
		{
			yield return Team.Red;
			yield return Team.Blue;
		}
	}

	[Net]
	public IDictionary<Team, int> Scores { get; set; }

	/// <summary>
	/// The Red Flag Carrier is a member of Team Blue.
	/// </summary>
	[Net]
	public Player RedFlagCarrier { get; set; }

	/// <summary>
	/// The Blue Flag Carrier is a member of Team Red.
	/// </summary>
	[Net]
	public Player BlueFlagCarrier { get; set; }

	[Net]
	public BogRoll RedFlag { get; set; }

	[Net]
	public BogRoll BlueFlag { get; set; }

	[Net]
	public GameState CurrentState { get; set; }

	[Net]
	public TimeUntil TimeUntilNextState { get; set; }

	public override bool AllowDamage => CurrentState == GameState.Combat;

	internal override void Initialize()
	{
		_ = GameLoop();

		Scores = null;
		foreach ( var team in Teams )
			Scores.Add( team, 0 );
	}

	protected async Task GameLoop()
	{
		CurrentState = GameState.Lobby;
		await WaitForPlayers();

		CurrentState = GameState.Countdown;
		await WaitAsync( CountdownDuration );

		CurrentState = GameState.Build;
		await WaitAsync( BuildPhaseDuration );

		CurrentState = GameState.Combat;
		await WaitAsync( CombatPhaseDuration );

		CurrentState = GameState.GameOver;
		await WaitAsync( GameOverDuration );
	}

	private async Task WaitForPlayers()
	{
		while ( PlayerCount < MinimumPlayers )
		{
			await Task.DelayRealtimeSeconds( 1f );
		}
	}

	private async Task WaitAsync( float time )
	{
		TimeUntilNextState = time;
		await Task.DelayRealtimeSeconds( time );
	}

	internal override void OnClientJoined( IClient client )
	{
		base.OnClientJoined( client );

		// Assign the client a team.
		var teamComponent = client.Components.GetOrCreate<TeamComponent>();
		if ( teamComponent != null )
		{
			teamComponent.Team = TeamSystem.GetTeamWithFewestPlayers();
		}

		Log.Info( $"Fortwars: Assigned {client} to team {teamComponent.Team}" );

		MoveToSpawnpoint( client );
	}

	/// <summary>
	/// PrepareLoadout for the CTF gamemode will load in the player's primary and secondary,
	/// plus the equipment from their chosen class.
	/// </summary>
	/// <param name="player">The player whose loadout we are preparing.</param>
	/// <param name="inventory">The inventory to add the items to.</param>
	internal override void PrepareLoadout( Player player, Inventory inventory )
	{
		inventory.AddWeapon(
			WeaponAsset.CreateInstance( WeaponAsset.FromPath( player.SelectedPrimaryWeapon ) ), true );
		inventory.AddWeapon(
			WeaponAsset.CreateInstance( WeaponAsset.FromPath( player.SelectedSecondaryWeapon ) ), false );

		if ( player.Class.Equipment == null )
			return;

		inventory.AddWeapon(
			WeaponAsset.CreateInstance( player.Class.Equipment ), false );
	}

	internal override void OnWeaponDropped( Player player, Weapon weapon )
	{
		if ( Game.IsClient )
			return;

		if ( weapon.WeaponAsset.Name != "Bog Roll" )
			return;

		var team = player.Client.Components.Get<TeamComponent>().Team;
		var dropPos = player.EyePosition + ( player.EyeRotation.Forward * 50f );

		if ( team == Team.Red )
		{
			BlueFlag = new BogRoll()
			{
				Position = dropPos,
				Team = Team.Blue,
			};
			BlueFlagCarrier = null;
		}
		else
		{
			RedFlag = new BogRoll()
			{
				Position = dropPos,
				Team = Team.Red,
			};
			RedFlagCarrier = null;
		}

	}

	internal override void MoveToSpawnpoint( IClient client )
	{
		var clientTeam = client.Components.Get<TeamComponent>().Team;
		var spawnpoints = All.OfType<InfoPlayerTeamspawn>().Where( x => x.Team == clientTeam );
		var randomSpawn = spawnpoints.OrderBy( x => Guid.NewGuid() ).FirstOrDefault();

		if ( randomSpawn == null )
		{
			Log.Warning( "Couldn't find spawnpoint!" );
			return;
		}

		client.Pawn.Position = randomSpawn.Position;
		client.Pawn.Rotation = randomSpawn.Rotation;
	}

	public void OnTouchFlagzone( Player player, Team team )
	{
		var playerTeam = player.Client.Components.Get<TeamComponent>().Team;
		Log.Info( $"Fortwars CTF: {player.Client.Name} with team {playerTeam} touched flagzone {team}" );

		// Player is touching their own flagzone.
		if ( playerTeam == team )
		{
			if ( !player.HasFlag )
				return;

			// Ensure the player's active weapon is a Bog Roll.
			if ( player.ActiveWeapon.WeaponAsset.Name != "Bog Roll" )
				return;

			// Ensure the player's team flag is home.
			if ( !FlagIsHome( playerTeam ) )
				return;

			// Score for the player's team.
			ScoreFlag( player, playerTeam );
			return;
		}

		// Player is touching the enemy flagzone.
		if ( player.HasFlag )
			return;

		// If flag is not missing, pick it up.
		if ( !FlagIsHome( team ) )
			return;

		PickupFlag( player, playerTeam );

	}

	/// <summary>
	/// Whether the flag for given team is in the flagzone.
	/// </summary>
	/// <param name="team">The team whose flagzone is being touched.</param>
	/// <returns>True, if the flag is home. Otherwise, false.</returns>
	public bool FlagIsHome( Team team )
	{
		if ( team == Team.Red )
		{
			return RedFlagCarrier is null && RedFlag is null;
		}
		else if ( team == Team.Blue )
		{
			return BlueFlagCarrier is null && BlueFlag is null;
		}

		// How did we get here?
		return false;
	}

	private void PickupFlag( Player player, Team team )
	{
		// Assign the carrier based on their team.
		if ( team == Team.Red )
		{
			BlueFlagCarrier = player;
		}
		else if ( team == Team.Blue )
		{
			RedFlagCarrier = player;
		}

		Log.Info( $"Fortwars CTF: The flag for team {team} has been stolen by {player.Client.Name}." );

		// Add the flag to the player's inventory.
		player.Inventory.AddWeapon(
			WeaponAsset.CreateInstance( WeaponAsset.FromPath( BogRollPath ) ), true );
	}

	private void ScoreFlag( Player player, Team team )
	{
		if ( player == BlueFlagCarrier )
		{
			BlueFlagCarrier = null;
		}
		else if ( player == RedFlagCarrier )
		{
			RedFlagCarrier = null;
		}

		Scores[team] += 1;
		player.Inventory.RemoveActiveWeapon();
	}

	[Event.Tick]
	public void Tick()
	{
		if ( !Debug )
			return;

		int i = 0;
		foreach ( var team in Teams )
		{
			DebugOverlay.ScreenText( $"{team}: {Scores[team]}", i++ );
		}
		DebugOverlay.ScreenText( $"RedFlagCarrier: {RedFlagCarrier}", i++ );
		DebugOverlay.ScreenText( $"BlueFlagCarrier: {BlueFlagCarrier}", i++ );
		DebugOverlay.ScreenText( $"RedFlag: {RedFlag}", i++ );
		DebugOverlay.ScreenText( $"BlueFlag: {BlueFlag}", i++ );
	}

	[ConCmd.Admin( "fw_ctf_set_state" )]
	public static void SetGameState( string state )
	{
		if ( GamemodeSystem.Instance is not CaptureTheFlag ctf )
			return;

		if ( Enum.TryParse<GameState>( state, true, out GameState newState ) )
		{
			ctf.CurrentState = newState;
			ctf.TimeUntilNextState = 500f;
		}
	}

	[ConVar.Replicated( "fw_debug_ctf" )]
	public static bool Debug { get; set; } = false;
}
