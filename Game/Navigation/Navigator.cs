using System;
using System.Collections.Generic;
using System.Linq;
using Game.Entities;
using Game.Geometry;
using Game.State;

namespace Game.Navigation
{
	public class Navigator : ITeamMember
	{
		public readonly GameState gameState;
		public readonly int shipId;

		public Navigator(int shipId, GameState gameState)
		{
			this.shipId = shipId;
			this.gameState = gameState;
		}

		public void StartTurn(TurnState turnState)
		{
		}

		public void EndTurn(TurnState turnState)
		{
		}

		public List<PathItem> FindPath(TurnState turnState, int ftarget, NavigationMethod navigationMethod)
		{
			var ship = turnState.FindMyShip(shipId);

			//if (FastShipPosition.Collides(ship.fposition, ftarget))
			//	return new List<PathItem>();

			var queue = new Queue<ShipPathChainItem>();
			queue.Enqueue(ShipPathChainItem.Start(ship.fposition, ftarget));

			var used = new Dictionary<ShipMovementState, ShipPathChainItem>();

			while (queue.Any())
			{
				var current = queue.Dequeue();
				if (current.depth != Settings.NAVIGATION_PATH_DEPTH)
				{
					var turnForecast = gameState.forecaster.GetTurnForecast(current.depth);
					foreach (var moveCommand in ShipMoveCommands.all)
					{
						var newShipMovement = FastShipPosition.Move(current.fposition, moveCommand);
						var newMovedPos = FastShipPosition.GetMovedPosition(newShipMovement);
						var newPos = FastShipPosition.GetFinalPosition(newShipMovement);
						var newMovementState = new ShipMovementState(newPos, current.depth + 1);
						if (!used.ContainsKey(newMovementState))
						{
							var onMyShip = false;
							foreach (var otherShip in turnState.myShips)
							{
								if (otherShip == ship)
									continue;
								var otherPosition = turnForecast.myShipsSourcePositions[otherShip.index];
								uint myMovement;
								uint otherMovement;
								var collisionType = CollisionChecker.Move(current.fposition, moveCommand, otherPosition, turnForecast.myShipsMoveCommands[otherShip.index], out myMovement, out otherMovement);
								if ((collisionType & (CollisionType.MyMove | CollisionType.MyRotation)) != CollisionType.None)
								{
									newShipMovement = myMovement;
									newMovedPos = FastShipPosition.GetMovedPosition(newShipMovement);
									newPos = FastShipPosition.GetFinalPosition(newShipMovement);
									newMovementState = new ShipMovementState(newPos, current.depth + 1);
									onMyShip = used.ContainsKey(newMovementState);
									break;
								}
							}

							if (onMyShip)
								continue;

							var onEnemyShip = false;
							if (current.depth == 0)
							{
								foreach (var enemyShip in turnState.enemyShips)
								{
									var enemyPosition = enemyShip.fposition;
									foreach (var enemyMoveCommand in ShipMoveCommands.all)
									{
										uint myMovement;
										uint enemyMovement;
										var collisionType = CollisionChecker.Move(current.fposition, moveCommand, enemyPosition, enemyMoveCommand, out myMovement, out enemyMovement);
										if ((collisionType & (CollisionType.MyMove | CollisionType.MyRotation)) != CollisionType.None)
										{
											newShipMovement = myMovement;
											newMovedPos = FastShipPosition.GetMovedPosition(newShipMovement);
											newPos = FastShipPosition.GetFinalPosition(newShipMovement);
											newMovementState = new ShipMovementState(newPos, current.depth + 1);
											onEnemyShip = used.ContainsKey(newMovementState);
											break;
										}
									}
								}
							}
							else
							{
								var prevEnemyFinalPositions = gameState.forecaster.GetTurnForecast(Math.Min(current.depth - 1, Settings.NAVIGATOR_ENEMY_POSITION_DEPTH)).enemyShipsFinalPositions;
								foreach (var enemyPosition in prevEnemyFinalPositions)
								{
									uint myMovement;
									uint enemyMovement;
									var collisionType = CollisionChecker.Move(current.fposition, moveCommand, enemyPosition, ShipMoveCommand.Wait, out myMovement, out enemyMovement);
									if ((collisionType & (CollisionType.MyMove | CollisionType.MyRotation)) != CollisionType.None)
									{
										newShipMovement = myMovement;
										newMovedPos = FastShipPosition.GetMovedPosition(newShipMovement);
										newPos = FastShipPosition.GetFinalPosition(newShipMovement);
										newMovementState = new ShipMovementState(newPos, current.depth + 1);
										onEnemyShip = used.ContainsKey(newMovementState);
										break;
									}
								}

								//onEnemyShip = gameState.forecaster.GetTurnForecast(Math.Min(current.depth, Settings.NAVIGATOR_ENEMY_POSITION_DEPTH)).enemyShipsFinalPositions
								//	.Any(m => FastShipPosition.CollidesShip(newPos, m) || FastShipPosition.CollidesShip(newMovedPos, m));

							}

							if (onEnemyShip)
							{
								//used.Add(newMovementState, null);
								continue;
							}

							var damage = turnForecast.mineDamageCoordMap[FastShipPosition.Coord(newPos)]
										+ turnForecast.mineDamageCoordMap[FastShipPosition.Bow(newPos)]
										+ turnForecast.mineDamageCoordMap[FastShipPosition.Stern(newPos)]
										+ turnForecast.nearMineDamageCoordMap[FastShipPosition.Bow(newPos)]
										+ turnForecast.nearMineDamageCoordMap[FastShipPosition.Stern(newPos)];

							if (newMovedPos != newPos)
								damage += turnForecast.mineDamageCoordMap[FastShipPosition.Bow(newMovedPos)]
										+ turnForecast.mineDamageCoordMap[FastShipPosition.Stern(newMovedPos)]
										+ turnForecast.nearMineDamageCoordMap[FastShipPosition.Bow(newMovedPos)]
										+ turnForecast.nearMineDamageCoordMap[FastShipPosition.Stern(newMovedPos)];

							var cannonedBowOrStern = turnForecast.cannonballCoordsMap[FastShipPosition.Bow(newPos)] || turnForecast.cannonballCoordsMap[FastShipPosition.Stern(newPos)];
							if (cannonedBowOrStern)
								damage += Constants.LOW_DAMAGE;

							var cannonedCenter = turnForecast.cannonballCoordsMap[FastShipPosition.Coord(newPos)];
							if (cannonedCenter)
								damage += Constants.HIGH_DAMAGE;

							if (Settings.NEAR_ENEMYSHIP_VIRTUAL_DAMAGE > 0)
							{
								var nearEnemyShip = turnState.enemyShips.Any(m => FastShipPosition.DistanceTo(newPos, m.fcoord) < Settings.NEAR_ENEMY_SHIP_MIN_DIST);
								if (nearEnemyShip)
									damage += Settings.NEAR_ENEMYSHIP_VIRTUAL_DAMAGE;
							}

							var next = current.Next(newPos, moveCommand, ftarget, damage);
							queue.Enqueue(next);
							used.Add(newMovementState, next);
						}
					}
				}
			}

			ShipPathChainItem bestChainItem = null;
			foreach (var chainItem in used.Values.Where(v => v != null))
			{
				if (chainItem.prev != null)
				{
					if (bestChainItem == null)
						bestChainItem = chainItem;
					else
					{
						switch (navigationMethod)
						{
							case NavigationMethod.Approach:
								bestChainItem = SelectBestPath_Approach(chainItem, bestChainItem, ship);
								break;
							case NavigationMethod.Collect:
								bestChainItem = SelectBestPath_Collect(chainItem, bestChainItem, ship);
								break;
							default:
								bestChainItem = SelectBestPath(chainItem, bestChainItem, ship);
								break;
						}
					}
				}
			}

			if (bestChainItem == null)
				return new List<PathItem>();

			var chainDump = new List<ShipPathChainItem>();
			var chain = new List<PathItem>();
			while (bestChainItem.prev != null)
			{
				chain.Add(new PathItem{command = bestChainItem.command , targetPosition = bestChainItem.fposition, sourcePosition = bestChainItem.prev.fposition});
				chainDump.Add(bestChainItem);
				bestChainItem = bestChainItem.prev;
			}
			chainDump.Reverse();
			if (Settings.DUMP_BEST_PATH)
			{
				Console.Error.WriteLine($"Best path for ship {shipId}");
				foreach (var item in chainDump)
				{
					Console.Error.WriteLine($"{item.command} - {FastShipPosition.ToShipPosition(item.fposition)} - dmg:{item.damage}");
				}
			}
			chain.Reverse();
			return chain;
		}

