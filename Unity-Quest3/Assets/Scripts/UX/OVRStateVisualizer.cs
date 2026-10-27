using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Playables;

using MixedReality.Toolkit;
using MixedReality.Toolkit.UX;

using WeightType = MixedReality.Toolkit.UX.IAnimationMixableEffect.WeightType;

/// <summary>
/// A Playables-based interaction feedback system specialized for OVRPressableButton.
/// Adapted from MRTK StateVisualizer, but reads states from OVRPressableButton.
/// </summary>
[AddComponentMenu("Custom/OVR State Visualizer")]
[RequireComponent(typeof(Animator))]
public class OVRStateVisualizer : MonoBehaviour
{
    private const float keepAliveTime = 0.2f;
    private const int defaultWakeUpEventCount = 8;

    [Serializable]
    public class State
    {
        [SerializeReference]
        [Tooltip("The list of effects to apply.")]
        private List<IEffect> effects = new List<IEffect>();

        public List<IEffect> Effects => effects;

        public float Value { get; set; }
        public float PreviousValue { get; set; }

        [SerializeField]
        private bool isVariable;

        public bool IsVariable
        {
            get => isVariable;
            set => isVariable = value;
        }
    }

    [SerializeField]
    private SerializableDictionary<string, State> stateContainers = new SerializableDictionary<string, State>();

    [SerializeField]
    [Tooltip("The connected OVR pressable button.")]
    private OVRPressableButton ovrButton;

    [SerializeField]
    [Tooltip("The Animator to be used as the output for the Playable graph.")]
    private Animator animator;

    private readonly Dictionary<string, State> defaultStates = new Dictionary<string, State>()
    {
        { "Disabled", new State() },
        { "PassiveHover", new State() },
        { "ActiveHover", new State() },
        { "Select", new State() { IsVariable = true } },
        { "Toggle", new State() }
    };

    private PlayableGraph playableGraph;
    private AnimationLayerMixerPlayable animationMixerPlayable;
    private float sleepTimer = 0f;

    private readonly List<UnityAction> unsubscribeActions = new List<UnityAction>(defaultWakeUpEventCount);
    private readonly Dictionary<IEffect, int> mixableIndices = new Dictionary<IEffect, int>();

    private static readonly ProfilerMarker OVRStateVisualizerUpdateStateValuesMarker =
        new ProfilerMarker("[OVR] OVRStateVisualizer.UpdateStateValues");

    private static readonly ProfilerMarker OVRStateVisualizerEvaluateEffectsMarker =
        new ProfilerMarker("[OVR] OVRStateVisualizer.EvaluateEffects");

    public OVRPressableButton OVRButton
    {
        get
        {
            if (ovrButton == null)
            {
                ovrButton = GetComponentInParent<OVRPressableButton>();
            }

            return ovrButton;
        }
        set => ovrButton = value;
    }

    public Animator Animator
    {
        get => animator;
        set => animator = value;
    }

