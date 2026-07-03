using UnityEngine;

[CreateAssetMenu(fileName = "CardIconConfig", menuName = "YGO/Card Icon Config")]
public class CardIconConfig : ScriptableObject
{
    [Header("Atributos")]
    public Sprite darkIcon;
    public Sprite lightIcon;
    public Sprite fireIcon;
    public Sprite waterIcon;
    public Sprite earthIcon;
    public Sprite windIcon;

    [Header("Tipos")]
    public Sprite dragonIcon;
    public Sprite spellcasterIcon;
    public Sprite fiendIcon;
    public Sprite beastIcon;
    public Sprite insectIcon;
    public Sprite plantIcon;
    public Sprite fishIcon;
    public Sprite aquaIcon;
    public Sprite seaSerpentIcon;
    public Sprite zombieIcon;
    public Sprite dinosaurIcon;
    public Sprite wingedBeastIcon;
    public Sprite warriorIcon;
    public Sprite machineIcon;
    public Sprite thunderIcon;

    public Sprite GetAttributeIcon(CardAttribute attr) => attr switch
    {
        CardAttribute.Dark => darkIcon,
        CardAttribute.Light => lightIcon,
        CardAttribute.Fire => fireIcon,
        CardAttribute.Water => waterIcon,
        CardAttribute.Earth => earthIcon,
        CardAttribute.Wind => windIcon,
        _ => null
    };

    public Sprite GetTypeIcon(MonsterType type) => type switch
    {
        MonsterType.Dragon => dragonIcon,
        MonsterType.Spellcaster => spellcasterIcon,
        MonsterType.Fiend => fiendIcon,
        MonsterType.Beast => beastIcon,
        MonsterType.Insect => insectIcon,
        MonsterType.Plant => plantIcon,
        MonsterType.Fish => fishIcon,
        MonsterType.Aqua => aquaIcon,
        MonsterType.SeaSerpent => seaSerpentIcon,
        MonsterType.Zombie => zombieIcon,
        MonsterType.Dinosaur => dinosaurIcon,
        MonsterType.WingedBeast => wingedBeastIcon,
        MonsterType.Warrior => warriorIcon,
        MonsterType.Machine => machineIcon,
        MonsterType.Thunder => thunderIcon,
        _ => null
    };
}