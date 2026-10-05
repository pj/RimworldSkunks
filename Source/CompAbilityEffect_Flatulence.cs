using Verse;
using RimWorld;
using UnityEngine;

namespace RimworldSkunks;

public class CompAbilityEffect_Flatulence : CompAbilityEffect
{
    public new CompProperties_AbilityFlatulence Props => (CompProperties_AbilityFlatulence)props;

    private int TotalGas => Mathf.CeilToInt(Props.cellsToFill * SkunkGasGrid.MaxGasPerCell);

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        base.Apply(target, dest);
        Pawn pawn = parent.pawn;
        pawn.MapHeld?.GetComponent<SkunkGasGrid>()?.AddGas(pawn.PositionHeld, TotalGas);
    }
}
