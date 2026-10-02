using System.Collections.Generic;
using Events;
using UI;
using UnityEngine;

namespace Museum
{
    // The Museum curator: plays the intro on the player's first Museum visit (UI.MuseumUI opens the
    // panel once IntroConversation finishes), runs the Collection tab's Talk / Turn In conversations
    // with the curator reading out each newly donated rune and accessory, and sends the notification
    // when a rune tablet is mined. Scene-placed singleton (child of GameManager) for its dialog asset.
    public class MuseumCuratorController : Singleton<MuseumCuratorController>
    {
        public const string IntroConversation = "Museum.Intro";
        public const string RetranslationConversation = "Museum.Retranslation";
        private const string ChatConversation = "Museum.Chat";
        private const string TurnInConversation = "Museum.TurnIn";

        [SerializeField] private CuratorDialog dialog;

        private string activeConversation;

        public CuratorDialog Dialog => dialog;
        public bool IsTalking => activeConversation != null;

        protected override void Initialize()
        {
            base.Initialize();
            if (dialog == null) Debug.LogError("MuseumCuratorController.dialog is not assigned.");
        }

        private void OnEnable()
        {
            GameManager.EventService.Add<DialogFinishedEvent>(OnDialogFinished);
            GameManager.EventService.Add<RuneFoundEvent>(OnRuneFound);
        }

        private void OnDisable()
        {
            GameManager.EventService.Remove<DialogFinishedEvent>(OnDialogFinished);
            GameManager.EventService.Remove<RuneFoundEvent>(OnRuneFound);
        }

        public void PlayIntro()
        {
            RuneCollection.Instance.MarkCuratorMet();
            StartConversation(IntroConversation, BuildLines(dialog.IntroLines));
        }

        // The story's twist (Story.StoryManager): the curator has rechecked his translations.
        public void PlayRetranslation()
        {
            GameManager.StoryManager.MarkRetranslationSeen();
            StartConversation(RetranslationConversation, BuildLines(GameManager.StoryManager.Content.RetranslationLines));
        }

        // Collection tab's Talk button.
        public void Chat() => StartConversation(ChatConversation, BuildLines(GameManager.StoryManager.CuratorChatter() ?? CuratorDialog.PickRandom(dialog.Chatter)));

        // Collection tab's Turn In button.
        public void TurnIn()
        {
            var collection = RuneCollection.Instance;
            if (collection.FoundCount == 0)
            {
                StartConversation(TurnInConversation, BuildLines(CuratorDialog.PickRandom(dialog.NothingNewLines)));
                return;
            }

            var result = collection.TurnIn();
            string countText = result.NewRunes.Count == 1 ? "A new rune" : $"{result.NewRunes.Count} new runes";
            var lines = BuildLines(string.Format(CuratorDialog.PickRandom(dialog.TurnInLines), countText));

            // Once he has rechecked his work, new runes get their true reading.
            var story = GameManager.StoryManager;
            bool retranslated = story.RetranslationSeen;
            foreach (var rune in result.NewRunes)
            {
                string line = retranslated
                    ? string.Format(CuratorDialog.PickRandom(story.Content.TrueRuneTranslatedLines), rune.DisplayName, rune.TrueTranslation)
                    : string.Format(CuratorDialog.PickRandom(dialog.RuneTranslatedLines), rune.DisplayName, rune.CuratorTranslation);
                lines.AddRange(BuildLines(line));
            }

            int runeCount = GameManager.MuseumCollectionDatabase.RuneCount;
            bool completedCollection = collection.DonatedCount >= runeCount;
            foreach (var accessory in result.NewAccessories)
            {
                // The finale below hands over the last accessory itself.
                if (completedCollection && accessory.UnlockAtRuneCount >= runeCount) continue;
                lines.AddRange(BuildLines(string.Format(CuratorDialog.PickRandom(dialog.AccessoryUnlockedLines), accessory.DisplayName)));
            }

            if (completedCollection) lines.AddRange(BuildLines(retranslated ? story.Content.AllFoundAfterRetranslationLines : dialog.AllFoundLines));

            StartConversation(TurnInConversation, lines);
        }

        private void StartConversation(string conversationId, List<DialogLine> lines)
        {
            activeConversation = conversationId;
            GameManager.EventService.Dispatch(new DialogRequestedEvent(conversationId, lines));
        }

        private void OnDialogFinished(DialogFinishedEvent evt)
        {
            if (evt.ConversationId == activeConversation) activeConversation = null;
        }

        private List<DialogLine> BuildLines(params string[] entries)
        {
            var lines = new List<DialogLine>();
            if (entries == null) return lines;

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                foreach (var page in entry.Split(CuratorDialog.PageSeparator))
                {
                    var text = page.Trim();
                    if (text.Length > 0) lines.Add(new DialogLine(dialog.SpeakerName, dialog.Portrait, text));
                }
            }
            return lines;
        }

        // Replaces the old flat "+1 Artifact" toast: every player pickup still shows one (with its
        // rune's tablet), and a rune never seen before also says so - an automaton's find only
        // gets a toast when it's new, since automaton pickups are otherwise silent.
        private void OnRuneFound(RuneFoundEvent evt)
        {
            if (evt.IsNew)
            {
                string finder = evt.ByPlayer ? "+1 <color=purple>Artifact</color> - new rune" : "An automaton dug up a new rune";
                GameManager.EventService.Dispatch(new NotificationEvent(
                    $"{finder}: <b>{evt.Rune.DisplayName}</b>! Show it to the Museum curator.", NotificationUrgency.Queued, evt.Rune.Icon));
                return;
            }

            if (evt.ByPlayer)
            {
                GameManager.EventService.Dispatch(new NotificationEvent("+1 <color=purple>Artifact</color>", NotificationUrgency.Queued, evt.Rune.Icon));
            }
        }
    }
}
