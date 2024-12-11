using Verse;
using RimWorld;

namespace RimworldSkunks;

public class CompProperties_AbilityFlatulence : CompProperties_AbilityEffect
{
    public float flatulenceRadius;

    public float cellsToFill;
    public float durationSeconds;
    public EffecterDef effecterReleasing;

    public CompProperties_AbilityFlatulence()
    {
        compClass = typeof(CompAbilityEffect_Flatulence);
    }
}
