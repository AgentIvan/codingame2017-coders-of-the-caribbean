﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game.Entities;
using Game.Geometry;

namespace Experiments
{
	class Program
	{
		private static void Main(string[] args)
		{
			var state = @"
2
6
1 SHIP 4 14 5 0 57 1
3 SHIP 5 14 5 2 28 1
0 SHIP 12 10 5 2 36 0
2 SHIP 19 9 2 2 45 0
5 MINE 5 16 0 0 0 0
7 MINE 1 16 0 0 0 0
".Trim();
			/*
			//===
			strategies[1] = new WalkAroundStrategy(2, true);
			strategies[3] = new WalkAroundStrategy(2, true);
			//===


			Iteration(int.MaxValue, new StringReader(state));*/
		}
		/*
		private static void Main2(string[] args)
		{
			var ship = new Ship(1, new Coord(6, 15), owner: 1, rum: 100, orientation: 0, speed: 2);
			enemyShips = new List<Ship>
			{
				new Ship(666, new Coord(6, 20), owner: 0, rum: 100, orientation: 0, speed: 2)
			};
			myShips = new List<Ship> { ship };
			var fireTarget = SelectFireTarget(ship);
			Console.Out.WriteLine(fireTarget);
		}

		private static void Main3(string[] args)
		{
			var ship = new Ship(1, new Coord(6, 19), owner: 1, rum: 100, orientation: 0, speed: 0);
			shipsFired.Add(ship.id, true);
			mines = new List<Mine>();
			cannonballs = new List<Cannonball>();
			myShips = new List<Ship>
			{
				ship,
				new Ship(2, new Coord(8, 19), owner: 1, rum: 100, orientation: 4, speed: 0)
			};
			enemyShips = new List<Ship>();
			Preprocess();
			//ManualMove(ship, new Coord(2, 2));
		}*/
	}
}