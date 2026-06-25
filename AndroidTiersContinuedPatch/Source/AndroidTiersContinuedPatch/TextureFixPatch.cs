using System;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;

namespace AndroidTiersContinuedPatch
{
    [StaticConstructorOnStartup]
    public static class TextureFixPatch
    {
        static TextureFixPatch()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                int fixCount = 0;
                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
                {
                    if (def.graphicData == null || string.IsNullOrEmpty(def.graphicData.texPath)) continue;

                    string texPathLower = def.graphicData.texPath.ToLower();
                    bool isTargetAnimal = texPathLower.Contains("things/pawn/");
                    bool isTargetDroid = texPathLower.Contains("things/droids/");

                    if (isTargetAnimal)
                    {
                        if (string.IsNullOrEmpty(def.uiIconPath))
                        {
                            string path = def.graphicData.texPath + "_east";
                            Texture2D tex = ContentFinder<Texture2D>.Get(path, false);
                            if (tex != null)
                            {
                                def.uiIconPath = path;
                                def.uiIcon = tex;
                                fixCount++;
                            }
                        }
                    }
                    else if (isTargetDroid)
                    {
                        if (string.IsNullOrEmpty(def.uiIconPath))
                        {
                            string path = def.graphicData.texPath + "_south";
                            Texture2D tex = ContentFinder<Texture2D>.Get(path, false);
                            if (tex != null)
                            {
                                def.uiIconPath = path;
                                def.uiIcon = tex;
                                fixCount++;
                            }
                        }
                    }
                }
                
                // Removed bad recipe loop

                if (fixCount > 0)
                {
                    Log.Message(string.Format("[AndroidTiersContinuedPatch] Successfully mapped {0} missing UI textures for Droids/Animals.", fixCount));
                }
            });
        }
    }
}
