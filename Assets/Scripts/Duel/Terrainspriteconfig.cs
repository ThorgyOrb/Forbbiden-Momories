using System;
using System.Collections.Generic;
using UnityEngine;

// ────────────────────────────────────────────────────────────────────────────
//  TerrainSpriteConfig
//  ScriptableObject que mapea TerrainType → Sprite.
//  Crea una instancia en: Assets > Create > YGO > Terrain Sprite Config
// ────────────────────────────────────────────────────────────────────────────
[CreateAssetMenu(fileName = "TerrainSpriteConfig", menuName = "YGO/Terrain Sprite Config")]
public class TerrainSpriteConfig : ScriptableObject
{
    [Serializable]
    public struct TerrainEntry
    {
        public TerrainType terrain;
        public Sprite sprite;
    }

    public List<TerrainEntry> entries = new();

    public Sprite GetSprite(TerrainType terrain)
    {
        foreach (var e in entries)
            if (e.terrain == terrain) return e.sprite;
        return null;
    }
}