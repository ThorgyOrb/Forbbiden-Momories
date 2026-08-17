using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera/actualiza <see cref="OpponentData"/> a partir de los CSV de Forbidden Memories
/// en la raíz de Assets (duelistas, mazos y tablas de drop, extraídos de
/// yugioh-fm-db.pages.dev). Rellena identidad + mazo + <c>powRewards</c>/<c>tecRewards</c>/
/// <c>bcdRewards</c>; NUNCA toca los campos que solo se ajustan a mano (story, portrait,
/// aiStrategy, arena, música…), así que reimportar es seguro.
///
/// Los <c>card_id</c> del CSV son del FM original, no coinciden con <see cref="CardData.cardId"/>
/// (que es el image_id de ygoprodeck) — el enlace es por NOMBRE normalizado. decks.csv y
/// drops.csv ya traen el nombre en su columna "card", así que no hace falta cards.csv.
///
/// Menú: YGO ▸ Setup ▸ Importar oponentes (Forbidden Memories CSV).
/// </summary>
public static class FMOpponentImporter
{
    const string DuelistsCsv = "Assets/forbidden_memories_duelists.csv";
    const string DecksCsv = "Assets/forbidden_memories_decks.csv";
    const string DropsCsv = "Assets/forbidden_memories_drops.csv";
    const string OutDir = "Assets/Resources/Opponents/Data";
    const int DeckSize = 40;

    /// <summary>
    /// Nombres del CSV de FM que son el MISMO card real bajo una traducción/tipografía
    /// distinta (verificado a mano contra el catálogo, uno por uno — el emparejamiento
    /// difuso automático propone falsos positivos peligrosos, p. ej. "Red-eyes B. Dragon"
    /// → "Red-Eyes Baby Dragon" en vez de "Red-Eyes Black Dragon"). Cualquier card_id del
    /// CSV que no esté aquí ni case exacto por nombre normalizado se OMITE.
    /// </summary>
    static readonly Dictionary<int, string> Alias = new()
    {
        { 7, "Winged Dragon, Guardian of the Fortress #1" },
        { 21, "Exodia the Forbidden One" },
        { 82, "Red-Eyes Black Dragon" },
        { 119, "Trial of Nightmare" },
        { 186, "Fiend Reflection #2" },
        { 217, "Black Skull Dragon" },
        { 379, "La Jinn the Mystical Genie of the Lamp" },
        { 399, "Swordsman from a Distant Land" },
        { 415, "Mechanicalchaser" },
        { 426, "Stone Dragon" },
        { 480, "Kuwagata α" },
        { 552, "Winged Dragon, Guardian of the Fortress #2" },
        { 570, "Trakodon" },
        { 595, "Fiend Reflection #1" },
        { 661, "Crush Card Virus" },
        { 664, "Eternal Drought" },
        { 674, "Beastly Mirror Ritual" },
        { 675, "Blue-Eyes Ultimate Dragon" },
        { 713, "Meteor Black Dragon" },
    };