    private void OnValidate()
    {
        EnsureDefaultStates();

        if (ovrButton == null)
        {
            ovrButton = GetComponentInParent<OVRPressableButton>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private UnityAction Subscribe<T>(UnityEvent<T> genericEvent, UnityAction callback)
    {
        UnityAction<T> wrapper = (_) => callback();
        genericEvent.AddListener(wrapper);
        return () => genericEvent.RemoveListener(wrapper);
    }

    private UnityAction Subscribe(UnityEvent evt, UnityAction callback)
    {
        evt.AddListener(callback);
        return () => evt.RemoveListener(callback);
    }

    protected virtual void Start()
    {
        OnValidate();

        if (OVRButton != null)
        {
            unsubscribeActions.Add(Subscribe(OVRButton.OnHoverEnteredOVR, WakeUp));
            unsubscribeActions.Add(Subscribe(OVRButton.OnHoverExitedOVR, WakeUp));
            unsubscribeActions.Add(Subscribe(OVRButton.OnPressedOVR, WakeUp));
            unsubscribeActions.Add(Subscribe(OVRButton.OnReleasedOVR, WakeUp));
            unsubscribeActions.Add(Subscribe(OVRButton.OnSelectionProgressChangedOVR, WakeUp));

            unsubscribeActions.Add(Subscribe(OVRButton.IsToggled.OnEntered, WakeUp));
            unsubscribeActions.Add(Subscribe(OVRButton.IsToggled.OnExited, WakeUp));
            unsubscribeActions.Add(Subscribe(OVRButton.OnEnabled, WakeUp));
            unsubscribeActions.Add(Subscribe(OVRButton.OnDisabled, WakeUp));
        }

        playableGraph = PlayableGraph.Create();

        var animationPlayableOutput = AnimationPlayableOutput.Create(
            playableGraph,
            "Animation",
            GetComponent<Animator>());

        animationMixerPlayable = AnimationLayerMixerPlayable.Create(playableGraph, 1);
        animationPlayableOutput.SetSourcePlayable(animationMixerPlayable);

        foreach (var kvp in stateContainers)
        {
            foreach (IEffect effect in kvp.Value.Effects)
            {
                if (effect == null) { continue; }

                effect.Setup(playableGraph, gameObject);

                if (effect is IPlayableEffect playableEffect)
                {
                    if (effect is IAnimationMixableEffect mixableEffect)
                    {
                        int currentSlot = animationMixerPlayable.GetInputCount();
                        animationMixerPlayable.SetInputCount(currentSlot + 1);
                        animationMixerPlayable.SetLayerAdditive((uint)currentSlot, false);

                        playableGraph.Connect(playableEffect.Playable, 0, animationMixerPlayable, currentSlot);
                        mixableIndices.Add(mixableEffect, currentSlot);
                    }
                    else
                    {
                        ScriptPlayableOutput output =
                            ScriptPlayableOutput.Create(playableGraph, kvp.Key + ":" + effect.GetType().Name);
                        output.SetSourcePlayable(playableEffect.Playable);
                    }
                }
            }
        }

        animator.enabled = true;
        playableGraph.Play();
        enabled = true;
    }

    protected virtual void EnsureDefaultStates()
    {
        foreach (var kvp in defaultStates)
        {
            if (!stateContainers.ContainsKey(kvp.Key))
            {
                stateContainers.Add(kvp.Key, kvp.Value);
            }
        }
    }

    private void WakeUp()
    {
        enabled = true;
        sleepTimer = keepAliveTime;
    }

    private void Update()
    {
        bool valueChanged = UpdateStateValues();

        if (valueChanged && !animator.enabled)
        {
            animator.enabled = true;
            playableGraph.Play();
            sleepTimer = keepAliveTime;
        }

        if (!animator.enabled)
        {
            return;
        }

        if (EvaluateEffects())
        {
#pragma warning disable UNT0004
            sleepTimer -= Time.fixedDeltaTime;
#pragma warning restore UNT0004

            // Sleep logic intentionally disabled for OVR-driven interaction debugging.
            /*
            if (sleepTimer <= 0 && OVRButton != null && !OVRButton.OVRIsPressed && !OVRButton.OVRIsHovered)
            {
                animator.enabled = false;
                playableGraph.Stop();
                enabled = false;
            }
            */
        }
        else
        {
            sleepTimer = keepAliveTime;
        }
    }

    private void OnDestroy()
    {
        foreach (var unsubscribeAction in unsubscribeActions)
        {
            unsubscribeAction();
        }

        if (playableGraph.IsValid())
        {
            playableGraph.Destroy();
        }
    }

    public State GetState(string stateName)
    {
        if (stateContainers.TryGetValue(stateName, out var state))
        {
            return state;
        }

        return null;
    }

    public void AddEffect(string stateName, IEffect effect)
    {
        if (!stateContainers.ContainsKey(stateName))
        {
            stateContainers.Add(stateName, new State());
        }

        stateContainers[stateName].Effects.Add(effect);
    }

    public bool RemoveEffect(string stateName, IEffect effect)
    {
        if (stateContainers.ContainsKey(stateName))
        {
            return stateContainers[stateName].Effects.Remove(effect);
        }

        return false;
    }

    internal bool UpdateStateValue(string stateName, float newValue)
    {
        if (stateContainers.TryGetValue(stateName, out var state))
        {
            state.Value = newValue;

            if (!Mathf.Approximately(state.PreviousValue, newValue))
            {
                state.PreviousValue = newValue;
                return true;
            }

            state.PreviousValue = newValue;
        }

        return false;
    }

    protected virtual bool UpdateStateValues()
    {
        if (OVRButton != null)
        {
            using (OVRStateVisualizerUpdateStateValuesMarker.Auto())
            {
                bool parameterChanged = false;
                parameterChanged |= UpdateStateValue("Disabled", !OVRButton.enabled ? 1 : 0);
                parameterChanged |= UpdateStateValue("PassiveHover", OVRButton.OVRIsHovered ? 1 : 0);
                parameterChanged |= UpdateStateValue("ActiveHover", OVRButton.OVRIsHovered ? 1 : 0);
                parameterChanged |= UpdateStateValue("Select", OVRButton.SelectionProgress);
                parameterChanged |= UpdateStateValue("Toggle", OVRButton.IsToggled.Active ? 1 : 0);
                return parameterChanged;
            }
        }

        return false;
    }

    private bool EvaluateEffects()
    {
        using (OVRStateVisualizerEvaluateEffectsMarker.Auto())
        {
            bool allEffectsDone = true;

            foreach (var kvp in stateContainers)
            {
                foreach (IEffect effect in kvp.Value.Effects)
                {
                    if (effect == null) { continue; }

                    if (effect is IAnimationMixableEffect mixableEffect)
                    {
                        allEffectsDone &= UpdateWeight(mixableEffect, kvp.Value);
                    }

                    allEffectsDone &= effect.Evaluate(kvp.Value.Value);
                }
            }

            return allEffectsDone;
        }
    }

    private bool UpdateWeight(IAnimationMixableEffect mixableEffect, State state)
    {
        bool done = true;

        if (mixableEffect.WeightMode == WeightType.MatchStateValue)
        {
            animationMixerPlayable.SetInputWeight(mixableEffect.Playable, state.Value);
        }
        else if (mixableEffect.WeightMode == WeightType.Transition)
        {
            float currentWeight = animationMixerPlayable.GetInputWeight(mixableIndices[mixableEffect]);

            bool shouldBeActive = !Mathf.Approximately(state.Value, 0.0f);
            int transitionDirection = shouldBeActive ? 1 : -1;

            float newWeight = Mathf.Clamp01(
                currentWeight + transitionDirection * (Time.deltaTime / mixableEffect.TransitionDuration));

            animationMixerPlayable.SetInputWeight(mixableEffect.Playable, newWeight);

            if ((shouldBeActive && !Mathf.Approximately(newWeight, 1.0f)) ||
                (!shouldBeActive && !Mathf.Approximately(newWeight, 0.0f)))
            {
                done = false;
            }
        }
        else
        {
            animationMixerPlayable.SetInputWeight(mixableEffect.Playable, 1.0f);
        }

        return done;
    }
}