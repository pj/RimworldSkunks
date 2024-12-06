using Verse;
using RimWorld;

namespace Flatulence;

public class CompProperties_AbilityFlatulence : CompProperties_AbilityEffect
{
    public float flatulenceRadius;

    public float cellsToFill;
    public float durationSeconds;

    public CompProperties_AbilityFlatulence()
    {
        compClass = typeof(CompAbilityEffect_Flatulence);
    }
}
