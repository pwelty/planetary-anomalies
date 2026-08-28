using System;
using System.Text;
using PlanetaryAnomalies;

// Compiled together with src/AnomalyMath.cs by scripts/verify.ps1. Prints the generator's output
// for a fixed set of inputs, which is compared against tests/golden-generator.txt.
//
// The recipe pool here is synthetic and fixed on purpose. The point is to lock the *arithmetic* --
// density, presence, selection -- not the contents of DSP's recipe database, which legitimately
// varies with the game version and other mods.
internal static class GoldenRunner
{
    public static void Main()
    {
        int[] pool = new int[147];
        for (int i = 0; i < pool.Length; i++)
        {
            // Deliberately not 1..147: sparse, irregular ids resemble real proto ids and would
            // expose any accidental dependence on a recipe's position in the list.
            pool[i] = 1 + i * 3 + (i % 7);
        }

        int[] seeds = { 0, 1, 42, 3664027, 22135963, 40078654, 78535137, 2147483647, -1 };
        int[] planets = { 101, 102, 103, 104, 201, 301, 1201, 1704, 3602, 5406, 6401 };

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# Generator golden output. Regenerate ONLY with a deliberate AnomalySystemVersion bump.");
        sb.AppendLine("# Format: seed planetId version -> density anomalous recipeId");

        foreach (int version in new[] { 1, 2 })
        {
            foreach (int seed in seeds)
            {
                int density = AnomalyMath.DensityFor(seed, version);
                foreach (int planet in planets)
                {
                    bool anomalous = AnomalyMath.IsAnomalous(seed, planet, version, density);
                    int recipe = anomalous ? AnomalyMath.ChooseRecipeId(seed, planet, version, pool) : -1;
                    sb.AppendLine(string.Format("{0} {1} {2} -> {3} {4} {5}",
                        seed, planet, version, density, anomalous ? "yes" : "no", recipe));
                }
            }
        }

        Console.Write(sb.ToString());
    }
}
