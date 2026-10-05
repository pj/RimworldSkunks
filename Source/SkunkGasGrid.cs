using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace RimworldSkunks;

// The vanilla GasGrid packs four gas types into one uint per cell and all four slots are
// taken, with their colours fixed in the gas shader. Skunk gas therefore lives in its own
// per-map grid, which follows the vanilla spreading rules and draws its own brown clouds.
public class SkunkGasGrid : MapComponent
{
    public const int MaxGasPerCell = 255;

    private const int MinDiffusion = 17;
    private const int DiffusionIntervalTicks = 30;
    private const int DissipationIntervalTicks = 60;
    private const int DissipationAmount = 3;
    private const int DissipationAmountRoofed = 2;
    private const int PawnEffectIntervalTicks = 60;
    private const float MaxOverflowFloodfillRadius = 40f;

    private const string GasTexPath = "Things/Gas/GasCloudThickA";
    private const int AlphaSteps = 8;
    private const float MaxAlpha = 0.8f;
    private static readonly Color GasColor = new Color(0.45f, 0.29f, 0.11f);

    private byte[] density;

    [Unsaved(false)]
    private List<int> activeCells = new List<int>();

    [Unsaved(false)]
    private List<IntVec3> cardinalDirections = new List<IntVec3>(GenAdj.CardinalDirections);

    private static Material[] gasMats;
    private static List<Pawn> tmpPawns = new List<Pawn>();

    public SkunkGasGrid(Map map)
        : base(map)
    {
    }

    public bool AnyGas => activeCells.Count > 0;

    public override void FinalizeInit()
    {
        EnsureGrid();
        activeCells.Clear();
        for (int i = 0; i < density.Length; i++)
        {
            if (density[i] != 0)
            {
                activeCells.Add(i);
            }
        }
    }

    private void EnsureGrid()
    {
        int numCells = map.cellIndices.NumGridCells;
        if (density == null || density.Length != numCells)
        {
            density = new byte[numCells];
        }
    }

    public byte DensityAt(IntVec3 cell)
    {
        if (density == null || !cell.InBounds(map))
        {
            return 0;
        }
        return density[map.cellIndices.CellToIndex(cell)];
    }

    public void AddGas(IntVec3 cell, int amount, bool canOverflow = true)
    {
        if (amount <= 0 || !map.gasGrid.GasCanMoveTo(cell))
        {
            return;
        }
        EnsureGrid();
        int index = map.cellIndices.CellToIndex(cell);
        int newDensity = density[index] + amount;
        SetDensity(index, Mathf.Min(newDensity, MaxGasPerCell));
        if (canOverflow && newDensity > MaxGasPerCell)
        {
            Overflow(cell, newDensity - MaxGasPerCell);
        }
    }

    private void SetDensity(int index, int value)
    {
        if (density[index] == 0 && value > 0)
        {
            activeCells.Add(index);
        }
        density[index] = (byte)value;
    }

    private void Overflow(IntVec3 cell, int amount)
    {
        int remainingAmount = amount;
        map.floodFiller.FloodFill(cell, map.gasGrid.GasCanMoveTo, delegate(IntVec3 c)
        {
            int num = Mathf.Min(remainingAmount, MaxGasPerCell - DensityAt(c));
            if (num > 0)
            {
                AddGas(c, num, canOverflow: false);
                remainingAmount -= num;
            }
            return remainingAmount <= 0;
        }, GenRadial.NumCellsInRadius(MaxOverflowFloodfillRadius), rememberParents: true);
    }

    public override void MapComponentTick()
    {
        if (!AnyGas)
        {
            return;
        }
        int ticksGame = Find.TickManager.TicksGame;
        if (ticksGame % DiffusionIntervalTicks == 0)
        {
            Diffuse();
        }
        if (ticksGame % DissipationIntervalTicks == 0)
        {
            Dissipate();
        }
        if (ticksGame % PawnEffectIntervalTicks == 0)
        {
            AffectPawns();
        }
    }

