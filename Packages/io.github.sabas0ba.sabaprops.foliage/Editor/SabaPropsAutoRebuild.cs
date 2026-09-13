using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>
    /// Coalesces repeated Inspector and Scene-handle edits into one rebuild.
    /// </summary>
    public static class SabaPropsAutoRebuild
    {
        private const double DelaySeconds = 0.4;

        private sealed class PendingBuild
        {
            public UnityEngine.Object owner;
            public Action build;
            public double dueAt;
        }

        private static readonly Dictionary<int, PendingBuild> Pending =
            new Dictionary<int, PendingBuild>();

        public static void Schedule(UnityEngine.Object owner, Action build)
        {
            if (owner == null || build == null)
            {
                return;
            }

            Pending[owner.GetInstanceID()] = new PendingBuild
            {
                owner = owner,
                build = build,
                dueAt = EditorApplication.timeSinceStartup + DelaySeconds,
            };

            EditorApplication.update -= FlushDueBuilds;
            EditorApplication.update += FlushDueBuilds;
        }

        public static void Cancel(UnityEngine.Object owner)
        {
            if (owner == null)
            {
                return;
            }

            Pending.Remove(owner.GetInstanceID());
            StopWhenIdle();
        }

        private static void FlushDueBuilds()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            var due = new List<int>();
            foreach (KeyValuePair<int, PendingBuild> pair in Pending)
            {
                if (pair.Value.owner == null || pair.Value.dueAt <= now)
                {
                    due.Add(pair.Key);
                }
            }

            foreach (int id in due)
            {
                PendingBuild pending = Pending[id];
                Pending.Remove(id);

                if (pending.owner == null)
                {
                    continue;
                }

                try
                {
                    pending.build();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, pending.owner);
                }
            }

            StopWhenIdle();
        }

        private static void StopWhenIdle()
        {
            if (Pending.Count == 0)
            {
                EditorApplication.update -= FlushDueBuilds;
            }
        }
    }
}
