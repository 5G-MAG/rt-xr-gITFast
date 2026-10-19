// MPEG_scene_dynamic (ISO/IEC 23090-14:2024(E), 5.2.4 and 7.2): scene updates arrive as samples
// of a track; a sync sample holds a complete glTF JSON document, a non-sync sample holds a JSON
// Patch document (RFC 6902). The media side hands each sample to MpegSceneUpdates.Enqueue with
// its presentation time (the scene activation time). This file holds the JSON document model,
// the patch application, the controller that activates updates, and the in-place update of the node
// graph (transform, hierarchy, added and removed nodes, mesh of an added node).
// Limit: changes to materials, meshes, textures, animations, cameras, skins, lights and new media
// need a rebuild of the loaded content, which the loader cannot do in place. Such a change is logged
// once, the live scene keeps its previous content for it, and the new document stays the base of later
// patches. MpegSceneUpdates.DocumentUpdated carries every new document.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace GLTFast {

    using Schema;

    /// <summary>
    /// Minimal JSON document model: null, bool, double, string, List&lt;object&gt;, Dictionary&lt;string,object&gt;.
    /// </summary>
    internal static class MiniJson {

        public static bool TryParse(string text, out object value) {
            value = null;
            if (string.IsNullOrEmpty(text)) return false;
            int p = 0;
            try {
                SkipWs(text, ref p);
                value = ParseValue(text, ref p);
                SkipWs(text, ref p);
                return p == text.Length;
            } catch (Exception) {
                value = null;
                return false;
            }
        }

        static void SkipWs(string s, ref int p) {
            while (p < s.Length && (s[p] == ' ' || s[p] == '\t' || s[p] == '\n' || s[p] == '\r' || s[p] == '﻿')) p++;
        }

        static object ParseValue(string s, ref int p) {
            SkipWs(s, ref p);
            if (p >= s.Length) throw new FormatException("end of text");
            char c = s[p];
            if (c == '{') {
                p++;
                var dict = new Dictionary<string, object>();
                SkipWs(s, ref p);
                if (s[p] == '}') { p++; return dict; }
                while (true) {
                    SkipWs(s, ref p);
                    string key = ParseString(s, ref p);
                    SkipWs(s, ref p);
                    if (s[p] != ':') throw new FormatException("colon");
                    p++;
                    dict[key] = ParseValue(s, ref p);
                    SkipWs(s, ref p);
                    if (s[p] == ',') { p++; continue; }
                    if (s[p] == '}') { p++; return dict; }
                    throw new FormatException("object");
                }
            }
            if (c == '[') {
                p++;
                var list = new List<object>();
                SkipWs(s, ref p);
                if (s[p] == ']') { p++; return list; }
                while (true) {
                    list.Add(ParseValue(s, ref p));
                    SkipWs(s, ref p);
                    if (s[p] == ',') { p++; continue; }
                    if (s[p] == ']') { p++; return list; }
                    throw new FormatException("array");
                }
            }
            if (c == '"') return ParseString(s, ref p);
            if (string.CompareOrdinal(s, p, "true", 0, 4) == 0) { p += 4; return true; }
            if (string.CompareOrdinal(s, p, "false", 0, 5) == 0) { p += 5; return false; }
            if (string.CompareOrdinal(s, p, "null", 0, 4) == 0) { p += 4; return null; }
            int start = p;
            while (p < s.Length && "+-0123456789.eE".IndexOf(s[p]) >= 0) p++;
            if (p == start) throw new FormatException("value");
            return double.Parse(s.Substring(start, p - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static string ParseString(string s, ref int p) {
            if (s[p] != '"') throw new FormatException("string");
            p++;
            var sb = new StringBuilder();
            while (true) {
                char c = s[p++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[p++];
                switch (e) {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        sb.Append((char)Convert.ToInt32(s.Substring(p, 4), 16));
                        p += 4;
                        break;
                    default: throw new FormatException("escape");
                }
            }
        }

        public static string Serialize(object v) {
            var sb = new StringBuilder();
            Write(sb, v);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object v) {
            if (v == null) { sb.Append("null"); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is double) {
                double d = (double)v;
                if (Math.Floor(d) == d && Math.Abs(d) < 1e15) sb.Append(((long)d).ToString(CultureInfo.InvariantCulture));
                else sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            var str = v as string;
            if (str != null) {
                sb.Append('"');
                foreach (char c in str) {
                    switch (c) {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                            else sb.Append(c);
                            break;
                    }
                }
                sb.Append('"');
                return;
            }
            var list = v as List<object>;
            if (list != null) {
                sb.Append('[');
                for (int i = 0; i < list.Count; i++) {
                    if (i > 0) sb.Append(',');
                    Write(sb, list[i]);
                }
                sb.Append(']');
                return;
            }
            var dict = v as Dictionary<string, object>;
            if (dict != null) {
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict) {
                    if (!first) sb.Append(',');
                    first = false;
                    Write(sb, kv.Key);
                    sb.Append(':');
                    Write(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            sb.Append("null");
        }

        public static object Clone(object v) {
            var list = v as List<object>;
            if (list != null) {
                var copy = new List<object>(list.Count);
                foreach (var item in list) copy.Add(Clone(item));
                return copy;
            }
            var dict = v as Dictionary<string, object>;
            if (dict != null) {
                var copy = new Dictionary<string, object>();
                foreach (var kv in dict) copy[kv.Key] = Clone(kv.Value);
                return copy;
            }
            return v;
        }

        public static bool Equal(object a, object b) {
            return Serialize(a) == Serialize(b);
        }
    }

    /// <summary>
    /// JSON Patch operations (add, remove, replace, move, copy, test) on a MiniJson document.
    /// </summary>
    internal static class JsonPatch {

        static string[] Tokens(string pointer) {
            if (pointer == null) throw new FormatException("path");
            if (pointer.Length == 0) return new string[0];
            if (pointer[0] != '/') throw new FormatException("pointer");
            var parts = pointer.Substring(1).Split('/');
            for (int i = 0; i < parts.Length; i++) {
                parts[i] = parts[i].Replace("~1", "/").Replace("~0", "~");
            }
            return parts;
        }

        static bool Child(object parent, string token, out object child) {
            child = null;
            var dict = parent as Dictionary<string, object>;
            if (dict != null) return dict.TryGetValue(token, out child);
            var list = parent as List<object>;
            int idx;
            if (list != null && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out idx) && idx >= 0 && idx < list.Count) {
                child = list[idx];
                return true;
            }
            return false;
        }

        static bool Parent(object root, string[] tokens, out object parent) {
            parent = root;
            for (int i = 0; i < tokens.Length - 1; i++) {
                object next;
                if (!Child(parent, tokens[i], out next)) return false;
                parent = next;
            }
            return parent != null;
        }

        static bool Get(object root, string[] tokens, out object value) {
            value = root;
            for (int i = 0; i < tokens.Length; i++) {
                object next;
                if (!Child(value, tokens[i], out next)) return false;
                value = next;
            }
            return true;
        }

        static bool Add(ref object root, string[] tokens, object value) {
            if (tokens.Length == 0) { root = value; return true; }
            object parent;
            if (!Parent(root, tokens, out parent)) return false;
            string last = tokens[tokens.Length - 1];
            var dict = parent as Dictionary<string, object>;
            if (dict != null) { dict[last] = value; return true; }
            var list = parent as List<object>;
            if (list == null) return false;
            if (last == "-") { list.Add(value); return true; }
            int idx;
            if (!int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out idx) || idx < 0 || idx > list.Count) return false;
            list.Insert(idx, value);
            return true;
        }

        static bool Remove(ref object root, string[] tokens) {
            if (tokens.Length == 0) return false;
            object parent;
            if (!Parent(root, tokens, out parent)) return false;
            string last = tokens[tokens.Length - 1];
            var dict = parent as Dictionary<string, object>;
            if (dict != null) return dict.Remove(last);
            var list = parent as List<object>;
            int idx;
            if (list == null || !int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out idx) || idx < 0 || idx >= list.Count) return false;
            list.RemoveAt(idx);
            return true;
        }

        static bool Replace(ref object root, string[] tokens, object value) {
            if (tokens.Length == 0) { root = value; return true; }
            object existing;
            if (!Get(root, tokens, out existing)) return false;
            object parent;
            if (!Parent(root, tokens, out parent)) return false;
            string last = tokens[tokens.Length - 1];
            var dict = parent as Dictionary<string, object>;
            if (dict != null) { dict[last] = value; return true; }
            var list = parent as List<object>;
            int idx;
            if (list == null || !int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out idx) || idx < 0 || idx >= list.Count) return false;
            list[idx] = value;
            return true;
        }

        /// <summary>
        /// Applies all operations to a copy of the document. An operation that fails on a node that
        /// is not in the document is discarded (5.2.4.1); any other failure rejects the whole patch.
        /// </summary>
        public static bool Apply(object document, object patch, out object result, out int discarded) {
            result = null;
            discarded = 0;
            var ops = patch as List<object>;
            if (ops == null) return false;
            object doc = MiniJson.Clone(document);
            foreach (var item in ops) {
                var op = item as Dictionary<string, object>;
                if (op == null) return false;
                object opNameObj, pathObj;
                if (!op.TryGetValue("op", out opNameObj) || !op.TryGetValue("path", out pathObj)) return false;
                string opName = opNameObj as string;
                string path = pathObj as string;
                bool ok;
                try {
                    ok = ApplyOne(ref doc, opName, path, op);
                } catch (FormatException) {
                    return false;
                }
                if (!ok) {
                    if (path != null && path.StartsWith("/nodes/", StringComparison.Ordinal)) {
                        discarded++;
                        continue;
                    }
                    return false;
                }
            }
            result = doc;
            return true;
        }

        static bool ApplyOne(ref object doc, string opName, string path, Dictionary<string, object> op) {
            string[] tokens = Tokens(path);
            object value;
            switch (opName) {
                case "add":
                    if (!op.TryGetValue("value", out value)) return false;
                    return Add(ref doc, tokens, MiniJson.Clone(value));
                case "remove":
                    return Remove(ref doc, tokens);
                case "replace":
                    if (!op.TryGetValue("value", out value)) return false;
                    return Replace(ref doc, tokens, MiniJson.Clone(value));
                case "move":
                case "copy": {
                    object fromObj;
                    if (!op.TryGetValue("from", out fromObj)) return false;
                    string[] from = Tokens(fromObj as string);
                    object src;
                    if (!Get(doc, from, out src)) return false;
                    object moved = MiniJson.Clone(src);
                    if (opName == "move" && !Remove(ref doc, from)) return false;
                    return Add(ref doc, tokens, moved);
                }
                case "test": {
                    if (!op.TryGetValue("value", out value)) return false;
                    object current;
                    return Get(doc, tokens, out current) && MiniJson.Equal(current, value);
                }
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Queue of scene updates handed over by the media side, plus the document state.
    /// </summary>
    public static class MpegSceneUpdates {

        public sealed class SceneUpdate {
            public int media;
            /// <summary>Track index of the media entry, or -1 when not known.</summary>
            public int track;
            /// <summary>Presentation time of the sample in seconds: the scene activation time.</summary>
            public double time;
            /// <summary>True for a sync sample (glTF JSON document), false for a JSON Patch document.</summary>
            public bool syncSample;
            public byte[] payload;
            /// <summary>update_mode of GLTFPatchConfigBox (7.2.2.3): 0 or 1. Only used for non-sync samples.</summary>
            public int updateMode;
        }

        static readonly List<SceneUpdate> pending = new List<SceneUpdate>();
        static readonly Dictionary<int, double> mediaTimes = new Dictionary<int, double>();

        /// <summary>Text of the glTF document that is being played (the base of the first patch).</summary>
        public static string OriginalDocument { get; private set; }

        /// <summary>Raised after an update was applied: the new document text and its activation time.</summary>
        public static event System.Action<string, double> DocumentUpdated;

        public static void SetOriginalDocument(string json) {
            OriginalDocument = json;
            lock (pending) {
                pending.Clear();
                mediaTimes.Clear();
            }
        }

        /// <summary>
        /// Hands over one sample of a scene update track (7.2.1). Called by the media side.
        /// </summary>
        public static void Enqueue(int media, int track, double activationTime, bool syncSample, byte[] utf8Payload, int updateMode = 0) {
            lock (pending) {
                var update = new SceneUpdate {
                    media = media, track = track, time = activationTime,
                    syncSample = syncSample, payload = utf8Payload, updateMode = updateMode
                };
                // ordered by activation time; equal times keep their order of arrival
                int at = pending.Count;
                while (at > 0 && pending[at - 1].time > activationTime) at--;
                pending.Insert(at, update);
            }
        }

        /// <summary>Reports the playback time of a media entry, used to decide when updates become active.</summary>
        public static void SetMediaTime(int media, double time) {
            lock (pending) {
                mediaTimes[media] = time;
            }
        }

        internal static bool TryDequeueDue(int media, int track, out SceneUpdate update) {
            lock (pending) {
                double now;
                bool haveClock = mediaTimes.TryGetValue(media, out now);
                for (int i = 0; i < pending.Count; i++) {
                    var u = pending[i];
                    if (u.media != media) continue;
                    if (track >= 0 && u.track >= 0 && u.track != track) continue;
                    // 5.2.4.3: the activation time is a presentation time of the media; without a media clock
                    // the sample waits
                    if (!haveClock || u.time > now) continue;
                    pending.RemoveAt(i);
                    update = u;
                    return true;
                }
            }
            update = null;
            return false;
        }

        internal static void RaiseDocumentUpdated(string json, double time) {
            var handler = DocumentUpdated;
            if (handler != null) {
                handler(json, time);
            }
        }
    }

    /// <summary>
    /// Activates scene updates at their activation time (5.2.4.3).
    /// </summary>
    public class MpegSceneDynamicController : MonoBehaviour {

        int m_Media;
        int m_Track;
        MpegSceneContext m_Context;
        object m_Active;       // document that results from the last applied update
        object m_LastSync;     // document of the last sync sample
        // live node objects, one entry per node of the document (null when the node has no object)
        List<GameObject> m_ActiveSlots;
        List<GameObject> m_SyncSlots;
        Transform m_RootParent;
        bool m_Initialised;
        // member of a node object used while a patch is applied: index of the node in the base document
        const string SlotKey = "\u0001slot";

        public void Init(MpegSceneContext context, MpegSceneDynamic ext) {
            m_Context = context;
            m_Media = ext.media;
            m_Track = ext.track;
            var media = context.Root.extensions != null ? context.Root.extensions.MPEG_media : null;
            if (media == null || media.media == null || m_Media < 0 || m_Media >= media.media.Length) {
                MpegWarn.Once("scene_dynamic/media", "MPEG_scene_dynamic: media " + m_Media + " is not listed in MPEG_media");
            }
        }

        bool EnsureBase() {
            if (m_Initialised) return m_Active != null;
            m_Initialised = true;
            object doc;
            if (MiniJson.TryParse(MpegSceneUpdates.OriginalDocument, out doc)) {
                m_Active = doc;
                m_LastSync = doc;
                m_ActiveSlots = LiveSlots(doc);
                m_SyncSlots = m_ActiveSlots;
                var sceneNodes = IntList(SceneNodes(doc, m_Context.SceneIndex));
                if (sceneNodes != null) {
                    foreach (int r in sceneNodes) {
                        if (r >= 0 && r < m_ActiveSlots.Count && m_ActiveSlots[r] != null) {
                            m_RootParent = m_ActiveSlots[r].transform.parent;
                            break;
                        }
                    }
                }
                return true;
            }
            MpegWarn.Once("scene_dynamic/base", "MPEG_scene_dynamic: the original document is not available, scene updates are ignored");
            return false;
        }

        void Update() {
            MpegSceneUpdates.SceneUpdate update;
            while (MpegSceneUpdates.TryDequeueDue(m_Media, m_Track, out update)) {
                try {
                    Activate(update);
                } catch (Exception e) {
                    Debug.LogWarning("MPEG_scene_dynamic: update failed: " + e.Message);
                }
            }
        }

        void Activate(MpegSceneUpdates.SceneUpdate update) {
            if (!EnsureBase()) return;
            if (update.payload == null || update.payload.Length == 0) return;
            string text = Encoding.UTF8.GetString(update.payload);
            object parsed;
            if (!MiniJson.TryParse(text, out parsed)) {
                Debug.LogWarning("MPEG_scene_dynamic: a sample is not valid JSON, update discarded");
                return;
            }
            object result;
            object baseDoc;
            List<GameObject> baseSlots;
            int[] origin;
            if (update.syncSample) {
                // a complete document: node i continues node i of the active document
                baseDoc = m_Active;
                baseSlots = m_ActiveSlots;
                result = parsed;
                MarkNodes(result);
                origin = Origins(result);
            } else {
                baseDoc = update.updateMode == 1 ? m_LastSync : m_Active;
                baseSlots = update.updateMode == 1 ? m_SyncSlots : m_ActiveSlots;
                object marked = MiniJson.Clone(baseDoc);
                MarkNodes(marked);
                int discarded;
                if (!JsonPatch.Apply(marked, parsed, out result, out discarded)) {
                    Debug.LogWarning("MPEG_scene_dynamic: patch could not be applied, update discarded");
                    return;
                }
                if (discarded > 0) {
                    Debug.LogWarning("MPEG_scene_dynamic: " + discarded + " update command(s) for nodes that are not in the active scene were discarded");
                }
                origin = Origins(result);
                // 5.2.4.1: the client updates the references after an update that removes or reorders nodes
                RenumberNodeReferences(result, origin, Count(baseDoc, "nodes"));
            }
            StripMarkers(result);
            if (!IsConsistent(result)) {
                Debug.LogWarning("MPEG_scene_dynamic: the updated scene graph is not consistent (a reference is out of range or the node graph has a cycle), update discarded");
                return;
            }

            var newSlots = new List<GameObject>();
            int nodeCount = Count(result, "nodes");
            for (int i = 0; i < nodeCount; i++) {
                GameObject slot = null;
                if (origin[i] >= 0 && origin[i] < baseSlots.Count && baseSlots[origin[i]] != null) slot = baseSlots[origin[i]];
                newSlots.Add(slot);
            }
            InterruptBehaviors(baseDoc, result, origin);
            ApplyScene(baseDoc, result, origin, baseSlots, newSlots);
            m_Active = result;
            m_ActiveSlots = newSlots;
            if (update.syncSample) {
                m_LastSync = result;
                m_SyncSlots = newSlots;
            }
            MpegSceneUpdates.RaiseDocumentUpdated(MiniJson.Serialize(result), update.time);
        }

        // The behaviors of the interactivity extension of the scene (or of the file level extension)
        List<object> BehaviorsOf(object doc, out List<object> actions) {
            actions = null;
            var scenes = ListOf(doc, "scenes");
            Dictionary<string, object> ext = null;
            if (scenes != null && m_Context.SceneIndex >= 0 && m_Context.SceneIndex < scenes.Count) {
                var sd = scenes[m_Context.SceneIndex] as Dictionary<string, object>;
                object e;
                if (sd != null && sd.TryGetValue("extensions", out e)) {
                    var ed = e as Dictionary<string, object>;
                    object si;
                    if (ed != null && ed.TryGetValue("MPEG_scene_interactivity", out si)) ext = si as Dictionary<string, object>;
                }
            }
            if (ext == null) {
                var root = doc as Dictionary<string, object>;
                object e;
                if (root != null && root.TryGetValue("extensions", out e)) {
                    var ed = e as Dictionary<string, object>;
                    object si;
                    if (ed != null && ed.TryGetValue("MPEG_scene_interactivity", out si)) ext = si as Dictionary<string, object>;
                }
            }
            if (ext == null) return null;
            object a, b;
            if (ext.TryGetValue("actions", out a)) actions = a as List<object>;
            return ext.TryGetValue("behaviors", out b) ? b as List<object> : null;
        }

        // 8.2.3, Figure 13: "When a new scene description update is received": an on-going behavior that is no more defined is stopped and
        // its interrupt action is processed; "To check if the behavior is still defined, the application should check if the scene description
        // update leads to: removal of this behavior in the behaviors array; modification of any parameter of this behavior; removal of any of the
        // referenced nodes by the action."
        void InterruptBehaviors(object before, object after, int[] origin) {
            List<object> beforeActions, afterActions;
            var oldBehaviors = BehaviorsOf(before, out beforeActions);
            if (oldBehaviors == null) return;
            var newBehaviors = BehaviorsOf(after, out afterActions);
            for (int i = 0; i < oldBehaviors.Count; i++) {
                var behavior = VirtualSceneGraph.GetBehaviorByIndex(i) as Behavior;
                if (behavior == null || !behavior.OnGoing) continue;
                // the behavior is still defined when an identical one is in the updated array (removing another behavior shifts the indices)
                string oldText = MiniJson.Serialize(oldBehaviors[i]);
                bool defined = false;
                if (newBehaviors != null) foreach (var candidate in newBehaviors) if (MiniJson.Serialize(candidate) == oldText) { defined = true; break; }
                if (defined && beforeActions != null) {
                    // removal of a node that an action of the behavior refers to
                    var bd = oldBehaviors[i] as Dictionary<string, object>;
                    object acts;
                    if (bd != null && bd.TryGetValue("actions", out acts)) {
                        foreach (int ai in IntList(acts) ?? new List<int>()) {
                            if (ai < 0 || ai >= beforeActions.Count) continue;
                            var ad = beforeActions[ai] as Dictionary<string, object>;
                            object nodes;
                            if (ad == null || !ad.TryGetValue("nodes", out nodes)) continue;
                            foreach (int n in IntList(nodes) ?? new List<int>()) {
                                bool kept = false;
                                for (int j = 0; j < origin.Length; j++) if (origin[j] == n) { kept = true; break; }
                                if (!kept) defined = false;
                            }
                        }
                    }
                }
                if (!defined) {
                    Debug.Log("XRTEST behavior " + i + " is on-going and no more defined by the update: it is stopped, its interrupt action is processed");
                    behavior.Interrupt();
                }
            }
        }

        static List<object> ListOf(object doc, string key) {
            var d = doc as Dictionary<string, object>;
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v as List<object>;
            return null;
        }

        static int Count(object doc, string key) {
            var l = ListOf(doc, key);
            return l == null ? 0 : l.Count;
        }

        static bool IndexOk(object v, int count) {
            if (!(v is double)) return false;
            double d = (double)v;
            return d >= 0 && d < count && Math.Floor(d) == d;
        }

        /// <summary>
        /// References that the update commands can break: scene roots, node children, mesh, camera and skin of a
        /// node, skin joints, primitive materials and animation targets. A cycle in the node graph is refused
        /// because the instantiated hierarchy cannot contain one.
        /// </summary>
        static bool IsConsistent(object doc) {
            var root = doc as Dictionary<string, object>;
            if (root == null) return false;
            int nodes = Count(doc, "nodes");
            var scenes = ListOf(doc, "scenes");
            if (scenes != null) {
                foreach (var s in scenes) {
                    var sd = s as Dictionary<string, object>;
                    object sn;
                    if (sd != null && sd.TryGetValue("nodes", out sn)) {
                        var l = sn as List<object>;
                        if (l == null) return false;
                        foreach (var n in l) if (!IndexOk(n, nodes)) return false;
                    }
                }
            }
            var nodeList = ListOf(doc, "nodes");
            if (nodeList != null) {
                foreach (var n in nodeList) {
                    var nd = n as Dictionary<string, object>;
                    if (nd == null) return false;
                    object v;
                    if (nd.TryGetValue("children", out v)) {
                        var l = v as List<object>;
                        if (l == null) return false;
                        foreach (var c in l) if (!IndexOk(c, nodes)) return false;
                    }
                    if (nd.TryGetValue("mesh", out v) && !IndexOk(v, Count(doc, "meshes"))) return false;
                    if (nd.TryGetValue("camera", out v) && !IndexOk(v, Count(doc, "cameras"))) return false;
                    if (nd.TryGetValue("skin", out v) && !IndexOk(v, Count(doc, "skins"))) return false;
                }
                if (HasCycle(nodeList)) return false;
            }
            var skins = ListOf(doc, "skins");
            if (skins != null) {
                foreach (var s in skins) {
                    var sd = s as Dictionary<string, object>;
                    if (sd == null) return false;
                    object v;
                    if (sd.TryGetValue("skeleton", out v) && !IndexOk(v, nodes)) return false;
                    if (sd.TryGetValue("joints", out v)) {
                        var l = v as List<object>;
                        if (l == null) return false;
                        foreach (var j in l) if (!IndexOk(j, nodes)) return false;
                    }
                }
            }
            var meshes = ListOf(doc, "meshes");
            if (meshes != null) {
                int materials = Count(doc, "materials");
                foreach (var m in meshes) {
                    var md = m as Dictionary<string, object>;
                    object prims;
                    if (md == null || !md.TryGetValue("primitives", out prims)) continue;
                    var pl = prims as List<object>;
                    if (pl == null) return false;
                    foreach (var pr in pl) {
                        var pd = pr as Dictionary<string, object>;
                        object mat;
                        if (pd != null && pd.TryGetValue("material", out mat) && !IndexOk(mat, materials)) return false;
                    }
                }
            }
            var animations = ListOf(doc, "animations");
            if (animations != null) {
                foreach (var a in animations) {
                    var ad = a as Dictionary<string, object>;
                    object channels;
                    if (ad == null || !ad.TryGetValue("channels", out channels)) continue;
                    var cl = channels as List<object>;
                    if (cl == null) return false;
                    foreach (var ch in cl) {
                        var cd = ch as Dictionary<string, object>;
                        object target;
                        if (cd == null || !cd.TryGetValue("target", out target)) continue;
                        var td = target as Dictionary<string, object>;
                        object tn;
                        if (td != null && td.TryGetValue("node", out tn) && !IndexOk(tn, nodes)) return false;
                    }
                    // sampler input and output accessors
                    object samplers;
                    if (ad.TryGetValue("samplers", out samplers)) {
                        var sl = samplers as List<object>;
                        if (sl == null) return false;
                        foreach (var sm in sl) {
                            var smd = sm as Dictionary<string, object>;
                            object ai;
                            if (smd == null) return false;
                            if (smd.TryGetValue("input", out ai) && !IndexOk(ai, Count(doc, "accessors"))) return false;
                            if (smd.TryGetValue("output", out ai) && !IndexOk(ai, Count(doc, "accessors"))) return false;
                        }
                    }
                }
            }
            return ReferencesConsistent(doc);
        }

        // the references between accessors, buffer views, buffers, textures, images, samplers and materials
        static bool ReferencesConsistent(object doc) {
            int accessors = Count(doc, "accessors"), views = Count(doc, "bufferViews"), buffers = Count(doc, "buffers");
            int textures = Count(doc, "textures"), images = Count(doc, "images"), samplers = Count(doc, "samplers");
            object v;
            var accessorList = ListOf(doc, "accessors");
            if (accessorList != null) foreach (var a in accessorList) {
                var ad = a as Dictionary<string, object>;
                if (ad == null) return false;
                if (ad.TryGetValue("bufferView", out v) && !IndexOk(v, views)) return false;
            }
            var viewList = ListOf(doc, "bufferViews");
            if (viewList != null) foreach (var b in viewList) {
                var bd = b as Dictionary<string, object>;
                if (bd == null) return false;
                if (bd.TryGetValue("buffer", out v) && !IndexOk(v, buffers)) return false;
            }
            var textureList = ListOf(doc, "textures");
            if (textureList != null) foreach (var t in textureList) {
                var td = t as Dictionary<string, object>;
                if (td == null) return false;
                if (td.TryGetValue("source", out v) && !IndexOk(v, images)) return false;
                if (td.TryGetValue("sampler", out v) && !IndexOk(v, samplers)) return false;
            }
            var imageList = ListOf(doc, "images");
            if (imageList != null) foreach (var im in imageList) {
                var imd = im as Dictionary<string, object>;
                if (imd == null) return false;
                if (imd.TryGetValue("bufferView", out v) && !IndexOk(v, views)) return false;
            }
            var meshList = ListOf(doc, "meshes");
            if (meshList != null) foreach (var m in meshList) {
                var md = m as Dictionary<string, object>;
                object prims;
                if (md == null || !md.TryGetValue("primitives", out prims)) continue;
                foreach (var pr in (prims as List<object>) ?? new List<object>()) {
                    var pd = pr as Dictionary<string, object>;
                    if (pd == null) return false;
                    if (pd.TryGetValue("indices", out v) && !IndexOk(v, accessors)) return false;
                    object attrs;
                    if (pd.TryGetValue("attributes", out attrs)) {
                        var atd = attrs as Dictionary<string, object>;
                        if (atd == null) return false;
                        foreach (var kv in atd) if (!IndexOk(kv.Value, accessors)) return false;
                    }
                }
            }
            var materialList = ListOf(doc, "materials");
            if (materialList != null) foreach (var m in materialList) {
                var md = m as Dictionary<string, object>;
                if (md == null) return false;
                if (!TextureRefOk(md, "normalTexture", textures) || !TextureRefOk(md, "occlusionTexture", textures) || !TextureRefOk(md, "emissiveTexture", textures)) return false;
                object pbr;
                if (md.TryGetValue("pbrMetallicRoughness", out pbr)) {
                    var pd = pbr as Dictionary<string, object>;
                    if (pd == null) return false;
                    if (!TextureRefOk(pd, "baseColorTexture", textures) || !TextureRefOk(pd, "metallicRoughnessTexture", textures)) return false;
                }
            }
            return true;
        }

        static bool TextureRefOk(Dictionary<string, object> owner, string key, int textures) {
            object r;
            if (!owner.TryGetValue(key, out r)) return true;
            var rd = r as Dictionary<string, object>;
            object index;
            return rd != null && rd.TryGetValue("index", out index) && IndexOk(index, textures);
        }

        static bool HasCycle(List<object> nodeList) {
            // 0 unvisited, 1 on the current path, 2 done
            var state = new int[nodeList.Count];
            for (int s = 0; s < nodeList.Count; s++) {
                if (state[s] != 0) continue;
                var stack = new Stack<KeyValuePair<int, int>>();
                stack.Push(new KeyValuePair<int, int>(s, 0));
                state[s] = 1;
                while (stack.Count > 0) {
                    var top = stack.Pop();
                    var children = IntList(ChildrenOf(nodeList[top.Key]));
                    if (children != null && top.Value < children.Count) {
                        stack.Push(new KeyValuePair<int, int>(top.Key, top.Value + 1));
                        int c = children[top.Value];
                        if (c < 0 || c >= nodeList.Count) continue;
                        if (state[c] == 1) return true;
                        if (state[c] == 0) {
                            state[c] = 1;
                            stack.Push(new KeyValuePair<int, int>(c, 0));
                        }
                    } else {
                        state[top.Key] = 2;
                    }
                }
            }
            return false;
        }

        static object ChildrenOf(object node) {
            var d = node as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue("children", out v) ? v : null;
        }

        static object SceneNodes(object doc, int sceneIndex) {
            var scenes = ListOf(doc, "scenes");
            if (scenes == null || sceneIndex < 0 || sceneIndex >= scenes.Count) return null;
            var sd = scenes[sceneIndex] as Dictionary<string, object>;
            object v;
            return sd != null && sd.TryGetValue("nodes", out v) ? v : null;
        }

        /// <summary>Integer values of a JSON array of indices, or null when it is not one.</summary>
        static List<int> IntList(object v) {
            var l = v as List<object>;
            if (l == null) return null;
            var result = new List<int>(l.Count);
            foreach (var item in l) {
                if (!(item is double)) return null;
                result.Add((int)(double)item);
            }
            return result;
        }

        List<GameObject> LiveSlots(object doc) {
            int count = Count(doc, "nodes");
            var slots = new List<GameObject>(count);
            for (int i = 0; i < count; i++) {
                GameObject go;
                slots.Add(m_Context != null && m_Context.NodeObjects.TryGetValue(i, out go) && go != null ? go : null);
            }
            return slots;
        }

        // Each node object of a base document gets the index it has there, so that the nodes can be
        // followed through the operations of a patch (add, remove, move and copy reorder the array).
        static void MarkNodes(object doc) {
            var l = ListOf(doc, "nodes");
            if (l == null) return;
            for (int i = 0; i < l.Count; i++) {
                var d = l[i] as Dictionary<string, object>;
                if (d != null) d[SlotKey] = (double)i;
            }
        }

        /// <summary>Base index of every node of the result, or -1 for a node that is new (a copy keeps the mark only once).</summary>
        static int[] Origins(object doc) {
            var l = ListOf(doc, "nodes");
            var origin = new int[l == null ? 0 : l.Count];
            var seen = new HashSet<int>();
            for (int i = 0; i < origin.Length; i++) {
                origin[i] = -1;
                var d = l[i] as Dictionary<string, object>;
                object v;
                if (d != null && d.TryGetValue(SlotKey, out v) && v is double) {
                    int k = (int)(double)v;
                    if (seen.Add(k)) origin[i] = k;
                }
            }
            return origin;
        }

        static void StripMarkers(object doc) {
            var l = ListOf(doc, "nodes");
            if (l == null) return;
            foreach (var n in l) {
                var d = n as Dictionary<string, object>;
                if (d != null) d.Remove(SlotKey);
            }
        }

        // Maps a reference to a node of the base document to its index in the result. Returns false when that
        // node was removed. References to indices beyond the base node count are written for the result already.
        static bool Remap(int[] map, object value, out object mapped) {
            mapped = value;
            if (!(value is double)) return true;
            int idx = (int)(double)value;
            if (idx < 0 || idx >= map.Length) return true;
            if (map[idx] < 0) return false;
            mapped = (double)map[idx];
            return true;
        }

        static void RemapList(int[] map, object list) {
            var l = list as List<object>;
            if (l == null) return;
            for (int i = l.Count - 1; i >= 0; i--) {
                object mapped;
                if (!Remap(map, l[i], out mapped)) l.RemoveAt(i);
                else l[i] = mapped;
            }
        }

        /// <summary>
        /// 5.2.4.1: "The client shall update all references after every successful scene document update
        /// operation." Reading taken here: a patch addresses nodes by the numbering of the document it applies
        /// to, so when it removes or reorders nodes the references to nodes (scene roots, children of the nodes
        /// that already existed, skin joints and skeleton, animation targets) follow their node, and a reference
        /// to a removed node is dropped. References written by the patch for nodes it adds are left as written.
        /// </summary>
        static void RenumberNodeReferences(object doc, int[] origin, int baseCount) {
            bool identity = origin.Length == baseCount;
            for (int i = 0; identity && i < origin.Length; i++) {
                if (origin[i] != i) identity = false;
            }
            if (identity) return;
            var map = new int[baseCount];
            for (int i = 0; i < baseCount; i++) map[i] = -1;
            for (int i = 0; i < origin.Length; i++) {
                if (origin[i] >= 0 && origin[i] < baseCount) map[origin[i]] = i;
            }
            var scenes = ListOf(doc, "scenes");
            if (scenes != null) {
                foreach (var s in scenes) {
                    var sd = s as Dictionary<string, object>;
                    object v;
                    if (sd != null && sd.TryGetValue("nodes", out v)) RemapList(map, v);
                }
            }
            var nodes = ListOf(doc, "nodes");
            if (nodes != null) {
                for (int i = 0; i < nodes.Count; i++) {
                    if (origin[i] < 0) continue;
                    var nd = nodes[i] as Dictionary<string, object>;
                    object v;
                    if (nd != null && nd.TryGetValue("children", out v)) RemapList(map, v);
                }
            }
            var skins = ListOf(doc, "skins");
            if (skins != null) {
                foreach (var s in skins) {
                    var sd = s as Dictionary<string, object>;
                    if (sd == null) continue;
                    object v, mapped;
                    if (sd.TryGetValue("joints", out v)) RemapList(map, v);
                    if (sd.TryGetValue("skeleton", out v) && !Remap(map, v, out mapped)) sd.Remove("skeleton");
                    else if (sd.TryGetValue("skeleton", out v)) { Remap(map, v, out mapped); sd["skeleton"] = mapped; }
                }
            }
            var animations = ListOf(doc, "animations");
            if (animations != null) {
                foreach (var a in animations) {
                    var ad = a as Dictionary<string, object>;
                    object channels;
                    if (ad == null || !ad.TryGetValue("channels", out channels)) continue;
                    var cl = channels as List<object>;
                    if (cl == null) continue;
                    for (int c = cl.Count - 1; c >= 0; c--) {
                        var cd = cl[c] as Dictionary<string, object>;
                        object target;
                        if (cd == null || !cd.TryGetValue("target", out target)) continue;
                        var td = target as Dictionary<string, object>;
                        object tn, mapped;
                        if (td == null || !td.TryGetValue("node", out tn)) continue;
                        if (!Remap(map, tn, out mapped)) cl.RemoveAt(c);
                        else td["node"] = mapped;
                    }
                }
            }
        }

        static float[] Floats(object v, int length) {
            var l = v as List<object>;
            if (l == null || l.Count != length) return null;
            var f = new float[length];
            for (int i = 0; i < length; i++) {
                if (!(l[i] is double)) return null;
                f[i] = (float)(double)l[i];
            }
            return f;
        }

        static bool SameValue(Dictionary<string, object> a, Dictionary<string, object> b, string key) {
            object va = null, vb = null;
            if (a != null) a.TryGetValue(key, out va);
            if (b != null) b.TryGetValue(key, out vb);
            return MiniJson.Equal(va, vb);
        }

        static bool TransformChanged(Dictionary<string, object> before, Dictionary<string, object> after) {
            return !SameValue(before, after, "translation") || !SameValue(before, after, "rotation") ||
                   !SameValue(before, after, "scale") || !SameValue(before, after, "matrix");
        }

        // a node without the member in the document that is not a transform, a name or the children
        static object WithoutGraphMembers(object node) {
            var copy = MiniJson.Clone(node) as Dictionary<string, object>;
            if (copy == null) return node;
            copy.Remove("translation");
            copy.Remove("rotation");
            copy.Remove("scale");
            copy.Remove("matrix");
            copy.Remove("children");
            copy.Remove("name");
            return copy;
        }

        // the document without the node graph (the nodes and the root nodes of the scenes)
        static object WithoutNodeGraph(object doc) {
            var copy = MiniJson.Clone(doc) as Dictionary<string, object>;
            if (copy == null) return doc;
            copy.Remove("nodes");
            var scenes = ListOf(copy, "scenes");
            if (scenes != null) {
                foreach (var s in scenes) {
                    var sd = s as Dictionary<string, object>;
                    if (sd != null) sd.Remove("nodes");
                }
            }
            return copy;
        }

        static void SetTransform(GameObject go, Dictionary<string, object> nd) {
            var node = new Node();
            object v;
            if (nd.TryGetValue("translation", out v)) node.translation = Floats(v, 3);
            if (nd.TryGetValue("rotation", out v)) node.rotation = Floats(v, 4);
            if (nd.TryGetValue("scale", out v)) node.scale = Floats(v, 3);
            if (nd.TryGetValue("matrix", out v)) node.matrix = Floats(v, 16);
            Vector3 position, scale;
            Quaternion rotation;
            node.GetTransform(out position, out rotation, out scale);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
        }

        /// <summary>
        /// Brings the instantiated scene to the new document: transforms of changed nodes, the hierarchy, nodes
        /// that were added (with the meshes that were loaded with the file) and nodes that left the scene.
        /// </summary>
        void ApplyScene(object before, object after, int[] origin, List<GameObject> baseSlots, List<GameObject> slots) {
            var nodes = ListOf(after, "nodes");
            var oldNodes = ListOf(before, "nodes");
            int n = nodes == null ? 0 : nodes.Count;

            // nodes reachable from the root nodes of the scene, and the parent of each
            var parent = new int[n];
            var reachable = new bool[n];
            var stack = new Stack<int>();
            var roots = IntList(SceneNodes(after, m_Context.SceneIndex));
            if (roots != null) {
                foreach (int r in roots) {
                    if (r < 0 || r >= n || reachable[r]) continue;
                    reachable[r] = true;
                    parent[r] = -1;
                    stack.Push(r);
                }
            }
            while (stack.Count > 0) {
                int i = stack.Pop();
                var children = IntList(ChildrenOf(nodes[i]));
                if (children == null) continue;
                foreach (int c in children) {
                    if (c < 0 || c >= n || reachable[c]) continue;
                    reachable[c] = true;
                    parent[c] = i;
                    stack.Push(c);
                }
            }

            bool limit = false;
            for (int i = 0; i < n; i++) {
                if (!reachable[i]) continue;
                var nd = nodes[i] as Dictionary<string, object>;
                if (nd == null) continue;
                if (slots[i] == null) {
                    slots[i] = CreateNode(i, nd);
                    continue;
                }
                Dictionary<string, object> old = null;
                if (oldNodes != null && origin[i] >= 0 && origin[i] < oldNodes.Count) old = oldNodes[origin[i]] as Dictionary<string, object>;
                if (old == null) continue;
                if (TransformChanged(old, nd)) SetTransform(slots[i], nd);
                if (!MiniJson.Equal(WithoutGraphMembers(old), WithoutGraphMembers(nd))) limit = true;
            }
            // hierarchy, before anything is destroyed so that surviving children leave a removed parent
            for (int i = 0; i < n; i++) {
                if (!reachable[i] || slots[i] == null) continue;
                Transform wanted = parent[i] >= 0 && slots[parent[i]] != null ? slots[parent[i]].transform : m_RootParent;
                if (slots[i].transform.parent != wanted) slots[i].transform.SetParent(wanted, false);
            }
            var used = new HashSet<GameObject>();
            for (int i = 0; i < n; i++) {
                if (reachable[i] && slots[i] != null) used.Add(slots[i]);
                else slots[i] = null;
            }
            DestroyUnused(m_ActiveSlots, used);
            DestroyUnused(baseSlots, used);

            m_Context.NodeObjects.Clear();
            m_Context.NodeIndices.Clear();
            for (int i = 0; i < n; i++) {
                if (slots[i] == null) continue;
                m_Context.NodeObjects[i] = slots[i];
                m_Context.NodeIndices.Add(i);
            }

            if (limit) {
                MpegWarn.Once("scene_dynamic/nodeparts",
                    "MPEG_scene_dynamic: an update changes the mesh, skin, camera or an extension of a node that already exists; those parts stay as loaded (limit of the in-place update)");
            }
            if (!MiniJson.Equal(WithoutNodeGraph(before), WithoutNodeGraph(after))) {
                MpegWarn.Once("scene_dynamic/content",
                    "MPEG_scene_dynamic: an update changes materials, meshes, textures, animations, cameras, skins or media; these are not rebuilt in place, the live scene keeps its loaded content (limit of the in-place update)");
            }
        }

        static void DestroyUnused(List<GameObject> list, HashSet<GameObject> used) {
            if (list == null) return;
            foreach (var go in list) {
                if (go != null && !used.Contains(go)) Destroy(go);
            }
        }

        GameObject CreateNode(int index, Dictionary<string, object> nd) {
            object v;
            string name = nd.TryGetValue("name", out v) ? v as string : null;
            var go = new GameObject(string.IsNullOrEmpty(name) ? "node " + index : name);
            SetTransform(go, nd);
            if (nd.ContainsKey("skin") || nd.ContainsKey("camera") || nd.ContainsKey("extensions")) {
                MpegWarn.Once("scene_dynamic/newnode",
                    "MPEG_scene_dynamic: a node added by an update is created with its transform and mesh only; skin, camera and extensions are not instantiated (limit of the in-place update)");
            }
            if (nd.TryGetValue("mesh", out v) && v is double) AttachMesh(go, (int)(double)v);
            return go;
        }

        // Only meshes that were loaded with the file can be shown; their primitives are rendered without skinning.
        void AttachMesh(GameObject go, int meshIndex) {
            var root = m_Context.Root;
            var meshes = m_Context.Import.GetMeshPrimitiveMeshes(meshIndex);
            if (meshes == null || root.meshes == null || meshIndex >= root.meshes.Length || root.meshes[meshIndex].primitives == null) {
                MpegWarn.Once("scene_dynamic/mesh", "MPEG_scene_dynamic: mesh " + meshIndex + " of an added node was not loaded with the file, the node has no geometry");
                return;
            }
            var primitives = root.meshes[meshIndex].primitives;
            int next = 0;
            for (int k = 0; k < meshes.Length; k++) {
                if (meshes[k] == null) continue;
                GameObject target = go;
                if (meshes.Length > 1) {
                    target = new GameObject("primitive " + k);
                    target.transform.SetParent(go.transform, false);
                }
                target.AddComponent<MeshFilter>().sharedMesh = meshes[k];
                var materials = new UnityEngine.Material[Math.Max(1, meshes[k].subMeshCount)];
                for (int s = 0; s < materials.Length; s++) {
                    int materialIndex = next < primitives.Length ? primitives[next].material : -1;
                    next++;
                    materials[s] = m_Context.Import.GetMaterial(materialIndex) ?? m_Context.Import.GetDefaultMaterial();
                }
                target.AddComponent<MeshRenderer>().sharedMaterials = materials;
            }
        }
    }
}
