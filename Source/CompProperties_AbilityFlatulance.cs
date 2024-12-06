using Verse;
using RimWorld;

namespace Flatulence;

public class CompProperties_AbilityFlatulence : CompProperties_AbilityEffect
{
    public float flatulenceRadius;

    public CompProperties_AbilityFlatulence()
    {
        compClass = typeof(CompAbilityEffect_Flatulence);
    }
}
