using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace QuarryCo
{
    public class Building_Quarry : Building
    {
        private bool active = true;

        public bool IsActive { get { return active && !ForbidUtility.IsForbidden(this, Faction.OfPlayer) && Map != null; } }

        public QuarrySize Size
        {
            get
            {
                if (def.size.x >= 15) return QuarrySize.Omega;
                if (def.size.x >= 13) return QuarrySize.Titan;
                if (def.size.x >= 11) return QuarrySize.Colossal;
                if (def.size.x >= 9) return QuarrySize.Grand;
                if (def.size.x >= 7) return QuarrySize.Large;
                if (def.size.x >= 5) return QuarrySize.Medium;
                return QuarrySize.Small;
            }
        }

        public int MaxWorkers { get { return Mathf.Max(1, Mathf.RoundToInt(def.size.x * def.size.z * QuarryCoMod.Settings.WorkersPerTile)); } }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref active, "active", true);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos())
                yield return g;

            yield return new Command_Toggle
            {
                defaultLabel = "Quarrying",
                defaultDesc = "Toggle whether pawns should quarry here.",
                isActive = () => active,
                toggleAction = () => active = !active,
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Mine")
            };
        }

        public void SpawnRandomResource(Pawn worker)
        {
            ThingDef resourceDef;
            int amount;
            PickResource(out resourceDef, out amount);

            if (resourceDef == null)
                return;

            Thing thing = ThingMaker.MakeThing(resourceDef);
            thing.stackCount = amount;

            GenPlace.TryPlaceThing(thing, worker.Position, Map, ThingPlaceMode.Near);

            if (QuarryCoMod.Settings.AutoHaul && thing.Spawned && thing.def.EverHaulable)
            {
                thing.SetForbidden(false, false);
                if (Map.designationManager.DesignationOn(thing, DesignationDefOf.Haul) == null)
                    Map.designationManager.AddDesignation(new Designation(thing, DesignationDefOf.Haul));
            }
        }

        private void PickResource(out ThingDef def, out int amount)
        {
            float roll = Rand.Value;

            if (Size == QuarrySize.Omega)
            {
                // Omega: stone 0%, steel 45%, gold 10%, jade 10%, plasteel 10%, uranium 10%, components 10%, silver 5%
                if (roll < 0.45f)
                {
                    def = ThingDefOf.Steel;
                    amount = Rand.RangeInclusive(50, 100);
                    return;
                }
                if (roll < 0.55f)
                {
                    def = ThingDefOf.Gold;
                    amount = Rand.RangeInclusive(12, 25);
                    return;
                }
                if (roll < 0.65f)
                {
                    def = ThingDefOf.Jade;
                    amount = Rand.RangeInclusive(12, 25);
                    return;
                }
                if (roll < 0.75f)
                {
                    def = ThingDefOf.Plasteel;
                    amount = Rand.RangeInclusive(15, 35);
                    return;
                }
                if (roll < 0.85f)
                {
                    def = ThingDefOf.Uranium;
                    amount = Rand.RangeInclusive(12, 22);
                    return;
                }
                if (roll < 0.95f)
                {
                    def = ThingDefOf.ComponentIndustrial;
                    amount = Rand.RangeInclusive(8, 15);
                    return;
                }
                if (roll <= 1.0f)
                {
                    def = ThingDefOf.Silver;
                    amount = Rand.RangeInclusive(40, 80);
                    return;
                }
            }
            else if (Size == QuarrySize.Titan)
            {
                // Titan: stone 2%, steel 40%, slag 2%, silver 6%, gold 10%, jade 10%, plasteel 10%, uranium 10%, components 10%
                if (roll < 0.40f)
                {
                    def = ThingDefOf.Steel;
                    amount = Rand.RangeInclusive(40, 80);
                    return;
                }
                if (roll < 0.50f)
                {
                    def = ThingDefOf.Gold;
                    amount = Rand.RangeInclusive(8, 20);
                    return;
                }
                if (roll < 0.60f)
                {
                    def = ThingDefOf.Jade;
                    amount = Rand.RangeInclusive(8, 20);
                    return;
                }
                if (roll < 0.70f)
                {
                    def = ThingDefOf.Plasteel;
                    amount = Rand.RangeInclusive(12, 25);
                    return;
                }
                if (roll < 0.80f)
                {
                    def = ThingDefOf.Uranium;
                    amount = Rand.RangeInclusive(10, 18);
                    return;
                }
                if (roll < 0.90f)
                {
                    def = ThingDefOf.ComponentIndustrial;
                    amount = Rand.RangeInclusive(5, 10);
                    return;
                }
                if (roll < 0.96f)
                {
                    def = ThingDefOf.Silver;
                    amount = Rand.RangeInclusive(30, 60);
                    return;
                }
                if (roll < 0.98f)
                {
                    def = ThingDefOf.ChunkSlagSteel;
                    amount = 3;
                    return;
                }
            }
            else if (Size == QuarrySize.Colossal)
            {
                // Colossal: stone 5%, steel 35%, slag 4%, silver 8%, gold 10%, jade 10%, plasteel 10%, uranium 10%, components 8%
                if (roll < 0.35f)
                {
                    def = ThingDefOf.Steel;
                    amount = Rand.RangeInclusive(20, 40);
                    return;
                }
                if (roll < 0.45f)
                {
                    def = ThingDefOf.Gold;
                    amount = Rand.RangeInclusive(5, 12);
                    return;
                }
                if (roll < 0.55f)
                {
                    def = ThingDefOf.Jade;
                    amount = Rand.RangeInclusive(5, 12);
                    return;
                }
                if (roll < 0.65f)
                {
                    def = ThingDefOf.Plasteel;
                    amount = Rand.RangeInclusive(8, 15);
                    return;
                }
                if (roll < 0.75f)
                {
                    def = ThingDefOf.Uranium;
                    amount = Rand.RangeInclusive(6, 12);
                    return;
                }
                if (roll < 0.83f)
                {
                    def = ThingDefOf.ComponentIndustrial;
                    amount = Rand.RangeInclusive(2, 5);
                    return;
                }
                if (roll < 0.91f)
                {
                    def = ThingDefOf.Silver;
                    amount = Rand.RangeInclusive(15, 30);
                    return;
                }
                if (roll < 0.95f)
                {
                    def = ThingDefOf.ChunkSlagSteel;
                    amount = 2;
                    return;
                }
            }
            else if (Size == QuarrySize.Grand)
            {
                // Grand: stone 25%, steel 20%, slag 10%, silver 9%, gold 9%, jade 9%, plasteel 7%, uranium 6%, components 5%
                if (roll < 0.09f)
                {
                    def = ThingDefOf.Gold;
                    amount = Rand.RangeInclusive(2, 5);
                    return;
                }
                if (roll < 0.18f)
                {
                    def = ThingDefOf.Jade;
                    amount = Rand.RangeInclusive(2, 5);
                    return;
                }
                if (roll < 0.25f)
                {
                    def = ThingDefOf.Plasteel;
                    amount = Rand.RangeInclusive(3, 8);
                    return;
                }
                if (roll < 0.31f)
                {
                    def = ThingDefOf.Uranium;
                    amount = Rand.RangeInclusive(3, 6);
                    return;
                }
                if (roll < 0.36f)
                {
                    def = ThingDefOf.ComponentIndustrial;
                    amount = Rand.RangeInclusive(1, 2);
                    return;
                }
                if (roll < 0.45f)
                {
                    def = ThingDefOf.Silver;
                    amount = Rand.RangeInclusive(5, 12);
                    return;
                }
                if (roll < 0.65f)
                {
                    def = ThingDefOf.Steel;
                    amount = Rand.RangeInclusive(8, 20);
                    return;
                }
                if (roll < 0.75f)
                {
                    def = ThingDefOf.ChunkSlagSteel;
                    amount = 1;
                    return;
                }
            }
            else if (Size == QuarrySize.Large)
            {
                // Large: stone 35%, steel 20%, slag 10%, silver 8%, gold 7%, jade 7%, plasteel 5%, uranium 4%, components 4%
                if (roll < 0.07f)
                {
                    def = ThingDefOf.Gold;
                    amount = Rand.RangeInclusive(1, 3);
                    return;
                }
                if (roll < 0.14f)
                {
                    def = ThingDefOf.Jade;
                    amount = Rand.RangeInclusive(1, 3);
                    return;
                }
                if (roll < 0.19f)
                {
                    def = ThingDefOf.Plasteel;
                    amount = Rand.RangeInclusive(2, 5);
                    return;
                }
                if (roll < 0.23f)
                {
                    def = ThingDefOf.Uranium;
                    amount = Rand.RangeInclusive(2, 4);
                    return;
                }
                if (roll < 0.27f)
                {
                    def = ThingDefOf.ComponentIndustrial;
                    amount = 1;
                    return;
                }
                if (roll < 0.35f)
                {
                    def = ThingDefOf.Silver;
                    amount = Rand.RangeInclusive(3, 8);
                    return;
                }
                if (roll < 0.55f)
                {
                    def = ThingDefOf.Steel;
                    amount = Rand.RangeInclusive(5, 15);
                    return;
                }
                if (roll < 0.65f)
                {
                    def = ThingDefOf.ChunkSlagSteel;
                    amount = 1;
                    return;
                }
            }
            else if (Size == QuarrySize.Medium)
            {
                // Medium: stone 40%, steel 20%, slag 15%, silver 8%, gold 5%, plasteel 4%, uranium 3%, components 2%, jade 3%
                if (roll < 0.05f)
                {
                    def = ThingDefOf.Gold;
                    amount = Rand.RangeInclusive(1, 2);
                    return;
                }
                if (roll < 0.08f)
                {
                    def = ThingDefOf.Jade;
                    amount = Rand.RangeInclusive(1, 2);
                    return;
                }
                if (roll < 0.12f)
                {
                    def = ThingDefOf.Plasteel;
                    amount = Rand.RangeInclusive(1, 3);
                    return;
                }
                if (roll < 0.15f)
                {
                    def = ThingDefOf.Uranium;
                    amount = Rand.RangeInclusive(1, 3);
                    return;
                }
                if (roll < 0.17f)
                {
                    def = ThingDefOf.ComponentIndustrial;
                    amount = 1;
                    return;
                }
                if (roll < 0.25f)
                {
                    def = ThingDefOf.Silver;
                    amount = Rand.RangeInclusive(2, 5);
                    return;
                }
                if (roll < 0.45f)
                {
                    def = ThingDefOf.Steel;
                    amount = Rand.RangeInclusive(3, 10);
                    return;
                }
                if (roll < 0.60f)
                {
                    def = ThingDefOf.ChunkSlagSteel;
                    amount = 1;
                    return;
                }
            }
            else
            {
                // Small: stone 50%, steel 20%, slag 15%, silver 5%, steel chunks leftover
                if (roll < 0.05f)
                {
                    def = ThingDefOf.Silver;
                    amount = Rand.RangeInclusive(1, 3);
                    return;
                }
                if (roll < 0.25f)
                {
                    def = ThingDefOf.Steel;
                    amount = Rand.RangeInclusive(2, 8);
                    return;
                }
                if (roll < 0.40f)
                {
                    def = ThingDefOf.ChunkSlagSteel;
                    amount = 1;
                    return;
                }
                if (roll < 0.50f)
                {
                    def = ThingDefOf.ComponentIndustrial;
                    amount = 1;
                    return;
                }
            }

            def = RandomStoneChunk();
            amount = 1;
        }

        private ThingDef RandomStoneChunk()
        {
            var chunks = new List<ThingDef>();
            foreach (var td in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (td.IsNonResourceNaturalRock && td.building != null && td.building.mineableThing != null)
                    chunks.Add(td.building.mineableThing);
            }

            if (chunks.Count > 0)
                return chunks.RandomElement();

            return ThingDef.Named("ChunkSandstone");
        }
    }

    public enum QuarrySize
    {
        Small,
        Medium,
        Large,
        Grand,
        Colossal,
        Titan,
        Omega
    }
}
