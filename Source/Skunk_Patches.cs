using System;
using RimWorld;
using Verse;
using HarmonyLib;

namespace RimworldSkunks;

[HarmonyPatch(typeof(Map), nameof(Map.ConstructComponents))]
public class Map_ConstructComponents_Patch
{
    static void Postfix(Map __instance)
    {
        Log.Message("Skunk_Patches.Postfix");
    }
}

[StaticConstructorOnStartup]
public static class Skunk_Patches
{
    static Skunk_Patches()
    {
        var harmony = new Harmony("nz.kiwi.johnson.rimworldskunks");
        harmony.PatchAll();
        Log.Error("Skunks: Harmony patches applied successfully");
    }
}