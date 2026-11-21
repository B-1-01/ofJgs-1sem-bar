using System;
using UnityEngine;

/// <summary>
/// Identifies the different ingredient types used throughout the game.
/// Shared between recipes, bottles, and drink containers.
/// </summary>
public enum IngredientType
{
    None,
    Vodka,
    Beer,
    Rum,
    Coke,
    Lime,
    Ice,
    Sugar,
    Gin,
    Tequilla

    // Add new ingredients at the end to preserve existing enum values.
}

/// <summary>
/// Defines how ingredient quantities are displayed.
/// </summary>
public enum IngredientUnit
{
    Milliliters,
    Units
}

/// <summary>
/// Associates an ingredient with its display unit.
/// </summary>
[Serializable]
public class IngredientUnitEntry
{
    [SerializeField] private IngredientType ingredient;
    [SerializeField] private IngredientUnit unit;

    public IngredientType Ingredient => ingredient;
    public IngredientUnit Unit => unit;

    public IngredientUnitEntry() { }

    public IngredientUnitEntry(IngredientType ingredient, IngredientUnit unit)
    {
        this.ingredient = ingredient;
        this.unit = unit;
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(IngredientUnitEntry))]
public class IngredientUnitEntryDrawer : UnityEditor.PropertyDrawer
{
    public override void OnGUI(
        Rect position,
        UnityEditor.SerializedProperty property,
        GUIContent label)
    {
        UnityEditor.EditorGUI.BeginProperty(position, label, property);

        Rect fields = UnityEditor.EditorGUI.PrefixLabel(position, label);

        float spacing = 6f;
        float ingredientWidth = (fields.width - spacing) * 0.65f;

        Rect ingredientRect = new(
            fields.x, fields.y, ingredientWidth, fields.height);

        Rect unitRect = new(
            ingredientRect.xMax + spacing,
            fields.y,
            fields.width - ingredientWidth - spacing,
            fields.height);

        // Ingredient types are assigned automatically.
        using (new UnityEditor.EditorGUI.DisabledScope(true))
        {
            UnityEditor.EditorGUI.PropertyField(
                ingredientRect,
                property.FindPropertyRelative("ingredient"),
                GUIContent.none);
        }

        UnityEditor.EditorGUI.PropertyField(
            unitRect,
            property.FindPropertyRelative("unit"),
            GUIContent.none);

        UnityEditor.EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(
        UnityEditor.SerializedProperty property,
        GUIContent label)
    {
        return UnityEditor.EditorGUIUtility.singleLineHeight;
    }
}
#endif
