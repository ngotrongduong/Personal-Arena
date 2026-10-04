using System.Collections.Generic;
using PersonalArena.Core;
using PersonalArena.Core.Survivor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersonalArena.View
{
    /// <summary>The hero: body per class, block bubble, animation state and the pose of each frame.</summary>
    public sealed partial class SurvivorRenderer
    {
        private void BuildHero()
        {
            heroRoot = CreateChild("Hero", actorsRoot);
            heroBody = CreateChild("Body Visual", heroRoot);
            BuildHeroBody("warrior");

            heroShadow = CreateBlobShadow(heroRoot, 1.3f);
            heroShieldGlow = effects.CreateShield(heroRoot);
            BuildBubble();
            BuildBuffAuras();
            heroStars = effects.CreateStunStars(heroRoot, 1.85f);
            heroTrail = effects.CreateTrail(heroRoot, 0.75f, DashColor, 0.95f, 0.22f);

            // A warm torch glow around the hero keeps the knight readable in the moonlit graveyard.
            GameObject lightObject = new GameObject("Hero Light");
            lightObject.transform.SetParent(heroRoot, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            Light heroLight = lightObject.AddComponent<Light>();
            heroLight.type = LightType.Point;
            heroLight.range = 10f;
            heroLight.intensity = 1.5f;
            heroLight.color = new Color(1f, 0.8f, 0.58f);
            heroLight.shadows = LightShadows.None;
            heroLight.renderMode = LightRenderMode.ForcePixel;
        }

        /// <summary>
        /// (Re)builds the hero model for a class (warrior knight, mage, hooded archer) with the shared hero clips.
        /// Classes without an art entry fall back to the default hero model inside <see cref="ArenaArtSet.HeroFor"/>.
        /// </summary>
        private void BuildHeroBody(string classId)
        {
            heroClassId = classId;
            heroAnimator?.Dispose();
            heroAnimator = null;
            for (int i = heroBody.childCount - 1; i >= 0; i--)
            {
                GameObject child = heroBody.GetChild(i).gameObject;
                // Detach first so GetComponentsInChildren below never sees the old model during a deferred Destroy.
                child.SetActive(false);
                child.transform.SetParent(null, false);
                DestroyUnityObject(child);
            }

            ArenaArtSet.HeroLook look = artSet != null && artSet.HasCharacters ? artSet.HeroFor(classId) : default;
            if (look.Body != null)
            {
                heroBodyScale = artSet.CharacterScale * (look.Scale > 0f ? look.Scale : 1f);
                Animator animator = SpawnCharacter(look.Body, heroBody, look.MainHand, look.OffHand);
                heroAnimator = new CharacterAnimator(animator, new[]
                {
                    artSet.HeroIdle, artSet.HeroRun, artSet.HeroBlock, artSet.HeroStrikeA, artSet.HeroStrikeB,
                    artSet.HeroKick, artSet.HeroDash, artSet.HeroHit, artSet.HeroDeath,
                    artSet.HeroThrow, artSet.HeroCast, artSet.HeroDodgeBack
                }, HeroLoops, "Survivor Hero");
                heroAnimator.ResetTo(HeroIdle);
            }
            else
            {
                heroBodyScale = 1f;
                if (heroFallbackMaterial == null)
                {
                    heroFallbackMaterial = CreateStandard("Hero Fallback", new Color(0.22f, 0.48f, 0.85f), 0.3f);
                }
                heroFallbackMaterial.color = classId == "mage" ? new Color(0.45f, 0.3f, 0.85f)
                    : classId == "archer" ? new Color(0.3f, 0.6f, 0.3f)
                    : new Color(0.22f, 0.48f, 0.85f);
                GameObject body = CreatePrimitive("Hero Body", PrimitiveType.Capsule, heroBody, heroFallbackMaterial);
                body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                body.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            }
            heroBody.localScale = Vector3.one * heroBodyScale;
            heroRenderers = heroBody.GetComponentsInChildren<Renderer>(true);
            heroFlashTinted = false;
        }

        /// <summary>The mage's mana shield: a camera-facing ring plus a soft fill, shown while blocking.</summary>
        private void BuildBubble()
        {
            heroBubble = CreateChild("Mana Bubble", heroRoot);
            heroBubble.localPosition = new Vector3(0f, 0.95f, 0f);
            bubbleRingMaterial = Own(FxAssets.Create("Mana Bubble Ring", FxAssets.Ring, true));
            bubbleFillMaterial = Own(FxAssets.Create("Mana Bubble Fill", FxAssets.RadialGlow, true));
            heroBubbleFill = CreateFlatQuad("Fill", heroBubble, bubbleFillMaterial);
            heroBubbleRing = CreateFlatQuad("Ring", heroBubble, bubbleRingMaterial);
            heroBubble.gameObject.SetActive(false);
        }

        private void PresentBubble(bool blocking, float realDelta)
        {
            bubbleFlash = Mathf.Max(0f, bubbleFlash - realDelta * 4f);
            bool show = heroHasBubble && (blocking || bubbleFlash > 0f);
            if (heroBubble.gameObject.activeSelf != show)
            {
                heroBubble.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }

            Camera camera = ResolveCamera();
            Vector3 toCamera = camera != null ? -camera.transform.forward : Vector3.up;
            // The flat quads face +Y; turn them toward the camera so the bubble reads as a sphere outline.
            heroBubble.rotation = Quaternion.FromToRotation(Vector3.up, toCamera);
            float pulse = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 6f);
            const float radius = 1.15f;
            float ringSize = radius * RingQuadPerRadius * pulse;
            heroBubbleRing.localScale = new Vector3(ringSize, 1f, ringSize);
            float fillSize = radius * 2.3f * pulse;
            heroBubbleFill.localScale = new Vector3(fillSize, 1f, fillSize);
            Color tint = Color.Lerp(BubbleColor, bubbleFlashColor, bubbleFlash);
            float strength = blocking ? 1f : bubbleFlash;
            bubbleRingMaterial.color = new Color(tint.r, tint.g, tint.b, (0.55f + 0.45f * bubbleFlash) * strength);
            bubbleFillMaterial.color = new Color(tint.r, tint.g, tint.b, (0.14f + 0.3f * bubbleFlash) * strength);
        }

        /// <summary>Block feedback: the mage's bubble flashes, everyone else's front shield does.</summary>
        private void FlashGuard(Color color)
        {
            if (heroHasBubble)
            {
                bubbleFlash = 1f;
                bubbleFlashColor = color;
            }
            else
            {
                heroShieldGlow?.Flash(color);
            }
        }

        // ------------------------------------------------------------------ hero

        private void UpdateHeroAnimationState()
        {
            SurvivorHero hero = sim.Hero;
            if (heroAnimator == null)
            {
                heroWasAlive = hero.Alive;
                return;
            }

            if (!hero.Alive)
            {
                if (heroWasAlive)
                {
                    heroAnimator.PlayOneShot(HeroDeath, 1f, true);
                }
            }
            else
            {
                if (!heroWasAlive)
                {
                    heroAnimator.ResetTo(HeroIdle);
                }
                float speed = hero.Velocity.Length;
                heroRunning = heroRunning ? speed > 0.2f : speed > 0.45f;
                if (hero.Blocking)
                {
                    heroAnimator.SetBase(HeroBlock, 1f);
                }
                else if (heroRunning)
                {
                    float top = Mathf.Max(0.5f, sim.DerivedStats.MoveSpeed);
                    heroAnimator.SetBase(HeroRun, Mathf.Clamp(speed / top, 0.6f, 1.3f) * 1.15f);
                }
                else
                {
                    heroAnimator.SetBase(HeroIdle, 1f);
                }
            }
            heroWasAlive = hero.Alive;
        }

        private void PresentHero(float realDelta)
        {
            SurvivorHero hero = sim.Hero;
            Vector3 target = Vector3.Lerp(heroPrevious, heroCurrent, interpolationAlpha);
            float targetYaw = Mathf.LerpAngle(heroPreviousYaw, heroCurrentYaw, interpolationAlpha);
            bool dashing = hero.Dashing && hero.Alive;
            if (heroSnap || (heroDisplayPosition - target).sqrMagnitude > SnapDistance * SnapDistance || realDelta <= 0f)
            {
                heroDisplayPosition = target;
                heroDisplayYaw = targetYaw;
                heroPositionVelocity = Vector3.zero;
                heroYawVelocity = 0f;
                lastAfterimagePosition = target;
                heroSnap = false;
            }
            else
            {
                heroDisplayPosition = dashing
                    ? target
                    : Vector3.SmoothDamp(heroDisplayPosition, target, ref heroPositionVelocity, PositionSmoothTime, Mathf.Infinity, realDelta);
                heroDisplayYaw = Mathf.SmoothDampAngle(heroDisplayYaw, targetYaw, ref heroYawVelocity, HeroYawSmoothTime, Mathf.Infinity, realDelta);
            }

            heroRoot.SetPositionAndRotation(heroDisplayPosition, Quaternion.Euler(0f, heroDisplayYaw, 0f));
            bool blocking = hero.Blocking && hero.Alive;
            heroShieldGlow?.SetBlocking(blocking && !heroHasBubble);
            PresentBubble(blocking, realDelta);
            heroStars?.SetVisible(hero.Alive && hero.StunRemaining > 0f);
            if (heroShadow != null)
            {
                heroShadow.gameObject.SetActive(hero.Alive);
            }

            bool flash = heroFlashRemaining > 0f;
            if (flash || heroFlashTinted)
            {
                SetRendererTint(heroRenderers, new Color(1.7f, 0.55f, 0.55f), flash);
                heroFlashTinted = flash;
            }

            if (heroTrail != null && heroTrail.emitting != dashing)
            {
                heroTrail.emitting = dashing;
            }
            if (dashing)
            {
                if (!heroWasDashing || (heroDisplayPosition - lastAfterimagePosition).sqrMagnitude >= AfterimageSpacing * AfterimageSpacing)
                {
                    lastAfterimagePosition = heroDisplayPosition;
                    effects.Afterimage(heroRenderers, DashColor, 0.36f);
                }
            }
            else if (heroWasDashing && hero.Alive)
            {
                effects.Puff(heroDisplayPosition, DustColor, 5, 0.6f, 1.2f, 0.6f, 0.3f);
            }
            heroWasDashing = dashing;

            heroAnimator?.Tick(animationDelta);
        }

        private void PlayHeroOneShot(int state, float speed, bool interruptOthers)
        {
            if (heroAnimator == null || !sim.Hero.Alive)
            {
                return;
            }
            if (!interruptOthers && heroAnimator.OneShot >= 0)
            {
                return;
            }
            heroAnimator.PlayOneShot(state, speed, false);
        }
    }
}
