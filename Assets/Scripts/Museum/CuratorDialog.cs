using UnityEngine;

namespace Museum
{
    // Everything the Museum curator says (MuseumCuratorController). Within one entry, "|" splits it
    // into several click-through pages. {0}/{1} placeholders are noted per pool. Field defaults are
    // the shipped script, so a freshly created asset is ready to use.
    [CreateAssetMenu(fileName = "CuratorDialog", menuName = "Museum/Curator Dialog")]
    public class CuratorDialog : ScriptableObject
    {
        public const char PageSeparator = '|';

        public string SpeakerName = "Prof. Percival Dustworth";
        public Sprite Portrait;

        [Tooltip("First Museum visit - every page is played, in order, before the panel opens.")]
        [TextArea(2, 5)]
        public string[] IntroLines =
        {
            "STOP! Don't touch anything! ...Oh. Oh my. A VISITOR. Nobody visits! Welcome, welcome to the Museum!",
            "Professor Percival Dustworth. Curator, archivist, and sole member of the Museum Appreciation Society. Membership is very exclusive. Nobody else applied.",
            "You've been digging up those stone tablets, haven't you? RUNE tablets! Messages from the Makers, the ones who dug this mine long before us!",
            "There are twenty different runes, and I have translated every single one. Perfectly. With total accuracy. Please do not check my work.",
            "Bring me each new rune you find and I'll put it on display. You keep your Stellar Credits - I only want the knowledge. And the glory. Mostly the glory.",
            "Donate enough and I'll lend you a few genuine relics from the archives to wear. They're priceless. Please don't drill with them. You're going to drill with them.",
        };

        [Tooltip("Shown in the Collection tab's speech bubble each time the Museum opens - one picked at random.")]
        [TextArea(2, 4)]
        public string[] Greetings =
        {
            "Welcome back! Please keep your drill at least three meters from the exhibits.",
            "Ah, my favourite visitor! Also my only visitor. Still counts.",
            "Quiet, please. The runes are resting.",
            "Do wipe your treads. That's ancient dust, and it's MY ancient dust.",
            "Another day, another breakthrough! Yesterday I learned to read. Today: everything else.",
            "Back so soon? Found something old? Something dusty? Something INSCRIBED?",
        };

        [Tooltip("The Talk button - one entry picked at random; use | for multiple pages.")]
        [TextArea(2, 5)]
        public string[] Chatter =
        {
            "Did you know the Makers ate soup with forks?|I deduced this from a fork.|I may have dropped that fork myself.",
            "I once spent eleven years translating a single tablet.|It said 'THIS SIDE UP.'|It was upside down.",
            "My mother wanted me to be a dentist.|I said, 'Mother, I want to dig up the past!'|She said teeth are also in the past.|We haven't spoken since.",
            "The gift shop is closed.|There is no gift shop.|But if there WERE, it would be closed.",
            "Every artifact tells a story.|Most of them are about rocks.|The Makers were VERY into rocks.",
            "I put a label on everything.|This chair is labelled 'Chair, Probably.'|Science is about humility.",
            "Have you met that fellow with the critters, down in the caves?|Lovely man. Smells like moss.|He tried to donate a rock named Gerald. I said no. Gerald has not forgiven me.",
        };

        [Tooltip("Turning in with no new runes - one picked at random.")]
        [TextArea(2, 4)]
        public string[] NothingNewLines =
        {
            "Nothing new? I'll file this visit under 'Disappointments, Ongoing.'",
            "I've already got all of those! Look harder. Look DEEPER. Literally - that's how mines work.",
            "Empty-handed! Bold. I shall display your hands as an exhibit: 'Hands, Empty, Modern Era.'",
        };

        [Tooltip("Before a turn-in's runes are read out - one picked at random. {0} = e.g. \"2 new runes\".")]
        [TextArea(2, 4)]
        public string[] TurnInLines =
        {
            "Ooh! {0}! Hold still, let me find my good monocle... my GOOD monocle... ah. It was on my face.",
            "{0}! Into the display case you go! Gently! GENTLY!",
            "Let's see what you've brought... {0}! Oh, my heart. Somebody fetch my fainting couch.",
        };

        [Tooltip("Read out for each newly donated rune - one picked at random per rune. {0} = rune name, {1} = its CuratorTranslation.")]
        [TextArea(2, 4)]
        public string[] RuneTranslatedLines =
        {
            "<b>{0}</b>! Translated, it reads: \"{1}\"",
            "Ahh, <b>{0}</b>. A classic. Roughly: \"{1}\"",
            "<b>{0}</b>... yes... YES! It says: \"{1}\" Profound.",
        };

        [Tooltip("A new accessory was just unlocked - one picked at random. {0} = accessory name.")]
        [TextArea(2, 4)]
        public string[] AccessoryUnlockedLines =
        {
            "For such a generous donation, the Museum lends you the {0}! Wear it with dignity.|You can put it on from the Collection tab, right here.",
            "I've been saving this for a worthy visitor... the {0}! You're the only visitor, but you're ALSO worthy.|Try it on in the Collection tab!",
        };

        [Tooltip("Every rune has been donated - played once, in order, right after it happens.")]
        [TextArea(2, 5)]
        public string[] AllFoundLines =
        {
            "That's... that's TWENTY. That's all of them. The complete Maker alphabet. In MY museum!",
            "Do you know what this means? At last I can read the Makers' great inscription, the one carved over the vault door!",
            "It says... 'KEEP DIGGING.' Well. They were very consistent.",
            "Here, take the Halo of the Makers. I'd wear it myself, but it clashes with my hat.",
        };

        public static string PickRandom(string[] pool) =>
            pool == null || pool.Length == 0 ? string.Empty : pool[Random.Range(0, pool.Length)];
    }
}
