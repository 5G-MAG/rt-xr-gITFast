/*
* Copyright (c) 2023 InterDigital
* Licensed under the License terms of 5GMAG software (the "License").
* You may not use this file except in compliance with the License.
* You may obtain a copy of the License at https://www.5g-mag.com/license .
* Unless required by applicable law or agreed to in writing, software distributed under the License is
* distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and limitations under the License.
*/

using GLTFast.Schema;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace GLTFast
{
    /// <summary>
    /// Main component of interactivity, handle the activation of triggers 
    /// and invoke related actions
    /// </summary>
    public class Behavior : MonoBehaviour, IMpegInteractivityBehavior
    {
        private bool m_IsRunning = false;

        private IMpegInteractivityTrigger[] m_Trigger;
        private IMpegInteractivityAction[] m_Action;

        public bool IsShared => m_Shared;
        private bool m_Shared;

        private CombinationExpression m_Combination;
        [NonSerialized] private bool[] m_CurrentFrameResultStates;

        private bool m_LastFrameCombinationResult;
        private bool m_HasEverEntered;
        private bool m_HasEverExited;
        private bool m_HasAlreadyEntered;

        private Schema.Behavior.ActionsControl m_ActionControl;
        private TriggerActivationControl m_TriggerActivationControl;
        private IMpegInteractivityAction m_InterruptAction;
        private bool m_HasLaunched;
        private int m_Priority;
        private int[] m_AffectedNodes;

        public int Priority => m_Priority;
        public int[] AffectedNodes => m_AffectedNodes;

        private List<System.Action> m_GameEngineActions;

        public void InitializeBehavior(Schema.Behavior bhv)
        {
            // Cache trigger control
            m_TriggerActivationControl = bhv.triggersActivationControl;

            // Get behavior triggers instances
            m_Trigger = new IMpegInteractivityTrigger[bhv.triggers.Length];
            for (int i = 0; i < bhv.triggers.Length; i++)
            {
                int trigIndex = bhv.triggers[i];
                m_Trigger[i] = VirtualSceneGraph.GetTriggerFromIndex(trigIndex);
            }

            // Get behavior actions instances
            m_Action = new IMpegInteractivityAction[bhv.actions.Length];
            for (int i = 0; i < bhv.actions.Length; i++)
            {
                int actIndex = bhv.actions[i];
                m_Action[i] = VirtualSceneGraph.GetActionFromIndex(actIndex);
            }

            // triggersCombinationControl: '#' trigger index, '&' AND, '|' OR, '~' NOT, parentheses to group.
            // An empty string is a logical OR between all the triggers (ISO/IEC 23090-14:2024, Table 42).
            // '#n' reading, one for the whole string: when every n is listed in this behavior's triggers array,
            // n is a trigger index; otherwise n is a position in that array (the content in use does both).
            m_Combination = CombinationExpression.Parse(bhv.triggersCombinationControl, bhv.triggers);
            m_ActionControl = bhv.actionsControl;
            m_CurrentFrameResultStates = new bool[bhv.triggers.Length];

            // Table 42: priority arbitrates behaviors that affect the same nodes (see BehaviorArbiter).
            m_Priority = bhv.priority;
            var nodeSet = new HashSet<int>();
            for (int i = 0; i < m_Action.Length; i++)
            {
                int[] nodes = ActionAffectedNodes.Get(m_Action[i]);
                if (nodes == null) continue;
                for (int n = 0; n < nodes.Length; n++) nodeSet.Add(nodes[n]);
            }
            if (nodeSet.Count > 0)
            {
                m_AffectedNodes = new int[nodeSet.Count];
                nodeSet.CopyTo(m_AffectedNodes);
            }

            if (bhv.interruptAction >= 0)
            {
                int index = bhv.interruptAction;
                m_InterruptAction = VirtualSceneGraph.GetActionFromIndex(index);
            }

            // Helper to get game object actions associated with mpeg actions
            m_GameEngineActions = new List<System.Action>();
            m_IsRunning = true;
        }

        public bool AreTriggersActived()
        {
            if(!m_IsRunning)
            {
                return false;
            }

            RunTriggers();

            bool combinationResult = m_Combination.Evaluate(m_CurrentFrameResultStates);
            bool activated = IsActivated(m_TriggerActivationControl, m_LastFrameCombinationResult, combinationResult);
            if (combinationResult && !m_LastFrameCombinationResult) m_HasEverEntered = true;
            if (!combinationResult && m_LastFrameCombinationResult) m_HasEverExited = true;
            m_LastFrameCombinationResult = combinationResult;
            m_LastActivated = activated;
            return activated;
        }

        private void RunTriggers()
        {
            // Run all triggers and store the boolean result in an array
            for(int i = 0; i < m_Trigger.Length; i++)
            {
                m_CurrentFrameResultStates[i] = m_Trigger[i].MeetConditions();
            }
        }

        // Table 43: when the combination of the triggers launches the actions.
        private bool IsActivated(TriggerActivationControl control, bool last, bool now)
        {
            switch (control)
            {
                case TriggerActivationControl.TRIGGER_ACTIVATE_FIRST_ENTER: return now && !last && !m_HasEverEntered;
                case TriggerActivationControl.TRIGGER_ACTIVATE_EACH_ENTER: return now && !last;
                case TriggerActivationControl.TRIGGER_ACTIVE_ON: return now;
                case TriggerActivationControl.TRIGGER_ACTIVATE_FIRST_EXIT: return !now && last && !m_HasEverExited;
                case TriggerActivationControl.TRIGGER_ACTIVATE_EACH_EXIT: return !now && last;
                case TriggerActivationControl.TRIGGER_ACTIVATE_OFF: return !now;
            }
            return false;
        }

        public void ActivateActions()
        {
            m_HasLaunched = true;
            // on-going bookkeeping: the longest delay of the actions, and whether a media or animation action was launched
            float longest = 0f;
            for (int a = 0; a < m_Action.Length; a++)
            {
                if (m_Action[a] != null && m_Action[a].Delay > longest) longest = m_Action[a].Delay;
                if (m_Action[a] is ActionMedia || m_Action[a] is ActionAnimation) m_PlayLaunched = true;
            }
            m_DelayUntil = Mathf.Max(m_DelayUntil, Time.time + longest);
            if (m_ActionControl == Schema.Behavior.ActionsControl.SEQUENTIAL)
            {
                // Table 42: each action runs in the order of the actions array. Reading (the text does not
                // say whether delays add up): action i+1 is started when action i has run, that is after
                // the delay of action i, and then applies its own delay.
                StartCoroutine(RunSequentially());
            }
            else
            {
                // PARALLEL: all actions are started now, each applies its own delay.
                for(int i = 0; i < m_Action.Length; i++)
                {
                    InvokeAction(m_Action[i]);
                }
            }

            for(int i = 0; i < m_GameEngineActions.Count; i++)
            {
                try { m_GameEngineActions[i].Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        private System.Collections.IEnumerator RunSequentially()
        {
            for (int i = 0; i < m_Action.Length; i++)
            {
                var action = m_Action[i];
                InvokeAction(action);
                if (action != null && action.Delay > 0f)
                {
                    yield return new WaitForSeconds(action.Delay);
                    yield return null;   // the delayed action has run before the next one starts
                }
            }
        }

        // One failing action must not stop the others.
        private static void InvokeAction(IMpegInteractivityAction action)
        {
            if (action == null) return;
            try { action.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public void AddGameEngineAction(System.Action action)
        {
            m_GameEngineActions.Add(action);
        }

        /// <summary>
        /// Stops the on-going behavior and plays its interrupt action (Table 42, clause 8.2.3).
        /// A behavior is on-going when its trigger combination currently holds or when it has
        /// launched actions (a delay not yet expired, or a play action). This player receives no
        /// scene description updates; MpegSceneDynamicController calls this method when an update removes or modifies the behavior.
        /// </summary>
        /// <summary>8.2.3: a behavior is on-going when its triggers verify the activation control for the frame, or it has launched an action not yet finished (a play action, a delay not yet expired).</summary>
        public bool OnGoing { get { return m_IsRunning && (Time.time < m_DelayUntil || m_PlayLaunched || m_LastActivated); } }
        private float m_DelayUntil;
        // the triggers verified the triggersActivationControl value in the last frame (8.2.3, "an on-going behavior")
        private bool m_LastActivated;
        private bool m_PlayLaunched;

        public void Interrupt()
        {
            bool ongoing = m_HasLaunched || m_LastFrameCombinationResult;
            m_IsRunning = false;
            StopAllCoroutines();   // a sequence of actions that has not finished
            if (m_Action != null)
            {
                for (int i = 0; i < m_Action.Length; i++)
                {
                    // Cancel actions whose delay has not expired, then stop what they started.
                    var behaviour = m_Action[i] as MonoBehaviour;
                    if (behaviour != null) behaviour.StopAllCoroutines();
                    var stoppable = m_Action[i] as IMpegInteractivityStoppable;
                    if (stoppable != null) stoppable.Stop();
                }
            }
            if (ongoing && m_InterruptAction != null)
            {
                var interruptBehaviour = m_InterruptAction as MonoBehaviour;
                if (interruptBehaviour != null) interruptBehaviour.StopAllCoroutines();
                m_InterruptAction.Invoke();
            }
            m_HasLaunched = false;
            m_PlayLaunched = false;
            m_DelayUntil = 0f;
        }

        public void Dispose()
        {
            Destroy(gameObject);
        }
    }
}
namespace GLTFast
{
    /// <summary>
    /// Evaluates a triggersCombinationControl string against the trigger results of one frame.
    /// Grammar (highest precedence first): '~' (also '!'), '&amp;', '^', '|'; parentheses group.
    /// </summary>
    internal class CombinationExpression
    {
        private readonly string m_Text;
        private readonly int[] m_Slots;   // trigger slot for each '#n' in order of appearance
        private int m_Pos;
        private int m_SlotCursor;
        private bool[] m_States;
        private bool m_All;

        private CombinationExpression(string text, int[] slots, bool all)
        {
            m_Text = text; m_Slots = slots; m_All = all;
        }

        public static CombinationExpression Parse(string text, int[] behaviorTriggers)
        {
            text = (text ?? string.Empty).Replace(" ", string.Empty);
            if (text.Length == 0) return new CombinationExpression(text, new int[0], true);
            var numbers = new List<int>();
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '#') continue;
                int j = i + 1; int n = 0; bool any = false;
                while (j < text.Length && char.IsDigit(text[j])) { n = n * 10 + (text[j] - '0'); j++; any = true; }
                if (!any) { Debug.LogWarning("triggersCombinationControl: '#' without a number in '" + text + "'"); n = 0; }
                numbers.Add(n);
            }

            bool allListed = true;
            for (int i = 0; i < numbers.Count; i++)
            {
                if (System.Array.IndexOf(behaviorTriggers, numbers[i]) < 0) { allListed = false; break; }
            }

            var slots = new List<int>();
            for (int i = 0; i < numbers.Count; i++)
            {
                int n = numbers[i];
                int slot = allListed ? System.Array.IndexOf(behaviorTriggers, n) : n;
                if (slot >= behaviorTriggers.Length) { Debug.LogWarning("triggersCombinationControl: trigger #" + n + " is not part of this behavior"); slot = -1; }
                slots.Add(slot);
            }
            return new CombinationExpression(text, slots.ToArray(), false);
        }

        public bool Evaluate(bool[] states)
        {
            m_States = states;
            if (m_All)
            {
                for (int i = 0; i < states.Length; i++) if (states[i]) return true;
                return false;
            }
            m_Pos = 0; m_SlotCursor = 0;
            return ParseOr();
        }

        private bool ParseOr()
        {
            bool v = ParseXor();
            while (m_Pos < m_Text.Length && m_Text[m_Pos] == '|') { m_Pos++; bool r = ParseXor(); v = v || r; }
            return v;
        }
        private bool ParseXor()
        {
            bool v = ParseAnd();
            while (m_Pos < m_Text.Length && m_Text[m_Pos] == '^') { m_Pos++; bool r = ParseAnd(); v = v ^ r; }
            return v;
        }
        private bool ParseAnd()
        {
            bool v = ParseNot();
            while (m_Pos < m_Text.Length && m_Text[m_Pos] == '&') { m_Pos++; bool r = ParseNot(); v = v && r; }
            return v;
        }
        private bool ParseNot()
        {
            if (m_Pos < m_Text.Length && (m_Text[m_Pos] == '~' || m_Text[m_Pos] == '!')) { m_Pos++; return !ParseNot(); }
            return ParseAtom();
        }
        private bool ParseAtom()
        {
            if (m_Pos >= m_Text.Length) return false;
            char c = m_Text[m_Pos];
            if (c == '(')
            {
                m_Pos++; bool v = ParseOr();
                if (m_Pos < m_Text.Length && m_Text[m_Pos] == ')') m_Pos++;
                return v;
            }
            if (c == '#')
            {
                m_Pos++;
                while (m_Pos < m_Text.Length && char.IsDigit(m_Text[m_Pos])) m_Pos++;
                int slot = m_SlotCursor < m_Slots.Length ? m_Slots[m_SlotCursor] : -1;
                m_SlotCursor++;
                return slot >= 0 && slot < m_States.Length && m_States[slot];
            }
            m_Pos++;   // unknown character: skip
            return false;
        }
    }
}

namespace GLTFast
{
    internal static class NodeInteractivityLookup
    {
        /// <summary>The node-level trigger of the given type, or null (node triggers have distinct types).</summary>
        public static GLTFast.Schema.MpegNodeInteractivity.Trigger Find(GLTFast.Schema.Node node, GLTFast.Schema.TriggerType type)
        {
            var ext = node?.extensions?.MPEG_node_interactivity;
            if (ext?.triggers == null) return null;
            for (int i = 0; i < ext.triggers.Length; i++)
                if (ext.triggers[i].type == type) return ext.triggers[i];
            return null;
        }
    }
}
