using Verse;
using RimWorld;

namespace Flatulence;

public class CompAbilityEffect_Flatulence : CompAbilityEffect
{
    public new CompProperties_AbilityFlatulence Props => (CompProperties_AbilityFlatulence)props;

    public bool ShouldHaveInspectString
    {
        get
        {
            if (ModsConfig.BiotechActive)
            {
                return parent.pawn.RaceProps.Humanlike;
            }
            return false;
        }
    }

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        base.Apply(target, dest);
        DamageDef flatulence = DefDatabase<DamageDef>.GetNamed("Flatulence");
        Log.Message("Flatulence: " + flatulence.label);
        GenExplosion.DoExplosion(
            target.Cell, 
            parent.pawn.MapHeld, 
            Props.flatulenceRadius, 
            flatulence, 
            null, -1, -1f, null, null, null, null, null, 0f, 1, 
            GasType.RotStink
        );
    }

    public override void DrawEffectPreview(LocalTargetInfo target)
    {
        GenDraw.DrawRadiusRing(target.Cell, Props.flatulenceRadius);
    }

    public override string CompInspectStringExtra()
    {
        if (ShouldHaveInspectString)
        {
            if (parent.CanCast)
            {
                return "AbilityFlatulenceCharged".Translate();
            }
            return "AbilityFlatulenceRecharging".Translate(parent.CooldownTicksRemaining.ToStringTicksToPeriod());
        }
        return null;
    }
}
