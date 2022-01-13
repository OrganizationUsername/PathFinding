using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PathFinding.Core;
using PathFinding.Persistence;
using PathFinding.Shared.Models;
using Point = System.Numerics.Vector2;

namespace PathFinding.Shared.ViewModels;

public enum ClickMode { Player = 0, Conveyor = 1, Item = 2 }

public class MainWindowViewModel : ObservableObject
{
    private readonly Random _rand = new(1);
    public bool Paused { get; set; } = false;
    public string HoveredEntityDescription { get => _hoveredEntityDescription; set => SetProperty(ref _hoveredEntityDescription, value); }
    public State State { get; set; }
    public List<Tile> EntitiesToHighlight { get; set; } = new();
    public List<Tile> AlreadyClicked { get; } = new();
    public Cell[,] AnswerCells { get; set; }
    private int _fps;
    private string _hoveredEntityDescription;
    private string _tileString;
    public bool AllowDiagonal { get => _allowDiagonal; set => SetProperty(ref _allowDiagonal, value); }
    public bool AlwaysPath { get; set; } = true;
    public int TileSize { get; set; }
    public int Top { get; set; }
    public int Left { get; set; }
    public bool ShowNumbers { get; set; } = false;
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public bool LeftButtonClick { get; set; }
    public string TileString { get => _tileString; set => SetProperty(ref _tileString, value); }
    public int TileWidth { get; set; }
    public int TileHeight { get; set; }
    public int CellsScored { get => _cellsScored; set => SetProperty(ref _cellsScored, value); }
    public string NewTileWidth { get; set; }
    public string NewTileHeight { get; set; }
    public int Fps { get => _fps; set => SetProperty(ref _fps, value); }
    public IStatePersistence Sp;
    public DateTime LastRequestedPathFind { get; set; }
    private int _cellsScored;
    private int _currentPlayer = 1;
    public int CurrentPlayer { get => _currentPlayer; set => SetProperty(ref _currentPlayer, value); }
    public Dictionary<int, (Tile Source, Tile Destination)> PlayerDictionary = new();
    public Dictionary<int, List<(int, int)>> SolutionDictionary = new();
    private bool _allowDiagonal = true;
    public int PlayerCount { get; set; }
    public Tile SelectedConveyorTile { get; set; }
    public List<Conveyor> Conveyors { get; set; } = new();
    public List<Item> Items { get; set; } = new();
    public int ItemsCount => Items.Count;
    private int _tickCounter;
    public readonly int MaxCellNumber = 2;
    public int MaxClickMode = Enum.GetValues(typeof(ClickMode)).Cast<int>().Max();
    private string _selectedStringMode = Enum.GetNames(typeof(ClickMode)).First();
    private ClickMode _clickMode = ClickMode.Player;
    public List<string> StringModes { get; set; } = Enum.GetNames(typeof(ClickMode)).ToList();
    public string SelectedStringMode { get => _selectedStringMode; set { SetProperty(ref _clickMode, (ClickMode)Enum.Parse(typeof(ClickMode), value)); SetProperty(ref _selectedStringMode, value); } }
    public ClickMode ClickMode { get => _clickMode; set { SetProperty(ref _clickMode, value); SelectedStringMode = _clickMode.ToString(); } }
    public RelayCommand ResetCommand { get; set; }
    public AsyncRelayCommand<bool> ChangeDiagonalCommand { get; }