		private static ShipPathChainItem SelectBestPath(ShipPathChainItem chainItem, ShipPathChainItem bestChainItem, Ship ship)
		{
			if (chainItem.damage < bestChainItem.damage)
				return chainItem;
			if (chainItem.damage != bestChainItem.damage)
				return bestChainItem;
			if (chainItem.dist < bestChainItem.dist)
				return chainItem;
			if (chainItem.dist != bestChainItem.dist)
				return bestChainItem;
			if (chainItem.depth < bestChainItem.depth)
				return chainItem;
			if (chainItem.depth != bestChainItem.depth)
				return bestChainItem;

			if (ship.speed == 0)
			{
				if (chainItem.startCommand != ShipMoveCommand.Wait && bestChainItem.startCommand == ShipMoveCommand.Wait)
					return chainItem;
				return bestChainItem;
			}
			if (chainItem.startCommand == ShipMoveCommand.Wait)
				return chainItem;
			return bestChainItem;
		}

		private static ShipPathChainItem SelectBestPath_Approach(ShipPathChainItem chainItem, ShipPathChainItem bestChainItem, Ship ship)
		{
			if (chainItem.damage < bestChainItem.damage)
				return chainItem;
			if (chainItem.damage != bestChainItem.damage)
				return bestChainItem;

			var speed = FastShipPosition.Speed(chainItem.fposition);
			var bestSpeed = FastShipPosition.Speed(bestChainItem.fposition);
			if (bestChainItem.dist <= 2)
			{
				if (chainItem.depth < bestChainItem.depth)
					return chainItem;
				if (chainItem.depth != bestChainItem.depth)
					return bestChainItem;
				if (speed < bestSpeed)
					return chainItem;
				if (speed != bestSpeed)
					return bestChainItem;
				if (chainItem.dist < bestChainItem.dist)
					return chainItem;
				if (chainItem.dist != bestChainItem.dist)
					return bestChainItem;
			}
			else
			{
				if (chainItem.dist < bestChainItem.dist)
					return chainItem;
				if (chainItem.dist != bestChainItem.dist)
					return bestChainItem;
				if (chainItem.depth < bestChainItem.depth)
					return chainItem;
				if (chainItem.depth != bestChainItem.depth)
					return bestChainItem;
			}

			if (ship.speed == 0)
			{
				if (chainItem.startCommand != ShipMoveCommand.Wait && bestChainItem.startCommand == ShipMoveCommand.Wait)
					return chainItem;
				return bestChainItem;
			}
			if (chainItem.startCommand == ShipMoveCommand.Wait)
				return chainItem;
			return bestChainItem;
		}

