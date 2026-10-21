using UnityEngine;

/// <summary>Reproduce un sonido cuando el objeto choca con suficiente fuerza (lo añade SonidosMaquina).</summary>
public class ChoqueSonoro : MonoBehaviour
{
    public AudioClip clip;
    [Tooltip("Velocidad relativa mínima (m/s) para que suene.")]
    public float umbral = 0.1f;
    public float volumenMax = 1f;
    public float pitchMin = 0.9f, pitchMax = 1.1f;

    AudioSource fuente;
    float ultimo = -1f;

    void Awake()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f;
        fuente.minDistance = 0.4f;
        fuente.maxDistance = 15f;
    }

    void OnCollisionEnter(Collision choque)
    {
        float v = choque.relativeVelocity.magnitude;
        if (clip == null || v < umbral || Time.time - ultimo < 0.05f) return;
        ultimo = Time.time;
        fuente.pitch = Random.Range(pitchMin, pitchMax);
        fuente.PlayOneShot(clip, Mathf.Clamp01(v / 1.2f) * volumenMax);
    }
}
