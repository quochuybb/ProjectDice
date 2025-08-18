using UnityEngine;

[CreateAssetMenu(fileName = "New Character Sheet", menuName = "Characters/Character Sheet")]
public class CharacterSheet : ScriptableObject
{
    [Header("Identity")]
    public string characterName;
    public Sprite characterSprite;

    [Header("Combat Stats")]
    public Stat maxHealth;
    public Stat energy;
    public Stat might;
    public Stat intelligence;
    public Stat armor;
    public Stat speed;
    public Stat grit;

    [Header("Board Stats")]
    public Stat luck;
    public Stat growth;

    [Header("Skills")]
    public Skill[] startingSkills;
}