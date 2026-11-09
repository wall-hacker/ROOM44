using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace DialogueSystem.Events
{
    [CustomPropertyDrawer(typeof(DialogueEventBase), true)]
    public class DialogueEventDrawer : PropertyDrawer
    {
        private static readonly Dictionary<string, bool> _foldouts = new();

public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
{
    EditorGUI.BeginProperty(position, label, property);

    float lineH = EditorGUIUtility.singleLineHeight;
    Rect headerRect = new Rect(position.x, position.y, position.width, lineH);

    string typeName = property.managedReferenceValue?.GetType().Name;
    string key = property.propertyPath;

    if (property.managedReferenceValue == null)
    {
        // No value yet — just show the Add Event button, no foldout
        if (GUI.Button(headerRect, "Add Event"))
            ShowTypeMenu(property);
    }
    else
    {
        // Foldout arrow on the left, type name as label
        if (!_foldouts.ContainsKey(key)) _foldouts[key] = true;

        Rect foldoutRect = new Rect(position.x, position.y, position.width - 60f, lineH);
        Rect buttonRect  = new Rect(position.x + position.width - 56f, position.y, 56f, lineH);

        _foldouts[key] = EditorGUI.Foldout(foldoutRect, _foldouts[key], typeName, toggleOnLabelClick: true);

        if (GUI.Button(buttonRect, "Change"))
            ShowTypeMenu(property);

        if (_foldouts[key])
        {
            float y = position.y + lineH + 2f;
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (SerializedProperty.EqualContents(iterator, end)) break;

                float propHeight = EditorGUI.GetPropertyHeight(iterator, true);
                Rect fieldRect = new Rect(position.x, y, position.width, propHeight);

                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    Type objType = GetFieldType(iterator) ?? typeof(UnityEngine.Object);
                    EditorGUI.BeginChangeCheck();
                    var newObj = EditorGUI.ObjectField(fieldRect, new GUIContent(iterator.displayName),
                        iterator.objectReferenceValue, objType, allowSceneObjects: true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        iterator.objectReferenceValue = newObj;
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }
                else
                {
                    EditorGUI.PropertyField(fieldRect, iterator, true);
                }

                y += propHeight + 2f;
            }
        }
    }

    EditorGUI.EndProperty();
}

public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
{
    float height = EditorGUIUtility.singleLineHeight;

    string key = property.propertyPath;
    bool expanded = !_foldouts.ContainsKey(key) || _foldouts[key];

    if (property.managedReferenceValue != null && expanded)
    {
        var iterator = property.Copy();
        var end = iterator.GetEndProperty();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (SerializedProperty.EqualContents(iterator, end)) break;
            height += EditorGUI.GetPropertyHeight(iterator, true) + 2f;
        }
    }

    return height;
}

private void ShowTypeMenu(SerializedProperty property)
{
    var menu = new GenericMenu();
    var types = TypeCache.GetTypesDerivedFrom<DialogueEventBase>();
    foreach (var t in types)
    {
        if (t.IsAbstract) continue;
        var captured = t;
        menu.AddItem(new GUIContent(t.Name), false, () =>
        {
            property.managedReferenceValue = Activator.CreateInstance(captured);
            property.serializedObject.ApplyModifiedProperties();
        });
    }
    menu.ShowAsContext();
}

        /// <summary>
        /// Resolves the declared System.Type of an ObjectReference SerializedProperty
        /// by walking the managed reference's reflected fields.
        /// Falls back to UnityEngine.Object if the field can't be found.
        /// </summary>
        private static Type GetFieldType(SerializedProperty prop)
        {
            if (prop.serializedObject.targetObject == null)
                return null;

            // Walk the property path to find the FieldInfo on the managed reference type
            object managedRef = prop.GetDeclaringObject();
            if (managedRef == null) return null;

            string fieldName = prop.name;
            var field = managedRef.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic
            );

            return field?.FieldType;
        }
    }

    /// <summary>
    /// Extension to retrieve the object that directly declares a SerializedProperty
    /// (needed to resolve field types on [SerializeReference] managed objects).
    /// </summary>
    internal static class SerializedPropertyExtensions
    {
        public static object GetDeclaringObject(this SerializedProperty prop)
        {
            // The managed reference value IS the declaring object for direct children
            // Walk up one level via the parent path
            string path = prop.propertyPath;                    // e.g. "events.Array.data[0].audioSource"
            int lastDot = path.LastIndexOf('.');
            if (lastDot < 0) return prop.serializedObject.targetObject;

            string parentPath = path.Substring(0, lastDot);
            var parent = prop.serializedObject.FindProperty(parentPath);
            return parent?.managedReferenceValue;
        }
    }
}