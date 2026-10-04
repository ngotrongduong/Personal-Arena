using System.Collections.Generic;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>Animates slashes, floating text and afterimages each frame.</summary>
    public sealed partial class ArenaEffects
    {
        // ------------------------------------------------------------------ animation

        private void LateUpdate()
        {
            float delta = Time.unscaledDeltaTime;
            UpdateVfx(delta);
            UpdateStore(delta);
            Camera camera = Camera.main;

            for (int i = 0; i < slashes.Count; i++)
            {
                SlashFx slash = slashes[i];
                if (!slash.Active)
                {
                    continue;
                }
                slash.Age += delta;
                if (slash.Age >= slash.Lifetime)
                {
                    slash.Active = false;
                    slash.Transform.gameObject.SetActive(false);
                    continue;
                }
                UpdateSlash(slash);
            }

            for (int i = 0; i < labels.Count; i++)
            {
                Label label = labels[i];
                if (!label.Active)
                {
                    continue;
                }
                label.Age += delta;
                if (label.Age >= label.Lifetime)
                {
                    label.Active = false;
                    label.Transform.gameObject.SetActive(false);
                    continue;
                }
                UpdateLabel(label, camera);
            }

            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                Ghost ghost = ghosts[i];
                ghost.Age += delta;
                if (ghost.Age >= ghost.Lifetime)
                {
                    ReleaseGhost(ghost);
                    ghosts.RemoveAt(i);
                    continue;
                }
                float fade = 1f - ghost.Age / ghost.Lifetime;
                Color color = ghost.Color;
                color.a *= fade * fade;
                block.Clear();
                block.SetColor("_Color", color);
                for (int p = 0; p < ghost.Parts.Count; p++)
                {
                    GhostPart part = ghost.Parts[p];
                    for (int sub = 0; sub < part.Mesh.subMeshCount; sub++)
                    {
                        Graphics.DrawMesh(part.Mesh, part.Matrix, FxAssets.GhostMaterial, 0, null, sub, block, false, false);
                    }
                }
            }

            float time = Time.unscaledTime;
            for (int i = 0; i < stunStars.Count; i++)
            {
                StunStars stars = stunStars[i];
                if (stars.Root == null || !stars.Visible)
                {
                    continue;
                }
                for (int s = 0; s < stars.Stars.Length; s++)
                {
                    // Cartoon "dizzy" halo: gold stars circling the head, bobbing and wobbling as they go.
                    float angle = (time * 3.6f + stars.Phase) + s * Mathf.PI * 2f / stars.Stars.Length;
                    Transform star = stars.Stars[s].transform;
                    star.position = stars.Root.position + new Vector3(Mathf.Cos(angle) * 0.5f, 0.12f + Mathf.Sin(angle * 2f) * 0.08f, Mathf.Sin(angle) * 0.5f);
                    if (camera != null)
                    {
                        float wobble = Mathf.Sin(time * 5f + s * 2.1f) * 25f;
                        star.rotation = Quaternion.LookRotation(-camera.transform.forward, camera.transform.up) * Quaternion.Euler(0f, 0f, wobble) * Quaternion.Euler(90f, 0f, 0f);
                    }
                    float pulse = 0.9f + 0.1f * Mathf.Sin(time * 9f + s * 2f);
                    star.localScale = Vector3.one * (0.4f * pulse);
                    block.Clear();
                    block.SetColor("_Color", Color.white);
                    stars.Stars[s].SetPropertyBlock(block);
                }
            }

            for (int i = 0; i < shields.Count; i++)
            {
                ShieldGlow shield = shields[i];
                if (shield.Transform == null)
                {
                    continue;
                }
                shield.Intensity = Mathf.MoveTowards(shield.Intensity, shield.Blocking ? 1f : 0f, delta * (shield.Blocking ? 9f : 5f));
                shield.FlashAmount = Mathf.MoveTowards(shield.FlashAmount, 0f, delta * 4f);
                shield.Apply();
            }
        }

        private static void UpdateSlash(SlashFx slash)
        {
            float t = Mathf.Clamp01(slash.Age / slash.Lifetime);
            float eased = 1f - (1f - t) * (1f - t);
            float sweep = Mathf.Lerp(-55f, 45f, eased) * (slash.Mirror ? -1f : 1f);
            slash.Transform.SetPositionAndRotation(slash.Position,
                Quaternion.Euler(0f, slash.Yaw + sweep, 0f) * Quaternion.Euler(0f, 0f, slash.Mirror ? -14f : 14f));
            float grow = slash.Reach * Mathf.Lerp(0.8f, 1.08f, eased);
            slash.Transform.localScale = new Vector3(slash.Mirror ? -grow : grow, grow, grow);
            Color color = slash.Color;
            color.a *= t < 0.25f ? 1f : Mathf.Pow(Mathf.Clamp01(1f - (t - 0.25f) / 0.75f), 1.5f);
            if (slashBlock == null)
            {
                slashBlock = new MaterialPropertyBlock();
            }
            slashBlock.SetColor("_Color", color);
            slash.Renderer.SetPropertyBlock(slashBlock);
        }

        private Label CreateLabel()
        {
            Label label = new Label();
            GameObject root = new GameObject("Label");
            root.transform.SetParent(poolRoot, false);
            label.Transform = root.transform;

            GameObject shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(root.transform, false);
            shadowObject.transform.localPosition = new Vector3(0.025f, -0.025f, 0.01f);
            label.Shadow = ConfigureText(shadowObject);

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(root.transform, false);
            label.Text = ConfigureText(textObject);
            return label;
        }

        private TextMesh ConfigureText(GameObject target)
        {
            TextMesh text = target.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.075f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return text;
        }

        private static void UpdateLabel(Label label, Camera camera)
        {
            float t = Mathf.Clamp01(label.Age / label.Lifetime);
            float rise = 1f - (1f - t) * (1f - t);
            label.Transform.position = label.Start + new Vector3(0f, 0.2f + rise * 0.9f, 0f);
            float pop = t < 0.12f ? Mathf.Lerp(1.6f, 1f, t / 0.12f) : 1f;
            label.Transform.localScale = Vector3.one * (label.Scale * pop);
            if (camera != null)
            {
                label.Transform.rotation = camera.transform.rotation;
            }
            float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            Color color = label.Color;
            color.a *= alpha;
            label.Text.color = color;
            label.Shadow.color = new Color(0f, 0f, 0f, 0.75f * alpha);
        }

        private void ReleaseGhost(Ghost ghost)
        {
            for (int i = 0; i < ghost.Parts.Count; i++)
            {
                if (ghost.Parts[i].Pooled)
                {
                    meshPool.Push(ghost.Parts[i].Mesh);
                }
            }
            ghost.Parts.Clear();
        }
    }
}
