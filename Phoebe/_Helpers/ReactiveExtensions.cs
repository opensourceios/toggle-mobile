﻿using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Helpers
{
    public static class ReactiveExtensions
    {
        /// <summary>
        /// Filters out null results from chooser function
        /// </summary>
        public static IObservable<U> Choose<T,U> (this IObservable<T> observable, Func<T,U> chooser)
        where U : class
        {
            return observable.Select (chooser).Where (x => x != null);
        }

        public static IObservable<IList<T>> TimedBuffer<T> (this IObservable<T> observable, int milliseconds)
        {
            if (milliseconds > 0) {
                // TODO: This is firing up even if there're no events. Can it be improved?
                return observable
                       .Buffer (TimeSpan.FromMilliseconds (milliseconds))
                       .Where (b => b.Count > 0);
            } else {
                return observable
                .Select (x => new List<T> () { x });
            }
        }

        /// <summary>
        /// Processes async operations one after the other (SelectMany does it in parallel)
        /// </summary>
        public static IObservable<U> SelectAsync<T,U> (this IObservable<T> observable, Func<T,Task<U>> selector)
        {
            // SelectMany would process tasks in parallel, see https://goo.gl/eayv5N
            return observable.Select (x => Observable.FromAsync (() => selector (x))).Concat ();
        }
    }
}

