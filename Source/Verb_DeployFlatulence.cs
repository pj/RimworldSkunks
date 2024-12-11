using Verse;
using RimWorld;

namespace RimworldSkunks;

public class Verb_DeployFlatulence : Verb, IAbilityVerb
{
    public Ability ability;

    public Ability Ability
    {
        get
        {
            return ability;
        }
        set
        {
            ability = value;
        }
    }

    protected override bool TryCastShot()
    {
        Log.Message("TryCastShot Flatulence");
        CompAbilityEffect_Flatulence compReleaseGas = ability.CompOfType<CompAbilityEffect_Flatulence>();
        compReleaseGas.StartRelease();
        return true;
    }
}
