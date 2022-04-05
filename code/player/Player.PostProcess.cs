﻿// Copyright (c) 2022 Ape Tavern, do not share, re-distribute or modify
// without permission of its author (insert_email_here)

using Sandbox;

namespace Fortwars;

partial class FortwarsPlayer
{
	private static StandardPostProcess postProcess;

	private static void SetupPostProcessing()
	{
		Host.AssertClient();

		PostProcess.Add<StandardPostProcess>( new() );
		postProcess = PostProcess.Get<StandardPostProcess>();
	}

	[Event.Frame]
	private static void UpdatePostProcessing()
	{
		Host.AssertClient();

		if ( postProcess == null )
		{
			SetupPostProcessing();
		}

		postProcess.FilmGrain.Enabled = true;
		postProcess.FilmGrain.Intensity = 0.15f;
		postProcess.FilmGrain.Response = 1f;

		postProcess.LensDistortion.Enabled = true;
		postProcess.LensDistortion.K1 = 0f;
		postProcess.LensDistortion.K2 = -0.005f;

		postProcess.ChromaticAberration.Enabled = true;
		postProcess.ChromaticAberration.Offset = Vector3.Up * 0.0005f;

		postProcess.Vignette.Enabled = true;

		float vignette;
		if ( Local.Pawn is FortwarsPlayer { ActiveChild: FortwarsWeapon { IsAiming: true } } )
			vignette = 1.0f;
		else
			vignette = 0.0f;

		postProcess.Vignette.Intensity = postProcess.Vignette.Intensity.LerpTo( vignette, 10 * Time.Delta );

		postProcess.Vignette.Roundness = 1.5f;
		postProcess.Vignette.Smoothness = 100f;
		postProcess.Vignette.Color = Color.Black;

		if ( Local.Pawn is FortwarsPlayer player )
		{
			var healthT = player.Health.LerpInverse( 75f, 0.0f );

			postProcess.Vignette.Color = Color.Lerp( postProcess.Vignette.Color, Color.Red, healthT );
			postProcess.Vignette.Intensity = healthT * 10f;
		}
	}
}
