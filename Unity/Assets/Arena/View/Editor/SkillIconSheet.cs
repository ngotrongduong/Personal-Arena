using System.Collections.Generic;
using System.IO;
using PersonalArena.Core;
using UnityEditor;
using UnityEngine;

namespace PersonalArena.View.Editor
{
    /// <summary>Saves every class's skill icons as one PNG sheet (rows: Warrior, Mage, Archer) for review.</summary>
    public static class SkillIconSheet
    {
        [MenuItem("Personal Arena/Export Skill Icon Sheet")]
        public static void Export()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "images", "skill-icons.png"));
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-iconSheet")
                {
                    path = args[i + 1];
                }
            }

            IReadOnlyList<IReadOnlyList<string>> rows = SkillIconFactory.ClassSkillIds;
            const int gap = 16;
            int cell = SkillIconFactory.Size + gap;
            Texture2D sheet = new Texture2D(cell * 4 + gap, cell * rows.Count + gap, TextureFormat.RGBA32, false);
            Color32[] background = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < background.Length; i++)
            {
                background[i] = new Color32(24, 26, 36, 255);
            }
            sheet.SetPixels32(background);
            for (int row = 0; row < rows.Count; row++)
            {
                for (int column = 0; column < rows[row].Count; column++)
                {
                    SkillDef skill = new SkillDef { Id = rows[row][column] };
                    Color32[] icon = SkillIconFactory.DrawPixels(skill.Id, SkillIconFactory.ColorFor(skill, column));
                    int x0 = gap + column * cell;
                    int y0 = sheet.height - (row + 1) * cell;
                    for (int y = 0; y < SkillIconFactory.Size; y++)
                    {
                        for (int x = 0; x < SkillIconFactory.Size; x++)
                        {
                            Color32 pixel = icon[y * SkillIconFactory.Size + x];
                            if (pixel.a > 0)
                            {
                                Color under = sheet.GetPixel(x0 + x, y0 + y);
                                sheet.SetPixel(x0 + x, y0 + y, Color.Lerp(under, pixel, pixel.a / 255f));
                            }
                        }
                    }
                }
            }
            sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log("Skill icon sheet saved to " + path);
        }
    }
}
