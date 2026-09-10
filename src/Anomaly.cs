namespace PlanetaryAnomalies
{
    /// <summary>
    /// What a surface may say about an anomaly, once discovery and research are both accounted for.
    ///
    /// Three states rather than two because 0.4 proved two were not enough. Hiding an anomaly whose
    /// recipe is unresearched removed the noise, but it also removed the reason to go and look --
    /// and the first galaxy to run it hid 31 anomalies that were overwhelmingly military, from a
    /// player who had just discovered that using anomalies costs a war. Existence turned out to be
    /// cheap information; the name is the part that means nothing before the research lands.
    /// </summary>
    internal enum AnomalyVisibility
    {
        /// <summary>Say nothing at all: no anomaly, or one the player has chosen not to be told about.</summary>
        None,

        /// <summary>Mark the place, do not name the recipe. Something is here; come back.</summary>
        Marker,

        /// <summary>Name it, with the multiplier where there is room.</summary>
        Full
    }


    /// <summary>How much an anomaly says about itself before its recipe has been researched.</summary>
    internal enum UnresearchedDisplay
    {
        /// <summary>Nothing. The planet reads as ordinary. The 0.4 behaviour, and the default.</summary>
        Hide,

        /// <summary>The symbol without the name: somewhere to look, nothing to read yet.</summary>
        Marker,

        /// <summary>Everything, as in 0.3 and earlier.</summary>
        Show
    }

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
