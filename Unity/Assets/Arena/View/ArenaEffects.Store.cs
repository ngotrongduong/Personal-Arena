using System;
using System.Collections.Generic;
using UnityEngine;

namespace PersonalArena.View
{
    /// <summary>Slots of the optional store effect set (see <see cref="StoreVfxSet"/>).</summary>
    public enum StoreFx
    {
        FireExplosion,
        GroundBlast,
        SmokeBlast,
        RedBlast,
        SnowArea,
        FreezeCircle,
        SnowHit,
        ElectroHit,
        HolyHit,
        StarHit,
        StonesHit,
        GreenHit,
        HealAura,
        BuffAura,
        LightningAura,
        StarAura,
        HealCircle,
        MagicCircle,
        MagicCircle2,
        ShieldBlue,
        ShieldYellow,
        Teleport,
        PortalRed,
        Meteors,
        CrystalsFront,
        SmokeVortex,
        DustPuff,
        SparksYellow,
        PlexusArea,
        LaserArea,
        MagicBall,
        FireBall,
        IceBall,
        LightningBall,
        SwampBall,
        DeadBall,
        BlueBall,
        Explode,
        Explode5,
        Explode7,
        PoisonExplode,
        DeadExplode,
        RainbowExplode,
        MagicCircleExplode,
        SummonCircle,
        SummonCircle2,
        RuneOfMagic,
        Swamp,
        IceCloud,
        StormCloud,
        Portal,
        StarCore,
        Hole,
        FlameEmission,
        Kunai,
        ElementalArrow
    }

    /// <summary>
    /// Plays pooled prefab effects from the optional <see cref="StoreVfxSet"/>. Every call reports whether it
    /// played, so callers keep their built-in effect as the fallback.
    /// </summary>
    public sealed partial class ArenaEffects
    {
        private const int StorePerSlot = 6;

        private sealed class StoreInstance
        {
            public GameObject Root;
            public ParticleSystem[] Systems;
            public float Remaining;
            public bool Held;
        }

        private static readonly int StoreSlotCount = Enum.GetValues(typeof(StoreFx)).Length;
        private GameObject[] storePrefabs;
        private List<StoreInstance>[] storePools;
        private bool storeLoaded;

        public static int StoreCount => StoreSlotCount;

        // The second pack is authored about five times larger than this game's units.
        private static float StoreBaseScale(StoreFx slot)
        {
            return slot >= StoreFx.MagicBall ? 0.22f : 1f;
        }

        private void EnsureStore()
        {
            if (storeLoaded)
            {
                return;
            }
            storeLoaded = true;
            EnsureReady();
            storePrefabs = new GameObject[StoreSlotCount];
            storePools = new List<StoreInstance>[StoreSlotCount];
            for (int i = 0; i < StoreSlotCount; i++)
            {
                storePools[i] = new List<StoreInstance>(StorePerSlot);
            }
            StoreVfxSet set = Resources.Load<StoreVfxSet>(StoreVfxSet.ResourceName);
            if (set == null)
            {
                return;
            }
            int count = Mathf.Min(set.Names.Length, set.Prefabs.Length);
            for (int i = 0; i < count; i++)
            {
                if (Enum.TryParse(set.Names[i], out StoreFx slot))
                {
                    storePrefabs[(int)slot] = set.Prefabs[i];
                }
            }
        }

        public bool HasStore(StoreFx slot)
        {
            EnsureStore();
            return storePrefabs[(int)slot] != null;
        }

        /// <summary>Plays a one-shot store effect; false when the set has none or the slot's pool is busy.</summary>
        public bool Store(StoreFx slot, Vector3 position, float scale = 1f, float lifetime = 2f, float yawDegrees = 0f)
        {
            StoreInstance instance = AcquireStore(slot, poolRoot);
            if (instance == null)
            {
                return false;
            }
            instance.Root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            instance.Root.transform.localScale = Vector3.one * (scale * StoreBaseScale(slot));
            instance.Remaining = lifetime;
            instance.Held = false;
            StartStore(instance);
            return true;
        }

