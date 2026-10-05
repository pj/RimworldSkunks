using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimworldSkunks;

// Patches are applied once all defs are loaded: patching PregnancyUtility any earlier runs its
// static constructor before the defs it looks up exist.
[StaticConstructorOnStartup]
public static class SkunkPatches
{
    static SkunkPatches()
    {
        new Harmony("pj.rimworldskunks").PatchAll(typeof(SkunkPatches).Assembly);
    }
}

// Put on a gene to give its carriers faster pregnancies, multiple births or freedom from monogamy.
public class BreedingExtension : DefModExtension
{
    public float gestationSpeedFactor = 1f;
    public float twinChance;
    public float tripletChance;
    public bool unlimitedPartners;
}

public static class SkunkBreeding
{
    private static IEnumerable<BreedingExtension> ExtensionsOf(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            yield break;
        }
        List<Gene> genes = pawn.genes.GenesListForReading;
        for (int i = 0; i < genes.Count; i++)
        {
            if (genes[i].Active && genes[i].def.GetModExtension<BreedingExtension>() is BreedingExtension extension)
            {
                yield return extension;
            }
        }
    }

    public static float GestationSpeedFactor(Pawn pawn)
    {
        float factor = 1f;
        foreach (BreedingExtension extension in ExtensionsOf(pawn))
        {
            factor *= extension.gestationSpeedFactor;
        }
        return factor;
    }

    public static bool HasUnlimitedPartners(Pawn pawn)
    {
        return ExtensionsOf(pawn).Any(e => e.unlimitedPartners);
    }

    // How many babies beyond the first this birth produces.
    public static int RollExtraBabies(Pawn mother)
    {
        float twins = 0f;
        float triplets = 0f;
        foreach (BreedingExtension extension in ExtensionsOf(mother))
        {
            twins += extension.twinChance;
            triplets += extension.tripletChance;
        }
        float roll = Rand.Value;
        return roll < triplets ? 2 : roll < triplets + twins ? 1 : 0;
    }
}

// Pregnancy advances faster for carriers of a gene with a gestation speed factor.
[HarmonyPatch(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.TickInterval))]
public static class Patch_Hediff_Pregnant_TickInterval
{
    public static void Prefix(Hediff_Pregnant __instance, int delta)
    {
        Pawn pawn = __instance.pawn;
        if (pawn == null || !pawn.RaceProps.Humanlike)
        {
            return;
        }
        float factor = SkunkBreeding.GestationSpeedFactor(pawn);
        if (factor > 1f)
        {
            // The game adds one share of progress itself; this adds the rest.
            float normal = PawnUtility.BodyResourceGrowthSpeed(pawn) / (pawn.RaceProps.gestationPeriodDays * 60000f);
            __instance.Severity += normal * (factor - 1f) * delta;
        }
    }
}

// Twins and triplets: the birth is simply run again for each extra baby.
[HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
public static class Patch_PregnancyUtility_ApplyBirthOutcome
{
    private static bool deliveringExtra;

    public static void Postfix(object[] __args)
    {
        var outcome = (RitualOutcomePossibility)__args[0];
        var ritual = (Precept_Ritual)__args[2];
        var genes = (List<GeneDef>)__args[3];
        var geneticMother = (Pawn)__args[4];
        var birtherThing = (Thing)__args[5];
        var father = (Pawn)__args[6];
        var lordJobRitual = (LordJob_Ritual)__args[8];
        var assignments = (RitualRoleAssignments)__args[9];
        bool preventLetter = (bool)__args[10];
        if (deliveringExtra || !(birtherThing is Pawn mother) || mother.Dead)
        {
            return;
        }
        int extra = SkunkBreeding.RollExtraBabies(mother);
        if (extra == 0)
        {
            return;
        }
        deliveringExtra = true;
        try
        {
            for (int i = 0; i < extra && !mother.Dead; i++)
            {
                // Top quality, so the mother does not face the risk of dying in childbirth again for
                // each baby; whether the baby is healthy still follows the outcome of the birth.
                PregnancyUtility.ApplyBirthOutcome(outcome, 1f, ritual, genes, geneticMother, birtherThing, father, null, lordJobRitual, assignments, preventLetter);
            }
        }
        finally
        {
            deliveringExtra = false;
        }
    }
}

// The game asks a pawn's ideoligion whether having one more lover or spouse is acceptable by naming
// an event such as "has two spouses". For pawns without the pair-bonding instinct the question is
// swapped for an event no precept objects to, so they neither hold back nor drop existing partners.
[HarmonyPatch]
public static class Patch_PartnerCountEvents
{
    public static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(LovePartnerRelationUtility), nameof(LovePartnerRelationUtility.GetHistoryEventLoveRelationCount));
        yield return AccessTools.Method(typeof(LovePartnerRelationUtility), nameof(LovePartnerRelationUtility.GetHistoryEventForLoveRelationCountPlusOne));
        yield return AccessTools.Method(typeof(SpouseRelationUtility), nameof(SpouseRelationUtility.GetHistoryEventForSpouseCountPlusOne));
        yield return AccessTools.Method(typeof(SpouseRelationUtility), nameof(SpouseRelationUtility.GetHistoryEventForSpouseAndFianceCountPlusOne));
    }

    public static void Postfix(Pawn pawn, ref HistoryEventDef __result)
    {
        if (SkunkBreeding.HasUnlimitedPartners(pawn))
        {
            __result = SkunkDefOf.Skunk_TookPartner;
        }
    }
}

// Dev-mode tools (debug actions menu, "Skunks" section), also used by the end-to-end tests.
public static class SkunkDebug
{
    [DebugAction("Skunks", "Make pregnant", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void MakePregnantTool(Pawn pawn)
    {
        MakePregnant(pawn, null);
    }

    public static void MakePregnant(Pawn mother, Pawn father)
    {
        var pregnancy = (Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman, mother);
        pregnancy.SetParents(null, father, PregnancyUtility.GetInheritedGeneSet(father, mother));
        mother.health.AddHediff(pregnancy);
    }

    public static float PregnancyProgress(Pawn pawn)
    {
        return pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PregnantHuman)?.Severity ?? -1f;
    }

    // A healthy birth, without the labor. Returns how many children the mother has afterwards.
    public static int GiveBirth(Pawn mother, Pawn father)
    {
        PregnancyUtility.ApplyBirthOutcome(RitualOutcomeEffectDefOf.ChildBirth.BestOutcome, 1f, null, null, mother, mother, father);
        return mother.relations.Children.Count();
    }

    public static void MakeLovers(Pawn a, Pawn b)
    {
        a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
    }

    public static bool WouldTakeAnotherLover(Pawn pawn)
    {
        return new HistoryEvent(pawn.GetHistoryEventForLoveRelationCountPlusOne(), pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo();
    }

    public static bool WouldTakeAnotherSpouse(Pawn pawn)
    {
        return new HistoryEvent(pawn.GetHistoryEventForSpouseAndFianceCountPlusOne(), pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo();
    }
}
