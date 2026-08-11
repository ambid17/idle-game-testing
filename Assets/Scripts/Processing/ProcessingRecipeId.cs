namespace Processing
{
    // Base-tier crafted goods per Assets/Docs/processingImplementation.md - one recipe per ore
    // type except Coal, which has no recipe of its own. Prestige-only recipes (Steel, Tiara,
    // Crown, Shard of Possibility) are a follow-up phase and get appended here later, never
    // inserted, since this enum serializes as an int on ProcessingRecipeDefinition/save data.
    public enum ProcessingRecipeId : byte
    {
        Chairs,
        Pillars,
        Swords,
        Bracelets,
        Earrings,
        WeddingRings
    }
}
