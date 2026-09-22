using System.Collections.Generic;
using Events;
using UnityEngine;

namespace UI.Notifications
{
    // Single entry point for all UI notifications: every notification dispatches a
    // NotificationEvent and lands here, which routes it into one of two independent serial
    // queues by Urgency - TimeSensitive (top-middle) or Queued (bottom-right). Each queue shows
    // one item at a time and waits for it to finish its full display+fade before starting the
    // next, so a backlog of low-priority Queued notifications can never delay or get skipped by a
    // TimeSensitive one, and vice versa. Icon presence (NotificationEvent.Icon) picks which of the
    // two prefabs is instantiated, independent of which queue it lands in.
    public class NotificationManager : MonoBehaviour
    {
        [SerializeField] private Transform timeSensitiveContainer;
        [SerializeField] private Transform queuedContainer;
        [SerializeField] private NotificationToastUI iconTextPrefab;
        [SerializeField] private NotificationToastUI textOnlyPrefab;

        private Lane timeSensitiveLane;
        private Lane queuedLane;

        // Lanes are built in Awake, not Start, so they exist before OnEnable subscribes below -
        // Unity runs every object's Awake+OnEnable before any object's Start, so a NotificationEvent
        // dispatched from another script's own Start could otherwise arrive while timeSensitiveLane/
        // queuedLane were still null and get silently dropped instead of queued.
        private void Awake()
        {
            if (timeSensitiveContainer == null) Debug.LogError("NotificationManager.timeSensitiveContainer is not assigned.");
            if (queuedContainer == null) Debug.LogError("NotificationManager.queuedContainer is not assigned.");
            if (iconTextPrefab == null) Debug.LogError("NotificationManager.iconTextPrefab is not assigned.");
            if (textOnlyPrefab == null) Debug.LogError("NotificationManager.textOnlyPrefab is not assigned.");

            timeSensitiveLane = new Lane(timeSensitiveContainer, iconTextPrefab, textOnlyPrefab);
            queuedLane = new Lane(queuedContainer, iconTextPrefab, textOnlyPrefab);
        }

        private void OnEnable() => GameManager.EventService.Add<NotificationEvent>(OnNotification);
        private void OnDisable() => GameManager.EventService.Remove<NotificationEvent>(OnNotification);

        private void OnNotification(NotificationEvent evt)
        {
            var lane = evt.Urgency == NotificationUrgency.TimeSensitive ? timeSensitiveLane : queuedLane;
            lane?.Enqueue(evt);
        }

        // Drives both lanes every frame rather than only reacting to Enqueue/completion callbacks -
        // TryPlayNext is a no-op unless a lane is both idle and has something pending, so this just
        // makes sure a pending notification starts the instant its lane frees up, with no dependency
        // on precisely which call site happened to free it.
        private void Update()
        {
            timeSensitiveLane?.TryPlayNext();
            queuedLane?.TryPlayNext();
        }

        // One serial display slot: at most one notification animating at a time, next one starts
        // only once the previous fully finishes (display + fade).
        private class Lane
        {
            private readonly Transform container;
            private readonly NotificationToastUI iconTextPrefab;
            private readonly NotificationToastUI textOnlyPrefab;
            private readonly Queue<NotificationEvent> pending = new();

            private bool playing;

            public Lane(Transform container, NotificationToastUI iconTextPrefab, NotificationToastUI textOnlyPrefab)
            {
                this.container = container;
                this.iconTextPrefab = iconTextPrefab;
                this.textOnlyPrefab = textOnlyPrefab;
            }

            public void Enqueue(NotificationEvent evt) => pending.Enqueue(evt);

            public void TryPlayNext()
            {
                if (playing || pending.Count == 0 || container == null) return;

                var evt = pending.Dequeue();
                var prefab = evt.Icon != null ? iconTextPrefab : textOnlyPrefab;
                if (prefab == null) return;

                playing = true;
                var item = Object.Instantiate(prefab, container);
                item.Play(evt.Message, evt.Icon, evt.Urgency, () => playing = false);
            }
        }
    }
}
