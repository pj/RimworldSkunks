using RimworldDriver.Client;
using Xunit;

namespace RimworldSkunks.Tests;

[Collection(SkunkGameCollection.Name)]
public sealed class FlatulenceTests(SkunkGame fixture) : IAsyncLifetime
{
    private StinkScene scene = null!;

    public async Task InitializeAsync() => scene = await StinkScene.CreateAsync(fixture);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Skunks_carry_the_flatulence_gene_and_its_ability()
    {
        PawnInfo skunk = await scene.Skunk.InfoAsync();

        Assert.Equal("Skunk", skunk.Xenotype);
        Assert.True(skunk.HasGene("Skunk_Flatulence"));
        Assert.Contains(skunk.Abilities, a => a.Def == "Skunk_Flatulence" && a.CanCast);
    }

    [Fact]
    public async Task Flatulence_fills_the_area_around_the_skunk_with_gas_and_goes_on_cooldown()
    {
        Cell origin = await scene.Skunk.PositionAsync();
        Assert.Equal(0, await scene.GasDensityAsync(origin));

        await scene.ReleaseGasAsync();

        Assert.True(await scene.GasDensityAsync(origin) > 0);
        Assert.Contains((await scene.Skunk.InfoAsync()).Abilities, a => a.Def == "Skunk_Flatulence" && !a.CanCast);
    }

    [Fact]
    public async Task Gas_makes_bystanders_vomit_but_not_the_skunk()
    {
        await scene.ReleaseGasAsync();

        PawnInfo victim = await scene.Victim.InfoAsync();
        Assert.True(victim.HasHediff("Skunk_Stench"));
        Assert.Equal("Vomit", victim.Job);

        Assert.False((await scene.Skunk.InfoAsync()).HasHediff("Skunk_Stench"));
    }

    [Fact]
    public async Task Breathing_the_gas_for_long_leaves_a_bystander_dazed()
    {
        await scene.ReleaseGasAsync();

        // The daze strikes at random once the stench is severe; ten game-seconds per check, up to a minute.
        bool dazed = await scene.Game.TickUntilAsync(
            async () => (await scene.Victim.InfoAsync()).MentalState == "Skunk_StenchDaze",
            maxTicks: 3600, step: 60);

        Assert.True(dazed, "The victim never entered the stench daze");
        Assert.Null((await scene.Skunk.InfoAsync()).MentalState);
    }

    [SkippableFact]
    public async Task Gas_cloud_is_drawn_brown()
    {
        Skip.If(scene.Game.IsRemote, "A VM's virtual GPU does not draw the game faithfully; run this one on real hardware.");
        // Dawn light is dim and orange, which would muddy a colour comparison.
        await scene.SkipToMiddayAsync();
        Cell origin = await scene.Skunk.PositionAsync();
        Cell victimAt = await scene.Victim.PositionAsync();
        int away = victimAt.X > origin.X ? -1 : 1;
        // Two patches of the same grey concrete under the same light: one inside the cloud (on the
        // side away from the victim, so no pawn covers it) and one well outside it to compare with.
        Cell inCloud = origin.Offset(away * 3, 0);
        Cell clear = origin.Offset(away * 10, 0);
        await scene.PaveAsync(inCloud);
        await scene.PaveAsync(clear);

        await scene.ReleaseGasAsync();
        await scene.Game.LookAtAsync(origin);
        ScreenColor gassed = await scene.PatchColorAsync(inCloud);
        ScreenColor control = await scene.PatchColorAsync(clear);
        await SaveScreenshotAsync("gas-cloud.png");

        Assert.True(await scene.GasDensityAsync(inCloud) > 100, "The sampled cell should be thick with gas");
        Assert.Equal(0, await scene.GasDensityAsync(clear));
        string colors = $"in cloud {gassed}, clear {control}";
        Assert.True(gassed.DistanceTo(control) > 15, $"The cloud should be clearly visible: {colors}");
        // Brown is a dark, saturated orange.
        Assert.InRange(gassed.Hue, 15, 45);
        Assert.True(gassed.Saturation > control.Saturation + 0.15, $"The cloud should tint the ground brown: {colors}");
        Assert.True(gassed.R > gassed.G && gassed.G > gassed.B, $"Expected red > green > blue: {colors}");
    }

    private async Task SaveScreenshotAsync(string name)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(dir);
        await (await scene.Game.ScreenshotAsync()).SaveAsync(Path.Combine(dir, name));
    }
}
