using System;
using UnityEngine;

namespace Story
{
    // One pool of lines per StoryStage (index = (int)StoryStage). Use | for multiple dialog pages.
    [Serializable]
    public class StageLines
    {
        [TextArea(2, 5)]
        public string[] Lines = Array.Empty<string>();
    }

    // What the player reads the first time they examine a Seal Chamber. Use | for multiple pages.
    [Serializable]
    public class ChamberText
    {
        [Tooltip("Narration (no speaker) describing the room and its mural.")]
        [TextArea(2, 5)]
        public string Narration;
        [Tooltip("What the Bound says afterwards. Empty = it stays silent.")]
        [TextArea(2, 5)]
        public string Voice;
    }

    // Every line of the Seals story (GameDesignDoc "# Story & Endgame: The Seals") that isn't the
    // curator's or shopkeeper's everyday chatter: the Bound's whispers, the curator's and critter
    // keeper's stage lines, the Retranslation, chamber murals, the choice prompts and both
    // endings. Field defaults are the shipped script, so a freshly created asset is ready to use.
    [CreateAssetMenu(fileName = "StoryContent", menuName = "Story/Story Content")]
    public class StoryContent : ScriptableObject
    {
        public const char PageSeparator = '|';

        [Header("The Bound")]
        [Tooltip("Speaker name before the Retranslation.")]
        public string UnknownVoiceName = "???";
        public string VoiceName = "The Bound";
        public Color VoiceColor = new(0.78f, 0.49f, 1f, 1f);
        [Tooltip("Shown beside everything the Bound says.")]
        public Sprite VoicePortrait;
        [Tooltip("Index = layer index. The Bound says it once, the first time the player mines on that layer. Use | for multiple pages. Empty = silent.")]
        [TextArea(2, 4)]
        public string[] LayerWhispers =
        {
            "",
            "",
            "",
            "...digging. Something is digging.",
            "I hear your little engine. It has been so long since anything came down.",
            "The tablets you carry up to him. Do you know what they were holding shut?",
            "Your scholar reads my jailers' words and calls them warnings. Ask him who wrote them.",
            "The glow in the rock is mine. They mined me first. You are only finishing their work.",
            "Every time his machine sings, I can move a little more. Thank him for me.",
            "Close now. I can feel the heat of your drill. Do not be afraid of me.",
            "One door left. He will tell you to lock it. Ask yourself which of us has lied to you less.",
        };
        [Tooltip("Said right after a Keystone is taken.")]
        [TextArea(2, 4)]
        public string[] KeystoneTakenWhispers =
        {
            "Thank you.",
            "Lighter. Oh, that is lighter.",
            "It is only a stone. They were all only stones.",
        };