		private static ShipPathChainItem SelectBestPath_Collect(ShipPathChainItem chainItem, ShipPathChainItem bestChainItem, Ship ship)
		{
			if (chainItem.damage < bestChainItem.damage)
				return chainItem;
			if (chainItem.damage != bestChainItem.damage)
				return bestChainItem;
			if (chainItem.dist < bestChainItem.dist)
				return chainItem;
			if (chainItem.dist != bestChainItem.dist)
				return bestChainItem;

			var speed = FastShipPosition.Speed(chainItem.fposition);
			var bestSpeed = FastShipPosition.Speed(bestChainItem.fposition);
			if (bestChainItem.dist == 0)
			{
				if (chainItem.depth < bestChainItem.depth - 1)
					return chainItem;
				if (bestChainItem.depth < chainItem.depth - 1)
					return bestChainItem;

				if (speed < bestSpeed)
					return chainItem;
				if (speed != bestSpeed)
					return bestChainItem;
			}

			if (chainItem.depth < bestChainItem.depth)
				return chainItem;
			if (chainItem.depth != bestChainItem.depth)
				return bestChainItem;
			if (ship.speed == 0)
			{
				if (chainItem.startCommand != ShipMoveCommand.Wait && bestChainItem.startCommand == ShipMoveCommand.Wait)
					return chainItem;
				return bestChainItem;
			}
			if (chainItem.startCommand == ShipMoveCommand.Wait)
				return chainItem;
			return bestChainItem;
		}

