using RimWorld;
using Verse;

namespace RimworldSkunks;

[DefOf]
public static class SkunkDefOf
{
    public static GeneDef Skunk_Flatulence;
    public static HediffDef Skunk_Stench;
    public static HistoryEventDef Skunk_TookPartner;

    static SkunkDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(SkunkDefOf));
    }
}