        /// <summary>A looping store effect parented to <paramref name="parent"/>; the caller switches it with SetActive. Null when missing.</summary>
        public GameObject StoreAttach(StoreFx slot, Transform parent, float scale, float height = 0f)
        {
            EnsureStore();
            GameObject prefab = storePrefabs[(int)slot];
            if (prefab == null)
            {
                return null;
            }
            StoreInstance instance = CreateStore(prefab, parent);
            instance.Root.transform.localPosition = new Vector3(0f, height, 0f);
            instance.Root.transform.localRotation = Quaternion.identity;
            instance.Root.transform.localScale = Vector3.one * (scale * StoreBaseScale(slot));
            for (int i = 0; i < instance.Systems.Length; i++)
            {
                ParticleSystem.MainModule main = instance.Systems[i].main;
                main.loop = true;
            }
            return instance.Root;
        }

        private StoreInstance AcquireStore(StoreFx slot, Transform parent)
        {
            EnsureStore();
            GameObject prefab = storePrefabs[(int)slot];
            if (prefab == null)
            {
                return null;
            }
            List<StoreInstance> pool = storePools[(int)slot];
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].Root.activeSelf)
                {
                    return pool[i];
                }
            }
            if (pool.Count >= StorePerSlot)
            {
                return null;
            }
            StoreInstance created = CreateStore(prefab, parent);
            pool.Add(created);
            return created;
        }

        private static StoreInstance CreateStore(GameObject prefab, Transform parent)
        {
            GameObject root = Instantiate(prefab, parent);
            root.SetActive(false);
            // The packs' demo movers, self-destroyers, sounds, lights and colliders are not wanted here.
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Destroy(behaviour);
            }
            foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>(true))
            {
                Destroy(audio);
            }
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
            }
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.useUnscaledTime = true;
                main.playOnAwake = true;
            }
            return new StoreInstance { Root = root, Systems = systems };
        }

        private static void StartStore(StoreInstance instance)
        {
            instance.Root.SetActive(true);
            for (int i = 0; i < instance.Systems.Length; i++)
            {
                instance.Systems[i].Clear(false);
                instance.Systems[i].Play(false);
            }
        }

        private void UpdateStore(float delta)
        {
            if (storePools == null)
            {
                return;
            }
            for (int s = 0; s < storePools.Length; s++)
            {
                List<StoreInstance> pool = storePools[s];
                for (int i = 0; i < pool.Count; i++)
                {
                    StoreInstance instance = pool[i];
                    if (!instance.Root.activeSelf)
                    {
                        continue;
                    }
                    instance.Remaining -= delta;
                    if (instance.Remaining <= 0f)
                    {
                        instance.Root.SetActive(false);
                    }
                }
            }
        }

        private void ClearStore()
        {
            if (storePools == null)
            {
                return;
            }
            for (int s = 0; s < storePools.Length; s++)
            {
                for (int i = 0; i < storePools[s].Count; i++)
                {
                    storePools[s][i].Root.SetActive(false);
                }
            }
        }

        public const int GalleryPerPage = 4;

        /// <summary>-vfxGallery: one page of store effects in a row, each with its slot number and a 2 m radius ring.</summary>
        public void PlayStoreGallery(Vector3 center, int page)
        {
            for (int k = 0; k < GalleryPerPage; k++)
            {
                int slot = page * GalleryPerPage + k;
                if (slot >= StoreSlotCount)
                {
                    return;
                }
                Vector3 point = center + new Vector3(-9f + k * 6f, 0f, 1.5f);
                Store((StoreFx)slot, point, 1f, 2.8f);
                Shockwave(point, Color.white, 4f, 2.5f);
                Text(point + new Vector3(0f, 0.2f, -4f), slot.ToString(), Color.white, 1.2f, 2.8f);
            }
        }
    }
}