		public string Dump(string gameStateRef)
		{
			return $"new {nameof(Navigator)}({shipId}, {gameStateRef})";
		}

		private class ShipMovementState : IEquatable<ShipMovementState>
		{
			public readonly int depth;
			public readonly int fposition;

			public ShipMovementState(int fposition, int depth)
			{
				this.depth = depth;
				this.fposition = fposition;
			}

			public bool Equals(ShipMovementState other)
			{
				if (ReferenceEquals(null, other))
					return false;
				if (ReferenceEquals(this, other))
					return true;
				return fposition == other.fposition && depth == other.depth;
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj))
					return false;
				if (ReferenceEquals(this, obj))
					return true;
				if (obj.GetType() != this.GetType())
					return false;
				return Equals((ShipMovementState)obj);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return (fposition * 397) ^ depth;
				}
			}

			public static bool operator ==(ShipMovementState left, ShipMovementState right)
			{
				return Equals(left, right);
			}

			public static bool operator !=(ShipMovementState left, ShipMovementState right)
			{
				return !Equals(left, right);
			}

			public override string ToString()
			{
				return $"{nameof(depth)}: {depth}, {nameof(fposition)}: {FastShipPosition.ToShipPosition(fposition)}";
			}
		}

		private class ShipPathChainItem
		{
			public static ShipPathChainItem Start(int fposition, int ftarget)
			{
				return new ShipPathChainItem(null, ShipMoveCommand.Wait, fposition, 0, ShipMoveCommand.Wait, ftarget, 0);
			}

			public readonly ShipMoveCommand command;
			public readonly int depth;
			public readonly int dist;
			public readonly int fposition;
			public readonly int pathDamage;
			public readonly ShipPathChainItem prev;
			public readonly ShipMoveCommand startCommand;
			public int damage = int.MaxValue;

			private ShipPathChainItem(
				ShipPathChainItem prev,
				ShipMoveCommand command,
				int fposition,
				int depth,
				ShipMoveCommand startCommand,
				int ftarget,
				int pathDamage)
			{
				this.prev = prev;
				this.command = command;
				this.fposition = fposition;
				this.depth = depth;
				this.startCommand = startCommand;
				dist = FastShipPosition.DistanceTo(fposition, ftarget);
				this.pathDamage = pathDamage;
				if (depth == Settings.NAVIGATION_PATH_DEPTH)
					SetDamage(pathDamage);
			}

			public ShipPathChainItem Next(int nextPosition, ShipMoveCommand moveCommand, int ftarget, int nextDamage)
			{
				return new ShipPathChainItem(
					this,
					moveCommand,
					nextPosition,
					depth + 1,
					prev == null ? moveCommand : startCommand,
					ftarget,
					pathDamage + nextDamage);
			}

			public override string ToString()
			{
				return $"{(prev == null ? "ROOT: " : "")}{nameof(command)}: {command}, {nameof(depth)}: {depth}, {nameof(startCommand)}: {startCommand}, {nameof(fposition)}: {FastShipPosition.ToShipPosition(fposition)}, {nameof(damage)}: {damage}, {nameof(pathDamage)}: {pathDamage}, {nameof(dist)}: {dist}";
			}

			private void SetDamage(int newDamage)
			{
				var t = this;
				while (t != null && t.damage > newDamage)
				{
					t.damage = newDamage;
					t = t.prev;
				}
			}
		}
	}
}