    public MainWindowViewModel(IStatePersistence statePersistence)
    {
        Sp = statePersistence;

        PlayerCount = 3;

        for (var i = 1; i <= PlayerCount; i++) { PlayerDictionary.Add(i, (null, null)); }
        for (var i = 1; i <= PlayerCount; i++) { SolutionDictionary.Add(i, new()); }
        ConveyorTile.MaxCellNumber = MaxCellNumber;
        Item.MainWindowViewModel = this;
        Item.MaxCellNumber = MaxCellNumber;
        PixelWidth = 600;
        PixelHeight = 600;
        TileWidth = 10;
        TileHeight = 10;
        var tileSize = 10;
        TileSize = tileSize;
        State = new(TileWidth, TileHeight, tileSize, Sp);

        ResetCommand = new(Reset);
        ChangeDiagonalCommand = new(async (x) => { AllowDiagonal = x; await PlayerPathFinding(); });
        SetupMapString();
        UploadMapString(@"3972_G4MPKI6UrMG9OgaGZmcAmLpKbFnWit9TRq0AVogGrRYWkQKSyyR9MuH9AenBv5sSeR+axQ4OHRwOESlSJAS5Z9t9tbIp5bwH/db7lgtKvQ3fH/yDCwonLSau8Y5aty1SCrbmxv20+I7bysl9AkepUN0bb8SGsANKLOiJCCOVU9u/PdYBet5gBsp6XKAeo8WuNkoNM9i8x35DUGYmR2HshCll3M4bPrVsPmGmOrfbSlJsVi5AcEMLzgbP00rTOd1HKeRUSX4C8L9Z9J5BfOKtSR8zs44M8O4CnJ34LyKDi1+JtWLy3HcqERkmHi4KuYeEAVZOkn7jlh3Ids0oomZVmhr0uiun2U/QT+4nJNkSyHAzQT3YYiIzDiVYB7yJesuCYk2e3Df2H0LT9zIwdRrIDAGbymDcWdFEvewh74DIswbs9HJiTy4feNxBJYXpdsockTBZH82r/DHk/7rdEm5CteuHA4xBLV4z55I+3SfLdkRAOvlY8Pk2kj0A");
    }

    public async Task Tick(Point? point, bool leftClicked)
    {
        GetHoverElements(point);
        if (!Paused)
        {
            //TickConveyor();
            //RandomlyAddItem();
            _tickCounter++;
            if (_tickCounter >= 5)
            {
                Movement();
                _tickCounter = 0;
            }
        }
        if (leftClicked) { await HandleLeftClick(point); }
        else { AlreadyClicked.Clear(); }
    }

