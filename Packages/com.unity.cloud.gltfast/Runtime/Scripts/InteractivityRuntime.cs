using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    /// An action that can stop what it started (animation, media, block) when its
    /// behavior is interrupted.
    /// </summary>
    public interface IMpegInteractivityStoppable
    {
        void Stop();
    }

    /// <summary>
    /// Remembers the nodes each action modifies, so that behaviors in concurrence for the
    /// same node(s) can be arbitrated by priority (ISO/IEC 23090-14:2024, Table 42).
    /// </summary>
    internal static class ActionAffectedNodes
    {
        private static readonly ConditionalWeakTable<object, int[]> s_Nodes
            = new ConditionalWeakTable<object, int[]>();

        public static void Register(IMpegInteractivityAction instance, Schema.Action action)
        {
            if (instance == null || action == null || action.nodes == null) return;
            switch (action.type)
            {
                case Schema.ActionType.ACTION_ACTIVATE:
                case Schema.ActionType.ACTION_TRANSFORM:
                case Schema.ActionType.ACTION_BLOCK:
                case Schema.ActionType.ACTION_MANIPULATE:
                case Schema.ActionType.ACTION_SET_MATERIAL:
                    s_Nodes.Remove(instance);
                    s_Nodes.Add(instance, action.nodes);
                    break;
            }
        }

        public static int[] Get(IMpegInteractivityAction instance)
        {
            int[] nodes;
            if (instance != null && s_Nodes.TryGetValue(instance, out nodes)) return nodes;
            return null;
        }
    }

    /// <summary>
    /// Selects, among the behaviors activated in one frame, those that may run: when several
    /// behaviors affect the same node, only the one with the highest priority value is processed
    /// (Table 42). Equal priorities are resolved in the order of the behaviors array.
    /// </summary>
    internal static class BehaviorArbiter
    {
        public static void Select(List<IMpegInteractivityBehavior> activated, List<IMpegInteractivityBehavior> result)
        {
            result.Clear();
            // Stable insertion sort, highest priority first.
            var ordered = new List<IMpegInteractivityBehavior>(activated.Count);
            for (int i = 0; i < activated.Count; i++)
            {
                int pos = ordered.Count;
                while (pos > 0 && ordered[pos - 1].Priority < activated[i].Priority) pos--;
                ordered.Insert(pos, activated[i]);
            }

            var claimed = new HashSet<int>();
            for (int i = 0; i < ordered.Count; i++)
            {
                int[] nodes = ordered[i].AffectedNodes;
                bool conflict = false;
                if (nodes != null)
                {
                    for (int n = 0; n < nodes.Length; n++)
                    {
                        if (claimed.Contains(nodes[n])) { conflict = true; break; }
                    }
                }
                if (conflict) continue;
                if (nodes != null)
                {
                    for (int n = 0; n < nodes.Length; n++) claimed.Add(nodes[n]);
                }
                result.Add(ordered[i]);
            }
        }
    }
}

namespace GLTFast
{
    /// <summary>
    /// ISO/IEC 23090-14:2024, 8.2.1: a node whose MPEG_node_interactivity has a TRIGGER_COLLISION
    /// that no scene-level TRIGGER_COLLISION references is not used for collision detection, only
    /// by the physics engine. This gives such nodes their collider and rigid body, without targets.
    /// </summary>
    internal static class PhysicsOnlyNodes
    {
        public static void Apply(Schema.Root root, Schema.Scene scene)
        {
            if (root == null || root.nodes == null || scene == null || scene.nodes == null) return;

            var referenced = new HashSet<int>();
            var triggers = scene.extensions?.MPEG_scene_interactivity?.triggers;
            if (triggers != null)
            {
                for (int i = 0; i < triggers.Length; i++)
                {
                    if (triggers[i] == null || triggers[i].type != Schema.TriggerType.TRIGGER_COLLISION || triggers[i].nodes == null) continue;
                    for (int n = 0; n < triggers[i].nodes.Length; n++) referenced.Add(triggers[i].nodes[n]);
                }
            }

            var visited = new HashSet<int>();
            var stack = new Stack<int>();
            foreach (var id in scene.nodes) stack.Push((int)id);
            while (stack.Count > 0)
            {
                int index = stack.Pop();
                if (index < 0 || index >= root.nodes.Length || !visited.Add(index)) continue;
                var node = root.nodes[index];
                if (node.children != null)
                {
                    foreach (var child in node.children) stack.Push((int)child);
                }

                if (referenced.Contains(index)) continue;
                var nodeTrigger = NodeInteractivityLookup.Find(node, Schema.TriggerType.TRIGGER_COLLISION);
                if (nodeTrigger == null) continue;

                GameObject go = VirtualSceneGraph.GetGameObjectFromIndex(index);
                if (go == null || go.GetComponent<CollisionNodeTrigger>() != null) continue;
                var detector = go.AddComponent<CollisionNodeTrigger>();
                detector.InitExtension(nodeTrigger);
            }
        }
    }
}
