﻿// Copyright (c) 2022 Ape Tavern, do not share, re-distribute or modify
// without permission of its author (insert_email_here)

using System.Collections.Generic;

namespace Fortwars;

partial class Game
{
	private struct BuildLogEntry
	{
		public BuildLogEntry( int tick, FortwarsBlock block, FortwarsPlayer player )
		{
			Tick = tick;
			Block = block;
			Player = player;
		}

		public int Tick { get; set; }
		public FortwarsBlock Block { get; set; }
		public FortwarsPlayer Player { get; set; }
	}

	private List<BuildLogEntry> buildLogEntries = new List<BuildLogEntry>();
}
