namespace PlanetaryAnomalies
{
    /// <summary>
    /// One planet, one recipe, one output multiplier. Stage 0 has exactly one of these and
    /// deliberately no framework around it.
    /// </summary>
    internal sealed class PlanetAnomaly
    {
        /// <summary>Planet the anomaly applies to. Stage 0: always the home/birth planet.</summary>
        internal readonly int PlanetId;

        /// <summary>Recipe the anomaly applies to.</summary>
        internal readonly int RecipeId;

        /// <summary>Output multiplier. Stage 0: 10, chosen to be unmistakable in the output slot.</summary>
        internal readonly int OutputMultiplier;

        /// <summary>
        /// A private copy of the recipe's execute data with the product counts already
        /// multiplied. This is never the instance held in the static
        /// <c>RecipeProto.recipeExecuteData</c> dictionary, and none of its arrays are shared
        /// with it -- assemblers on other planets must keep seeing vanilla data.
        /// </summary>
        internal readonly RecipeExecuteData AnomalousExecuteData;

        internal PlanetAnomaly(int planetId, int recipeId, int outputMultiplier, RecipeExecuteData anomalousExecuteData)
        {
            PlanetId = planetId;
            RecipeId = recipeId;
            OutputMultiplier = outputMultiplier;
            AnomalousExecuteData = anomalousExecuteData;
        }
    }
}