        [Header("Curator (Museum.MuseumCuratorController)")]
        [Tooltip("Collection tab greeting, one picked at random. Empty stage = the curator's ordinary greetings. {0} = Seals still holding.")]
        public StageLines[] CuratorGreetings =
        {
            new(),
            new() { Lines = new[]
            {
                "Welcome back! Did you feel that little shake? Perfectly normal. Mines settle. So I'm told. By me.",
                "You've been deeper! I can smell it. Old air. Lovely. Slightly ominous. Mostly lovely.",
                "The Resonator has been humming on its own again. I've asked it to stop. It has not.",
            } },
            new() { Lines = new[]
            {
                "Oh. It's you. Good. Please don't read the old labels. I've put the true ones on top.",
                "I've stopped dusting the Resonator. I don't like standing near it.",
                "Every tablet you brought me was holding something shut. I put them in a display case.",
            } },
            new() { Lines = new[]
            {
                "You're close to the bottom now. I've left the lights on, in case you'd rather come back up.",
                "{0} of 4 Seals still hold. I counted twice. I'd count a third time but I'm afraid of the answer.",
                "Whatever it says to you down there - it has had a very long time to practise.",
            } },
        };
        [Tooltip("The Talk button, mixed in with the curator's ordinary chatter. Empty stage = ordinary chatter only.")]
        public StageLines[] CuratorChatter =
        {
            new(),
            new() { Lines = new[]
            {
                "Those trap rooms you've been finding - the darts, the crushers.|I assumed they guarded treasure.|But every trigger faces DOWN the shaft.|Who builds a trap to catch something climbing up?",
                "There's a rune I can't place. It keeps turning up next to 'DIG'.|It might be 'KEEP'. It might be 'NEVER'.|They're very similar. One has a little hat.",
            } },
            new() { Lines = new[]
            {
                "The Makers didn't dig this mine to take something out.|They dug it to put something in.|And then they filled it back up. Carefully. In layers.",
                "The Resonator isn't a workshop.|It's a key. A very slow key.|And I've been turning it for you every time you asked. With a smile!",
                "It talks to you, doesn't it?|It says the Makers robbed it.|It may even be true. Jailers are rarely polite.|That doesn't tell us what it does when it gets out.",
            } },
            new() { Lines = new[]
            {
                "If you choose to mend the last Seal, it will want back what was taken.|Every Keystone pried loose is a debt.|I'm sorry. I'd have told you sooner, if I'd been able to read.",
                "I keep thinking about the one who carved 'SORRY' down there.|Sorry to us? Or sorry to IT?|I've translated it nine times. It doesn't say.",
            } },
        };
        [Tooltip("Played once, in order, on the first Museum visit after reaching the Retranslation stage.")]
        [TextArea(2, 5)]
        public string[] RetranslationLines =
        {
            "Ah. You're here. Sit down. No - don't sit on that, it's an exhibit. Stand, then.",
            "I checked my work. I know I asked you not to. I asked MYSELF not to. I did it anyway.",
            "The rune I've been reading as 'DIG'. It isn't 'DIG'. It's 'DEPTH'. As in: keep it in the.",
            "The inscription over the vault door doesn't say KEEP DIGGING. It says KEEP IT BURIED.",
            "The tablets aren't messages. They're Seals. Thousands of them, layer on layer, holding something down there.",
            "And the Resonator - my lovely Resonator - breaks them to power your Attunements. Every Resonance the mine shakes, because something underneath has a little more room.",
            "I've re-labelled the whole collection. The true readings are in the Collection tab. I'm so sorry about the soup one.",
            "We can't put back what's been spent. But the last Seal is still whole, at the very bottom. If you can reach it, there may be a way to mend this.",
            "Or you could stop digging. ...No. No, I didn't think so. Neither could I.",
        };
        [Tooltip("Replaces the curator's RuneTranslatedLines after the Retranslation. {0} = rune name, {1} = its TrueTranslation.")]
        [TextArea(2, 4)]
        public string[] TrueRuneTranslatedLines =
        {
            "<b>{0}</b>. I know this one now. It reads: \"{1}\"",
            "<b>{0}</b>... I'd rather not, but: \"{1}\"",
            "<b>{0}</b>. Properly, this time: \"{1}\"",
        };
        [Tooltip("Replaces the curator's AllFoundLines when the collection is completed after the Retranslation.")]
        [TextArea(2, 5)]
        public string[] AllFoundAfterRetranslationLines =
        {
            "That's twenty. The whole Maker alphabet. I used to dream about this.",
            "I can read all of it now. Every wall down there says the same thing, twenty different ways.",
            "Here. The Halo of the Makers. I think they'd want someone to have it who's still going down.",
        };
        public string[] CuratorGreetingsAfterReseal =
        {
            "Quiet, isn't it? I'd forgotten what quiet sounded like. Tea?",
            "The Resonator hasn't hummed since. I've put a doily on it.",
        };

        [Header("Critter keeper (Critters.CritterShopController)")]
        [Tooltip("The Talk button, mixed in with the shopkeeper's ordinary chatter. Empty stage = ordinary chatter only.")]
        public StageLines[] ShopkeeperChatter =
        {
            new(),
            new() { Lines = new[] { "Critters are movin' house.|Whole families, headin' UP the shaft.|Critters don't go up. Up's where the birds are." } },
            new() { Lines = new[] { "The deep ones've started hummin'.|Same note as that machine in the Museum.|I asked a Void Wisp what it meant. It just looked sorry for me." } },
            new() { Lines = new[] { "Somethin' down there's been singin' to my critters.|They ain't scared of it.|Critters is scared of everything. Make of that what you like." } },
        };
        public string[] ShopkeeperChatterAfterReseal =
        {
            "Deep ones stopped hummin'.|Jar's gone awful quiet.|Reckon they miss it. Don't tell the Professor.",
        };

        [Header("Resonance prompt (UI.MuseumPrestigeConfirmUI)")]
        public string FirstResonancePrompt = "Begin Resonance? The Resonator will retune your rig, and the mine will collapse and reshape. This reset is permanent.";
        [Tooltip("Index = (int)StoryStage. {0} = Seals still holding.")]
        [TextArea(2, 4)]
        public string[] ResonancePrompts =
        {
            "Begin Resonance? The mine will collapse and reshape. This reset is permanent.",
            "Begin Resonance? The tremors have been getting worse. The mine will collapse. This reset is permanent.",
            "Begin Resonance? You know what it spends now. The mine will collapse. This reset is permanent.",
            "Begin Resonance? {0} of 4 Seals still hold. The mine will collapse. This reset is permanent.",
        };
        public string ResonancePromptAfterEnding = "Begin Resonance? The mine will collapse and reshape. This reset is permanent.";

