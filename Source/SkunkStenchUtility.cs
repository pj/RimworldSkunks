using Verse;
using Verse.AI;
using RimWorld;

namespace RimworldSkunks;

public static class SkunkStenchUtility
{
    // Severity gained per exposure check while standing in a full-density cell.
    private const float SeverityPerExposure = 0.25f;

    public static bool IsAffectedByStench(Pawn pawn)
    {
        if (!pawn.RaceProps.IsFlesh)
        {
            return false;
        }
        // Carriers of the gene are used to their own smell.
        return !(pawn.genes?.HasActiveGene(SkunkDefOf.Skunk_Flatulence) ?? false);
    }

    public static void DoStenchEffects(Pawn pawn, float densityPercent)
    {
        if (pawn.Dead || !IsAffectedByStench(pawn))
        {
            return;
        }
        Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(SkunkDefOf.Skunk_Stench);
        bool firstWhiff = hediff == null;
        if (firstWhiff)
        {
            hediff = pawn.health.AddHediff(SkunkDefOf.Skunk_Stench);
        }
        hediff.Severity += SeverityPerExposure * densityPercent;
        if (firstWhiff)
        {
            TryVomit(pawn);
        }
    }

    private static void TryVomit(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Downed || !pawn.Awake() || pawn.jobs == null || pawn.CurJobDef == JobDefOf.Vomit)
        {
            return;
        }
        pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Vomit), JobCondition.InterruptForced, null, resumeCurJobAfterwards: true);
    }
}