    public async Task HandleLeftClick(Point? point)
    {
        if (!point.HasValue) return;
        var tile = GetTileAtLocation(point);
        if (tile is null) return;
        switch (ClickMode)
        {
            case ClickMode.Player:
                await TryFlipElement(tile); return;
            case ClickMode.Conveyor:
                PlaceConveyor(tile);
                GetHoverElements(point);
                break;
            case ClickMode.Item:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void PlaceConveyor(Tile hoveredTile)
    {
        if (!hoveredTile.IsPassable || hoveredTile.ConveyorTile is not null) { return; }

        //Trace.WriteLine($"({hoveredTile.X},{hoveredTile.Y}) clicked.");

        //ToDo: Make it so `r` rotates this.
        //ToDo: I need to think about when conveyors should be joined and when they shouldn't be.
        (int X, int Y) direction = (0, 1); //This is backwards because I set up conveyors wrong with right-clicking.

        //Selected tile has no ConveyorTile. 
        var ct = new ConveyorTile() { Tile = hoveredTile, Direction = direction };
        ct.Setup();
        hoveredTile.TileRole = TileRole.Conveyor;

        Tile somethingElseTargetTile = null;
        if (hoveredTile.X - direction.X >= 0 && hoveredTile.X - direction.X < TileWidth && hoveredTile.Y - direction.Y >= 0 && hoveredTile.Y - direction.Y < TileHeight)
        {
            somethingElseTargetTile = State.TileGrid[hoveredTile.X - direction.X, hoveredTile.Y - direction.Y];
        }

        if (hoveredTile.InboundConveyorTiles.Any() && somethingElseTargetTile?.ConveyorTile is not null)
        {
            foreach (var x in hoveredTile.InboundConveyorTiles) { x.NextConveyorTile = ct; }

            var tailConveyorTile = hoveredTile.InboundConveyorTiles.First(); //indeterministic
            hoveredTile.ConveyorTile = ct;
            ct.Conveyor = tailConveyorTile.Conveyor;
            tailConveyorTile.Conveyor.ConveyorTiles.Add(ct);
            somethingElseTargetTile?.InboundConveyorTiles.Add(ct);
            var otherConveyor = somethingElseTargetTile.ConveyorTile.Conveyor;
            foreach (var tile in otherConveyor.ConveyorTiles)
            {
                tile.Conveyor = tailConveyorTile.Conveyor;
                ct.Conveyor.ConveyorTiles.Add(tile);
            }
            Conveyors.Remove(otherConveyor);
            Trace.WriteLine($"0. Added new ct ({ct.Tile.X},{ct.Tile.Y}) to existing conveyor since it lands on this one. Conveyor = {string.Join(", ", tailConveyorTile.Conveyor.ConveyorTiles.Select(ctx => $"({ctx.Tile.X},{ctx.Tile.Y})"))}.");
            return;
        }

        //If any conveyorTile would land on the highlighted one, then make add this conveyorTile to that conveyor.
        if (hoveredTile.InboundConveyorTiles.Any())
        {
            foreach (var x in hoveredTile.InboundConveyorTiles) { x.NextConveyorTile = ct; }
            var tailConveyorTile = hoveredTile.InboundConveyorTiles.First(); //indeterministic
            hoveredTile.ConveyorTile = ct;
            Trace.WriteLine($"({ct.Tile.X},{ct.Tile.Y}) conveyorTile set");
            ct.Conveyor = tailConveyorTile.Conveyor;
            ct.NextConveyorTile = somethingElseTargetTile?.ConveyorTile;
            Trace.WriteLine($"CT's ({ct.Location.X},{ct.Location.Y})'s next tile is: ({ct.NextConveyorTile?.Location.X},{ct.NextConveyorTile?.Location.Y})");
            tailConveyorTile.Conveyor.ConveyorTiles.Add(ct);
            somethingElseTargetTile?.InboundConveyorTiles.Add(ct);
            Trace.WriteLine($"1. Added new ct ({ct.Tile.X},{ct.Tile.Y}) to existing conveyor since it lands on this one. Conveyor = {string.Join(", ", tailConveyorTile.Conveyor.ConveyorTiles.Select(ctx => $"({ctx.Tile.X},{ctx.Tile.Y})"))}.");
            return;
        }

        //If the conveyor, would land on another tile
        if (somethingElseTargetTile?.ConveyorTile is not null)
        {
            //ToDo: I also need to cascade changes here.
            ct.Conveyor = somethingElseTargetTile.ConveyorTile.Conveyor;
            ct.NextConveyorTile = somethingElseTargetTile.ConveyorTile;
            ct.Conveyor.ConveyorTiles.Add(ct);
            hoveredTile.ConveyorTile = ct;
            Trace.WriteLine($"2. This ct ({ct.Tile.X},{ct.Tile.Y}) lands on another conveyorTile. Conveyor = {somethingElseTargetTile.ConveyorTile.Conveyor.GetTileText()}.");
            Trace.WriteLine($"({hoveredTile.X},{hoveredTile.Y}) conveyorTile set");
            somethingElseTargetTile?.InboundConveyorTiles.Add(ct);
            return;
        }

        var c = new Conveyor();
        c.ConveyorTiles.Add(ct);
        ct.Conveyor = c;
        hoveredTile.ConveyorTile = ct;
        Trace.WriteLine($"({ct.Tile.X},{ct.Tile.Y}) conveyorTile set");
        Conveyors.Add(c);
        ct.Tile.TileRole = TileRole.Conveyor;
        somethingElseTargetTile?.InboundConveyorTiles.Add(ct);
        Trace.WriteLine($"3. This ct ({ct.Tile.X},{ct.Tile.Y}) constitutes a new Conveyor! Conveyor = {string.Join(", ", ct.Conveyor.ConveyorTiles.Select(ctx => $"({ctx.Tile.X},{ctx.Tile.Y})"))}.");
    }

    public async void HandleRightClick(Point? point)
    {
        if (!point.HasValue) return;
        var tile = GetTileAtLocation(point);
        if (tile is null) return;
        switch (ClickMode)
        {
            case ClickMode.Player:
                await HandleRightClickPlayerMode(tile);
                break;
            case ClickMode.Conveyor:
                //await HandleRightClickAddConveyorNode(tile);
                break;
            case ClickMode.Item:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Reset()
    {
        if (int.TryParse(NewTileWidth, out var tileWidth) && tileWidth > 10 && int.TryParse(NewTileHeight, out var tileHeight) && tileHeight > 10)
        {
            Conveyors.Clear();
            Items.Clear();
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            TileSize = Math.Max(Math.Min(PixelWidth / tileWidth, PixelHeight / tileHeight), 3);
            State = new(TileWidth, TileHeight, TileSize, Sp);
            AnswerCells = null;
            PlayerDictionary.Clear();
            SolutionDictionary.Clear();
            for (var i = 1; i <= PlayerCount; i++) { PlayerDictionary.Add(i, (null, null)); SolutionDictionary.Add(i, new()); }
        }
    }

    public void UploadMapString(string mapString)
    {
        var mapStringResult = State.UploadMapString(mapString);
        if (!mapStringResult.Success) return;

        var lineNumber = 1;
        State = new(mapStringResult.X, mapStringResult.Y, State.TileSize, Sp);
        for (var x = 0; x < mapStringResult.X; x++)
        {
            lineNumber++;
            var row = mapStringResult.mapStringArray[lineNumber];
            for (var y = 0; y < mapStringResult.Y; y++) { State.TileGrid[x, y].IsPassable = row[y] == '0'; }
        }

        TileWidth = State.X;
        TileHeight = State.Y;
    }

    public void SetupMapString() => TileString = State.SetupMapString();

    public void GetHoverElements(Point? point)
    {
        if (!point.HasValue) return;
        var tile = GetTileAtLocation(point);
        if (tile is null) return;
        if (tile == EntitiesToHighlight.FirstOrDefault()) return;
        var conveyorsTiles = (tile.ConveyorTile is null ? "" : $"{Environment.NewLine}Conveyor: {string.Join(", ", tile.ConveyorTile.Conveyor.ConveyorTiles.Select(t => $"({t.Tile.X},{t.Tile.Y})"))}");
        var landingTiles = tile.InboundConveyorTiles.Any() ? $"{Environment.NewLine}Incoming: {string.Join(", ", tile.InboundConveyorTiles.Select(t => $"({t.Tile.X},{t.Tile.Y})"))}" : "";
        HoveredEntityDescription = $"{tile.X},{tile.Y}{conveyorsTiles}{landingTiles}";
        EntitiesToHighlight.Clear();
        EntitiesToHighlight.Add(tile);
    }

    public async Task<(List<Cell> SolutionCells, Cell[,] AllCells, long TimeToSolve, DateTime thisDate)> PathFinding(Tile destination, Tile source, bool allowDiagonal, DateTime requestDate, bool forceWalkable = false)
    {
        Cell destCell = new() { Id = destination.Id, HScore = 0, X = destination.X, Y = destination.Y, Passable = destination.IsPassable };
        Cell sourceCell = new() { Id = source.Id, HScore = Math.Abs(source.X - destination.X) + Math.Abs(source.Y - destination.Y), X = source.X, Y = source.Y, Passable = source.IsPassable };

        var cells = new Cell[State.X, State.Y];

        (destCell, sourceCell) = GetStateCells(destination, source, destCell, cells, sourceCell);
        if (forceWalkable) { destCell.Passable = true; sourceCell.Passable = true; }

        //Not really useful at the moment.
        //await Chunking(cells, requestDate);
        var solution = await Solver.SolveAsync(cells, sourceCell, destCell, null, requestDate, allowDiagonal);
        if (solution.SolutionCells is null || !solution.SolutionCells.Any()) return default;
        Trace.WriteLine($"{solution.TimeToSolve}ms to solve ({sourceCell.X},{sourceCell.Y}) to ({destCell.X},{destCell.Y}).");
        return solution;
    }

    private (Cell destCell, Cell sourceCell) GetStateCells(Tile destination, Tile source, Cell destCell, Cell[,] cells, Cell sourceCell)
    {
        foreach (var t in State.Tiles)
        {
            var hScore = Math.Abs(t.X - destCell.X) + Math.Abs(t.Y - destCell.Y);
            var cell = new Cell()
            {
                HScore = hScore * 10,
                GScore = int.MaxValue / 2,
                X = t.X,
                Y = t.Y,
                Id = t.Id,
                Passable = t.IsPassable
            };
            if (t == destination) { destCell = cell; }
            if (t == source) { sourceCell = cell; }
            cells[t.X, t.Y] = cell;
        }
        return (destCell, sourceCell);
    }

    public async Task PlayerPathFinding()
    {
        var requestDate = DateTime.Now;
        LastRequestedPathFind = requestDate;

        var keys = PlayerDictionary.Keys.ToArray();
        var notableCount = 0;
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];
            var (source, destination) = PlayerDictionary[key];
            SolutionDictionary[key].Clear();

            if (destination is null || source is null) { continue; }

            var solution = await PathFinding(destination, source, AllowDiagonal, requestDate);

            if (solution.SolutionCells is null || !solution.SolutionCells.Any() || solution.thisDate != LastRequestedPathFind) continue;

            Trace.WriteLine($"{solution.TimeToSolve}ms to solve ({source.X},{source.Y}) to ({source.X},{destination.Y}).");

            foreach (var cell in solution.SolutionCells) { SolutionDictionary[key].Add((cell.X, cell.Y)); }

            foreach (var cell in solution.AllCells) { notableCount = (cell.FCost > int.MaxValue / 3) ? notableCount + 1 : notableCount; }

            //CellsScored = notableCount; //Wrong at the moment
            //AnswerCells = solution.AllCells; //Wrong at the moment
        }
    }

    public void TryRotateConveyor(Point? point)
    {
        if (!point.HasValue) return;
        var tile = GetTileAtLocation(point);
        if (tile is null || tile.ConveyorTile is null) return;

        var ct = tile.ConveyorTile;

        var listOfThings = new List<(int, int)>() { (1, 0), (0, 1), (-1, 0), (0, -1) };

        var listOfThings2 = new List<ConveyorTile.Coordinate>() { new(1, 0), new(0, 1), new(-1, 0), new(0, -1) };
        var nextDirection2 = listOfThings2[(listOfThings2.IndexOf(new(ct.Direction.X, ct.Direction.Y)) + 1) % listOfThings2.Count];

        var calledNext = ct.Location - nextDirection2;

        //go through all of the conveyors that would have been connected. Subtract them from the current conveyor's conveyorTiles

        var listOfRemovableCts = new List<ConveyorTile>();
        var theNext = ct.NextConveyorTile;

        while (theNext is not null)
        {
            listOfRemovableCts.Add(theNext);
            theNext = theNext.NextConveyorTile;
        }
        Trace.WriteLine($"{listOfRemovableCts.Count} items in listOfRemovableCts.");

        var newConveyor = new Conveyor();
        var temporaryCollection = ct.Conveyor.ConveyorTiles.Where(cc => !listOfRemovableCts.Contains(cc)).ToList();
        Trace.WriteLine($"{temporaryCollection.Count} conveyorTiles can be removed.");
        foreach (var conveyorTile in temporaryCollection)
        {
            ct.Conveyor.ConveyorTiles.Remove(conveyorTile);
            newConveyor.ConveyorTiles.Add(conveyorTile);
            conveyorTile.Conveyor = newConveyor;
        }

        Conveyors.Add(newConveyor);

        Trace.WriteLine($"Next target = ({calledNext.X},{calledNext.Y}).");

        if (ct.Location.X - ct.Location.X >= 0 && ct.Location.X - ct.Location.X < TileWidth && ct.Location.Y - ct.Location.Y >= 0 && ct.Location.Y - ct.Location.Y < TileHeight)
        {
            var currentTargetTile = State.TileGrid[ct.Location.X - ct.Direction.X, ct.Location.Y - ct.Direction.Y];
            if (currentTargetTile.InboundConveyorTiles.Remove(ct))
            {
                Trace.WriteLine($"Removed ({ct.Location.X},{ct.Location.Y}) from ({currentTargetTile.X}, {currentTargetTile.Y})'s inbound."); //good
            }
        }

        if (ct.Location.X - nextDirection2.X >= 0 && ct.Location.X - nextDirection2.X < TileWidth && ct.Location.Y - nextDirection2.Y >= 0 && ct.Location.Y - nextDirection2.Y < TileHeight)
        {
            var upcomingTargetTile = State.TileGrid[ct.Location.X - nextDirection2.X, ct.Location.Y - nextDirection2.Y];
            //if upcomingTargetTile has exactly zero incoming, but has a conveyorTile
            if (upcomingTargetTile.InboundConveyorTiles.Count == 0 && upcomingTargetTile.ConveyorTile is not null)
            {
                //Get all current conveyors in a list
                var existing = ct.Conveyor.ConveyorTiles.ToList();
                Trace.WriteLine("Existing= " + string.Join(", ", existing.Select(x => $"({x.Location.X},{x.Location.Y})")));
                var nextConveyor = upcomingTargetTile.ConveyorTile.Conveyor;
                Conveyors.Remove(ct.Conveyor);
                foreach (var c in existing)
                {
                    c.Conveyor = nextConveyor;
                    //ct.Conveyor.ConveyorTiles.Remove(c);
                    nextConveyor.ConveyorTiles.Add(c);

                    Trace.WriteLine($"Changed ({c.Location.X},{c.Location.Y})'s conveyor to {nextConveyor.GetTileText()}");
                }
                //Conveyors.Remove(ct.Conveyor);
            }
            upcomingTargetTile.InboundConveyorTiles.Add(ct);
        }


        ct.Direction = (nextDirection2.X, nextDirection2.Y);
        ct.Setup();
    }

    private async Task TryFlipElement(Tile tile)
    {
        if (AlreadyClicked.Any(t => t.Id == tile.Id)) return; //ToDo: This could be done with storing a dictionary better, I think.
        if (tile.TileRole == TileRole.Conveyor) return;
        tile.IsPassable = !tile.IsPassable;
        if (!tile.IsPassable)
        {
            tile.TileRole = TileRole.Nothing;
            foreach (var kvp in PlayerDictionary)
            {
                if (kvp.Value.Destination == tile) PlayerDictionary[kvp.Key] = (kvp.Value.Source, null);
                if (kvp.Value.Source == tile) PlayerDictionary[kvp.Key] = (null, kvp.Value.Source);
            }
        }

        AlreadyClicked.Add(tile);
        EntitiesToHighlight.Clear();
        EntitiesToHighlight.Add(tile);
        if (AlwaysPath) await PlayerPathFinding();
        SetupMapString();
    }

    public Tile GetTileAtLocation(Point? point)
    {
        if (!point.HasValue) return null;
        var xThing = (int)Math.Floor((point.Value.X + Left) / TileSize);
        var yThing = (int)Math.Floor((point.Value.Y + Top) / TileSize);

        if (xThing >= TileWidth || yThing >= TileHeight) return null;

        return State.TileGrid[xThing, yThing];
    }

    private async Task HandleRightClickAddConveyorNode(Tile tile)
    {
        //ToDo: Need to make placing conveyors better. 
        //ToDo: Here, I should make sure that it is either IsPassable or it lands on another conveyor.
        if (SelectedConveyorTile is null && (tile.IsPassable || tile.TileRole == TileRole.Conveyor))
        {
            SelectedConveyorTile = tile;
            return;
        }

        if (!tile.IsPassable && tile.TileRole != TileRole.Conveyor) return;

        //ToDo: This is messed up
        var result = await PathFinding(SelectedConveyorTile, tile, false, DateTime.Now, true);
        if (result.SolutionCells is null) return;
        var conveyor = new Conveyor() { Id = Conveyors.Count + 1 };

        SelectedConveyorTile = null;

        //ToDo: I should go through this in reverse?
        for (var index = 0; index < result.SolutionCells.Count; index++)
        {
            var cell = result.SolutionCells[index];
            var nextCell = (index + 1 == result.SolutionCells.Count) ? null : result.SolutionCells[index + 1];
            var tempTile = State.TileGrid[result.SolutionCells[index].X, result.SolutionCells[index].Y];
            if (nextCell is null)
            {
                var ctx = new ConveyorTile() { Tile = tempTile, Direction = (0, 0) };
                conveyor.ConveyorTiles.Add(new() { Tile = tempTile, Direction = (0, 0) });
                tempTile.ConveyorTile = ctx;
                continue;
            }

            var (x, y) = (cell.X - nextCell.X, cell.Y - nextCell.Y);
            tempTile.IsPassable = false;
            tempTile.TileRole = TileRole.Conveyor;
            var ct = new ConveyorTile() { Tile = tempTile, Direction = (X: x, Y: y), Conveyor = conveyor };
            tempTile.ConveyorTile = ct;
            ct.Setup();
            conveyor.ConveyorTiles.Add(ct);
        }

        for (var index = 0; index < conveyor.ConveyorTiles.Count; index++)
        {
            var conveyorTile = conveyor.ConveyorTiles[index];
            var nextConveyorTile = (index + 1 == conveyor.ConveyorTiles.Count) ? null : conveyor.ConveyorTiles[index + 1];
            conveyorTile.NextConveyorTile = nextConveyorTile;
        }

        Conveyors.Add(conveyor);

        //ToDo: Separate this to another method. 
        for (var index = 0; index < Conveyors.Count; index++)
        {
            var conv = Conveyors[index];
            var masterTile = conv.ConveyorTiles[^1];
            if (masterTile.NextConveyorTile is not null) continue;

            for (var i = 0; i < Conveyors.Count; i++)
            {
                if (i == index) continue;
                var convSlave = Conveyors[i];
                var slaveTile = convSlave.ConveyorTiles.FirstOrDefault(t =>
                    t.Tile.X == masterTile.Tile.X && t.Tile.Y == masterTile.Tile.Y);
                if (slaveTile is null) continue;
                masterTile.NextConveyorTile = slaveTile;
                masterTile.Direction = slaveTile.Direction;
            }
        }

        //foreach (var co in Conveyors) { /*I suck at debug with nulls.*/ Trace.WriteLine($"ConveyorID: {co.Id}" + string.Join("=> ", co.ConveyorTiles.Select(x => $"({x.Tile.X},{x.Tile.Y}) to ({x.NextConveyorTile?.Tile.X ?? -1},{x.NextConveyorTile?.Tile.Y ?? -1})"))); }

        await PlayerPathFinding();
    }

    private async Task HandleRightClickPlayerMode(Tile tile)
    {
        //ToDo: Make sure right-clicking source/destination doesn't put it in a bad state.
        if (tile is null) return;
        var keys = PlayerDictionary.Keys.ToArray();
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];

            var kvp = PlayerDictionary[key];
            if (kvp.Destination is not null && !kvp.Destination.IsPassable) kvp = (kvp.Source, null);
            if (kvp.Source is not null && !kvp.Source.IsPassable) kvp = (null, kvp.Destination);
            if (kvp.Destination == tile)
            {
                PlayerDictionary[key] = (kvp.Source, null);
                tile.TileRole = TileRole.Nothing;
                return;
            }

            if (kvp.Source == tile)
            {
                PlayerDictionary[key] = (null, kvp.Source);
                tile.TileRole = TileRole.Nothing;
                return;
            }
        }

        if (tile.TileRole == TileRole.Nothing && tile.IsPassable)
        {
            if (PlayerDictionary[CurrentPlayer].Source is null)
            {
                PlayerDictionary[CurrentPlayer] = (tile, PlayerDictionary[CurrentPlayer].Destination);
                tile.TileRole = TileRole.Source;
                if (AlwaysPath) await PlayerPathFinding();
                return;
            }

            if (PlayerDictionary[CurrentPlayer].Destination is null)
            {
                PlayerDictionary[CurrentPlayer] = (PlayerDictionary[CurrentPlayer].Source, tile);
                tile.TileRole = TileRole.Destination;
                if (AlwaysPath) await PlayerPathFinding();
                return;
            }
        }
    }