        [Header("Seal Chambers (index = chamber, top to bottom)")]
        public ChamberText[] Chambers =
        {
            new()
            {
                Narration = "A chamber of fitted brick. On the far wall, a mural: small figures in a long line, each carrying a tablet DOWN a stair.|At the centre a Keystone sits in a socket of black stone. It is warm.",
                Voice = "",
            },
            new()
            {
                Narration = "The mural here is cut deeper. The figures are no longer carrying their tablets. They are pressing them into the walls, shoulder to shoulder.|Below them something large was drawn, and then chiselled away.",
                Voice = "They would not even leave my picture.",
            },
            new()
            {
                Narration = "No figures in this mural. Only the tablets, ring inside ring, and at the centre a single shape with its hands open.|Someone has carved one word under it, in a hurry. You don't need the curator to read it. It says SORRY.",
                Voice = "Take it. It is only a stone. I am so tired of stones.",
            },
        };
        public ChamberText Vault = new()
        {
            Narration = "The last door. The Seal fills the far wall, turning slowly, humming the same note as the Resonator.|You remember what the curator said: mend it, and it will want back what was taken.",
            Voice = "You came all this way. I will not lie to you now: I do not know what I will do when I stand up.|I have not stood in so long.",
        };

        [Header("Choice prompts (UI.StoryChoiceUI)")]
        public string KeystoneTitle = "The Keystone";
        [Tooltip("{0} = artifact payout, {1} / {2} = Seals holding now / after, {3} / {4} = Reseal cost now / after.")]
        [TextArea(4, 8)]
        public string KeystoneWarning = "Pry it loose and the Seal it anchors breaks for good. The Museum will pay <color=purple>+{0} Artifacts</color>.\n\n"
            + "<color=#FF6060>Seals holding: {1} of 4 now, {2} of 4 after.</color>\n"
            + "<color=#FF6060>Mending the last Seal (the Reseal ending) will cost {4} Artifacts instead of {3}.</color>\n\n"
            + "<b>Permanent.</b> Resonance won't bring it back.";
        [Tooltip("{0} = artifact payout.")]
        public string KeystoneTakeLabel = "Take it (+{0})";
        public string KeystoneLeaveLabel = "Leave it";
        public string VaultTitle = "The Last Seal";
        [Tooltip("{0} = Reseal cost, {1} = artifacts held.")]
        [TextArea(2, 5)]
        public string VaultBody = "Break it, and whatever is below goes free.\nMend it, and the Seal takes back what was taken: {0} Artifacts. You have {1}.\n\nEither choice is final.";
        public string VaultReleaseLabel = "Release";
        [Tooltip("{0} = Reseal cost.")]
        public string VaultResealLabel = "Reseal ({0})";
        public string VaultLeaveLabel = "Not yet";
        [Tooltip("{0} = Reseal cost, {1} = artifacts held.")]
        public string ResealTooExpensive = "The Seal needs {0} Artifacts to mend. You have {1}.";

