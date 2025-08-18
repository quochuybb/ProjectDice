using UnityEngine;

[CreateAssetMenu(fileName = "New Character Sheet", menuName = "Characters/Character Sheet")]
public class CharacterSheet : ScriptableObject
{
    [Header("Identity")]
    public string characterName;
    public Sprite characterSprite;

    [Header("Combat Stats")]
    public float maxHealth;
    public float energy;
    public float might;
    public float intelligence;
    public float armor;
    public float speed;
    public float grit;


    [Header("Board Stats")]
    public float luck;
    public float growth;

    [Header("Skills")]
    public Skill[] startingSkills;
}