    [MenuItem("YGO/Setup/Importar oponentes (Forbidden Memories CSV)")]
    public static void Import()
    {
        if (!File.Exists(DuelistsCsv) || !File.Exists(DecksCsv) || !File.Exists(DropsCsv))
        {
            EditorUtility.DisplayDialog("Faltan CSV",
                $"No encuentro los tres CSV esperados en la raíz de Assets:\n{DuelistsCsv}\n{DecksCsv}\n{DropsCsv}",
                "Ok");
            return;
        }

        // ── 1) Catálogo de cartas: nombre normalizado → CardData (la de menor cardId si hay arte alternativo). ──
        var catalog = new Dictionary<string, CardData>();
        foreach (var card in Resources.LoadAll<CardData>("Cards/Data"))
        {
            string key = Normalize(card.cardName);
            if (!catalog.TryGetValue(key, out var existingCard) || card.cardId < existingCard.cardId)
                catalog[key] = card;
        }
        if (catalog.Count == 0)
        {
            EditorUtility.DisplayDialog("Sin catálogo",
                "No hay CardData en Resources/Cards/Data — importa primero el set de cartas.", "Ok");
            return;
        }

        // ── 2) Resolver TODOS los card_id que aparecen en decks.csv/drops.csv una sola vez. ──
        var decksRows = ParseCsv(File.ReadAllText(DecksCsv));
        var dropsRows = ParseCsv(File.ReadAllText(DropsCsv));

        var resolved = new Dictionary<int, CardData>();
        var missedNames = new HashSet<string>();
        void ResolveAll(List<string[]> rows, int idCol, int nameCol)
        {
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Length <= Math.Max(idCol, nameCol)) continue;
                if (!int.TryParse(row[idCol], out int fmId) || resolved.ContainsKey(fmId)) continue;

                string lookupName = Alias.TryGetValue(fmId, out var alias) ? alias : row[nameCol];
                if (catalog.TryGetValue(Normalize(lookupName), out var card))
                    resolved[fmId] = card;
                else
                    missedNames.Add($"{fmId}\t{row[nameCol]}");
            }
        }
        ResolveAll(decksRows, idCol: 3, nameCol: 4);
        ResolveAll(dropsRows, idCol: 2, nameCol: 3);

        // ── 3) Duelistas. ──
        var duelistsRows = ParseCsv(File.ReadAllText(DuelistsCsv));
        var duelists = new List<(int id, string name)>();
        for (int i = 1; i < duelistsRows.Count; i++)
        {
            var row = duelistsRows[i];
            if (row.Length < 2 || !int.TryParse(row[0], out int id)) continue;
            duelists.Add((id, row[1]));
        }

        // ── 4) Mazo por duelista: pool ponderado (decks.csv) → 40 cartas por muestreo con repetición. ──
        var deckPool = new Dictionary<int, List<(CardData card, float rate)>>();
        for (int i = 1; i < decksRows.Count; i++)
        {
            var row = decksRows[i];
            if (row.Length < 6 || !int.TryParse(row[0], out int duelistId)) continue;
            if (!int.TryParse(row[3], out int fmId) || !resolved.TryGetValue(fmId, out var card)) continue;
            float rate = ParsePercent(row[5]);
            if (!deckPool.TryGetValue(duelistId, out var list)) deckPool[duelistId] = list = new List<(CardData, float)>();
            list.Add((card, rate));
        }

        // ── 5) Tablas de drop por duelista y rango (drops.csv; se ignoran filas con rank vacío — duplican decks.csv). ──
        var dropPool = new Dictionary<(int duelistId, string rank), List<(CardData card, float rate)>>();
        for (int i = 1; i < dropsRows.Count; i++)
        {
            var row = dropsRows[i];
            if (row.Length < 6 || !int.TryParse(row[0], out int duelistId)) continue;
            string rank = row[4];
            if (string.IsNullOrEmpty(rank)) continue;
            if (!int.TryParse(row[2], out int fmId) || !resolved.TryGetValue(fmId, out var card)) continue;
            float rate = ParsePercent(row[5]);
            var key = (duelistId, rank);
            if (!dropPool.TryGetValue(key, out var list)) dropPool[key] = list = new List<(CardData, float)>();
            list.Add((card, rate));
        }

        // ── 6) Assets existentes por opponentId (reimportar actualiza, no pisa campos manuales). ──
        var existing = new Dictionary<int, OpponentData>();
        foreach (var opp in Resources.LoadAll<OpponentData>("Opponents/Data"))
            existing[opp.opponentId] = opp;

        int created = 0, updated = 0;
        var report = new StringBuilder();

        foreach (var (id, name) in duelists)
        {
            var rng = new System.Random(id * 7919 + 17); // determinista, igual convención que OpponentDeckFiller

            var deck = new List<CardData>();
            if (deckPool.TryGetValue(id, out var pool) && pool.Count > 0)
            {
                float total = pool.Sum(e => Mathf.Max(0.0001f, e.rate));
                for (int n = 0; n < DeckSize; n++)
                    deck.Add(WeightedPick(rng, pool, total));
            }

            RewardTable BuildTable(string rank)
            {
                var table = new RewardTable();
                if (dropPool.TryGetValue((id, rank), out var entries))
                    foreach (var (card, rate) in entries)
                        table.entries.Add(new DropEntry { card = card, probability = Mathf.Clamp01(rate / 100f) });
                return table;
            }

            bool isNew = !existing.TryGetValue(id, out var opp) || opp == null;
            if (isNew)
            {
                opp = ScriptableObject.CreateInstance<OpponentData>();
                opp.opponentId = id;
                opp.appearanceOrder = id;
                string path = AssetDatabase.GenerateUniqueAssetPath($"{OutDir}/{SanitizeFileName(name)}.asset");
                AssetDatabase.CreateAsset(opp, path);
                created++;
            }
            else updated++;

            opp.opponentName = name;
            opp.deck = deck;
            opp.powRewards = BuildTable("S/A POW");
            opp.tecRewards = BuildTable("S/A TEC");
            opp.bcdRewards = BuildTable("B/C/D");
            EditorUtility.SetDirty(opp);

            report.AppendLine($"• {name} (id {id}): mazo {deck.Count}, POW {opp.powRewards.entries.Count}, " +
                              $"TEC {opp.tecRewards.entries.Count}, B/C/D {opp.bcdRewards.entries.Count}");
        }

        AssetDatabase.SaveAssets();

        string summary = $"Duelistas: {duelists.Count} ({created} creados, {updated} actualizados)\n" +
                          $"Cartas de FM resueltas: {resolved.Count}\n" +
                          $"Cartas de FM omitidas (sin equivalente en el catálogo): {missedNames.Count}\n\n" +
                          report;
        Debug.Log($"FMOpponentImporter:\n{summary}");
        if (missedNames.Count > 0)
            Debug.Log("FMOpponentImporter — omitidas:\n" + string.Join("\n", missedNames.OrderBy(s => s)));

        EditorUtility.DisplayDialog("Importar oponentes (Forbidden Memories)",
            $"Duelistas: {duelists.Count} ({created} creados, {updated} actualizados)\n" +
            $"Cartas resueltas: {resolved.Count} · omitidas: {missedNames.Count}\n\n" +
            "Detalle completo en la consola. Ajusta a mano historia, retrato, IA, arena y música " +
            "de cada rival en su OpponentData (Resources/Opponents/Data).",
            "Ok");
    }

    static CardData WeightedPick(System.Random rng, List<(CardData card, float rate)> pool, float total)
    {
        if (total <= 0f) return pool[rng.Next(pool.Count)].card;
        double r = rng.NextDouble() * total;
        foreach (var (card, rate) in pool)
        {
            r -= Mathf.Max(0.0001f, rate);
            if (r <= 0) return card;
        }
        return pool[pool.Count - 1].card;
    }

    static float ParsePercent(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0f;
        s = s.TrimEnd('%');
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }

    static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s.ToLowerInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }

    static readonly char[] InvalidFileChars = Path.GetInvalidFileNameChars();

    static string SanitizeFileName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
            sb.Append(Array.IndexOf(InvalidFileChars, c) >= 0 || c == '"' || c == '.' ? '_' : c);
        string clean = sb.ToString().Trim();
        return clean.Length == 0 ? "Duelist" : clean;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  CSV — parser mínimo RFC-4180 (comillas, comas y saltos de línea en campo).
    // ─────────────────────────────────────────────────────────────────────
    static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"': inQuotes = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row.ToArray()); row.Clear();
                    break;
                default: field.Append(c); break;
            }
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row.ToArray()); }

        // BOM en la primera celda de la primera fila.
        if (rows.Count > 0 && rows[0].Length > 0 && rows[0][0].Length > 0 && rows[0][0][0] == '﻿')
            rows[0][0] = rows[0][0].Substring(1);

        return rows;
    }
}