        [Header("Release (Story.ReleaseCinematic) - narration unless a line starts with {voice} / {curator}")]
        [Tooltip("At the Vault, as the last Seal breaks.")]
        [TextArea(2, 5)]
        public string[] ReleaseAwakening =
        {
            "The Seal comes apart under your drill like wet paper. The humming stops.",
            "{voice}Oh.|I am standing. I am STANDING.",
            "{voice}No. No - it is coming out of me. All of it. I cannot close my hands.",
            "{voice}This is why they buried me. Not for what I wanted. For what I AM.",
            "{voice}Not you. You opened the door. Let me do one kind thing first.|UP. Go UP.",
        };
        [Tooltip("On the surface, while it comes down.")]
        [TextArea(2, 5)]
        public string[] ReleaseLament =
        {
            "{voice}I did not want this. I told you I did not know what I would do. I did not lie.",
            "{voice}I cannot stop. You are the one thing left that I can choose not to break.|So I will not. Watch, little engine. Somebody should.",
        };
        public string ReleaseEndTitle = "THE BOUND IS FREE";
        [Tooltip("The end card, one line at a time. {keystones} is replaced by the ReleaseKeystoneOutcomes entry.")]
        [TextArea(2, 4)]
        public string[] ReleaseEndLines =
        {
            "Nothing was left of the surface. Not the Museum, not the Market, not the mine beneath them.",
            "{keystones}",
            "You were set down gently in the middle of it, and left alone.",
        };
        [Tooltip("Index = Keystones taken (0-3).")]
        [TextArea(2, 4)]
        public string[] ReleaseKeystoneOutcomes =
        {
            "Every Keystone was still in its socket. They broke with everything else.",
            "One Keystone was in a Museum drawer. The drawer is gone too.",
            "Two Keystones were in a Museum drawer. The drawer is gone too.",
            "Three Keystones were in a Museum drawer. The drawer is gone too.",
        };
        public string ReleaseEndClosing = "THE END";
        public string ReleaseEndThanks = "Thank you for playing.";
        public string ReleaseEndContinuePrompt = "Press any key";
        [Tooltip("Played once the player continues after the Release ending: they are back at the Vault, the Seal whole.")]
        [TextArea(2, 5)]
        public string[] RewindLines =
        {
            "You are standing in front of the last Seal. It is whole. It hums the same note it always has.",
            "{voice}You saw. That is what happens when I stand.|I showed you the only way I could. I am sorry it had to be like that.",
            "{voice}The door is still shut. You know what is behind it now. Choose.",
        };

        [Header("Reseal (Story.VoidCave) - narration unless a line starts with {voice} / {curator}")]
        [Tooltip("The Bound, when the player arrives in his cave.")]
        [TextArea(2, 5)]
        public string[] VoidCaveSpeech =
        {
            "Dark, and then not dark. Gold to the horizon: coin, crowns and cut stones, heaped like slag.|In the middle of it, something very large is sitting very still.",
            "{voice}Do not be afraid. I brought you here myself. I wanted to see you once, up close.|So. That is what a little engine looks like.",
            "{voice}Thank you. I mean it. You have let me rest.",
            "{voice}I asked you to open the door. Every day, I asked. I could not help asking, any more than I can help the rest of it.",
            "{voice}I cannot hold my own power. It comes out of me like breath.|If I had stood up, there would be no surface for you to go home to.",
            "{voice}I did not want to destroy your world. I only wanted to stop being alone in the dark.",
            "{voice}All this was theirs. The Makers paid me to stay down, as if I had a choice. It is no use to me. It never was.",
            "{voice}The Seal will hold now. The Keystones are part of the wall again, and the door will not open for anyone.|That is right. That is how it should be.",
            "{voice}Go home, little engine. Dig as much as you like. I will sleep through all of it.|The way back is there, when you are ready.",
        };
        [Tooltip("Speaking to the Bound again before leaving, one picked at random.")]
        [TextArea(2, 4)]
        public string[] VoidCaveRepeatLines =
        {
            "Still here? I do not mind. It is nice, having someone sit with me.",
            "Take nothing. It is all cursed with being mine.|...That was a joke. I am out of practice.",
            "Go on. The sun is still up there because of you. Go and stand in it.",
        };
        [Tooltip("Played once the player is home from the cave. {keystones} is replaced by the ResealKeystoneOutcomes entry.")]
        [TextArea(2, 5)]
        public string[] ResealHomecoming =
        {
            "The portal sets you down in front of the Depot and folds shut behind you. It does not open again.",
            "{keystones}",
            "{curator}It's holding. It's HOLDING! ...You look like you've seen something. No, don't tell me. I'd only try to label it.",
            "THE SEAL HOLDS.\nThank you for playing. The mine is still yours to dig - and it is gentler, now that nothing below is pushing back.",
        };
        [Tooltip("Index = Keystones taken (0-3).")]
        [TextArea(2, 4)]
        public string[] ResealKeystoneOutcomes =
        {
            "You never took a Keystone. The Seal asked almost nothing of you, and closed like a held breath let go.",
            "Somewhere below, one Keystone has sunk back into its wall for good. The chambers stand empty now.",
            "Somewhere below, two Keystones have sunk back into their walls for good. The chambers stand empty now.",
            "Somewhere below, three Keystones have sunk back into their walls for good, one after another, like a lock turning.",
        };

        public static string PickRandom(string[] pool) =>
            pool == null || pool.Length == 0 ? string.Empty : pool[UnityEngine.Random.Range(0, pool.Length)];

        public string[] StageLinesFor(StageLines[] pools, StoryStage stage)
        {
            int index = (int)stage;
            return pools != null && index < pools.Length && pools[index] != null ? pools[index].Lines : Array.Empty<string>();
        }
    }
}
