using UnityEngine;

namespace Critters
{
    // Everything the Critter Shop's owner says (CritterShopController). Within one entry, "|"
    // splits it into several click-through pages. {0}/{1} placeholders are noted per pool.
    // Field defaults are the shipped script, so a freshly created asset is ready to use.
    [CreateAssetMenu(fileName = "ShopkeeperDialog", menuName = "Critters/Shopkeeper Dialog")]
    public class ShopkeeperDialog : ScriptableObject
    {
        public const char PageSeparator = '|';

        public string SpeakerName = "Grizzle Mossbeard";
        public Sprite Portrait;

        [Tooltip("First meeting - every page is played, in order.")]
        [TextArea(2, 5)]
        public string[] IntroLines =
        {
            "AH! A VISITOR! Don't mind the smell, that's just Gerald. Gerald is a rock. We're very close.",
            "Name's Grizzle Mossbeard. I came down here forty years ago to find a lost sandwich. Never found it. Found my calling instead.",
            "The surface? Bah! Too much sky. Sky just goes UP forever. Who needs that? Down here the ceiling knows its place.",
            "I collect CRITTERS, you see. Little glowy fellas, crunchy fellas, the slimy ones who don't like being called slimy.",
            "Catch 'em with your hands, pop 'em in your jar, bring 'em to ol' Grizzle. I pay in real surface money. Found it in a cave. Don't ask which cave.",
            "Bring me enough different kinds and I'll knit little hats for those clanky robots of yours. Everyone deserves a hat. Even Gerald has a hat.",
            "Gerald's hat is also a rock.",
        };

        [Tooltip("Shown in the shop panel's speech bubble each time it opens - one picked at random.")]
        [TextArea(2, 4)]
        public string[] Greetings =
        {
            "Back again! Wipe your boots. Actually, don't. The dirt's load-bearing.",
            "Welcome, welcome! Mind the stalactites, they're shy.",
            "Oh good, a face that isn't carved into a wall. What've you got?",
            "Grizzle's Critter Emporium! Open 25 hours a day. Down here we have extra hours.",
            "You smell like the surface. Fresh air. Disgusting. Come in, come in.",
            "Shh! Gerald is sleeping. He's always sleeping. He's a rock.",
        };

        [Tooltip("The panel's Talk button - one entry picked at random; use | for multiple pages.")]
        [TextArea(2, 5)]
        public string[] Chatter =
        {
            "Did you know bats are just mice that got really into jazz?|I made that up. But I believe it.",
            "I once tried to leave the mine. Got as far as the second ladder.|Then I remembered the sun exists and came right back down.",
            "The trick to living underground is simple: befriend the moss.|The moss knows things.|The moss will not tell you those things. But it knows.",
            "People ask me, 'Grizzle, don't you get lonely?'|Nobody asks me that. Nobody's down here to ask.|...It's nice that you visit.",
            "My mother always said, 'Grizzle, you'll never amount to anything digging around in the dirt.'|Look at me now, Ma! I'm DEEP.",
            "Every night I tuck the fireflies in.|They don't sleep. They just glow at me judgmentally.|It's a good system.",
            "I've been eating mushroom soup for forty years.|Some of those mushrooms talk now.|Great listeners.",
        };

        [Tooltip("Turning in with an empty jar - one picked at random.")]
        [TextArea(2, 4)]
        public string[] EmptyJarLines =
        {
            "That jar's emptier than my social calendar. Go catch something!",
            "Nothing in there but air. I don't buy air. I have plenty of air. Well. Some air.",
            "An empty jar! Very avant-garde. Still not paying for it.",
        };

        [Tooltip("Before a turn-in's rewards - one picked at random. {0} = critter count, {1} = dollars paid.")]
        [TextArea(2, 4)]
        public string[] TurnInLines =
        {
            "Ooh, {0} of 'em! Let's have a look... here's ${1}. Don't spend it all on dirt.",
            "{0} critters! My babies! Here, take ${1} before I get emotional.",
            "Lovely, lovely. {0} little lodgers for the Emporium. That's ${1} for your trouble.",
        };

        [Tooltip("A new automaton hat was just unlocked - one picked at random. {0} = hat name.")]
        [TextArea(2, 4)]
        public string[] HatUnlockedLines =
        {
            "That's enough new friends for a new hat! I knitted you a {0}. Stick it on one of your robots at the Control Center.",
            "Hold still, I've got something for your clanky pals... a {0}! Fits any head. Or head-shaped bolt.",
        };

        [Tooltip("Every species has been turned in - played once, in order, right after it happens.")]
        [TextArea(2, 5)]
        public string[] AllFoundLines =
        {
            "Wait. Wait wait wait. That's... that's ALL of them. Every critter in the whole mine.",
            "Forty years I've been down here and you did it faster than I found my own left boot.",
            "Here. The crown. I was saving it for Gerald, but he'd want you to have it.",
            "Gerald is still a rock. But he's a proud rock.",
        };

        public static string PickRandom(string[] pool) =>
            pool == null || pool.Length == 0 ? string.Empty : pool[Random.Range(0, pool.Length)];
    }
}
