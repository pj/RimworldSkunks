using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace Flatulence;

public class CompAbilityEffect_Flatulence : CompAbilityEffect
{
    public new CompProperties_AbilityFlatulence Props => (CompProperties_AbilityFlatulence)props;

    private int remainingGas;
    private int TotalGas => Mathf.CeilToInt(Props.cellsToFill * 255f);
    private float GasReleasedPerTick => (float)TotalGas / Props.durationSeconds / 60f;

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

    private bool started = false;
    // public CompAbilityEffect_Flatulence()
    // {
    //     gasDensity = new uint[parent.pawn.MapHeld.cellIndices.NumGridCells];
    // }

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        Log.Message("Apply Flatulence");
        started = true;
        gasDensity = new uint[parent.pawn.MapHeld.cellIndices.NumGridCells];
        base.Apply(target, dest);
        // DamageDef flatulence = DefDatabase<DamageDef>.GetNamed("Flatulence");
        // Log.Message("Flatulence: " + flatulence.label);
        // GenExplosion.DoExplosion(
        //     target.Cell, 
        //     parent.pawn.MapHeld, 
        //     Props.flatulenceRadius, 
        //     flatulence, 
        //     null, -1, -1f, null, null, null, null, null, 0f, 1, 
        //     GasType.RotStink
        // );
    }

    public override void CompTick()
    {
        Map mapHeld = parent.pawn.MapHeld;
        if (!started || mapHeld == null)
        {
            Log.Message("Flatulence not started");
            Log.Message(started.ToString());
            Log.Message(mapHeld.ToString());
            return;
        }
        if (remainingGas > 0 && mapHeld.IsHashIntervalTick(30))
        {
            Log.Message("Adding gas");
            Log.Message(remainingGas.ToString());
            int num = Mathf.Min(remainingGas, Mathf.RoundToInt(GasReleasedPerTick * 30f));
            AddGas(mapHeld, parent.pawn.PositionHeld, num);
            remainingGas -= num;
            if (remainingGas <= 0)
            {
                started = false;
                remainingGas = TotalGas;
            }
        }

        if (mapHeld.IsHashIntervalTick(600))
        {
            RecalculateEverHadGas();
        }

        if (anyGasEverAdded)
        {
            return;
        }
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

    // Copy pasted from GasUtility

    private uint[] gasDensity;
    private bool anyGasEverAdded = false;

    private uint gasType = 1;

    public void RecalculateEverHadGas()
    {
        anyGasEverAdded = false;
        for (int i = 0; i < gasDensity.Length; i++)
        {
            if (gasDensity[i] != 0)
            {
                anyGasEverAdded = true;
                break;
            }
        }
    }
    public bool GasCanMoveTo(Map map, IntVec3 cell)
    {
        if (!cell.InBounds(map))
        {
            return false;
        }
        if (cell.Filled(map))
        {
            return cell.GetDoor(map)?.Open ?? false;
        }
        return true;
    }

    public byte DensityAt(Map map, IntVec3 cell)
    {
        return DensityAt(CellIndicesUtility.CellToIndex(cell, map.Size.x));
    }

    private byte DensityAt(int index)
    {
        return (byte)((gasDensity[index] >> (int)gasType) & 0xFFu);
    }

    private byte AdjustedDensity(int newDensity, out int overflow)
    {
        if (newDensity > 255)
        {
            overflow = newDensity - 255;
            return byte.MaxValue;
        }
        overflow = 0;
        if (newDensity < 0)
        {
            return 0;
        }
        return (byte)newDensity;
    }

    private void Overflow(Map map, IntVec3 cell, int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        int remainingAmount = amount;
        map.floodFiller.FloodFill(cell, (c) => GasCanMoveTo(map, c), delegate(IntVec3 c)
        {
            int num = Mathf.Min(remainingAmount, 255 - DensityAt(map, c));
            if (num > 0)
            {
                AddGas(map, c, num, canOverflow: false);
                remainingAmount -= num;
            }
            return remainingAmount <= 0;
        }, GenRadial.NumCellsInRadius(40f), rememberParents: true);
    }

    private void SetDirect(int index, byte value)
    {
        gasDensity[index] = (uint)(value << 8);
    }

    private void AddGas(Map map, IntVec3 cell, int amount, bool canOverflow = true)
    {
        if (amount <= 0 || !GasCanMoveTo(map, cell))
        {
            return;
        }
        anyGasEverAdded = true;
        int index = CellIndicesUtility.CellToIndex(cell, map.Size.x);
        byte b = DensityAt(index);
        int overflow = 0;
        b = AdjustedDensity(b + amount, out overflow);
        SetDirect(index, b);
        map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Gas);
        if (overflow > 0)
        {
            Overflow(map, cell, overflow);
        }
    }
}
