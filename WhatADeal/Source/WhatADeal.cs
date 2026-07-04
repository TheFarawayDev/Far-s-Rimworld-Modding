using RimWorld;
using Verse;
using System.Reflection;

namespace WhatADeal
{
    [StaticConstructorOnStartup]
    public static class WhatADealMod
    {
        static WhatADealMod()
        {
            int modifiedCount = 0;
            
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.category == ThingCategory.Item || def.category == ThingCategory.Building || def.category == ThingCategory.Pawn)
                {
                    if (def.tradeability == Tradeability.None)
                    {
                        def.tradeability = Tradeability.Sellable;
                        modifiedCount++;
                    }
                    else if (def.tradeability == Tradeability.Buyable)
                    {
                        def.tradeability = Tradeability.All;
                        modifiedCount++;
                    }
                    
                    // Make chunks very cheap
                    if (def.thingCategories != null)
                    {
                        foreach (var cat in def.thingCategories)
                        {
                            if (cat.defName == "StoneChunks" || cat.defName == "Chunks")
                            {
                                def.SetStatBaseValue(StatDefOf.MarketValue, 0.1f);
                                break;
                            }
                        }
                    }

                    // Dynamically assign trade tags so traders know to buy them
                    if (def.tradeTags == null)
                    {
                        def.tradeTags = new System.Collections.Generic.List<string>();
                    }

                    if (def.IsWeapon)
                    {
                        if (def.IsRangedWeapon && !def.tradeTags.Contains("WeaponRanged"))
                            def.tradeTags.Add("WeaponRanged");
                        if (def.IsMeleeWeapon && !def.tradeTags.Contains("WeaponMelee"))
                            def.tradeTags.Add("WeaponMelee");
                    }
                    else if (def.IsApparel)
                    {
                        if (!def.tradeTags.Contains("Apparel"))
                            def.tradeTags.Add("Apparel");
                        if (!def.tradeTags.Contains("Armor")) // Combat suppliers buy armor
                            def.tradeTags.Add("Armor");
                    }
                }
            }
            
            // Patch traders to buy un-tagged categories like chunks, unfinished, and corpses
            int tradersPatched = 0;
            ThingCategoryDef stoneChunks = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("StoneChunks");
            ThingCategoryDef chunks = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Chunks");
            ThingCategoryDef unfinished = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Unfinished");
            ThingCategoryDef corpsesAnimal = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("CorpsesAnimal");
            ThingCategoryDef corpsesMech = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("CorpsesMechanoid");

            FieldInfo catDefField = typeof(StockGenerator_Category).GetField("categoryDef", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (catDefField != null)
            {
                foreach (TraderKindDef trader in DefDatabase<TraderKindDef>.AllDefsListForReading)
                {
                    bool buysRaw = false;
                    foreach (StockGenerator gen in trader.stockGenerators)
                    {
                        StockGenerator_Category catGen = gen as StockGenerator_Category;
                        if (catGen != null)
                        {
                            ThingCategoryDef catDef = catDefField.GetValue(catGen) as ThingCategoryDef;
                            if (catDef != null && catDef.defName == "ResourcesRaw")
                            {
                                buysRaw = true;
                                break;
                            }
                        }
                    }

                    if (buysRaw)
                    {
                        if (stoneChunks != null) 
                        {
                            StockGenerator_Category genStone = new StockGenerator_Category();
                            catDefField.SetValue(genStone, stoneChunks);
                            trader.stockGenerators.Add(genStone);
                        }
                        if (chunks != null) 
                        {
                            StockGenerator_Category genChunks = new StockGenerator_Category();
                            catDefField.SetValue(genChunks, chunks);
                            trader.stockGenerators.Add(genChunks);
                        }
                        if (unfinished != null) 
                        {
                            StockGenerator_Category genUnf = new StockGenerator_Category();
                            catDefField.SetValue(genUnf, unfinished);
                            trader.stockGenerators.Add(genUnf);
                        }
                        if (corpsesAnimal != null) 
                        {
                            StockGenerator_Category genCA = new StockGenerator_Category();
                            catDefField.SetValue(genCA, corpsesAnimal);
                            trader.stockGenerators.Add(genCA);
                        }
                        if (corpsesMech != null) 
                        {
                            StockGenerator_Category genCM = new StockGenerator_Category();
                            catDefField.SetValue(genCM, corpsesMech);
                            trader.stockGenerators.Add(genCM);
                        }
                        tradersPatched++;
                    }
                }
            }
            else
            {
                Log.Error("[What A Deal!] Could not find categoryDef field on StockGenerator_Category via reflection.");
            }
            
            Log.Message(string.Format("[What A Deal!] Initialized. Made {0} items sellable. Patched {1} traders.", modifiedCount, tradersPatched));
        }
    }
}
