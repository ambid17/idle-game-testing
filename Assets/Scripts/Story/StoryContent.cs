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
    // epilogues. Field defaults are the shipped script, so a freshly created asset is ready to use.
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
        public string[] CuratorGreetingsAfterRelease =
        {
            "The sky was a different colour this morning. Nobody else seems to mind. I mind a little.",
            "It hasn't eaten anyone. I keep checking. Still no one.",
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
        public string[] ShopkeeperChatterAfterRelease =
        {
            "Critters all went quiet the day the light left.|Then they went back to bein' critters.|Reckon it said goodbye.",
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
        [Tooltip("{0} = artifact payout.")]
        [TextArea(2, 5)]
        public string KeystoneBody = "Pry it loose and the Seal it anchors breaks for good. The Museum would pay {0} Artifacts for it.\n\nThis cannot be undone.";
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

        [Header("Epilogues (narration unless marked; {voice} / {curator} at the start of a line picks the speaker)")]
        [TextArea(2, 5)]
        public string[] ReleaseEpilogue =
        {
            "You break the last Seal. The note the Resonator has been humming all this time finally stops.",
            "The mine does not collapse. The light in the ore goes out, vein by vein, and climbs past you up the shaft.",
            "{voice}Oh. Oh, I had forgotten the sky.",
            "It does not thank you. It does not harm you. By the time you reach the surface it is a brightness on the horizon, getting smaller.",
            "{keystones}",
            "{curator}Well. We're all still here. I've checked twice. ...I'm going to need a new label for the Resonator. 'Door, Formerly.'",
            "THE BOUND IS FREE.\nThank you for playing. The mine is still yours to dig - and ore sells for more, now that its light has somewhere to go.",
        };
        [TextArea(2, 5)]
        public string[] ResealEpilogue =
        {
            "You press the Artifacts back into the Seal, one by one. Each takes its place as if it had only been waiting.",
            "{voice}...I see. No. I understand. You have only ever known me as a noise in the dark.",
            "{voice}Dig carefully, little engine. I will still be here.",
            "The humming drops below hearing. The tremors stop.",
            "{keystones}",
            "{curator}It's holding. It's HOLDING! I've put a doily on the Resonator. Nobody is to touch it. Especially me.",
            "THE SEAL HOLDS.\nThank you for playing. The mine is still yours to dig - and it is gentler, now that nothing below is pushing back.",
        };
        [Tooltip("Replaces the {keystones} epilogue line. Index = Keystones taken (0-3).")]
        [TextArea(2, 4)]
        public string[] ReleaseKeystoneLines =
        {
            "You never took a Keystone. It left every one of them where it lay, as if out of manners.",
            "One socket stands empty on the way up. It pauses there a moment, and goes on.",
            "Two sockets stand empty on the way up. It does not look at them.",
            "The sockets where the Keystones sat are warm for days.",
        };
        [TextArea(2, 4)]
        public string[] ResealKeystoneLines =
        {
            "You never took a Keystone. The Seal asked almost nothing of you, and closed like a held breath let go.",
            "Somewhere above, one Keystone settles back into its socket.",
            "Somewhere above, two Keystones settle back into their sockets.",
            "Somewhere above, three Keystones settle back into their sockets, one after another, like a lock turning.",
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