    private void Diffuse()
    {
        // Cells that gain gas during this pass are appended and wait for the next one.
        int count = activeCells.Count;
        for (int i = 0; i < count; i++)
        {
            int index = activeCells[i];
            int gasA = density[index];
            if (gasA < MinDiffusion)
            {
                continue;
            }
            IntVec3 cell = map.cellIndices.IndexToCell(index);
            cardinalDirections.Shuffle();
            for (int j = 0; j < cardinalDirections.Count; j++)
            {
                IntVec3 neighbor = cell + cardinalDirections[j];
                if (!map.gasGrid.GasCanMoveTo(neighbor))
                {
                    continue;
                }
                int neighborIndex = map.cellIndices.CellToIndex(neighbor);
                int gasB = density[neighborIndex];
                int num = (gasA - gasB) / 2;
                if (num >= MinDiffusion)
                {
                    gasA -= num;
                    SetDensity(neighborIndex, gasB + num);
                    if (gasA < MinDiffusion)
                    {
                        break;
                    }
                }
            }
            density[index] = (byte)gasA;
        }
    }

    private void Dissipate()
    {
        for (int i = activeCells.Count - 1; i >= 0; i--)
        {
            int index = activeCells[i];
            IntVec3 cell = map.cellIndices.IndexToCell(index);
            int remaining = 0;
            // Gas caught inside something solid (a wall built over it, a closed door) is gone.
            if (map.gasGrid.GasCanMoveTo(cell))
            {
                remaining = density[index] - (cell.Roofed(map) ? DissipationAmountRoofed : DissipationAmount);
            }
            if (remaining <= 0)
            {
                density[index] = 0;
                activeCells.RemoveAt(i);
            }
            else
            {
                density[index] = (byte)remaining;
            }
        }
    }

    private void AffectPawns()
    {
        tmpPawns.Clear();
        tmpPawns.AddRange(map.mapPawns.AllPawnsSpawned);
        for (int i = 0; i < tmpPawns.Count; i++)
        {
            Pawn pawn = tmpPawns[i];
            if (!pawn.Spawned)
            {
                continue;
            }
            byte gas = DensityAt(pawn.Position);
            if (gas > 0)
            {
                SkunkStenchUtility.DoStenchEffects(pawn, (float)gas / MaxGasPerCell);
            }
        }
        tmpPawns.Clear();
    }

    public override void MapComponentDraw()
    {
        if (!AnyGas || !DebugViewSettings.drawGas)
        {
            return;
        }
        CellRect viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(2);
        float time = RealTime.UnpausedRealTime;
        float altitude = AltitudeLayer.Gas.AltitudeFor();
        for (int i = 0; i < activeCells.Count; i++)
        {
            int index = activeCells[i];
            IntVec3 cell = map.cellIndices.IndexToCell(index);
            if (!viewRect.Contains(cell) || map.fogGrid.IsFogged(index))
            {
                continue;
            }
            // Seeded per cell so each cloud keeps its own size, offset and spin between frames.
            Rand.PushState(index);
            float size = 1f + 2f * Rand.Range(0.4f, 0.6f);
            Vector3 pos = cell.ToVector3Shifted();
            pos.x += Rand.Range(-0.2f, 0.2f);
            pos.z += Rand.Range(-0.2f, 0.2f);
            pos.y = altitude + Rand.Range(-0.01f, 0.01f);
            float angle = Rand.Range(0f, 360f) + time * Rand.Range(8f, 20f) * (Rand.Bool ? 1f : -1f);
            Rand.PopState();
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, matrix, MaterialFor(density[index]), 0);
        }
    }

    // Opacity follows density. One material per opacity step, tinted through the material
    // colour, which every map shader honours.
    private static Material MaterialFor(byte gas)
    {
        if (gasMats == null)
        {
            gasMats = new Material[AlphaSteps];
            for (int i = 0; i < AlphaSteps; i++)
            {
                Color color = GasColor;
                color.a = MaxAlpha * (i + 1) / AlphaSteps;
                gasMats[i] = MaterialPool.MatFrom(GasTexPath, ShaderDatabase.Transparent, color, 3000);
            }
        }
        return gasMats[gas * (AlphaSteps - 1) / MaxGasPerCell];
    }

    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !AnyGas)
        {
            return;
        }
        DataExposeUtility.LookByteArray(ref density, "skunkGasDensity");
    }
}
