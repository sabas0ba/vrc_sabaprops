using System;
using System.Collections.Generic;
using System.Reflection;
using SabaProps.Tablet.Authoring;
using TMPro;
using UdonSharp;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Tablet.Editors
{
    /// <summary>定義の検査結果 1 件。</summary>
    public struct TabletIssue
    {
        public MessageType type;
        public string message;

        public TabletIssue(MessageType type, string message)
        {
            this.type = type;
            this.message = message;
        }
    }

    /// <summary>TabletDefinition の内容を Build 前に検査します。</summary>
    public static class TabletValidation
    {
        public static List<TabletIssue> Validate(TabletDefinition definition)
        {
            var issues = new List<TabletIssue>();
            if (definition == null)
            {
                return issues;
            }

            if (definition.pages.Count == 0 && !definition.includePlayerPage)
            {
                issues.Add(new TabletIssue(MessageType.Warning, "ページがありません。"));
            }

            var owners = new Dictionary<GameObject, string>();
            var groupGlobal = new Dictionary<string, bool>();

            for (int p = 0; p < definition.pages.Count; p++)
            {
                TabletPage page = definition.pages[p];
                if (page == null)
                {
                    continue;
                }

                string pageName = string.IsNullOrEmpty(page.title) ? "Page " + (p + 1) : page.title;
                if (page.entries.Count == 0)
                {
                    issues.Add(new TabletIssue(MessageType.Info, pageName + ": 項目がありません。"));
                }

                for (int e = 0; e < page.entries.Count; e++)
                {
                    TabletEntry entry = page.entries[e];
                    if (entry == null)
                    {
                        continue;
                    }

                    string where = pageName + " / " + (string.IsNullOrEmpty(entry.label) ? "#" + (e + 1) : entry.label) + ": ";
                    ValidateEntry(definition, entry, where, owners, groupGlobal, issues);
                }
            }

            foreach (GameObject item in definition.interactItems)
            {
                if (item != null && item.GetComponent<Collider>() == null)
                {
                    issues.Add(new TabletIssue(MessageType.Error,
                        item.name + ": Interact で召喚するアイテムに Collider がありません。"));
                }
            }

            if (!definition.keyTrigger && !definition.reachTrigger && definition.interactItems.Count == 0 && !definition.startVisible)
            {
                issues.Add(new TabletIssue(MessageType.Warning, "召喚方法が 1 つも有効ではありません。"));
            }

            TMP_FontAsset font = definition.theme != null && definition.theme.font != null
                ? definition.theme.font
                : TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                issues.Add(new TabletIssue(MessageType.Warning,
                    "TextMeshPro のフォントがありません。Window > TextMeshPro > Import TMP Essential Resources を実行するか、Theme でフォントを指定してください。"));
            }

            return issues;
        }

        private static void ValidateEntry(TabletDefinition definition, TabletEntry entry, string where,
            Dictionary<GameObject, string> owners, Dictionary<string, bool> groupGlobal, List<TabletIssue> issues)
        {
            switch (entry.kind)
            {
                case TabletEntryKind.Toggle:
                    if (Count(entry.objects) + Count(entry.invertedObjects) + Count(entry.colliders) + Count(entry.behaviours) == 0)
                    {
                        issues.Add(new TabletIssue(MessageType.Error, where + "切り替える対象がありません。"));
                    }

                    foreach (GameObject target in entry.objects)
                    {
                        if (target == null)
                        {
                            continue;
                        }

                        if (owners.TryGetValue(target, out string other))
                        {
                            issues.Add(new TabletIssue(MessageType.Warning,
                                where + target.name + " は " + other + " でも切り替えています。"));
                        }
                        else
                        {
                            owners.Add(target, where.TrimEnd(' ', ':'));
                        }

                        if (definition.transform.IsChildOf(target.transform) || target.transform.IsChildOf(definition.transform))
                        {
                            issues.Add(new TabletIssue(MessageType.Error, where + "タブレット自身やその親子は対象にできません。"));
                        }
                    }

                    if (!string.IsNullOrEmpty(entry.exclusiveGroup))
                    {
                        if (groupGlobal.TryGetValue(entry.exclusiveGroup, out bool global) && global != entry.global)
                        {
                            issues.Add(new TabletIssue(MessageType.Warning,
                                where + "グループ「" + entry.exclusiveGroup + "」に同期するものとしないものが混在しています。"));
                        }
                        else
                        {
                            groupGlobal[entry.exclusiveGroup] = entry.global;
                        }
                    }

                    break;

                case TabletEntryKind.Teleport:
                    if (entry.destination == null)
                    {
                        issues.Add(new TabletIssue(MessageType.Error, where + "移動先がありません。"));
                    }

                    break;

                case TabletEntryKind.CustomEvent:
                    if (entry.target == null)
                    {
                        issues.Add(new TabletIssue(MessageType.Error, where + "呼び出し先がありません。"));
                    }
                    else if (Array.IndexOf(ListEvents(entry.target.GetType()), entry.eventName) < 0)
                    {
                        issues.Add(new TabletIssue(MessageType.Error,
                            where + entry.target.GetType().Name + " に引数なしの public イベント「" + entry.eventName + "」がありません。"));
                    }
                    else if (entry.useArgument && entry.target.GetType().GetField("tabletArgument",
                        BindingFlags.Public | BindingFlags.Instance) == null)
                    {
                        issues.Add(new TabletIssue(MessageType.Error,
                            where + entry.target.GetType().Name + " に public int tabletArgument がありません。"));
                    }

                    break;

                case TabletEntryKind.PageLink:
                    if (entry.pageIndex < 0 || entry.pageIndex >= definition.pages.Count)
                    {
                        issues.Add(new TabletIssue(MessageType.Error, where + "リンク先のページ番号が範囲外です。"));
                    }

                    break;
            }
        }

        /// <summary>
        /// ボタンから呼べるイベント名。引数と戻り値の無い public メソッドのうち、
        /// UdonSharpBehaviour 自身が定義するものとその override を除いたものです。
        /// </summary>
        public static string[] ListEvents(Type type)
        {
            var names = new List<string>();
            if (type == null || !typeof(UdonSharpBehaviour).IsAssignableFrom(type))
            {
                return names.ToArray();
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.ReturnType != typeof(void) || method.GetParameters().Length != 0
                    || method.IsSpecialName || method.ContainsGenericParameters)
                {
                    continue;
                }

                Type declaring = method.GetBaseDefinition().DeclaringType;
                if (declaring == null || !typeof(UdonSharpBehaviour).IsAssignableFrom(declaring)
                    || declaring == typeof(UdonSharpBehaviour))
                {
                    continue;
                }

                if (!names.Contains(method.Name))
                {
                    names.Add(method.Name);
                }
            }

            names.Sort(StringComparer.Ordinal);
            return names.ToArray();
        }

        private static int Count<T>(T[] items) where T : UnityEngine.Object
        {
            int count = 0;
            if (items != null)
            {
                foreach (T item in items)
                {
                    if (item != null)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
