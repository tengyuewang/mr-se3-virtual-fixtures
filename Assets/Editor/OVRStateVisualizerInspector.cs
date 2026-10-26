using MixedReality.Toolkit;
using MixedReality.Toolkit.UX;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MixedReality.Toolkit.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(OVRStateVisualizer))]
    public class OVRStateVisualizerInspector : UnityEditor.Editor
    {
        private OVRStateVisualizer instance;
        private SerializedProperty ovrButton;
        private SerializedProperty animator;
        private SerializedProperty stateContainers;

        private List<Type> effectTypeList = new List<Type>();
        private string[] effectTypeNames;
        private GUIStyle valueLabelStyle;

        private readonly Dictionary<string, ReorderableList> effectLists = new Dictionary<string, ReorderableList>();

        protected virtual void OnEnable()
        {
            instance = target as OVRStateVisualizer;
            ovrButton = serializedObject.FindProperty("ovrButton");
            animator = serializedObject.FindProperty("animator");
            stateContainers = serializedObject.FindProperty("stateContainers").FindPropertyRelative("entries");

            List<Type> types = GetEffectTypes();
            effectTypeNames = new string[types.Count];

            for (int i = 0; i < types.Count; i++)
            {
                effectTypeNames[i] = types[i].Name;
            }
        }

        public override void OnInspectorGUI()
        {
            valueLabelStyle = EditorStyles.miniLabel;
            valueLabelStyle.alignment = TextAnchor.MiddleRight;
            valueLabelStyle.normal.textColor = Color.gray;

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(ovrButton);
            EditorGUILayout.PropertyField(animator);

            RenderStates();

            serializedObject.ApplyModifiedProperties();
        }

        private void RenderStates()
        {
            InspectorUIUtility.DrawTitle("Visual States");

            foreach (var kvp in stateContainers)
            {
                SerializedProperty kvpProperty = kvp as SerializedProperty;
                string stateName = kvpProperty.FindPropertyRelative("key").stringValue;

                if (ovrButton.objectReferenceValue != null &&
                    stateName == "Toggle" &&
                    (ovrButton.objectReferenceValue as OVRPressableButton).ToggleMode == StatefulInteractable.ToggleType.Button)
                {
                    continue;
                }

                var state = instance.GetState(stateName);
                float value = state != null ? state.Value : 0.0f;
                int numEffects = state != null ? state.Effects.Count : 0;
                bool isVariable = state != null && state.IsVariable;

                var previousColor = GUI.color;
                GUI.color = Color.Lerp(previousColor, Color.cyan, value);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    string effectsLabel = numEffects > 0
                        ? (numEffects + " effect" + (numEffects == 1 ? "" : "s") + ", ")
                        : "";

                    if (isVariable)
                    {
                        EditorGUILayout.LabelField(
                            effectsLabel + "Value: " + value.ToString("F2"),
                            EditorStyles.miniLabel,
                            GUILayout.ExpandWidth(false));
                    }
                    else
                    {
                        EditorGUILayout.LabelField(
                            effectsLabel + "Value: " + (Mathf.Approximately(value, 0.0f) ? "Off" : "On"),
                            EditorStyles.miniLabel,
                            GUILayout.ExpandWidth(false));
                    }
                }

                GUILayout.Space(-EditorGUIUtility.singleLineHeight);

                string stateFoldoutID = stateName + "State" + "_" + target.name;

                if (InspectorUIUtility.DrawSectionFoldoutWithKey(
                    stateName,
                    stateFoldoutID,
                    MRTKEditorStyles.TitleFoldoutStyle,
                    false))
                {
                    GUI.color = previousColor;

                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.Space();

                        if (effectLists.ContainsKey(stateName))
                        {
                            effectLists[stateName].DoLayoutList();
                        }
                        else
                        {
                            var newList = new ReorderableList(
                                serializedObject,
                                kvpProperty.FindPropertyRelative("value").FindPropertyRelative("effects"),
                                true,
                                true,
                                true,
                                true);

                            newList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                            {
                                rect.x += 10;
                                rect.width -= 10;
                                EditorGUI.PropertyField(rect, newList.serializedProperty.GetArrayElementAtIndex(index), true);
                            };

                            newList.elementHeightCallback =
                                (int index) => EditorGUI.GetPropertyHeight(newList.serializedProperty.GetArrayElementAtIndex(index), true);

                            newList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "Effects");

                            newList.onAddDropdownCallback = (Rect buttonRect, ReorderableList l) =>
                            {
                                GenericMenu menu = new GenericMenu();

                                foreach (var type in effectTypeList)
                                {
                                    menu.AddItem(
                                        new GUIContent(type.Name.Replace("Effect", string.Empty)),
                                        false,
                                        (object t) =>
                                        {
                                            Undo.RecordObject(instance, "Add effect");
                                            instance.AddEffect(stateName, Activator.CreateInstance((Type)t) as IEffect);
                                            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                                            serializedObject.ApplyModifiedProperties();
                                        },
                                        type);
                                }

                                menu.ShowAsContext();
                            };

                            effectLists.Add(stateName, newList);
                        }
                    }
                }

                GUI.color = previousColor;
                EditorGUILayout.Space(1.0f);
            }
        }

        private List<Type> GetEffectTypes()
        {
            effectTypeList.Clear();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (assembly.FullName?.Contains("Tests") ?? true) { continue; }
                FilterTypes(assembly, effectTypeList);
            }

            effectTypeList.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
            return effectTypeList;
        }

        private void FilterTypes(Assembly assembly, List<Type> output)
        {
            foreach (var type in assembly.GetLoadableTypes())
            {
                if (type.IsAbstract || type.IsInterface || !DoesImplement(type, typeof(IEffect)))
                {
                    continue;
                }

                output.Add(type);
            }
        }

        private bool DoesImplement(Type type, Type interfaceType)
        {
            var interfaces = type.GetInterfaces();
            for (var i = 0; i < interfaces.Length; i++)
            {
                if (interfaces[i] == interfaceType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}