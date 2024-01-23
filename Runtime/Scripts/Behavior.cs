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
using System.Text.RegularExpressions;
using System.Threading;

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

        [NonSerialized] private char[] m_CombinationControlSymbols;
        private const char AND_COMBINATION = '&';
        private const char OR_COMBINATION = '|';
        private const char XOR_COMBINATION = '^';
        private const char NOT_COMBINATION = '!';
        [NonSerialized] private bool[] m_CurrentFrameResultStates;

        private bool m_LastFrameCombinationResult;
        private bool m_HasEverEntered;
        private bool m_HasEverExited;
        private bool m_HasAlreadyEntered;

        private Schema.Behavior.ActionsControl m_ActionControl;
        private TriggerActivationControl m_TriggerActivationControl;
        private IMpegInteractivityAction m_InterruptAction;

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

            // TODO: Find a better way to evaluate expression
            // Triggers combination control are written in a simple way: 'Trigger_index' 'Combination value'.
            // Trigger indexes are defined like this: #1 #2 #3
            // Combination value can be any bitwise operation like &, !, |, ^...
            string _withoutDiese = bhv.triggersCombinationControl.Replace("#", string.Empty);
            string _combination = Regex.Replace(_withoutDiese, @"[\d-]", string.Empty);

            m_CombinationControlSymbols = _combination.ToCharArray();
            m_CurrentFrameResultStates = new bool[bhv.triggers.Length];

            // TODO: Add behavior priority

            if (bhv.interruptAction.HasValue)
            {
                int index = bhv.interruptAction.Value;
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

            bool combinationResult = AreTriggersMeetCombinationControl();
            TriggerActivationControl actControl = ProcessState(m_HasEverExited, m_HasEverEntered, m_LastFrameCombinationResult, combinationResult);
            UpdateStates(actControl);
            m_LastFrameCombinationResult = combinationResult;
            return actControl == m_TriggerActivationControl;
        }

        private void RunTriggers()
        {
            // Run all triggers and store the boolean result in an array
            for(int i = 0; i < m_Trigger.Length; i++)
            {
                m_CurrentFrameResultStates[i] = m_Trigger[i].MeetConditions();
            }
        }

        private void UpdateStates(TriggerActivationControl actControl)
        {
            if(actControl == TriggerActivationControl.TRIGGER_ACTIVATE_FIRST_ENTER)
            {
                m_HasEverEntered = true;
            }
            else if(actControl == TriggerActivationControl.TRIGGER_ACTIVATE_FIRST_EXIT)
            {
                m_HasEverExited = true;
            }
        }

        private bool AreTriggersMeetCombinationControl()
        {
            bool result = false;

            // Compare pairs of booleans
            for (int i = 0; i < m_CurrentFrameResultStates.Length; i++)
            {
                result = m_CurrentFrameResultStates[i];

                if (i + 1 != m_CurrentFrameResultStates.Length)
                {
                    if(i >= m_CombinationControlSymbols.Length) {
                        break;
                    }

                    // Compare pairs of trigger results given a combination control symbol
                    switch (m_CombinationControlSymbols[i])
                    {
                        case AND_COMBINATION: result = m_CurrentFrameResultStates[i] && m_CurrentFrameResultStates[i + 1]; break;
                        case OR_COMBINATION: result = m_CurrentFrameResultStates[i] || m_CurrentFrameResultStates[i + 1]; break;
                        case XOR_COMBINATION: result = m_CurrentFrameResultStates[i] ^= m_CurrentFrameResultStates[i + 1]; break;
                        case NOT_COMBINATION: result = m_CurrentFrameResultStates[i] != m_CurrentFrameResultStates[i + 1]; break;
                    }

                    if (!result)
                    {
                        return false;
                    }
                }
            }

            return result;
        }

        private TriggerActivationControl ProcessState(bool hasEverExited, bool hasEverEntered, bool lastCombinationResult, bool combinationResult)
        {
            TriggerActivationControl result = 0;

            // Test activations
            if (combinationResult) result = TriggerActivationControl.TRIGGER_ACTIVE_ON;
            if (!combinationResult) result = TriggerActivationControl.TRIGGER_ACTIVATE_OFF;

            if (combinationResult && !hasEverEntered) result = TriggerActivationControl.TRIGGER_ACTIVATE_FIRST_ENTER;
            if (!lastCombinationResult && combinationResult) result = TriggerActivationControl.TRIGGER_ACTIVATE_EACH_ENTER;

            if (!combinationResult && lastCombinationResult && !hasEverExited) result = TriggerActivationControl.TRIGGER_ACTIVATE_FIRST_EXIT;
            if (!combinationResult && lastCombinationResult) result = TriggerActivationControl.TRIGGER_ACTIVATE_EACH_EXIT;

            return result;
        }

        public void ActivateActions()
        {
            for(int i = 0; i < m_Action.Length; i++)
            {
                // TODO: Very costly operation - should implement efficient multithreading
                if(m_ActionControl == Schema.Behavior.ActionsControl.PARALLEL)
                {
                    System.Action _act = () => m_Action[i].Invoke();
                    ThreadStart _st = new ThreadStart(_act);
                    Thread _thread = new Thread(_st);
                    _thread.Start();
                } 
                else
                {
                    m_Action[i].Invoke();
                }
            }

            for(int i = 0; i < m_GameEngineActions.Count; i++)
            {
                m_GameEngineActions[i].Invoke();
            }
        }

        public void AddGameEngineAction(System.Action action)
        {
            m_GameEngineActions.Add(action);
        }

        public void Interrupt()
        {
            m_IsRunning = false;
            m_InterruptAction?.Invoke();
        }
    }
}