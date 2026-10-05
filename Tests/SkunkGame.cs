using System.Reflection;
using RimworldDriver.Client;
using Xunit;

namespace RimworldSkunks.Tests;

/// <summary>
/// One RimWorld with the Skunks mod loaded and one colony, shared by every test: booting the
/// game is slow, and regenerating a colony per test is more than a VM's virtual GPU survives.
/// Each test works in its own corner of the map, far enough apart that gas clouds never meet.
/// </summary>
public sealed class SkunkGame : IAsyncLifetime
{
    // Away from the centre, where the colony's own starting pawns mill about.
    private static readonly (int X, int Z)[] Corners = [(15, 15), (60, 15), (15, 60), (60, 60), (37, 12), (37, 62), (12, 37), (62, 37), (26, 26), (48, 26), (26, 48), (48, 48)];
    private int scenes;

    public RimWorldGame Game { get; private set; } = null!;
    public GameMap Map { get; private set; } = null!;

    /// <summary>A part of the map no other test has used.</summary>
    public Cell NextSite()
    {
        int index = Interlocked.Increment(ref scenes) - 1;
        if (index >= Corners.Length)
        {
            throw new InvalidOperationException("The shared map has no unused site left; add more or start a second colony.");
        }
        return new Cell(Corners[index].X, Corners[index].Z);
    }

    public async Task InitializeAsync()
    {
        var options = new GameLaunchOptions();
        options.Mods.Add(typeof(SkunkGame).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RimworldSkunks.ModPath").Value!);
        Game = await RimWorldGame.LaunchAsync(options);
        Map = await Game.StartQuickTestAsync();
    }

    public async Task DisposeAsync()
    {
        if (Game != null)
        {
            await Game.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SkunkGameCollection : ICollectionFixture<SkunkGame>
{
    public const string Name = "skunk game";
}

/// <summary>One skunk and one ordinary colonist standing next to it, on their own patch of the shared map.</summary>
public sealed class StinkScene
{
    private const string GasGrid = "RimworldSkunks.SkunkGasGrid";

    public required RimWorldGame Game { get; init; }
    public required GameMap Map { get; init; }
    public required GamePawn Skunk { get; init; }
    public required GamePawn Victim { get; init; }

    public static async Task<StinkScene> CreateAsync(SkunkGame fixture)
    {
        RimWorldGame game = fixture.Game;
        GameMap map = fixture.Map;
        GamePawn skunk = await map.SpawnPawnAsync(new SpawnPawnOptions { Xenotype = "Skunk", BiologicalAge = 30, Near = fixture.NextSite(), Radius = 6 });
        GamePawn victim = await map.SpawnPawnAsync(new SpawnPawnOptions
        {
            Xenotype = "Baseliner",
            BiologicalAge = 30,
            Near = await skunk.PositionAsync(),
            Radius = 2,
        });
        // Drafted pawns hold their position instead of wandering out of the cloud.
        await victim.SetDraftedAsync();
        return new StinkScene { Game = game, Map = map, Skunk = skunk, Victim = victim };
    }

    /// <summary>Has the skunk use its power and runs the game until the gas is out.</summary>
    public async Task ReleaseGasAsync()
    {
        await Skunk.CastAbilityAsync("Skunk_Flatulence");
        await Game.TickAsync(90);
    }

    /// <summary>Skunk gas density in a cell, 0-255.</summary>
    public Task<int> GasDensityAsync(Cell cell) => Map.ComponentAsync<int>(GasGrid, "DensityAt", cell);

    /// <summary>Clears a cell and its neighbours down to plain concrete, as a neutral backdrop.</summary>
    public async Task PaveAsync(Cell center)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                Cell cell = center.Offset(dx, dz);
                foreach (ThingRef thing in await Map.CallAsync<List<ThingRef>>("thingGrid.ThingsListAt", cell))
                {
                    if (thing.Thing != Skunk.Id && thing.Thing != Victim.Id)
                    {
                        await Game.CallAsync<object?>(Target.Thing(thing.Thing), "Destroy", "Vanish");
                    }
                }
                await Map.CallAsync<object?>("terrainGrid.SetTerrain", cell, "Concrete");
            }
        }
    }

    /// <summary>The average on-screen colour of a paved patch (the cell and its neighbours).</summary>
    public async Task<ScreenColor> PatchColorAsync(Cell center)
    {
        int r = 0, g = 0, b = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                ScreenColor color = await Game.CellColorAsync(center.Offset(dx, dz));
                r += color.R;
                g += color.G;
                b += color.B;
            }
        }
        return new ScreenColor(r / 9, g / 9, b / 9);
    }

    /// <summary>Jumps the clock from the 6am start to noon, for neutral daylight.</summary>
    public async Task SkipToMiddayAsync()
    {
        int now = (await Game.StatusAsync()).TicksGame;
        await Game.CallAsync<object?>(Target.Static("Verse.Find"), "TickManager.DebugSetTicksGame", now + 6 * 2500);
        await Game.TickAsync(1);
    }

    private sealed record ThingRef(string Thing, string Def);

}
