namespace Critters
{
    // Every collectible critter species. Explicit, append-only values - the save file stores
    // these as ints (Persistence.CritterSaveData), so reordering or reusing a value silently
    // remaps every saved jar/collection entry.
    public enum CritterId
    {
        None = 0,
        GlowFirefly = 1,
        DustBunny = 2,
        PebbleBeetle = 3,
        CaveMoth = 4,
        DripSnail = 5,
        RockHopper = 6,
        CrystalBat = 7,
        LanternJelly = 8,
        OreMole = 9,
        SparkSalamander = 10,
        GemCrab = 11,
        MossSprite = 12,
        MagmaSlug = 13,
        VoidWisp = 14,
        EchoSerpent = 15,
        AncientTrilobite = 16,
    }

    // Automaton hats, unlocked by collecting critter species (HatDefinition.UnlockAtSpeciesCount).
    // Same append-only rule as CritterId - per-automaton hat choices are saved as these ints.
    public enum HatId
    {
        None = 0,
        HardHat = 1,
        PropellerBeanie = 2,
        TopHat = 3,
        WizardHat = 4,
        Crown = 5,
    }
}
