using Verse;
using RimWorld;

namespace RimworldSkunks;

public class CompProperties_AbilityFlatulence : CompProperties_AbilityEffect
{
    public float cellsToFill;

    public CompProperties_AbilityFlatulence()
    {
        compClass = typeof(CompAbilityEffect_Flatulence);
    }
}
