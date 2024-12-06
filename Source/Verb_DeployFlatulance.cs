using Verse;

namespace RimWorld;

public class Verb_DeployFlatulance : Verb, IAbilityVerb
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
        // CompReleaseGas compReleaseGas = ability.CompOfType<CompReleaseGas>();
        // compReleaseGas.StartRelease();
        return true;
    }
}
