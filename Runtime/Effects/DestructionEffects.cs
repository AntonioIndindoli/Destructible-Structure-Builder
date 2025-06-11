using UnityEngine;

namespace Mayuns.DSB
{
    public class DestructionEffects : MonoBehaviour
    {
        public enum EffectType
        {
            MemberDestroyed,
            WallDestroyed,
            Crumble
        }

        [System.Serializable]
        public class EffectInfo
        {
            public EffectType type;
            public AudioClip[] clips;
            [Range(0f,1f)]
            public float volume = 1f;
            public GameObject[] particlePrefabs;
        }

        public EffectInfo[] effects;

        private AudioSource audioSource;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        public void PlayMemberDestroyed()
        {
            PlayEffects(EffectType.MemberDestroyed, transform.position);
        }

        public void PlayWallDestroyed()
        {
            PlayEffects(EffectType.WallDestroyed, transform.position);
        }

        public void PlayCrumble()
        {
            PlayEffects(EffectType.Crumble, transform.position);
        }

        private void PlayEffects(EffectType type, Vector3 position)
        {
            if (effects == null) return;

            foreach (var effect in effects)
            {
                if (effect.type != type) continue;

                if (audioSource != null && effect.clips != null && effect.clips.Length > 0)
                {
                    AudioClip clip = effect.clips[Random.Range(0, effect.clips.Length)];
                    if (clip != null)
                    {
                        audioSource.PlayOneShot(clip, effect.volume);
                    }
                }

                if (effect.particlePrefabs != null)
                {
                    foreach (var prefab in effect.particlePrefabs)
                    {
                        if (prefab != null)
                        {
                            Instantiate(prefab, position, Quaternion.identity);
                        }
                    }
                }

                break;
            }
        }
    }
}