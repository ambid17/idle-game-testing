namespace Audio
{
    // Every sound effect the game can request. Code asks AudioService for a SoundId, and the
    // SoundLibrary asset maps it to the actual clip(s), so swapping or adding audio never touches
    // gameplay code. Values are explicit and append-only: SoundLibrary serializes these as ints,
    // so reordering or reusing a value silently remaps every existing library entry.
    public enum SoundId
    {
        None = 0,

        // Mining
        MiningHit = 1,
        MineDirt = 2,
        MineOre = 3,
        ArtifactFound = 4,
        PowerUpCollected = 5,

        // Player
        PlayerHurt = 20,
        ShieldBlock = 21,
        PlayerDeath = 22,
        PlayerRevive = 23,
        Jetpack = 24,
        Warning = 25,

        // Hazards
        ExplosiveFuse = 40,
        Explosion = 41,
        RockRumble = 42,
        RockLand = 43,
        GasRelease = 44,
        LavaSizzle = 45,

        // Economy / progression
        Sell = 60,
        UpgradePurchased = 61,
        PrestigeUpgradeQueued = 62,
        Prestige = 63,
        ProcessingStarted = 64,
        ProcessingCompleted = 65,

        // UI
        UIClick = 80,
        UIHover = 81,
    }
}