    public void TickConveyor() => Conveyors.ForEach(c => c.Tick = (c.Tick + 1) % 8);

    public void RandomlyAddItem()
    {
        if (!Conveyors.Any() || Items.Count > 10_000 || _rand.NextDouble() > 0.991) { return; }

        var conveyorIndex = _rand.Next(0, Conveyors.Count);
        var conveyor = Conveyors[conveyorIndex];
        if (!conveyor.ConveyorTiles.Any()) return;
        var conveyorTile = conveyor.ConveyorTiles.First();
        if (conveyorTile.Items.Any() || conveyorTile.NextConveyorTile is null) return;
        var x = _rand.NextDouble() > 0.5 ? 0 : MaxCellNumber - 1;
        var y = _rand.NextDouble() > 0.5 ? 0 : MaxCellNumber - 1;
        var item = new Item()
        {
            X = x,
            Y = y,
            ConveyorTile = conveyorTile,
            Inertia = (conveyorTile.Direction.X, conveyorTile.Direction.Y),
            Left = x == conveyorTile.Lane.X || y == conveyorTile.Lane.Y
        };
        conveyorTile.Items.Add(item);
        conveyor.Items.Add(item);
        Items.Add(item);
        OnPropertyChanged(nameof(ItemsCount));
    }

    public void Movement()
    {
        //ToDo: Think about how this will be performed without GUI
        //ToDo: Actually I need to go through items according to who is furthest down the line so I can account for collisions more effectively.
        //Maybe it would be the segment * 10 + (_maxCellNumber * the hoveredTile direction (if x=3 and direction = (1,3), then it's the first that would be checked.). I'd look at highest first.
        //ToDo: What if I kept the left and right hand sides separate? Just have to define left/right side of each conveyorTile. If you go from one conveyor to another, left/right isn't respected.
        //ToDo: There's some issue with interlacing conveyors.
        TickConveyor();
        //return;
        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];

            var (nextX, nextY, nextTile) = item.GetNextLocation();

            if (nextTile is null || nextTile.Direction.X + nextTile.Direction.Y == 0) { item.DeleteItem(); continue; }

            var anotherItem = nextTile.Items.FirstOrDefault(i => i.X == nextX && i.Y == nextY);
            if (anotherItem is not null) { /*Trace.WriteLine($"Another item is in the way at: Cell=({anotherItem.ConveyorTiles.Tile.X},{anotherItem.ConveyorTiles.Tile.Y}) ({anotherItem.X},{anotherItem.Y})" + $". An item is at Cell=({item.ConveyorTiles.Tile.X},{item.ConveyorTiles.Tile.Y}) ({item.X},{item.Y}). Our velocity is: {item.ConveyorTiles.Direction}");*/ continue; }

            (item.X, item.Y) = (nextX, nextY);

            if (item.ConveyorTile != nextTile)
            {
                item.ConveyorTile.Items.Remove(item);
                nextTile.Items.Add(item);
                item.ConveyorTile = nextTile;
            }
        }
    }
}