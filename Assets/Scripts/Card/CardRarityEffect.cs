using UnityEngine;
using UnityEngine.UI;

public class CardRarityEffect : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Image frameBorder;
    //[SerializeField] private Image rarityGem;

    [Header("Colores de marco por rareza")]
    [SerializeField] private Color colorCommon = new Color(0.67f, 0.67f, 0.67f);
    [SerializeField] private Color colorRare = new Color(0.23f, 0.55f, 0.88f);
    [SerializeField] private Color colorEpic = new Color(0.61f, 0.36f, 0.94f);
    [SerializeField] private Color colorLegendary = new Color(0.83f, 0.63f, 0.09f);

    [Header("Config de partículas por rareza")]
    [SerializeField] private int particlesCommon = 0;
    [SerializeField] private int particlesRare = 8;
    [SerializeField] private int particlesEpic = 18;
    [SerializeField] private int particlesLegendary = 35;

    public void Apply(CardRarity rarity)
    {
        Color frameColor = rarity switch
        {
            CardRarity.Common => colorCommon,
            CardRarity.Rare => colorRare,
            CardRarity.Epic => colorEpic,
            CardRarity.Legendary => colorLegendary,
            _ => colorCommon
        };

        frameBorder.color = frameColor;

        
        // Partículas
        int count = rarity switch
        {
            CardRarity.Common => particlesCommon,
            CardRarity.Rare => particlesRare,
            CardRarity.Epic => particlesEpic,
            CardRarity.Legendary => particlesLegendary,
            _ => 0
        };  
    }
  
}