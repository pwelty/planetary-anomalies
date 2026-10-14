using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Lets a lab on a cube world actually make its anomaly.
    ///
    /// Found in play on the first cube world tried: a lab on Sadira I, a Gravity Matrix world, never
    /// exported a single matrix, while the same lab at home worked. LabComponent.InternalUpdateAssemble
    /// refuses to start a craft when its output buffer would pass a fixed limit:
    ///
    ///     produced + productCount > 10 * ((speedOverride + 9999) / 10000)
    ///
    /// Assemblers scale their limit with the recipe's output; labs use the literal 10. Gravity Matrix
    /// makes 2 a craft, so 20 under the anomaly -- over the limit with an empty buffer, so the lab never
    /// starts. Universe Matrix makes 1, so 10 -- exactly at the limit, so it runs, but only when the
    /// buffer is completely empty, a batch at a time ("those work. I think").
    ///
    /// The limit is a constant inside the method, so there is no seam but the instruction itself:
    /// this transpiler replaces that 10 with LabOutputCap(recipeExecuteData) -- 10, or twice the
    /// largest output per craft if that is more. No vanilla matrix makes more than 2 a craft, so every
    /// ordinary lab still gets exactly 10. It reads only the lab's own data, so it is safe on the worker
    /// threads lab production runs on.
    ///
    /// If the pattern is not found exactly twice -- one check per output shape -- nothing is changed and
    /// the log says so; verify.ps1 asserts the pattern before a build ships.
    /// </summary>
    internal static class LabOutputCapPatch
    {
        private const int VanillaCap = 10;

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(LabComponent), "InternalUpdateAssemble")]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            FieldInfo speedOverride = AccessTools.Field(typeof(LabComponent), "speedOverride");
            FieldInfo executeData = AccessTools.Field(typeof(LabComponent), "recipeExecuteData");
            MethodInfo cap = AccessTools.Method(typeof(LabOutputCapPatch), "LabOutputCap");

            List<int> sites = new List<int>();
            for (int i = 0; i + 2 < codes.Count; i++)
            {
                if (IsTen(codes[i]) &&
                    codes[i + 1].opcode == OpCodes.Ldarg_0 &&
                    codes[i + 2].opcode == OpCodes.Ldfld && Equals(codes[i + 2].operand, speedOverride))
                {
                    sites.Add(i);
                }
            }

            if (sites.Count != 2 || speedOverride == null || executeData == null || cap == null)
            {
                Plugin.Log.LogWarning("Lab output limit: expected the limit check twice in " +
                                      "LabComponent.InternalUpdateAssemble, found " + sites.Count +
                                      ". Left unchanged, so labs on cube worlds whose matrix makes more than " +
                                      "one per craft will not start.");
                return codes;
            }

            // Back to front, so inserting does not move the sites still to come. The 10 becomes
            // ldarg.0 in place, keeping any branch label on it.
            for (int s = sites.Count - 1; s >= 0; s--)
            {
                int i = sites[s];
                codes[i].opcode = OpCodes.Ldarg_0;
                codes[i].operand = null;
                codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldfld, executeData));
                codes.Insert(i + 2, new CodeInstruction(OpCodes.Call, cap));
            }

            return codes;
        }

        /// <summary>10, or twice the largest output per craft if that is more.</summary>
        internal static int LabOutputCap(RecipeExecuteData data)
        {
            int limit = VanillaCap;
            if (data != null && data.productCounts != null)
            {
                for (int i = 0; i < data.productCounts.Length; i++)
                {
                    int room = data.productCounts[i] * 2;
                    if (room > limit)
                    {
                        limit = room;
                    }
                }
            }

            return limit;
        }

        private static bool IsTen(CodeInstruction code)
        {
            if (code.opcode != OpCodes.Ldc_I4_S && code.opcode != OpCodes.Ldc_I4)
            {
                return false;
            }

            try
            {
                return Convert.ToInt32(code.operand) == VanillaCap;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
