using System;
using System.Collections.Generic;
using System.Threading;

namespace Toggl.Phoebe
{
    public sealed class MessageBus
    {
        private readonly int threadId;
        private readonly SynchronizationContext threadContext;
        private readonly object syncRoot = new object ();
        private readonly Dictionary<Type, List<WeakReference>> registry =
            new Dictionary<Type, List<WeakReference>> ();
        private readonly Queue<Action> dispatchQueue = new Queue<Action> ();
        private volatile bool isScheduled;
        private bool isProcessing;

        public MessageBus ()
        {
            threadId = Thread.CurrentThread.ManagedThreadId;
            threadContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// Subscribe listener to receive messages for TMessage. This method returns a subscription object, which you
        /// need to keep in scope for how long you want to the listener to be active. The moment the subscription object
        /// is garbage collected message delivery to listeners is not guaranteed anymore.
        /// </summary>
        /// <param name="listener">Listener.</param>
        /// <param name="threadSafe">Indicates if the listener is thread-safe or not.</param>
        /// <typeparam name="TMessage">Type of the message to subscribe to.</typeparam>
        /// <returns>A subscription object, null if subscribing failed.</returns>
        public Subscription<TMessage> Subscribe<TMessage> (Action<TMessage> listener, bool threadSafe = false)
        where TMessage : Message
        {
            if (listener == null) {
                throw new ArgumentNullException ("listener");
            }

            var subscription = new Subscription<TMessage> (listener, threadSafe);

            lock (syncRoot) {
                List<WeakReference> subscriptions;
                if (!registry.TryGetValue (typeof (TMessage), out subscriptions)) {
                    subscriptions = new List<WeakReference> ();
                    registry [typeof (TMessage)] = subscriptions;
                }
                subscriptions.Add (new WeakReference (subscription));
            }

            return subscription;
        }

        /// <summary>
        /// Unsubscribes the specified subscription from receiving anymore messages.
        /// </summary>
        /// <param name="subscription">A subscription object from Subscribing to a message.</param>
        public void Unsubscribe<TMessage> (Subscription<TMessage> subscription)
        where TMessage : Message
        {
            lock (syncRoot) {
                foreach (var listeners in registry.Values) {
                    listeners.RemoveAll ((weak) => !weak.IsAlive || weak.Target == subscription);
                }
            }
        }

        /// <summary>
        /// Send the specified message.
        /// </summary>
        /// <param name="msg">Message.</param>
        /// <typeparam name="TMessage">Type of the message to send.</typeparam>
        public void Send<TMessage> (TMessage msg)
        where TMessage : Message
        {
            if (msg == null) {
                throw new ArgumentNullException ("msg");
            }

            List<Subscription<TMessage>> sendMain = null;
            List<Subscription<TMessage>> sendHere = null;
            var onMainThread = Thread.CurrentThread.ManagedThreadId == threadId;
            var needsPurge = false;

            // Process message:
            lock (syncRoot) {
                List<WeakReference> subscriptions;
                if (registry.TryGetValue (typeof (TMessage), out subscriptions)) {
                    foreach (var weak in subscriptions) {
                        var subscription = weak.Target as Subscription<TMessage>;
                        if (subscription != null) {
                            if (onMainThread || !subscription.IsThreadSafe) {
                                // Add the item to be called on the main thread
                                if (sendMain == null) {
                                    sendMain = new List<Subscription<TMessage>> ();
                                }
                                sendMain.Add (subscription);
                            } else {
                                // Add the item to be called on this thread
                                if (sendHere == null) {
                                    sendHere = new List<Subscription<TMessage>> ();
                                }
                                sendHere.Add (subscription);
                            }
                        } else {
                            needsPurge = true;
                        }
                    }
                }
            }

            // Dispatch messages (on this thread):
            if (sendHere != null) {
                foreach (var subscription in sendHere) {
                    subscription.Listener (msg);
                }
            }

            if (sendMain != null) {
                // Add to main thread dispatch queue:
                lock (syncRoot) {
                    foreach (var subscription in sendMain) {
                        dispatchQueue.Enqueue (delegate {
                            subscription.Listener (msg);
                        });
                    }
                }

                // Make sure the queue is processed now or in the future
                if (onMainThread) {
                    ProcessQueue ();
                } else if (!isScheduled) {
                    ScheduleProcessQueue ();
                }
            }

            // Purge dead subscriptions
            if (needsPurge) {
                lock (syncRoot) {
                    foreach (var listeners in registry.Values) {
                        listeners.RemoveAll ((weak) => !weak.IsAlive);
                    }
                }
            }
        }

        private void ScheduleProcessQueue ()
        {
            // TODO: This is added only for unit tests. Solve it differently?
            if (threadContext == null) {
                ProcessQueue ();
                return;
            }

            lock (syncRoot) {
                if (isScheduled) {
                    return;
                }

                isScheduled = true;
            }

            threadContext.Post ((s) => {
                try {
                    ProcessQueue ();
                } finally {
                    lock (syncRoot) {
                        isScheduled = false;
                    }
                }
            }, null);
        }

        private void ProcessQueue ()
        {
            // Make sure we don't start processing the remainder of the queue when a subscriber generates new messages
            if (isProcessing) {
                return;
            }

            isProcessing = true;
            try {
                while (true) {
                    Action act;

                    lock (syncRoot) {
                        if (dispatchQueue.Count > 0) {
                            act = dispatchQueue.Dequeue ();
                        } else {
                            return;
                        }
                    }

                    // Need to execute the item outside of lock to prevent recursive locking
                    act ();
                }
            } finally {
                isProcessing = false;
            }
        }
    }
}
