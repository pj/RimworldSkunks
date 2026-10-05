using RimworldDriver.Client;
using Xunit;

namespace RimworldSkunks.Tests;

[Collection(SkunkGameCollection.Name)]
public class BreedingTests(SkunkGame fixture)
{
    private static readonly Target Debug = Target.Static("RimworldSkunks.SkunkDebug");

    private Task<GamePawn> SpawnAsync(string xenotype, string gender, Cell near) =>
        fixture.Map.SpawnPawnAsync(new SpawnPawnOptions { Xenotype = xenotype, Gender = gender, BiologicalAge = 25, Near = near, Radius = 4 });

    [Fact]
    public async Task Skunks_carry_the_breeding_genes_and_their_drawbacks()
    {
        GamePawn skunk = await SpawnAsync("Skunk", "Female", fixture.NextSite());
        PawnInfo info = await skunk.InfoAsync();

        foreach (string gene in new[] { "Skunk_Prolific", "Skunk_Promiscuous", "MeleeDamage_Weak", "Instability_Mild", "Immunity_Weak" })
        {
            Assert.True(info.HasGene(gene), gene);
        }
    }

    [Fact]
    public async Task A_skunk_pregnancy_runs_three_times_as_fast()
    {
        Cell site = fixture.NextSite();
        GamePawn skunk = await SpawnAsync("Skunk", "Female", site);
        GamePawn baseliner = await SpawnAsync("Baseliner", "Female", site);
        GamePawn father = await SpawnAsync("Baseliner", "Male", site);
        await fixture.Game.CallAsync<object?>(Debug, "MakePregnant", skunk.Id, father.Id);
        await fixture.Game.CallAsync<object?>(Debug, "MakePregnant", baseliner.Id, father.Id);
        float skunkStart = await ProgressAsync(skunk);
        float baselinerStart = await ProgressAsync(baseliner);

        await fixture.Game.TickAsync(2500);

        float skunkGain = await ProgressAsync(skunk) - skunkStart;
        float baselinerGain = await ProgressAsync(baseliner) - baselinerStart;
        Assert.True(baselinerGain > 0);
        Assert.Equal(3.0, skunkGain / baselinerGain, 1);

        Task<float> ProgressAsync(GamePawn pawn) => fixture.Game.CallAsync<float>(Debug, "PregnancyProgress", pawn.Id);
    }

    [Fact]
    public async Task Skunk_mothers_usually_have_twins_or_triplets()
    {
        Cell site = fixture.NextSite();
        GamePawn father = await SpawnAsync("Baseliner", "Male", site);
        GamePawn skunk = await SpawnAsync("Skunk", "Female", site);
        GamePawn baseliner = await SpawnAsync("Baseliner", "Female", site);

        // Each birth adds 1 to 3 children for the skunk, exactly 1 for the baseliner.
        const int births = 12;
        int skunkChildren = 0;
        int multipleBirths = 0;
        for (int i = 0; i < births; i++)
        {
            int after = await fixture.Game.CallAsync<int>(Debug, "GiveBirth", skunk.Id, father.Id);
            Assert.InRange(after - skunkChildren, 1, 3);
            multipleBirths += after - skunkChildren > 1 ? 1 : 0;
            skunkChildren = after;
        }
        int baselinerChildren = 0;
        for (int i = 0; i < 4; i++)
        {
            baselinerChildren = await fixture.Game.CallAsync<int>(Debug, "GiveBirth", baseliner.Id, father.Id);
        }

        Assert.Equal(4, baselinerChildren);
        // 70% of births are multiple; fewer than 3 in 12 would be a one-in-five-thousand fluke.
        Assert.True(multipleBirths >= 3, $"only {multipleBirths} multiple births in {births}");
    }

    [Fact]
    public async Task A_skunk_with_a_lover_will_still_take_another_and_a_baseliner_will_not()
    {
        Cell site = fixture.NextSite();
        GamePawn skunk = await SpawnAsync("Skunk", "Female", site);
        GamePawn baseliner = await SpawnAsync("Baseliner", "Female", site);
        GamePawn first = await SpawnAsync("Baseliner", "Male", site);
        GamePawn second = await SpawnAsync("Baseliner", "Male", site);
        await fixture.Game.CallAsync<object?>(Debug, "MakeLovers", skunk.Id, first.Id);
        await fixture.Game.CallAsync<object?>(Debug, "MakeLovers", baseliner.Id, second.Id);

        Assert.True(await fixture.Game.CallAsync<bool>(Debug, "WouldTakeAnotherLover", skunk.Id));
        Assert.True(await fixture.Game.CallAsync<bool>(Debug, "WouldTakeAnotherSpouse", skunk.Id));
        Assert.False(await fixture.Game.CallAsync<bool>(Debug, "WouldTakeAnotherLover", baseliner.Id));
    }
}
