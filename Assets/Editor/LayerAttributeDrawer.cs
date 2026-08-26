using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(LayerAttribute))]
public class LayerAttributeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        if (prop.propertyType != SerializedPropertyType.Integer)
        {
            EditorGUI.PropertyField(pos, prop, label);
            return;
        }
        EditorGUI.BeginProperty(pos, label, prop);
        int picked = EditorGUI.LayerField(pos, label, prop.intValue);
        if (picked != prop.intValue) prop.intValue = picked;
        EditorGUI.EndProperty();
    }
}
